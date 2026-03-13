using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class CalendarTextDiagnosticsTests
{
    private readonly ITestOutputHelper _output;

    public CalendarTextDiagnosticsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InitialRefresh_ReportsCalendarMutationButtonInvalidationAndFontCacheProfiles()
    {
        UiTextRenderer.ConfigureRuntimeServicesForTests();
        var calendar = CreateCalendarHost(out var uiRoot);

        ResetDiagnostics();
        calendar.ResetDiagnosticsForTests();
        RunLayout(uiRoot);

        var calendarDiagnostics = calendar.GetDiagnosticsSnapshotForTests();
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var textTiming = UiTextRenderer.GetTimingSnapshotForTests();
        var backendTiming = FreeTypeFontRasterizer.GetTimingSnapshotForTests();
        var gridTiming = Grid.GetTimingSnapshotForTests();
        var uniformGridTiming = UniformGrid.GetTimingSnapshotForTests();
        var textLayoutMetrics = TextLayout.GetMetricsSnapshot();

        LogSnapshot("initial-refresh", calendarDiagnostics, buttonTiming, textTiming, backendTiming, gridTiming, uniformGridTiming, textLayoutMetrics);

        Assert.Equal(1, calendarDiagnostics.Total.MonthLabelTextChangeCount);
        Assert.Equal(7, calendarDiagnostics.Total.WeekDayLabelTextChangeCount);
        Assert.Equal(42, calendarDiagnostics.Total.DayButtonTextChangeCount);
        Assert.Equal(0, buttonTiming.TextPropertyChangedCount);
        Assert.Equal(0, buttonTiming.TextLayoutInvalidationCount);
        Assert.Equal(0, buttonTiming.IntrinsicNoWrapMeasureInvalidationCount);
        Assert.True(buttonTiming.IntrinsicNoWrapMeasurePathCount <= 2);
        Assert.True(textTiming.MeasureWidthCallCount > 0);
        Assert.True(backendTiming.MeasureCallCount > 0);
        Assert.True(backendTiming.GlyphAdvanceCacheMissCount > 0);
    }

    [Fact]
    public void MonthCycle_ReportsTextMutationDrivenButtonCacheInvalidationsWhileRendererAndBackendMostlyHitCaches()
    {
        UiTextRenderer.ConfigureRuntimeServicesForTests();
        var calendar = CreateCalendarHost(out var uiRoot);

        RunLayout(uiRoot);
        ResetDiagnostics();
        calendar.ResetDiagnosticsForTests();

        calendar.NextMonthButtonForTesting.InvokeFromInput();
        RunLayout(uiRoot);

        var calendarDiagnostics = calendar.GetDiagnosticsSnapshotForTests();
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var textTiming = UiTextRenderer.GetTimingSnapshotForTests();
        var backendTiming = FreeTypeFontRasterizer.GetTimingSnapshotForTests();
        var gridTiming = Grid.GetTimingSnapshotForTests();
        var uniformGridTiming = UniformGrid.GetTimingSnapshotForTests();
        var textLayoutMetrics = TextLayout.GetMetricsSnapshot();

        LogSnapshot("month-cycle", calendarDiagnostics, buttonTiming, textTiming, backendTiming, gridTiming, uniformGridTiming, textLayoutMetrics);

        Assert.True(calendarDiagnostics.Total.DayButtonTextChangeCount > 0);
        Assert.Equal(0, buttonTiming.TextPropertyChangedCount);
        Assert.Equal(0, buttonTiming.TextLayoutInvalidationCount);
        Assert.Equal(0, buttonTiming.IntrinsicNoWrapMeasureInvalidationCount);
        Assert.Equal(0, buttonTiming.IntrinsicNoWrapMeasureCacheMissCount);
        Assert.Equal(0, buttonTiming.IntrinsicNoWrapMeasureCacheHitCount);
        Assert.True(textTiming.MeasureWidthCallCount <= 1);
        Assert.True(textTiming.MetricsCacheMissCount <= 1);
        Assert.True(textTiming.LineHeightCacheHitCount > textTiming.LineHeightCacheMissCount);
        Assert.True(backendTiming.GlyphAdvanceCacheHitCount > backendTiming.GlyphAdvanceCacheMissCount);
        Assert.True(backendTiming.MeasureCallCount <= textTiming.MetricsCacheMissCount);
        Assert.True(backendTiming.KerningCacheHitCount > 0);
        Assert.Equal(0, uniformGridTiming.MeasureChildMeasureCount);
        Assert.Equal(0, uniformGridTiming.MeasureChildReuseCount);
        Assert.Equal(0, buttonTiming.MeasureOverrideElapsedTicks);
    }

    [Fact]
    public void Calendar_DayButtonDisplayTextChanges_NoLongerAffectMeasure()
    {
        UiTextRenderer.ConfigureRuntimeServicesForTests();

        var withTextCalendar = CreateCalendarHost(out var withTextRoot);
        RunLayout(withTextRoot);
        withTextCalendar.SetAllDayButtonTextForTests("31");
        var withTextMetrics = MeasureCalendarScenario(withTextCalendar, withTextRoot);

        UiTextRenderer.ConfigureRuntimeServicesForTests();
        var withoutTextCalendar = CreateCalendarHost(out var withoutTextRoot);
        RunLayout(withoutTextRoot);
        withoutTextCalendar.SetAllDayButtonTextForTests(string.Empty);
        var withoutTextMetrics = MeasureCalendarScenario(withoutTextCalendar, withoutTextRoot);

        _output.WriteLine(
            $"scenario=calendar-day-buttons withText[rootMeasureTicks={withTextMetrics.CalendarMeasureTicks}, rootExclusiveTicks={withTextMetrics.CalendarMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={withTextMetrics.ButtonMeasureTicks}, gridMeasureTicks={withTextMetrics.GridMeasureTicks}, uniformGridMeasureTicks={withTextMetrics.UniformGridMeasureTicks}, " +
            $"fontMeasureWidthTicks={withTextMetrics.FontMeasureWidthTicks}, fontMeasureWidthCalls={withTextMetrics.FontMeasureWidthCalls}, " +
            $"fontLineHeightTicks={withTextMetrics.FontLineHeightTicks}, fontLineHeightCalls={withTextMetrics.FontLineHeightCalls}]");
        _output.WriteLine(
            $"scenario=calendar-day-buttons withoutText[rootMeasureTicks={withoutTextMetrics.CalendarMeasureTicks}, rootExclusiveTicks={withoutTextMetrics.CalendarMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={withoutTextMetrics.ButtonMeasureTicks}, gridMeasureTicks={withoutTextMetrics.GridMeasureTicks}, uniformGridMeasureTicks={withoutTextMetrics.UniformGridMeasureTicks}, " +
            $"fontMeasureWidthTicks={withoutTextMetrics.FontMeasureWidthTicks}, fontMeasureWidthCalls={withoutTextMetrics.FontMeasureWidthCalls}, " +
            $"fontLineHeightTicks={withoutTextMetrics.FontLineHeightTicks}, fontLineHeightCalls={withoutTextMetrics.FontLineHeightCalls}]");

        Assert.Equal(0, withTextMetrics.CalendarMeasureTicks);
        Assert.Equal(0, withoutTextMetrics.CalendarMeasureTicks);
        Assert.Equal(0, withTextMetrics.ButtonMeasureTicks);
        Assert.Equal(0, withoutTextMetrics.ButtonMeasureTicks);
        Assert.Equal(0, withTextMetrics.FontMeasureWidthCalls);
        Assert.Equal(0, withoutTextMetrics.FontMeasureWidthCalls);
        Assert.Equal(0, withTextMetrics.FontLineHeightCalls);
        Assert.Equal(0, withoutTextMetrics.FontLineHeightCalls);
    }

    private void LogSnapshot(
        string scenario,
        CalendarDiagnosticsSnapshot calendarDiagnostics,
        ButtonTimingSnapshot buttonTiming,
        UiTextRendererTimingSnapshot textTiming,
        UiRuntimeFontBackendTimingSnapshot backendTiming,
        GridTimingSnapshot gridTiming,
        UniformGridTimingSnapshot uniformGridTiming,
        TextLayout.TextLayoutMetricsSnapshot textLayoutMetrics)
    {
        _output.WriteLine(
            $"scenario={scenario} refreshCount={calendarDiagnostics.RefreshCount} " +
            $"calendar[last: dayText={calendarDiagnostics.LastRefresh.DayButtonTextChangeCount}, dayEnabled={calendarDiagnostics.LastRefresh.DayButtonEnabledChangeCount}, " +
            $"dayBackground={calendarDiagnostics.LastRefresh.DayButtonBackgroundChangeCount}, dayForeground={calendarDiagnostics.LastRefresh.DayButtonForegroundChangeCount}, " +
            $"dayBorder={calendarDiagnostics.LastRefresh.DayButtonBorderBrushChangeCount}, weekText={calendarDiagnostics.LastRefresh.WeekDayLabelTextChangeCount}, " +
            $"monthText={calendarDiagnostics.LastRefresh.MonthLabelTextChangeCount}, navEnabled={calendarDiagnostics.LastRefresh.NavigationEnabledChangeCount}]");
        _output.WriteLine(
            $"scenario={scenario} button[measureTicks={buttonTiming.MeasureOverrideElapsedTicks}, textLayoutTicks={buttonTiming.ResolveTextLayoutElapsedTicks}, " +
            $"textChanged={buttonTiming.TextPropertyChangedCount}, textLayoutInvalidations={buttonTiming.TextLayoutInvalidationCount}, " +
            $"intrinsicInvalidations={buttonTiming.IntrinsicNoWrapMeasureInvalidationCount}, intrinsicHits={buttonTiming.IntrinsicNoWrapMeasureCacheHitCount}, " +
            $"intrinsicMisses={buttonTiming.IntrinsicNoWrapMeasureCacheMissCount}, textLayoutHits={buttonTiming.TextLayoutCacheHitCount}, " +
            $"textLayoutMisses={buttonTiming.TextLayoutCacheMissCount}, fastPath={buttonTiming.PlainTextMeasureFastPathCount}, " +
            $"intrinsicPath={buttonTiming.IntrinsicNoWrapMeasurePathCount}, layoutPath={buttonTiming.TextLayoutMeasurePathCount}]");
        _output.WriteLine(
            $"scenario={scenario} textRenderer[measureWidthTicks={textTiming.MeasureWidthElapsedTicks}, measureWidthCalls={textTiming.MeasureWidthCallCount}, " +
            $"lineHeightTicks={textTiming.GetLineHeightElapsedTicks}, lineHeightCalls={textTiming.GetLineHeightCallCount}, " +
            $"typefaceHits={textTiming.TypefaceCacheHitCount}, typefaceMisses={textTiming.TypefaceCacheMissCount}, metricsHits={textTiming.MetricsCacheHitCount}, " +
            $"metricsMisses={textTiming.MetricsCacheMissCount}, lineHeightHits={textTiming.LineHeightCacheHitCount}, lineHeightMisses={textTiming.LineHeightCacheMissCount}]");
        _output.WriteLine(
            $"scenario={scenario} backend[measureTicks={backendTiming.MeasureElapsedTicks}, measureCalls={backendTiming.MeasureCallCount}, " +
            $"rasterizeTicks={backendTiming.RasterizeElapsedTicks}, rasterizeCalls={backendTiming.RasterizeCallCount}, faceHits={backendTiming.FaceCacheHitCount}, " +
            $"faceMisses={backendTiming.FaceCacheMissCount}, faceSizeReuse={backendTiming.FaceSizeReuseHitCount}, faceSizeChanges={backendTiming.FaceSizeChangeCount}, " +
            $"glyphAdvanceHits={backendTiming.GlyphAdvanceCacheHitCount}, glyphAdvanceMisses={backendTiming.GlyphAdvanceCacheMissCount}, " +
            $"kerningHits={backendTiming.KerningCacheHitCount}, kerningMisses={backendTiming.KerningCacheMissCount}, " +
            $"verticalHits={backendTiming.VerticalMetricsCacheHitCount}, verticalMisses={backendTiming.VerticalMetricsCacheMissCount}]");
        _output.WriteLine(
            $"scenario={scenario} layout[gridMeasureTicks={gridTiming.MeasureOverrideElapsedTicks}, uniformGridMeasureTicks={uniformGridTiming.MeasureOverrideElapsedTicks}, " +
            $"uniformGridChildMeasures={uniformGridTiming.MeasureChildMeasureCount}, uniformGridChildReuses={uniformGridTiming.MeasureChildReuseCount}, " +
            $"textLayoutBuilds={textLayoutMetrics.BuildCount}, textLayoutNoWrapBuilds={textLayoutMetrics.NoWrapBuildCount}, cacheMisses={textLayoutMetrics.CacheMissCount}]");
    }

    private static Calendar CreateCalendarHost(out UiRoot uiRoot)
    {
        var calendar = new Calendar
        {
            Width = 340f,
            Height = 320f
        };

        var host = new Canvas
        {
            Width = 600f,
            Height = 420f
        };
        host.AddChild(calendar);

        uiRoot = new UiRoot(host);
        return calendar;
    }

    private static CalendarScenarioMetrics MeasureCalendarScenario(Calendar calendar, UiRoot uiRoot)
    {
        var before = CaptureTiming(calendar);
        ResetDiagnostics();
        RunLayout(uiRoot);

        var after = CaptureTiming(calendar);
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var gridTiming = Grid.GetTimingSnapshotForTests();
        var uniformGridTiming = UniformGrid.GetTimingSnapshotForTests();
        var textTiming = UiTextRenderer.GetTimingSnapshotForTests();

        return new CalendarScenarioMetrics(
            after.MeasureElapsedTicks - before.MeasureElapsedTicks,
            after.MeasureExclusiveElapsedTicks - before.MeasureExclusiveElapsedTicks,
            buttonTiming.MeasureOverrideElapsedTicks,
            gridTiming.MeasureOverrideElapsedTicks,
            uniformGridTiming.MeasureOverrideElapsedTicks,
            textTiming.MeasureWidthElapsedTicks,
            textTiming.MeasureWidthCallCount,
            textTiming.GetLineHeightElapsedTicks,
            textTiming.GetLineHeightCallCount);
    }

    private static FrameworkTimingSnapshot CaptureTiming(FrameworkElement element)
    {
        return new FrameworkTimingSnapshot(
            element.MeasureElapsedTicksForTests,
            element.MeasureExclusiveElapsedTicksForTests);
    }

    private static void ResetDiagnostics()
    {
        Button.ResetTimingForTests();
        Grid.ResetTimingForTests();
        UniformGrid.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();
        UiTextRenderer.ResetTimingForTests();
        FreeTypeFontRasterizer.ResetTimingForTests();
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 600, 420));
    }

    private readonly record struct FrameworkTimingSnapshot(
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks);

    private readonly record struct CalendarScenarioMetrics(
        long CalendarMeasureTicks,
        long CalendarMeasureExclusiveTicks,
        long ButtonMeasureTicks,
        long GridMeasureTicks,
        long UniformGridMeasureTicks,
        long FontMeasureWidthTicks,
        int FontMeasureWidthCalls,
        long FontLineHeightTicks,
        int FontLineHeightCalls);
}
