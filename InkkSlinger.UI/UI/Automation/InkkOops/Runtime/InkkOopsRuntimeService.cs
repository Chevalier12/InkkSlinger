using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsRuntimeService : IDisposable
{
    private readonly InkkOopsRuntimeOptions _options;
    private readonly InkkOopsHostConfiguration _hostConfiguration;
    private readonly InkkOopsGameHost _host;
    private readonly IInkkOopsScriptCatalog _scriptCatalog;
    private readonly InkkOopsScriptRunner _runner = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Action<InkkOopsRunResult>? _requestAppExit;
    private readonly object _sync = new();
    private readonly Task _pipeServerTask;
    private readonly InkkOopsLiveRequestDispatcher _liveRequestDispatcher;
    private PendingRunRequest? _pendingRequest;
    private bool _runActive;
    private bool _startupRequestQueued;
    private bool _recordingStarted;

    public InkkOopsRuntimeService(
        InkkOopsRuntimeOptions options,
        InkkOopsHostConfiguration hostConfiguration,
        InkkOopsGameHost host,
        Action<InkkOopsRunResult>? requestAppExit = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _hostConfiguration = hostConfiguration ?? throw new ArgumentNullException(nameof(hostConfiguration));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _requestAppExit = requestAppExit;
        _scriptCatalog = _hostConfiguration.ScriptCatalog;
        _liveRequestDispatcher = new InkkOopsLiveRequestDispatcher(
            _host,
            _scriptCatalog,
            ResolveArtifactRoot(),
            _hostConfiguration.ArtifactNamingPolicy);
        _pipeServerTask = Task.Run(RunPipeServerLoopAsync);
    }

    public IReadOnlyList<string> ListScripts()
    {
        return _scriptCatalog.ListScripts();
    }

    public void Update()
    {
        _host.OnAfterUpdate();
        TryStartRecording();
        TryQueueStartupRequest();
        StartPendingRunIfAvailable();
    }

    public void AfterDraw()
    {
        _host.OnAfterDraw();
    }

    public async Task<InkkOopsPipeResponse> SubmitRunAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_scriptCatalog.TryResolve(request.ScriptName, out _))
        {
            return new InkkOopsPipeResponse
            {
                Status = InkkOopsRunStatus.NotFound.ToString(),
                ScriptName = request.ScriptName,
                Message = $"Unknown script '{request.ScriptName}'."
            };
        }

        TaskCompletionSource<InkkOopsRunResult> completion;
        lock (_sync)
        {
            if (_runActive || _pendingRequest != null)
            {
                return new InkkOopsPipeResponse
                {
                    Status = InkkOopsRunStatus.Busy.ToString(),
                    ScriptName = request.ScriptName,
                    Message = "An InkkOops script is already queued or running."
                };
            }

            completion = new TaskCompletionSource<InkkOopsRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequest = new PendingRunRequest(
                request.ScriptName,
                RecordingPath: string.Empty,
                ArtifactRoot: string.IsNullOrWhiteSpace(request.ArtifactRootOverride) ? ResolveArtifactRoot() : request.ArtifactRootOverride,
                Completion: completion);
        }

        using var timeoutCts = request.TimeoutMilliseconds > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (timeoutCts != null)
        {
            timeoutCts.CancelAfter(request.TimeoutMilliseconds);
            cancellationToken = timeoutCts.Token;
        }

        using var registration = cancellationToken.Register(static state =>
            ((TaskCompletionSource<InkkOopsRunResult>)state!).TrySetCanceled(), completion);

        try
        {
            var result = await completion.Task.ConfigureAwait(false);
            return new InkkOopsPipeResponse
            {
                Status = result.Status.ToString(),
                ScriptName = result.ScriptName,
                ArtifactDirectory = result.ArtifactDirectory,
                Message = result.FailureMessage
            };
        }
        catch (OperationCanceledException)
        {
            return new InkkOopsPipeResponse
            {
                Status = InkkOopsRunStatus.Failed.ToString(),
                ScriptName = request.ScriptName,
                Message = "Request timed out or was canceled."
            };
        }
    }

    public Task<InkkOopsPipeResponse> SubmitRequestAsync(InkkOopsPipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kind = string.IsNullOrWhiteSpace(request.RequestKind)
            ? InkkOopsPipeRequestKinds.RunScript
            : request.RequestKind.Trim();
        if (string.Equals(kind, InkkOopsPipeRequestKinds.RunScript, StringComparison.Ordinal))
        {
            return SubmitRunAsync(request, cancellationToken);
        }

        lock (_sync)
        {
            if (_runActive || _pendingRequest != null)
            {
                return Task.FromResult(new InkkOopsPipeResponse
                {
                    Status = InkkOopsRunStatus.Busy.ToString(),
                    RequestKind = kind,
                    ScriptName = request.ScriptName,
                    Message = "An InkkOops script is already queued or running."
                });
            }
        }

        return _liveRequestDispatcher.SubmitAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _pipeServerTask.GetAwaiter().GetResult();
        }
        catch
        {
            // best effort shutdown
        }

        _liveRequestDispatcher.Dispose();
        _host.Dispose();
        _shutdown.Dispose();
    }

    private void TryStartRecording()
    {
        if (_recordingStarted ||
            !_options.RecordUserSession ||
            _host.UiRoot.LayoutExecutedFrameCount <= 0)
        {
            return;
        }

        _host.StartRecording(ResolveRecordingRoot(), _options.LaunchProjectPath);
        _recordingStarted = true;
    }

    private void TryQueueStartupRequest()
    {
        if (_startupRequestQueued ||
            (string.IsNullOrWhiteSpace(_options.StartupScriptName) && string.IsNullOrWhiteSpace(_options.StartupRecordingPath)) ||
            _host.UiRoot.LayoutExecutedFrameCount <= 0)
        {
            return;
        }

        lock (_sync)
        {
            if (_pendingRequest == null && !_runActive)
            {
                _pendingRequest = new PendingRunRequest(
                    _options.StartupScriptName,
                    _options.StartupRecordingPath,
                    ResolveArtifactRoot(),
                    Completion: null);
                _startupRequestQueued = true;
            }
        }
    }

    private void StartPendingRunIfAvailable()
    {
        PendingRunRequest? request = null;
        lock (_sync)
        {
            if (_runActive || _pendingRequest == null)
            {
                return;
            }

            request = _pendingRequest;
            _pendingRequest = null;
            _runActive = true;
        }

        _ = RunRequestAsync(request.Value);
    }

    private async Task RunRequestAsync(PendingRunRequest request)
    {
        var shouldExitWhenDone = request.Completion == null;
        InkkOopsRunResult result;
        try
        {
            if (!string.IsNullOrWhiteSpace(request.RecordingPath))
            {
                var root = Path.GetFullPath(request.ArtifactRoot);
                using var artifacts = new InkkOopsArtifacts(root, Path.GetFileNameWithoutExtension(request.RecordingPath), _hostConfiguration.ArtifactNamingPolicy);
                _host.SetArtifactRoot(artifacts.DirectoryPath);
                _host.ClearAutomationEvents();
                var script = InkkOopsRecordedSessionLoader.LoadFromJson(request.RecordingPath);
                var session = new InkkOopsSession(_host, artifacts);
                result = await _runner.RunAsync(script, session, _shutdown.Token).ConfigureAwait(false);
                artifacts.WriteResult(result);
                WritePlaybackActionLogMirror(request.RecordingPath, artifacts.GetActionLogPath());
            }
            else if (!_scriptCatalog.TryResolve(request.ScriptName, out var scriptDefinition) || scriptDefinition == null)
            {
                result = new InkkOopsRunResult(
                    InkkOopsRunStatus.NotFound,
                    request.ScriptName,
                    string.Empty,
                    commandCount: 0,
                    failureMessage: $"Unknown script '{request.ScriptName}'.");
            }
            else
            {
                var root = Path.GetFullPath(request.ArtifactRoot);
                using var artifacts = new InkkOopsArtifacts(root, request.ScriptName, _hostConfiguration.ArtifactNamingPolicy);
                _host.SetArtifactRoot(artifacts.DirectoryPath);
                _host.ClearAutomationEvents();
                var script = scriptDefinition.CreateScript();
                var session = new InkkOopsSession(_host, artifacts);
                result = await _runner.RunAsync(script, session, _shutdown.Token).ConfigureAwait(false);
                artifacts.WriteResult(result);
            }
        }
        catch (Exception ex)
        {
            result = new InkkOopsRunResult(
                InkkOopsRunStatus.Failed,
                request.ScriptName,
                string.Empty,
                commandCount: 0,
                failureMessage: ex.ToString());
        }

        request.Completion?.TrySetResult(result);
        lock (_sync)
        {
            _runActive = false;
        }

        if (shouldExitWhenDone)
        {
            _requestAppExit?.Invoke(result);
        }
    }

    private static void WritePlaybackActionLogMirror(string recordingPath, string actionLogPath)
    {
        if (string.IsNullOrWhiteSpace(recordingPath) ||
            string.IsNullOrWhiteSpace(actionLogPath) ||
            !File.Exists(actionLogPath))
        {
            return;
        }

        var recordingDirectory = InkkOopsRecordedSessionLoader.GetRecordingDirectoryPath(recordingPath);
        var mirrorPath = Path.Combine(recordingDirectory, "actionlog.txt");
        File.Copy(actionLogPath, mirrorPath, overwrite: true);
    }

    private async Task RunPipeServerLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    ResolveNamedPipeName(),
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                var payload = await ReadPipeMessageAsync(pipe).ConfigureAwait(false);
                var request = string.IsNullOrWhiteSpace(payload)
                    ? new InkkOopsPipeRequest()
                    : JsonSerializer.Deserialize<InkkOopsPipeRequest>(payload) ?? new InkkOopsPipeRequest();
                var response = await SubmitRequestAsync(request, _shutdown.Token).ConfigureAwait(false);
                var json = JsonSerializer.Serialize(response);
                await WritePipeMessageAsync(pipe, json).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _ = ex;
                await Task.Delay(100, _shutdown.Token).ConfigureAwait(false);
            }
        }
    }

    private static async Task WritePipeMessageAsync(Stream stream, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
        var lengthPrefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, bytes.Length);
        await stream.WriteAsync(lengthPrefix).ConfigureAwait(false);
        await stream.WriteAsync(bytes).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<string> ReadPipeMessageAsync(Stream stream)
    {
        var lengthPrefix = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, lengthPrefix).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthPrefix);
        if (length <= 0)
        {
            return string.Empty;
        }

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload).ConfigureAwait(false);
        return Encoding.UTF8.GetString(payload);
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset)).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new EndOfStreamException("The pipe closed before the full message was received.");
            }

            offset += read;
        }
    }

    private readonly record struct PendingRunRequest(
        string ScriptName,
        string RecordingPath,
        string ArtifactRoot,
        TaskCompletionSource<InkkOopsRunResult>? Completion);

    private string ResolveArtifactRoot()
    {
        return string.IsNullOrWhiteSpace(_options.ArtifactRoot)
            ? _hostConfiguration.DefaultArtifactRoot
            : _options.ArtifactRoot;
    }

    private string ResolveNamedPipeName()
    {
        return string.IsNullOrWhiteSpace(_options.NamedPipeName)
            ? _hostConfiguration.DefaultNamedPipeName
            : _options.NamedPipeName;
    }

    private string ResolveRecordingRoot()
    {
        return string.IsNullOrWhiteSpace(_options.RecordingRoot)
            ? _hostConfiguration.DefaultRecordingRoot
            : _options.RecordingRoot;
    }
}
