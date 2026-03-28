using System;
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
    private readonly InkkOopsGameHost _host;
    private readonly InkkOopsScriptRegistry _registry;
    private readonly InkkOopsScriptRunner _runner = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Action? _requestAppExit;
    private readonly object _sync = new();
    private readonly Task _pipeServerTask;
    private PendingRunRequest? _pendingRequest;
    private bool _runActive;
    private bool _startupRequestQueued;
    private bool _recordingStarted;

    public InkkOopsRuntimeService(InkkOopsRuntimeOptions options, InkkOopsGameHost host, Action? requestAppExit = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _requestAppExit = requestAppExit;
        _registry = new InkkOopsScriptRegistry();
        _pipeServerTask = Task.Run(RunPipeServerLoopAsync);
    }

    public IReadOnlyList<string> ListScripts()
    {
        return _registry.ListScripts();
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

        if (!_registry.TryResolve(request.ScriptName, out _))
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
                ArtifactRoot: string.IsNullOrWhiteSpace(request.ArtifactRootOverride) ? _options.ArtifactRoot : request.ArtifactRootOverride,
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

        _host.StartRecording(_options.RecordingRoot);
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
                    _options.ArtifactRoot,
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
                using var artifacts = new InkkOopsArtifacts(root, Path.GetFileNameWithoutExtension(request.RecordingPath));
                _host.SetArtifactRoot(artifacts.DirectoryPath);
                _host.ClearAutomationEvents();
                var session = new InkkOopsSession(_host, artifacts);
                var script = InkkOopsRecordedSessionLoader.LoadFromJson(request.RecordingPath);
                result = await _runner.RunAsync(script, session, _shutdown.Token).ConfigureAwait(false);
                artifacts.WriteResult(result);
            }
            else if (!_registry.TryResolve(request.ScriptName, out var builtinScript) || builtinScript == null)
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
                using var artifacts = new InkkOopsArtifacts(root, request.ScriptName);
                _host.SetArtifactRoot(artifacts.DirectoryPath);
                _host.ClearAutomationEvents();
                var session = new InkkOopsSession(_host, artifacts);
                result = await _runner.RunAsync(builtinScript.CreateScript(), session, _shutdown.Token).ConfigureAwait(false);
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
            _requestAppExit?.Invoke();
        }
    }

    private async Task RunPipeServerLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    _options.NamedPipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                var payload = await reader.ReadLineAsync().ConfigureAwait(false);
                var request = string.IsNullOrWhiteSpace(payload)
                    ? new InkkOopsPipeRequest()
                    : JsonSerializer.Deserialize<InkkOopsPipeRequest>(payload) ?? new InkkOopsPipeRequest();
                var response = await SubmitRunAsync(request, _shutdown.Token).ConfigureAwait(false);
                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                await writer.WriteLineAsync(json).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(100, _shutdown.Token).ConfigureAwait(false);
            }
        }
    }

    private readonly record struct PendingRunRequest(
        string ScriptName,
        string RecordingPath,
        string ArtifactRoot,
        TaskCompletionSource<InkkOopsRunResult>? Completion);
}
