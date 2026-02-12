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

    public string Name { get; set; } = string.Empty;

    public HandoffBehavior HandoffBehavior { get; set; } = HandoffBehavior.SnapshotAndReplace;

    public override void Invoke(DependencyObject target)
    {
        if (target is not FrameworkElement scope)
        {
            return;
        }

        if (Storyboard == null)
        {
            return;
        }

        AnimationManager.Current.BeginStoryboard(
            Storyboard,
            scope,
            string.IsNullOrWhiteSpace(Name) ? null : Name,
            null,
            isControllable: true,
            HandoffBehavior);
    }

    internal override void Invoke(TriggerActionContext context)
    {
        if (Storyboard == null)
        {
            return;
        }

        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        AnimationManager.Current.BeginStoryboard(
            Storyboard,
            scope,
            string.IsNullOrWhiteSpace(Name) ? null : Name,
            context.ResolveByName,
            isControllable: true,
            HandoffBehavior);
    }
}

public sealed class PauseStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        if (target is FrameworkElement scope)
        {
            InvokeByName(scope, s => s.Pause(AnimationManager.Current.CurrentTime));
        }
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Pause(AnimationManager.Current.CurrentTime));
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        if (string.IsNullOrWhiteSpace(BeginStoryboardName))
        {
            return;
        }

        if (AnimationManager.Current.TryResolveControllable(scope, BeginStoryboardName, out var instance) && instance != null)
        {
            apply(instance);
        }
    }
}

public sealed class ResumeStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        if (target is FrameworkElement scope)
        {
            InvokeByName(scope, s => s.Resume(AnimationManager.Current.CurrentTime));
        }
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Resume(AnimationManager.Current.CurrentTime));
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        if (string.IsNullOrWhiteSpace(BeginStoryboardName))
        {
            return;
        }

        if (AnimationManager.Current.TryResolveControllable(scope, BeginStoryboardName, out var instance) && instance != null)
        {
            apply(instance);
        }
    }
}

public sealed class StopStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        if (target is FrameworkElement scope)
        {
            InvokeByName(scope, s => s.Stop());
        }
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Stop());
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        if (string.IsNullOrWhiteSpace(BeginStoryboardName))
        {
            return;
        }

        if (AnimationManager.Current.TryResolveControllable(scope, BeginStoryboardName, out var instance) && instance != null)
        {
            apply(instance);
        }
    }
}

public sealed class RemoveStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public override void Invoke(DependencyObject target)
    {
        if (target is FrameworkElement scope)
        {
            InvokeByName(scope, s => s.Remove());
        }
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Remove());
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        if (string.IsNullOrWhiteSpace(BeginStoryboardName))
        {
            return;
        }

        if (AnimationManager.Current.TryResolveControllable(scope, BeginStoryboardName, out var instance) && instance != null)
        {
            apply(instance);
        }
    }
}

public sealed class SeekStoryboard : TriggerAction
{
    public string BeginStoryboardName { get; set; } = string.Empty;

    public TimeSpan Offset { get; set; } = TimeSpan.Zero;

    public TimeSeekOrigin Origin { get; set; } = TimeSeekOrigin.BeginTime;

    public override void Invoke(DependencyObject target)
    {
        if (target is FrameworkElement scope)
        {
            InvokeByName(scope, s => s.Seek(Offset, Origin, AnimationManager.Current.CurrentTime));
        }
    }

    internal override void Invoke(TriggerActionContext context)
    {
        var scope = context.Scope ?? context.Target as FrameworkElement;
        if (scope == null)
        {
            return;
        }

        InvokeByName(scope, s => s.Seek(Offset, Origin, AnimationManager.Current.CurrentTime));
    }

    private void InvokeByName(FrameworkElement scope, Action<StoryboardInstance> apply)
    {
        if (string.IsNullOrWhiteSpace(BeginStoryboardName))
        {
            return;
        }

        if (AnimationManager.Current.TryResolveControllable(scope, BeginStoryboardName, out var instance) && instance != null)
        {
            apply(instance);
        }
    }
}
