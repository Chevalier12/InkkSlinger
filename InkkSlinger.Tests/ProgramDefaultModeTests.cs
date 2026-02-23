using System;
using System.IO;
using Xunit;

namespace InkkSlinger.Tests;

public class ProgramDefaultModeTests
{
    [Fact]
    public void Program_DefaultModeTargetsCollectionViewParityDemo()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var programPath = Path.Combine(repoRoot, "Program.cs");
        var source = File.ReadAllText(programPath);

        Assert.Contains("--collectionview-parity-demo", source, StringComparison.Ordinal);
        Assert.Contains("isCollectionViewParityDemo = true;", source, StringComparison.Ordinal);
    }
}
