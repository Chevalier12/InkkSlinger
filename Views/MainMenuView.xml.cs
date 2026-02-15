using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class MainMenuView : UserControl
{
    private readonly MainMenuViewModel _viewModel = new();
    private SpriteFont? _currentFont;
    private bool _isTelemetryDirty;
    private float _telemetryThrottleSeconds;
    private const float TelemetryUpdateIntervalSeconds = 0.1f;

    public MainMenuView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "MainMenuView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        
        DataContext = _viewModel;
        InitializeDemo();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        _currentFont = font;
        ApplyFontRecursive(this, font);
    }

    public void ReportNativeWindowStatus(string status)
    {
        _viewModel.Status = status;
    }

    private void InitializeDemo()
    {
        if (DemoTextBox == null)
        {
            _viewModel.Status = "TextBox demo failed to initialize.";
            return;
        }

        LoadDocument(10000);
        _viewModel.Status = "TextBox stress demo ready.";
        UpdateTelemetry(force: true);
    }

    private void OnDemoTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        // Avoid per-keystroke telemetry UI updates; they can distort hitch behavior.
    }

    private void OnLoadTenThousandLinesClick(object? sender, RoutedSimpleEventArgs args)
    {
        LoadDocument(10000);
    }

    private void OnLoadFiftyThousandLinesClick(object? sender, RoutedSimpleEventArgs args)
    {
        LoadDocument(50000);
    }

    private void OnAppendFiveThousandLinesClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (DemoTextBox == null)
        {
            return;
        }

        var startIndex = DemoTextBox.LogicalLineCount;
        DemoTextBox.Text += BuildDocumentText(5000, startIndex);
        _viewModel.Status = "Appended 5,000 lines.";
        UpdateTelemetry(force: true);
    }

    private void OnClearClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (DemoTextBox == null)
        {
            return;
        }

        DemoTextBox.Text = string.Empty;
        _viewModel.Status = "Cleared document.";
        UpdateTelemetry(force: true);
    }

    private void OnToggleWrapClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (DemoTextBox == null)
        {
            return;
        }

        DemoTextBox.TextWrapping = DemoTextBox.TextWrapping == TextWrapping.Wrap
            ? TextWrapping.NoWrap
            : TextWrapping.Wrap;

        _viewModel.Status = $"Wrapping set to {DemoTextBox.TextWrapping}.";
        UpdateTelemetry(force: true);
    }

    private void OnCopyTelemetryClick(object? sender, RoutedSimpleEventArgs args)
    {
        UpdateTelemetry(force: true);
        var payload = BuildTelemetryClipboardText();
        var copiedToSystemClipboard = TryCopyToSystemClipboard(payload);
        TextClipboard.SetText(payload);
        var destination = copiedToSystemClipboard ? "system clipboard" : "app clipboard";
        _viewModel.Status = $"Telemetry copied to {destination} ({payload.Length:N0} chars) at {DateTime.Now:HH:mm:ss}.";
    }

    private void OnResetPerfCountersClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (DemoTextBox == null)
        {
            return;
        }

        DemoTextBox.ResetPerformanceSnapshot();
        UpdateTelemetry(force: true);
        _viewModel.Status = $"Performance counters reset at {DateTime.Now:HH:mm:ss}.";
    }

    private void LoadDocument(int lineCount)
    {
        if (DemoTextBox == null)
        {
            return;
        }

        DemoTextBox.Text = BuildDocumentText(lineCount, 0);
        if (_currentFont != null)
        {
            DemoTextBox.Font = _currentFont;
        }

        _viewModel.Status = $"Loaded {lineCount:N0} lines.";
        UpdateTelemetry(force: true);
    }

    private static string BuildDocumentText(int lineCount, int startIndex)
    {
        var builder = new StringBuilder(lineCount * 96);
        for (var i = 0; i < lineCount; i++)
        {
            var lineIndex = startIndex + i + 1;
            builder.Append("Line ");
            builder.Append(lineIndex.ToString("N0"));
            builder.Append(": sample payload for text layout, hit-testing, caret movement, and scrolling. Batch=");
            builder.Append((lineIndex % 97) + 1);
            builder.Append(" Priority=");
            builder.Append((lineIndex % 11) + 1);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!_isTelemetryDirty)
        {
            return;
        }

        _telemetryThrottleSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_telemetryThrottleSeconds < TelemetryUpdateIntervalSeconds)
        {
            return;
        }

        UpdateTelemetry(force: true);
    }

    private void UpdateTelemetry(bool force = false)
    {
        if (!force)
        {
            _isTelemetryDirty = true;
            return;
        }

        _isTelemetryDirty = false;
        _telemetryThrottleSeconds = 0f;

        if (DemoTextBox == null)
        {
            _viewModel.DocumentStatsText = "Chars: 0 | Lines: 0";
            _viewModel.ViewportStatsText = "Offset X/Y: 0 / 0";
            _viewModel.WrappingText = "Wrapping: n/a";
            _viewModel.InputTimingText = "Input(ms): Last 0 | Avg 0 | Max 0";
            _viewModel.RenderTimingText = "Render(ms): Last 0 | Avg 0 | Max 0";
            _viewModel.ViewportTimingText = "Viewport(ms): Last 0 | Avg 0 | Max 0 | Hit 0 | Miss 0";
            _viewModel.CaretTimingText = "Caret(ms): Last 0 | Avg 0 | Max 0";
            _viewModel.FrameLoopTimingText =
                "FrameLoop(ms): Update Last 0 | Avg 0 | Max 0; Draw Last 0 | Avg 0 | Max 0; DrawSkip 0/0/0%; LayoutSkip 0/0/0%";
            _viewModel.HitchLogText = "Hitches: 0";
            return;
        }

        _viewModel.DocumentStatsText = $"Chars: {DemoTextBox.TextLength:N0} | Lines: {DemoTextBox.LogicalLineCount:N0}";
        _viewModel.ViewportStatsText =
            $"Offset X/Y: {DemoTextBox.HorizontalOffsetForTesting:0.##} / {DemoTextBox.VerticalOffsetForTesting:0.##}";
        _viewModel.WrappingText = $"Wrapping: {DemoTextBox.TextWrapping}";

        var perf = DemoTextBox.GetPerformanceSnapshot();
        _viewModel.InputTimingText =
            $"Input(ms) Last/Avg/Max: {perf.LastInputMutationMilliseconds:0.###} / {perf.AverageInputMutationMilliseconds:0.###} / {perf.MaxInputMutationMilliseconds:0.###} " +
            $"[Edit {perf.LastInputEditMilliseconds:0.###}, Commit {perf.LastInputCommitMilliseconds:0.###}, Caret {perf.LastInputEnsureCaretMilliseconds:0.###}] Samples={perf.InputMutationSampleCount}";
        _viewModel.RenderTimingText =
            $"Render(ms) Last/Avg/Max: {perf.LastRenderMilliseconds:0.###} / {perf.AverageRenderMilliseconds:0.###} / {perf.MaxRenderMilliseconds:0.###} " +
            $"[Viewport {perf.LastRenderViewportMilliseconds:0.###}, Selection {perf.LastRenderSelectionMilliseconds:0.###}, Text {perf.LastRenderTextMilliseconds:0.###}, Caret {perf.LastRenderCaretMilliseconds:0.###}] Samples={perf.RenderSampleCount}";
        _viewModel.ViewportTimingText =
            $"ViewportState(ms) Last/Avg/Max: {perf.LastViewportStateMilliseconds:0.###} / {perf.AverageViewportStateMilliseconds:0.###} / {perf.MaxViewportStateMilliseconds:0.###} " +
            $"Hit={perf.ViewportStateCacheHitCount} Miss={perf.ViewportStateCacheMissCount} Calls={perf.ViewportStateSampleCount} | " +
            $"LayoutCache Hit={perf.LayoutCacheHitCount} Miss={perf.LayoutCacheMissCount}";
        _viewModel.CaretTimingText =
            $"Caret(ms) Last/Avg/Max: {perf.LastEnsureCaretMilliseconds:0.###} / {perf.AverageEnsureCaretMilliseconds:0.###} / {perf.MaxEnsureCaretMilliseconds:0.###} " +
            $"[Viewport {perf.LastEnsureCaretViewportMilliseconds:0.###}, LineLookup {perf.LastEnsureCaretLineLookupMilliseconds:0.###}, Width {perf.LastEnsureCaretWidthMilliseconds:0.###}, Offset {perf.LastEnsureCaretOffsetAdjustMilliseconds:0.###}] " +
            $"FastPath Hit/Miss={perf.EnsureCaretFastPathHitCount}/{perf.EnsureCaretFastPathMissCount} Samples={perf.EnsureCaretSampleCount}";

        var uiMetrics = UiRoot.Current?.GetMetricsSnapshot();
        if (uiMetrics is { } metrics)
        {
            _viewModel.FrameLoopTimingText =
                $"UiRoot Draw Exec/Skip: {metrics.DrawExecutedFrameCount}/{metrics.DrawSkippedFrameCount} | " +
                $"Layout Exec/Skip: {metrics.LayoutExecutedFrameCount}/{metrics.LayoutSkippedFrameCount} | " +
                $"Dirty: {metrics.LastDirtyAreaPercentage:P1} ({metrics.LastDirtyRectCount} rects) | " +
                $"Fallbacks: {metrics.FullRedrawFallbackCount}";
            _viewModel.HitchLogText =
                $"Toggles Retained/Dirty/Conditional: {BoolToToggle(metrics.UseRetainedRenderList)}/" +
                $"{BoolToToggle(metrics.UseDirtyRegionRendering)}/{BoolToToggle(metrics.UseConditionalDrawScheduling)} " +
                $"| Cache H/M/R: {metrics.LastFrameCacheHitCount}/{metrics.LastFrameCacheMissCount}/{metrics.LastFrameCacheRebuildCount}";
        }
        else
        {
            _viewModel.FrameLoopTimingText = "UiRoot telemetry unavailable.";
            _viewModel.HitchLogText = "Toggles: n/a";
        }
    }

    private string BuildTelemetryClipboardText()
    {
        var builder = new StringBuilder(512);
        builder.AppendLine(_viewModel.DocumentStatsText);
        builder.AppendLine(_viewModel.ViewportStatsText);
        builder.AppendLine(_viewModel.WrappingText);
        builder.AppendLine(_viewModel.InputTimingText);
        builder.AppendLine(_viewModel.RenderTimingText);
        builder.AppendLine(_viewModel.ViewportTimingText);
        builder.AppendLine(_viewModel.CaretTimingText);
        builder.AppendLine(_viewModel.FrameLoopTimingText);
        builder.AppendLine(_viewModel.HitchLogText);
        builder.Append("Status: ");
        builder.Append(_viewModel.Status);
        return builder.ToString();
    }

    private static string BoolToToggle(bool enabled)
    {
        return enabled ? "On" : "Off";
    }

    private static bool TryCopyToSystemClipboard(string text)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "clip.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return false;
            }

            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit(1500);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Button button)
        {
            button.Font = font;
        }

        if (element is TextBox textBox)
        {
            textBox.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
