using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class DispatcherPhaseOrderTests
{
    [Fact]
    public void Update_ExecutesDeterministicPhaseOrder()
    {
        Dispatcher.ResetForTests();
        AnimationManager.Current.ResetForTests();
        var root = new Panel();
        var uiRoot = new UiRoot(root);

        var viewport = new Viewport(0, 0, 1024, 768);
        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        AssertPhaseOrder(uiRoot.GetLastUpdatePhaseOrderForTests());

        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);
        AssertPhaseOrder(uiRoot.GetLastUpdatePhaseOrderForTests());
    }

    [Fact]
    public void DeferredBindingChange_IsAppliedBeforeLayoutPhase()
    {
        Dispatcher.ResetForTests();
        AnimationManager.Current.ResetForTests();

        var viewModel = new WidthViewModel { Width = 120f };
        var root = new Border
        {
            DataContext = viewModel
        };
        BindingOperations.SetBinding(
            root,
            FrameworkElement.WidthProperty,
            new Binding
            {
                Path = nameof(WidthViewModel.Width)
            });

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 1024, 768);

        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        uiRoot.EnqueueDeferredOperation(() => viewModel.Width = 260f);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.Equal(1, uiRoot.LastDeferredOperationCount);
        Assert.Equal(260f, root.Width);
        Assert.Equal(1, uiRoot.LayoutPasses);
        Assert.Equal(0, uiRoot.PendingDeferredOperationCount);
        AssertPhaseOrder(uiRoot.GetLastUpdatePhaseOrderForTests());
    }

    [Fact]
    public void InputPhaseInvalidation_CaretBlinkBecomesRenderReasonSameFrame()
    {
        Dispatcher.ResetForTests();
        AnimationManager.Current.ResetForTests();

        var root = new Panel();
        var textBox = new TextBox();
        root.AddChild(textBox);
        textBox.SetValue(TextBox.IsFocusedProperty, true);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 1280, 720);

        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        var tick = new GameTime(TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(600));
        uiRoot.Update(tick, viewport);

        var shouldDraw = uiRoot.ShouldDrawThisFrame(tick, viewport);

        Assert.True(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.CaretBlinkActive) != 0);
        AssertPhaseOrder(uiRoot.GetLastUpdatePhaseOrderForTests());
    }

    private static void AssertPhaseOrder(IReadOnlyList<UiUpdatePhase> phases)
    {
        Assert.Collection(
            phases,
            phase => Assert.Equal(UiUpdatePhase.InputAndEvents, phase),
            phase => Assert.Equal(UiUpdatePhase.BindingAndDeferred, phase),
            phase => Assert.Equal(UiUpdatePhase.Layout, phase),
            phase => Assert.Equal(UiUpdatePhase.Animation, phase),
            phase => Assert.Equal(UiUpdatePhase.RenderScheduling, phase));
    }

    private sealed class WidthViewModel : INotifyPropertyChanged
    {
        private float _width;

        public event PropertyChangedEventHandler? PropertyChanged;

        public float Width
        {
            get => _width;
            set
            {
                if (MathF.Abs(_width - value) <= 0.0001f)
                {
                    return;
                }

                _width = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
            }
        }
    }
}
