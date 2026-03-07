using System;

namespace InkkSlinger;

public sealed class TriggerActionContext
{
    public TriggerActionContext(DependencyObject target, FrameworkElement? scope, Func<string, object?>? resolveByName = null)
    {
        Target = target;
        Scope = scope;
        ResolveByName = resolveByName;
    }

    public DependencyObject Target { get; }

    public FrameworkElement? Scope { get; }

    public Func<string, object?>? ResolveByName { get; }
}

public sealed class BeginStoryboard : TriggerAction
{
    public Storyboard? Storyboard { get; set; }
    internal object? StoryboardResourceReference { get; set; }

    public string Name { get; set; } = string.Empty;

    public HandoffBehavior HandoffBehavior { get; set; } = HandoffBehavior.SnapshotAndReplace;

    public override void Invoke(DependencyObject target)
    {
        if (target is not FrameworkElement scope)
        {
            return;
        }

        var storyboard = ResolveStoryboard(scope);
        if (storyboard == null)
        {
            return;
        }

        AnimationManager.Current.BeginStoryboard(
            storyboard,
            scope,
            string.IsNullOrWhiteSpace(Name) ? null : Name,
            null,
            isControllable: true,
            HandoffBehavior);
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        var storyboard = ResolveStoryboard(scope);
        if (storyboard == null)
        {
            return;
        }

        AnimationManager.Current.BeginStoryboard(
            storyboard,
            scope,
            string.IsNullOrWhiteSpace(Name) ? null : Name,
            context.ResolveByName,
            isControllable: true,
            HandoffBehavior);
    }

    internal void PrepareMetadata(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        var storyboard = ResolveStoryboard(scope);
        if (storyboard == null)
        {
            return;
        }

        AnimationManager.Current.PrepareStoryboardMetadata(storyboard);
    }

    internal void WarmResolutionPath(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        var storyboard = ResolveStoryboard(scope);
        if (storyboard == null)
        {
            return;
        }

        var resolvedTargetCache = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal);
        var preparedMetadata = AnimationManager.Current.GetOrCreatePreparedStoryboardMetadata(storyboard);
        foreach (var descriptor in preparedMetadata.Lanes)
        {
            object? target;
            if (string.IsNullOrWhiteSpace(descriptor.Animation.TargetName))
            {
                target = scope;
            }
            else if (resolvedTargetCache.TryGetValue(descriptor.Animation.TargetName, out var cached))
            {
                target = cached;
            }
            else
            {
                target = context.ResolveByName?.Invoke(descriptor.Animation.TargetName) ??
                    NameScopeService.FindName(scope, descriptor.Animation.TargetName) ??
                    scope.FindName(descriptor.Animation.TargetName);
                resolvedTargetCache[descriptor.Animation.TargetName] = target;
            }

            if (target == null)
            {
                continue;
            }

            var sink = AnimationPropertyPathResolver.Resolve(target, descriptor.Animation.TargetProperty);
            _ = sink?.GetValue();
        }
    }


    private Storyboard? ResolveStoryboard(FrameworkElement scope)
    {
        if (Storyboard != null)
        {
            return Storyboard;
        }

        if (StoryboardResourceReference is not DynamicResourceReferenceExpression dynamicReference)
        {
            return null;
        }

        if (!scope.TryFindResource(dynamicReference.Key, out var resource))
        {
            return null;
        }

        return resource as Storyboard;
    }
}

public sealed class PauseStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        var scope = StoryboardActionHelper.ResolveScope(target);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Pause(AnimationManager.Current.CurrentTime));
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = StoryboardActionHelper.ResolveScope(context);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Pause(AnimationManager.Current.CurrentTime));
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        StoryboardActionHelper.InvokeControllableByName(scope, BeginStoryboardName, apply);
    }
}

public sealed class ResumeStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        var scope = StoryboardActionHelper.ResolveScope(target);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Resume(AnimationManager.Current.CurrentTime));
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = StoryboardActionHelper.ResolveScope(context);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Resume(AnimationManager.Current.CurrentTime));
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        StoryboardActionHelper.InvokeControllableByName(scope, BeginStoryboardName, apply);
    }
}

public sealed class StopStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        var scope = StoryboardActionHelper.ResolveScope(target);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Stop());
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = StoryboardActionHelper.ResolveScope(context);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Stop());
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        StoryboardActionHelper.InvokeControllableByName(scope, BeginStoryboardName, apply);
    }
}

public sealed class RemoveStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        var scope = StoryboardActionHelper.ResolveScope(target);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Remove());
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = StoryboardActionHelper.ResolveScope(context);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Remove());
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        StoryboardActionHelper.InvokeControllableByName(scope, BeginStoryboardName, apply);
    }
}

public sealed class SeekStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public TimeSpan Offset { get; set; } = TimeSpan.Zero;

    public TimeSeekOrigin Origin { get; set; } = TimeSeekOrigin.BeginTime;

    public override void Invoke(DependencyObject target)
    {
        var scope = StoryboardActionHelper.ResolveScope(target);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Seek(Offset, Origin, AnimationManager.Current.CurrentTime));
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = StoryboardActionHelper.ResolveScope(context);
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Seek(Offset, Origin, AnimationManager.Current.CurrentTime));
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        StoryboardActionHelper.InvokeControllableByName(scope, BeginStoryboardName, apply);
    }
}

public sealed class SetStoryboardSpeedRatio : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public float SpeedRatio { get; set; } = 1f;

    public override void Invoke(DependencyObject target)
    {
        ValidateSpeedRatio();

        var scope = StoryboardActionHelper.ResolveScope(target);
        if (scope == null)
        {
            return;
        }

        StoryboardActionHelper.InvokeControllableByName(
            scope,
            BeginStoryboardName,
            s => AnimationManager.Current.SetControllableSpeedRatio(s, SpeedRatio));
    }

    internal override void Invoke(TriggerActionContext context)
    {
        ValidateSpeedRatio();

        var scope = StoryboardActionHelper.ResolveScope(context);
        if (scope == null)
        {
            return;
        }

        StoryboardActionHelper.InvokeControllableByName(
            scope,
            BeginStoryboardName,
            s => AnimationManager.Current.SetControllableSpeedRatio(s, SpeedRatio));
    }

    private void ValidateSpeedRatio()
    {
        if (!float.IsFinite(SpeedRatio) || SpeedRatio <= 0f)
        {
            throw new InvalidOperationException(
                $"SetStoryboardSpeedRatio requires a finite positive SpeedRatio. Actual value: {SpeedRatio}.");
        }
    }
}

public sealed class SkipStoryboardToFill : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        var scope = StoryboardActionHelper.ResolveScope(target);
        if (scope == null)
        {
            return;
        }

        StoryboardActionHelper.InvokeControllableByName(
            scope,
            BeginStoryboardName,
            s => s.SkipToFill(AnimationManager.Current.CurrentTime));
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = StoryboardActionHelper.ResolveScope(context);
        if (scope == null)
        {
            return;
        }

        StoryboardActionHelper.InvokeControllableByName(
            scope,
            BeginStoryboardName,
            s => s.SkipToFill(AnimationManager.Current.CurrentTime));
    }
}

internal static class StoryboardActionHelper
{
    public static FrameworkElement? ResolveScope(DependencyObject target)
    {
        return target as FrameworkElement;
    }

    public static FrameworkElement? ResolveScope(TriggerActionContext context)
    {
        return context.Scope ?? context.Target as FrameworkElement;
    }

    public static void InvokeControllableByName(
        FrameworkElement scope,
        string beginStoryboardName,
        Action<StoryboardInstance> apply)
    {
        if (string.IsNullOrWhiteSpace(beginStoryboardName))
        {
            return;
        }

        if (AnimationManager.Current.TryResolveControllable(scope, beginStoryboardName, out var instance) && instance != null)
        {
            apply(instance);
        }
    }
}
