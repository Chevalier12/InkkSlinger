using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class Storyboard : Timeline
{
    private readonly List<Timeline> _children = new();

    public IList<Timeline> Children => _children;

    public void Begin(
        FrameworkElement containingObject,
        bool isControllable = false,
        HandoffBehavior handoff = HandoffBehavior.SnapshotAndReplace)
    {
        AnimationManager.Current.BeginStoryboard(this, containingObject, null, null, isControllable, handoff);
    }

    public void Pause(FrameworkElement containingObject)
    {
        AnimationManager.Current.PauseStoryboard(this, containingObject);
    }

    public void Resume(FrameworkElement containingObject)
    {
        AnimationManager.Current.ResumeStoryboard(this, containingObject);
    }

    public void Stop(FrameworkElement containingObject)
    {
        AnimationManager.Current.StopStoryboard(this, containingObject);
    }

    public void Remove(FrameworkElement containingObject)
    {
        AnimationManager.Current.RemoveStoryboard(this, containingObject);
    }

    public void Seek(FrameworkElement containingObject, TimeSpan offset)
    {
        AnimationManager.Current.SeekStoryboard(this, containingObject, offset);
    }

    public void SetSpeedRatio(FrameworkElement containingObject, float speedRatio)
    {
        AnimationManager.Current.SetStoryboardSpeedRatio(this, containingObject, speedRatio);
    }

    public static void SetTargetName(object timeline, string value)
    {
        if (timeline is AnimationTimeline animation)
        {
            animation.TargetName = value ?? string.Empty;
        }
    }

    public static string GetTargetName(object timeline)
    {
        return timeline is AnimationTimeline animation ? animation.TargetName : string.Empty;
    }

    public static void SetTargetProperty(object timeline, string value)
    {
        if (timeline is AnimationTimeline animation)
        {
            animation.TargetProperty = value ?? string.Empty;
        }
    }

    public static string GetTargetProperty(object timeline)
    {
        return timeline is AnimationTimeline animation ? animation.TargetProperty : string.Empty;
    }
}
