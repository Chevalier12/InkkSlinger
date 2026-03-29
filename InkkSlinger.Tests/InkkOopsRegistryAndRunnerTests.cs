using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkOopsRegistryAndRunnerTests
{
    [Fact]
    public void Registry_Discovers_BuiltInScript()
    {
        var registry = new InkkOopsScriptRegistry(typeof(Game1).Assembly);

        Assert.Contains("buttons-resize-hover-repro", registry.ListScripts());
        Assert.True(registry.TryResolve("buttons-resize-hover-repro", out var script));
        Assert.NotNull(script);
        Assert.Equal("buttons-resize-hover-repro", script!.CreateScript().Name);
    }

    [Fact]
    public void Registry_Rejects_Duplicate_ScriptNames()
    {
        const string source = """
using InkkSlinger;
public sealed class ScriptOne : IInkkOopsBuiltinScript
{
    public string Name => "dup";
    public InkkOopsScript CreateScript() => new InkkOopsScript("dup");
}
public sealed class ScriptTwo : IInkkOopsBuiltinScript
{
    public string Name => "dup";
    public InkkOopsScript CreateScript() => new InkkOopsScript("dup");
}
""";

        var assembly = CompileTestAssembly(source);
        var ex = Assert.Throws<InvalidOperationException>(() => new InkkOopsScriptRegistry(assembly));
        Assert.Contains("Duplicate InkkOops script name 'dup'", ex.Message);
    }

    [Fact]
    public async Task Runner_Returns_Failed_Status_And_CommandIndex()
    {
        using var host = new InkkOopsTestHost(new Canvas());
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "runner-failure");
        var session = new InkkOopsSession(host, artifacts);
        var script = new InkkOopsScript("runner-failure")
            .Add(new TestCommand("first"))
            .Add(new ThrowingCommand("boom"))
            .Add(new TestCommand("never"));

        var runner = new InkkOopsScriptRunner();
        var result = await runner.RunAsync(script, session, CancellationToken.None);

        Assert.Equal(InkkOopsRunStatus.Failed, result.Status);
        Assert.Equal(1, result.FailedCommandIndex);
        Assert.Contains("Throw(boom)", result.FailedCommandDescription);
        Assert.Contains("boom", result.FailureMessage);
    }

    [Fact]
    public void PipeMessages_RoundTrip_Through_Json()
    {
        var request = new InkkOopsPipeRequest
        {
            ScriptName = "buttons-resize-hover-repro",
            TimeoutMilliseconds = 5000,
            ArtifactRootOverride = "artifacts/custom"
        };
        var response = new InkkOopsPipeResponse
        {
            Status = InkkOopsRunStatus.Completed.ToString(),
            ScriptName = request.ScriptName,
            ArtifactDirectory = "artifacts/inkkoops/run-1",
            Message = string.Empty
        };

        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
        var roundTripRequest = System.Text.Json.JsonSerializer.Deserialize<InkkOopsPipeRequest>(requestJson);
        var roundTripResponse = System.Text.Json.JsonSerializer.Deserialize<InkkOopsPipeResponse>(responseJson);

        Assert.NotNull(roundTripRequest);
        Assert.NotNull(roundTripResponse);
        Assert.Equal(request.ScriptName, roundTripRequest!.ScriptName);
        Assert.Equal(response.ArtifactDirectory, roundTripResponse!.ArtifactDirectory);
    }

    [Theory]
    [InlineData(InkkOopsRunStatus.Completed, InkkOopsExitCodes.Success)]
    [InlineData(InkkOopsRunStatus.Failed, InkkOopsExitCodes.Failed)]
    [InlineData(InkkOopsRunStatus.Busy, InkkOopsExitCodes.Busy)]
    [InlineData(InkkOopsRunStatus.NotFound, InkkOopsExitCodes.NotFound)]
    public void ExitCodes_Map_RunStatuses_To_ProcessExitCodes(InkkOopsRunStatus status, int expectedExitCode)
    {
        Assert.Equal(expectedExitCode, InkkOopsExitCodes.FromStatus(status));
        Assert.Equal(expectedExitCode, InkkOopsExitCodes.FromStatus(status.ToString()));
    }

    [Fact]
    public void ExitCodes_Unknown_Status_Maps_To_Failed()
    {
        Assert.Equal(InkkOopsExitCodes.Failed, InkkOopsExitCodes.FromStatus("bogus"));
    }

    private static Assembly CompileTestAssembly(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToArray();
        var compilation = CSharpCompilation.Create(
            $"InkkOopsRegistryTests_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }

    private sealed class TestCommand : IInkkOopsCommand
    {
        private readonly string _name;

        public TestCommand(string name)
        {
            _name = name;
        }

        public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

        public string Describe() => $"Test({_name})";

        public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCommand : IInkkOopsCommand
    {
        private readonly string _message;

        public ThrowingCommand(string message)
        {
            _message = message;
        }

        public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

        public string Describe() => $"Throw({_message})";

        public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(_message);
        }
    }
}
