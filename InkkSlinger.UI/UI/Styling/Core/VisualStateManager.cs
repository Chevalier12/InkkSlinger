using System.Diagnostics;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public static class VisualStateManager
{
    private static long s_goToStateCallCount;
    private static long s_goToStateTicks;
    private static long s_goToElementStateCallCount;
    private static long s_goToElementStateTicks;
    private static long s_tryGoToStateCallCount;
    private static long s_tryGoToStateTicks;
    private static long s_matchedGroupCount;
    private static long s_groupGoToStateCallCount;
    private static long s_groupGoToStateTicks;
    private static long s_groupApplySettersTicks;
    private static long s_groupStoryboardTicks;
    private static long s_setTemplateTriggerValueCount;
    private static long s_clearTemplateTriggerValueCount;
    private static long s_clearStateCallCount;

    public static readonly DependencyProperty VisualStateGroupsProperty =
        DependencyProperty.RegisterAttached(
            "VisualStateGroups",
            typeof(VisualStateGroupCollection),
            typeof(VisualStateManager),
            new FrameworkPropertyMetadata(null));

    internal static void ResetTelemetryForTests()
    {
        s_goToStateCallCount = 0;
        s_goToStateTicks = 0;
        s_goToElementStateCallCount = 0;
        s_goToElementStateTicks = 0;
        s_tryGoToStateCallCount = 0;
        s_tryGoToStateTicks = 0;
        s_matchedGroupCount = 0;
        s_groupGoToStateCallCount = 0;
        s_groupGoToStateTicks = 0;
        s_groupApplySettersTicks = 0;
        s_groupStoryboardTicks = 0;
        s_setTemplateTriggerValueCount = 0;
        s_clearTemplateTriggerValueCount = 0;
        s_clearStateCallCount = 0;
    }

    internal static VisualStateTelemetrySnapshot GetTelemetrySnapshotForTests()
    {
        return new VisualStateTelemetrySnapshot(
            s_goToStateCallCount,
            TicksToMilliseconds(s_goToStateTicks),
            s_goToElementStateCallCount,
            TicksToMilliseconds(s_goToElementStateTicks),
            s_tryGoToStateCallCount,
            TicksToMilliseconds(s_tryGoToStateTicks),
            s_matchedGroupCount,
            s_groupGoToStateCallCount,
            TicksToMilliseconds(s_groupGoToStateTicks),
            TicksToMilliseconds(s_groupApplySettersTicks),
            TicksToMilliseconds(s_groupStoryboardTicks),
            s_setTemplateTriggerValueCount,
            s_clearTemplateTriggerValueCount,
            s_clearStateCallCount);
    }

    public static VisualStateGroupCollection GetVisualStateGroups(DependencyObject dependencyObject)
    {
        if (dependencyObject.GetValue(VisualStateGroupsProperty) is VisualStateGroupCollection groups)
        {
            return groups;
        }

        groups = new VisualStateGroupCollection();
        dependencyObject.SetValue(VisualStateGroupsProperty, groups);
        return groups;
    }

    public static void SetVisualStateGroups(DependencyObject dependencyObject, VisualStateGroupCollection? value)
    {
        dependencyObject.SetValue(VisualStateGroupsProperty, value);
    }

    public static bool GoToState(Control control, string stateName, bool useTransitions = true)
    {
        _ = useTransitions;

        s_goToStateCallCount++;
        var start = Stopwatch.GetTimestamp();

        if (TryGetStateHost(control, out var stateHost) && stateHost != null)
        {
            var context = new TriggerActionContext(
                control,
                stateHost,
                name => control.FindTemplateNamedObject(name));
            if (TryGoToState(stateHost, context, stateName))
            {
                s_goToStateTicks += Stopwatch.GetTimestamp() - start;
                return true;
            }
        }

        if (control is FrameworkElement frameworkElement)
        {
            var matched = GoToElementState(frameworkElement, stateName, useTransitions);
            s_goToStateTicks += Stopwatch.GetTimestamp() - start;
            return matched;
        }

        s_goToStateTicks += Stopwatch.GetTimestamp() - start;
        return false;
    }

    public static bool GoToElementState(FrameworkElement element, string stateName, bool useTransitions = true)
    {
        _ = useTransitions;

        s_goToElementStateCallCount++;
        var start = Stopwatch.GetTimestamp();

        var context = new TriggerActionContext(
            element,
            element,
            name => NameScopeService.FindName(element, name) ?? element.FindName(name));
        var matched = TryGoToState(element, context, stateName);
        s_goToElementStateTicks += Stopwatch.GetTimestamp() - start;
        return matched;
    }

    internal static void ClearState(FrameworkElement? stateHost)
    {
        if (stateHost == null)
        {
            return;
        }

        if (stateHost.GetValue(VisualStateGroupsProperty) is not VisualStateGroupCollection groups)
        {
            return;
        }

        for (var i = 0; i < groups.Count; i++)
        {
            s_clearStateCallCount++;
            groups[i].ClearState(stateHost);
        }
    }

    internal static void RecordGroupTransition(long totalTicks, long applySettersTicks, long storyboardTicks, int setCount, int clearCount)
    {
        s_matchedGroupCount++;
        s_groupGoToStateCallCount++;
        s_groupGoToStateTicks += totalTicks;
        s_groupApplySettersTicks += applySettersTicks;
        s_groupStoryboardTicks += storyboardTicks;
        s_setTemplateTriggerValueCount += setCount;
        s_clearTemplateTriggerValueCount += clearCount;
    }

    private static bool TryGetStateHost(Control control, out FrameworkElement? stateHost)
    {
        foreach (var child in control.GetVisualChildren())
        {
            if (child is FrameworkElement frameworkElement)
            {
                stateHost = frameworkElement;
                return true;
            }
        }

        stateHost = null;
        return false;
    }

    private static bool TryGoToState(DependencyObject target, TriggerActionContext context, string stateName)
    {
        s_tryGoToStateCallCount++;
        var start = Stopwatch.GetTimestamp();

        if (target.GetValue(VisualStateGroupsProperty) is not VisualStateGroupCollection groups)
        {
            s_tryGoToStateTicks += Stopwatch.GetTimestamp() - start;
            return false;
        }

        var matched = false;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (!group.ContainsState(stateName))
            {
                continue;
            }

            matched = group.GoToState(context, stateName) || matched;
        }

        s_tryGoToStateTicks += Stopwatch.GetTimestamp() - start;
        return matched;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}

