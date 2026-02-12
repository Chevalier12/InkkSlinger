using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public class TextBoxPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public TextBoxPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
        TextClipboard.ResetForTests();
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void TypingLargeDocument_LogsDiagnosticsSnapshot()
    {
        var textBox = CreatePreparedTextBox(lineCount: 2000, wrapping: TextWrapping.NoWrap);
        textBox.ResetPerformanceSnapshot();

        var elapsed = TypeCharacters(textBox, characterCount: 40, simulateHeavyTelemetryRead: false);
        var snapshot = textBox.GetPerformanceSnapshot();

        _output.WriteLine($"ElapsedMs={elapsed.TotalMilliseconds:0.00}");
        _output.WriteLine($"Commits={snapshot.CommitCount}, DeferredScheduled={snapshot.DeferredSyncScheduledCount}, DeferredFlushed={snapshot.DeferredSyncFlushCount}, ImmediateSync={snapshot.ImmediateSyncCount}");
        _output.WriteLine($"NoWrapEditAttempts={snapshot.IncrementalNoWrapEditAttemptCount}, NoWrapEditSuccess={snapshot.IncrementalNoWrapEditSuccessCount}");
        _output.WriteLine($"IncrementalEditSuccess={snapshot.IncrementalVirtualEditSuccessCount}, IncrementalEditFallback={snapshot.IncrementalVirtualEditFallbackCount}");
        _output.WriteLine($"ViewportLayoutBuilds={snapshot.ViewportLayoutBuildCount}, FullLayoutBuilds={snapshot.FullLayoutBuildCount}, VirtualRangeBuilds={snapshot.VirtualRangeBuildCount}, VirtualLineBuilds={snapshot.VirtualLineBuildCount}");
        _output.WriteLine($"TextSyncMs={snapshot.TextSyncMilliseconds:0.000}");
        _output.WriteLine($"Buffer: Materializations={snapshot.BufferDiagnostics.TextMaterializationCount}, Pieces={snapshot.BufferDiagnostics.PieceCount}, Length={snapshot.BufferDiagnostics.Length}, Lines={snapshot.BufferDiagnostics.LogicalLineCount}");

        Assert.Equal(40, snapshot.CommitCount);
        Assert.True(snapshot.IncrementalNoWrapEditAttemptCount > 0);
        Assert.True(snapshot.BufferDiagnostics.Length > 0);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void TypingLargeWrappedDocument_LogsDiagnosticsSnapshot()
    {
        var textBox = CreatePreparedTextBox(lineCount: 2000, wrapping: TextWrapping.Wrap);
        textBox.ResetPerformanceSnapshot();

        var elapsed = TypeCharacters(textBox, characterCount: 40, simulateHeavyTelemetryRead: false);
        var snapshot = textBox.GetPerformanceSnapshot();

        _output.WriteLine($"ElapsedMs={elapsed.TotalMilliseconds:0.00}");
        _output.WriteLine($"Commits={snapshot.CommitCount}, DeferredScheduled={snapshot.DeferredSyncScheduledCount}, DeferredFlushed={snapshot.DeferredSyncFlushCount}, ImmediateSync={snapshot.ImmediateSyncCount}");
        _output.WriteLine($"NoWrapEditAttempts={snapshot.IncrementalNoWrapEditAttemptCount}, NoWrapEditSuccess={snapshot.IncrementalNoWrapEditSuccessCount}");
        _output.WriteLine($"IncrementalEditSuccess={snapshot.IncrementalVirtualEditSuccessCount}, IncrementalEditFallback={snapshot.IncrementalVirtualEditFallbackCount}");
        _output.WriteLine($"ViewportLayoutBuilds={snapshot.ViewportLayoutBuildCount}, FullLayoutBuilds={snapshot.FullLayoutBuildCount}, VirtualRangeBuilds={snapshot.VirtualRangeBuildCount}, VirtualLineBuilds={snapshot.VirtualLineBuildCount}");
        _output.WriteLine($"InputLastMs={snapshot.LastInputMutationMilliseconds:0.000}, InputEnsureCaretMs={snapshot.LastInputEnsureCaretMilliseconds:0.000}");
        _output.WriteLine($"TextSyncMs={snapshot.TextSyncMilliseconds:0.000}");
        _output.WriteLine($"Buffer: Materializations={snapshot.BufferDiagnostics.TextMaterializationCount}, Pieces={snapshot.BufferDiagnostics.PieceCount}, Length={snapshot.BufferDiagnostics.Length}, Lines={snapshot.BufferDiagnostics.LogicalLineCount}");

        Assert.Equal(40, snapshot.CommitCount);
        Assert.True(snapshot.IncrementalVirtualEditSuccessCount > 0);
        Assert.True(snapshot.BufferDiagnostics.Length > 0);
        Assert.InRange(snapshot.ViewportLayoutBuildCount, 0, 4);
        Assert.InRange(snapshot.LastInputEnsureCaretMilliseconds, 0d, 0.1d);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void TypingLargeDocument_HeavyTelemetryRead_ShowsCost()
    {
        var baselineTextBox = CreatePreparedTextBox(lineCount: 1500, wrapping: TextWrapping.NoWrap);
        baselineTextBox.ResetPerformanceSnapshot();
        var baseline = TypeCharacters(baselineTextBox, characterCount: 20, simulateHeavyTelemetryRead: false);
        var baselineSnapshot = baselineTextBox.GetPerformanceSnapshot();

        var heavyTelemetryTextBox = CreatePreparedTextBox(lineCount: 1500, wrapping: TextWrapping.NoWrap);
        heavyTelemetryTextBox.ResetPerformanceSnapshot();
        var heavyTelemetry = TypeCharacters(heavyTelemetryTextBox, characterCount: 20, simulateHeavyTelemetryRead: true);
        var heavySnapshot = heavyTelemetryTextBox.GetPerformanceSnapshot();

        _output.WriteLine($"BaselineElapsedMs={baseline.TotalMilliseconds:0.00}, HeavyTelemetryElapsedMs={heavyTelemetry.TotalMilliseconds:0.00}");
        _output.WriteLine($"BaselineMaterializations={baselineSnapshot.BufferDiagnostics.TextMaterializationCount}, HeavyMaterializations={heavySnapshot.BufferDiagnostics.TextMaterializationCount}");
        _output.WriteLine($"BaselineTextSyncMs={baselineSnapshot.TextSyncMilliseconds:0.000}, HeavyTextSyncMs={heavySnapshot.TextSyncMilliseconds:0.000}");
        _output.WriteLine($"BaselinePieces={baselineSnapshot.BufferDiagnostics.PieceCount}, HeavyPieces={heavySnapshot.BufferDiagnostics.PieceCount}");

        Assert.True(baselineSnapshot.CommitCount > 0);
        Assert.True(heavySnapshot.CommitCount > 0);
        Assert.True(baselineSnapshot.BufferDiagnostics.Length > 0);
        Assert.True(heavySnapshot.BufferDiagnostics.Length > 0);
    }

    private TimeSpan TypeCharacters(PerformanceTextBox textBox, int characterCount, bool simulateHeavyTelemetryRead)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < characterCount; i++)
        {
            textBox.FireTextInput((char)('a' + (i % 26)));
            if (!simulateHeavyTelemetryRead)
            {
                continue;
            }

            var text = textBox.Text;
            _ = text.Length;
            _ = CountLines(text);
        }

        textBox.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(150)));
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private PerformanceTextBox CreatePreparedTextBox(int lineCount, TextWrapping wrapping)
    {
        var textBox = new PerformanceTextBox
        {
            Width = 920f,
            Height = 520f,
            Font = CreateTestSpriteFont(),
            TextWrapping = wrapping,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        textBox.Text = BuildDocumentText(lineCount);
        textBox.Measure(new Vector2(920f, 520f));
        textBox.Arrange(new LayoutRect(0f, 0f, 920f, 520f));
        textBox.Focus();
        textBox.FireKeyDown(Keys.End);
        return textBox;
    }

    private static SpriteFont CreateTestSpriteFont()
    {
        return (SpriteFont)RuntimeHelpers.GetUninitializedObject(typeof(SpriteFont));
    }

    private static string BuildDocumentText(int lineCount)
    {
        var builder = new StringBuilder(lineCount * 96);
        for (var i = 0; i < lineCount; i++)
        {
            var lineIndex = i + 1;
            builder.Append("Line ");
            builder.Append(lineIndex.ToString("N0"));
            builder.Append(": synthetic payload for TextBox performance instrumentation. Batch=");
            builder.Append((lineIndex % 97) + 1);
            builder.Append(" Priority=");
            builder.Append((lineIndex % 11) + 1);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private sealed class PerformanceTextBox : TextBox
    {
        public void FireKeyDown(Keys key, ModifierKeys modifiers = ModifierKeys.None, bool isRepeat = false)
        {
            RaisePreviewKeyDown(key, isRepeat, modifiers);
            RaiseKeyDown(key, isRepeat, modifiers);
        }

        public void FireTextInput(char character)
        {
            RaisePreviewTextInput(character);
            RaiseTextInput(character);
        }
    }
}
