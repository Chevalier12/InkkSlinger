using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCalendarMonthNavigationHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;

    [Fact]
    public void ControlsCatalog_CalendarMonthNavigation_WritesHotspotDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            AnimationManager.Current.ResetForTests();
            VisualTreeHelper.ResetInstrumentationForTests();

            var catalog = new ControlsCatalogView
            {
                Width = ViewportWidth,
                Height = ViewportHeight
            };

            var host = new Canvas
            {
                Width = ViewportWidth,
                Height = ViewportHeight
            };
            host.AddChild(catalog);

            var uiRoot = new UiRoot(host);
            RunFrame(uiRoot, 16);

            ClickCatalogButton(uiRoot, catalog, "Calendar");
            RunFrame(uiRoot, 32);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var previewRoot = Assert.IsAssignableFrom<UIElement>(previewHost.Content);
            var previewRootFramework = Assert.IsAssignableFrom<FrameworkElement>(previewRoot);
            var calendar = Assert.IsType<Calendar>(FindFirstVisualChild<Calendar>(previewRoot));

            PrimeRetainedRenderStateForDiagnostics(uiRoot);
            var beforeElementTimings = SnapshotElementTimings(previewRootFramework);

            ResetDiagnostics();
            calendar.ResetDiagnosticsForTests();
            AnimationManager.Current.ResetTelemetryForTests();
            VisualTreeHelper.ResetInstrumentationForTests();
            Freezable.ResetTelemetryForTests();
            UIElement.ResetFreezableInvalidationBatchTelemetryForTests();

            var clickTarget = GetCenter(calendar.NextMonthButtonForTesting.LayoutSlot);
            var clickElapsedMs = MeasureMilliseconds(() => Click(uiRoot, clickTarget));
            var frameElapsedMs = MeasureMilliseconds(() => RunFrame(uiRoot, 48));

            var calendarDiagnostics = calendar.GetDiagnosticsSnapshotForTests();
            var buttonTiming = Button.GetTimingSnapshotForTests();
            var dayButtonTiming = CalendarDayButton.GetTimingSnapshotForTests();
            var gridTiming = Grid.GetTimingSnapshotForTests();
            var uniformGridTiming = UniformGrid.GetTimingSnapshotForTests();
            var textLayoutMetrics = TextLayout.GetMetricsSnapshot();
            var textTiming = UiTextRenderer.GetTimingSnapshotForTests();
            var backendTiming = FreeTypeFontRasterizer.GetTimingSnapshotForTests();
            var inputMetrics = uiRoot.GetInputMetricsSnapshot();
            var rootMetrics = uiRoot.GetMetricsSnapshot();
            var renderTelemetry = uiRoot.GetRenderTelemetrySnapshotForTests();
            var performanceTelemetry = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var freezableTelemetry = Freezable.GetTelemetrySnapshotForTests();
            var freezableBatchTelemetry = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
            var elementHotspots = CaptureElementTimingDeltas(previewRootFramework, beforeElementTimings)
                .OrderByDescending(static delta => delta.Timing.MeasureExclusiveElapsedTicks)
                .ThenByDescending(static delta => delta.Timing.MeasureElapsedTicks)
                .ThenByDescending(static delta => delta.Timing.ArrangeElapsedTicks)
                .ThenBy(static delta => delta.Path, StringComparer.Ordinal)
                .ToArray();
            var typeHotspots = elementHotspots
                .GroupBy(static delta => delta.Element.GetType().Name, StringComparer.Ordinal)
                .Select(static group => new TypeHotspot(
                    group.Key,
                    group.Count(),
                    group.Sum(static delta => delta.Timing.MeasureWork),
                    group.Sum(static delta => delta.Timing.ArrangeWork),
                    group.Sum(static delta => delta.Timing.MeasureElapsedTicks),
                    group.Sum(static delta => delta.Timing.MeasureExclusiveElapsedTicks),
                    group.Sum(static delta => delta.Timing.ArrangeElapsedTicks)))
                .OrderByDescending(static hotspot => hotspot.MeasureExclusiveElapsedTicks)
                .ThenByDescending(static hotspot => hotspot.MeasureElapsedTicks)
                .ThenByDescending(static hotspot => hotspot.ArrangeElapsedTicks)
                .ThenBy(static hotspot => hotspot.TypeName, StringComparer.Ordinal)
                .ToArray();

            var logPath = GetDiagnosticsLogPath("controls-catalog-calendar-month-navigation-hotspot");
            var lines = new List<string>
            {
                "scenario=Controls Catalog Calendar month navigation hotspot diagnostics",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=open Controls Catalog",
                "step_2=click Calendar button from sidebar",
                "step_3=click the '>' month navigation button",
                "step_4=attribute the stall across calendar refresh, layout, rendering, and retained-tree telemetry",
                $"click_elapsed_ms={clickElapsedMs:0.###}",
                $"frame_elapsed_ms={frameElapsedMs:0.###}",
                $"calendar_refresh_count={calendarDiagnostics.RefreshCount}",
                $"calendar_last_month_label={calendar.MonthLabelTextForTesting}",
                $"calendar_day_button_count={calendar.DayButtonsForTesting.Count}"
            };

            lines.Add(string.Empty);
            lines.Add("calendar_refresh_counts:");
            lines.Add(
                $"day_text_changes={calendarDiagnostics.LastRefresh.DayButtonTextChangeCount} day_enabled_changes={calendarDiagnostics.LastRefresh.DayButtonEnabledChangeCount} " +
                $"day_background_changes={calendarDiagnostics.LastRefresh.DayButtonBackgroundChangeCount} day_foreground_changes={calendarDiagnostics.LastRefresh.DayButtonForegroundChangeCount} " +
                $"day_border_changes={calendarDiagnostics.LastRefresh.DayButtonBorderBrushChangeCount} week_text_changes={calendarDiagnostics.LastRefresh.WeekDayLabelTextChangeCount} " +
                $"month_text_changes={calendarDiagnostics.LastRefresh.MonthLabelTextChangeCount} nav_enabled_changes={calendarDiagnostics.LastRefresh.NavigationEnabledChangeCount}");

            lines.Add(string.Empty);
            lines.Add("calendar_refresh_timings:");
            lines.Add(
                $"total_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.TotalElapsedTicks):0.###} month_label_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.MonthLabelElapsedTicks):0.###} " +
                $"weekday_labels_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.WeekDayLabelsElapsedTicks):0.###} day_loop_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.DayLoopElapsedTicks):0.###} " +
                $"day_setup_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.DayButtonDateSetupElapsedTicks):0.###} day_text_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.DayButtonTextElapsedTicks):0.###} " +
                $"day_enabled_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.DayButtonEnabledElapsedTicks):0.###} day_background_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.DayButtonBackgroundElapsedTicks):0.###} " +
                $"day_foreground_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.DayButtonForegroundElapsedTicks):0.###} day_border_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.DayButtonBorderBrushElapsedTicks):0.###} " +
                $"nav_buttons_ms={TicksToMilliseconds(calendarDiagnostics.LastRefreshTiming.NavigationButtonsElapsedTicks):0.###}");

            lines.Add(string.Empty);
            lines.Add("framework_timings:");
            lines.Add(
                $"button_measure_ms={TicksToMilliseconds(buttonTiming.MeasureOverrideElapsedTicks):0.###} button_render_ms={TicksToMilliseconds(buttonTiming.RenderElapsedTicks):0.###} " +
                $"button_text_layout_ms={TicksToMilliseconds(buttonTiming.ResolveTextLayoutElapsedTicks):0.###} button_content_changed={buttonTiming.ContentPropertyChangedCount} " +
                $"button_text_layout_invalidations={buttonTiming.TextLayoutInvalidationCount} button_intrinsic_invalidations={buttonTiming.IntrinsicNoWrapMeasureInvalidationCount}");
            lines.Add(
                $"calendar_day_button_render_ms={TicksToMilliseconds(dayButtonTiming.RenderElapsedTicks):0.###} calendar_day_button_render_calls={dayButtonTiming.RenderCallCount} " +
                $"calendar_day_button_non_empty_render_calls={dayButtonTiming.NonEmptyRenderCallCount}");
            lines.Add(
                $"grid_measure_ms={TicksToMilliseconds(gridTiming.MeasureOverrideElapsedTicks):0.###} uniform_grid_measure_ms={TicksToMilliseconds(uniformGridTiming.MeasureOverrideElapsedTicks):0.###} " +
                $"uniform_grid_child_measures={uniformGridTiming.MeasureChildMeasureCount} uniform_grid_child_reuses={uniformGridTiming.MeasureChildReuseCount}");
            lines.Add(
                $"text_layout_build_ms={TicksToMilliseconds(textLayoutMetrics.BuildElapsedTicks):0.###} text_layout_layout_ms={TicksToMilliseconds(textLayoutMetrics.LayoutElapsedTicks):0.###} " +
                $"text_layout_build_count={textLayoutMetrics.BuildCount} text_layout_cache_misses={textLayoutMetrics.CacheMissCount}");
            lines.Add(
                $"text_renderer_measure_width_ms={TicksToMilliseconds(textTiming.MeasureWidthElapsedTicks):0.###} text_renderer_measure_width_calls={textTiming.MeasureWidthCallCount} " +
                $"text_renderer_line_height_ms={TicksToMilliseconds(textTiming.GetLineHeightElapsedTicks):0.###} text_renderer_line_height_calls={textTiming.GetLineHeightCallCount}");
            lines.Add(
                $"font_backend_measure_ms={TicksToMilliseconds(backendTiming.MeasureElapsedTicks):0.###} font_backend_measure_calls={backendTiming.MeasureCallCount} " +
                $"font_backend_glyph_advance_hits={backendTiming.GlyphAdvanceCacheHitCount} font_backend_glyph_advance_misses={backendTiming.GlyphAdvanceCacheMissCount}");

            lines.Add(string.Empty);
            lines.Add("ui_root_phase_timings:");
            lines.Add(
                $"input_phase_ms={performanceTelemetry.InputPhaseMilliseconds:0.###} binding_phase_ms={performanceTelemetry.BindingPhaseMilliseconds:0.###} " +
                $"layout_phase_ms={performanceTelemetry.LayoutPhaseMilliseconds:0.###} animation_phase_ms={performanceTelemetry.AnimationPhaseMilliseconds:0.###} " +
                $"render_scheduling_phase_ms={performanceTelemetry.RenderSchedulingPhaseMilliseconds:0.###} visual_update_ms={performanceTelemetry.VisualUpdateMilliseconds:0.###}");
            lines.Add(
                $"hottest_layout_measure_element={performanceTelemetry.HottestLayoutMeasureElementType}:{performanceTelemetry.HottestLayoutMeasureElementName}:{performanceTelemetry.HottestLayoutMeasureElementMilliseconds:0.###} " +
                $"hottest_layout_arrange_element={performanceTelemetry.HottestLayoutArrangeElementType}:{performanceTelemetry.HottestLayoutArrangeElementName}:{performanceTelemetry.HottestLayoutArrangeElementMilliseconds:0.###}");
            lines.Add(
                $"input_hover_ms={inputMetrics.LastInputHoverUpdateMilliseconds:0.###} input_route_ms={inputMetrics.LastInputPointerRouteMilliseconds:0.###} " +
                $"input_hit_tests={inputMetrics.HitTestCount} input_routed_events={inputMetrics.RoutedEventCount} input_pointer_events={inputMetrics.PointerEventCount}");

            lines.Add(string.Empty);
            lines.Add("render_retained_telemetry:");
            lines.Add(
                $"dirty_rect_count={rootMetrics.LastDirtyRectCount} dirty_area_pct={rootMetrics.LastDirtyAreaPercentage:0.###} full_redraw_fallbacks={rootMetrics.FullRedrawFallbackCount} " +
                $"retained_nodes={rootMetrics.RetainedRenderNodeCount} retained_high_cost_nodes={rootMetrics.RetainedHighCostNodeCount} retained_dirty_visuals={rootMetrics.LastRetainedDirtyVisualCount}");
            lines.Add(
                $"retained_traversals={renderTelemetry.RetainedTraversalCount} dirty_region_traversals={renderTelemetry.DirtyRegionTraversalCount} retained_nodes_visited={renderTelemetry.RetainedNodesVisited} " +
                $"retained_nodes_drawn={renderTelemetry.RetainedNodesDrawn} dirty_root_count={renderTelemetry.DirtyRootCount} dirty_threshold_fallbacks={renderTelemetry.DirtyRegionThresholdFallbackCount} " +
                $"full_dirty_rebuilds={renderTelemetry.FullDirtyRetainedRebuildCount} full_dirty_structure_changes={renderTelemetry.FullDirtyVisualStructureChangeCount}");

            lines.Add(string.Empty);
            lines.Add("freezable_telemetry:");
            lines.Add(
                $"on_changed_calls={freezableTelemetry.OnChangedCallCount} on_changed_ms={freezableTelemetry.OnChangedMilliseconds:0.###} " +
                $"hottest_on_changed={freezableTelemetry.HottestOnChangedType}:{freezableTelemetry.HottestOnChangedMilliseconds:0.###}");
            lines.Add(
                $"batch_flushes={freezableBatchTelemetry.FlushCount} batch_flush_targets={freezableBatchTelemetry.FlushTargetCount} queued_targets={freezableBatchTelemetry.QueuedTargetCount} " +
                $"max_pending_targets={freezableBatchTelemetry.MaxPendingTargetCount} batch_flush_ms={freezableBatchTelemetry.FlushMilliseconds:0.###}");

            lines.Add(string.Empty);
            lines.Add("type_hotspots:");
            foreach (var hotspot in typeHotspots.Take(12))
            {
                lines.Add(
                    $"{hotspot.TypeName} elements={hotspot.ElementCount} measure_work={hotspot.MeasureWork} arrange_work={hotspot.ArrangeWork} " +
                    $"measure_ms={TicksToMilliseconds(hotspot.MeasureElapsedTicks):0.###} measure_exclusive_ms={TicksToMilliseconds(hotspot.MeasureExclusiveElapsedTicks):0.###} " +
                    $"arrange_ms={TicksToMilliseconds(hotspot.ArrangeElapsedTicks):0.###}");
            }

            lines.Add(string.Empty);
            lines.Add("element_hotspots:");
            foreach (var hotspot in elementHotspots.Take(12))
            {
                lines.Add(
                    $"{hotspot.Path} measure_work={hotspot.Timing.MeasureWork} arrange_work={hotspot.Timing.ArrangeWork} " +
                    $"measure_ms={TicksToMilliseconds(hotspot.Timing.MeasureElapsedTicks):0.###} measure_exclusive_ms={TicksToMilliseconds(hotspot.Timing.MeasureExclusiveElapsedTicks):0.###} " +
                    $"arrange_ms={TicksToMilliseconds(hotspot.Timing.ArrangeElapsedTicks):0.###}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, lines);

            Assert.True(File.Exists(logPath));
            Assert.True(calendarDiagnostics.LastRefreshTiming.TotalElapsedTicks > 0);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static void ResetDiagnostics()
    {
        Button.ResetTimingForTests();
        CalendarDayButton.ResetTimingForTests();
        Grid.ResetTimingForTests();
        UniformGrid.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();
        UiTextRenderer.ResetTimingForTests();
        FreeTypeFontRasterizer.ResetTimingForTests();
    }

    private static double MeasureMilliseconds(Action action)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        return TicksToMilliseconds(Stopwatch.GetTimestamp() - start);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static void PrimeRetainedRenderStateForDiagnostics(UiRoot uiRoot)
    {
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));
    }

    private static void ClickCatalogButton(UiRoot uiRoot, ControlsCatalogView view, string buttonText)
    {
        var button = FindCatalogButton(view, buttonText);
        Assert.NotNull(button);
        Click(uiRoot, GetCenter(button!.LayoutSlot));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static Dictionary<FrameworkElement, LayoutTiming> SnapshotElementTimings(UIElement root)
    {
        return EnumerateVisualTree(root)
            .OfType<FrameworkElement>()
            .ToDictionary(
                static element => element,
                static element => new LayoutTiming(
                    element.MeasureWorkCount,
                    element.ArrangeWorkCount,
                    element.MeasureElapsedTicksForTests,
                    element.MeasureExclusiveElapsedTicksForTests,
                    element.ArrangeElapsedTicksForTests));
    }

    private static IReadOnlyList<ElementHotspot> CaptureElementTimingDeltas(
        UIElement root,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot)
    {
        return EnumerateVisualTree(root)
            .OfType<FrameworkElement>()
            .Select(element => new ElementHotspot(
                element,
                DescribeVisualPath(element, root),
                GetLayoutTimingDelta(element, beforeSnapshot)))
            .Where(static hotspot =>
                hotspot.Timing.MeasureWork > 0 ||
                hotspot.Timing.ArrangeWork > 0 ||
                hotspot.Timing.MeasureElapsedTicks > 0 ||
                hotspot.Timing.MeasureExclusiveElapsedTicks > 0 ||
                hotspot.Timing.ArrangeElapsedTicks > 0)
            .ToArray();
    }

    private static LayoutTiming GetLayoutTimingDelta(
        FrameworkElement element,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot)
    {
        beforeSnapshot.TryGetValue(element, out var before);
        return new LayoutTiming(
            element.MeasureWorkCount - before.MeasureWork,
            element.ArrangeWorkCount - before.ArrangeWork,
            element.MeasureElapsedTicksForTests - before.MeasureElapsedTicks,
            element.MeasureExclusiveElapsedTicksForTests - before.MeasureExclusiveElapsedTicks,
            element.ArrangeElapsedTicksForTests - before.ArrangeElapsedTicks);
    }

    private static string DescribeVisualPath(FrameworkElement element, UIElement root)
    {
        var segments = new Stack<string>();
        UIElement? current = element;
        while (current != null)
        {
            segments.Push(DescribeElement(current));
            if (ReferenceEquals(current, root))
            {
                break;
            }

            current = current.VisualParent;
        }

        return string.Join(" > ", segments);
    }

    private static string DescribeElement(UIElement element)
    {
        return element switch
        {
            Button button when !string.IsNullOrEmpty(button.GetContentText()) => $"{nameof(Button)}(\"{button.GetContentText()}\")",
            Label label when !string.IsNullOrEmpty(label.GetContentText()) => $"{nameof(Label)}(\"{label.GetContentText()}\")",
            UniformGrid uniformGrid => $"{nameof(UniformGrid)}(Rows={uniformGrid.Rows}, Columns={uniformGrid.Columns})",
            Grid grid => $"{nameof(Grid)}(Rows={grid.RowDefinitions.Count}, Columns={grid.ColumnDefinitions.Count})",
            _ => element.GetType().Name
        };
    }

    private static Button? FindCatalogButton(UIElement root, string text)
    {
        if (root is Button button && string.Equals(button.GetContentText(), text, StringComparison.Ordinal))
        {
            return button;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindCatalogButton(child, text);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<UIElement> EnumerateVisualTree(UIElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var nested in EnumerateVisualTree(child))
            {
                yield return nested;
            }
        }
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunFrame(UiRoot uiRoot, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, ViewportWidth, ViewportHeight));
    }

    private static string GetDiagnosticsLogPath(string fileNameWithoutExtension)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "artifacts", "diagnostics", $"{fileNameWithoutExtension}.txt");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (current.EnumerateFiles("InkkSlinger.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(
            resources.ToList(),
            resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private readonly record struct ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);

    private readonly record struct LayoutTiming(
        long MeasureWork,
        long ArrangeWork,
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks,
        long ArrangeElapsedTicks);

    private readonly record struct ElementHotspot(
        FrameworkElement Element,
        string Path,
        LayoutTiming Timing);

    private readonly record struct TypeHotspot(
        string TypeName,
        int ElementCount,
        long MeasureWork,
        long ArrangeWork,
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks,
        long ArrangeElapsedTicks);
}
