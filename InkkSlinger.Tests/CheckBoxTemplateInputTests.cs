using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CheckBoxTemplateInputTests
{
    [Fact]
    public void AppXmlCheckBox_ClickingContentArea_TogglesAndTemplateChildrenDoNotStealHitTest()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var host = new Canvas
            {
                Width = 640,
                Height = 360
            };

            var checkBox = new CheckBox
            {
                Width = 260,
                Height = 44,
                Content = "Receive updates"
            };
            host.AddChild(checkBox);
            Canvas.SetLeft(checkBox, 48f);
            Canvas.SetTop(checkBox, 56f);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 640, 360);

            var contentPresenter = FindDescendant<ContentPresenter>(checkBox);
            Assert.False(contentPresenter.IsHitTestVisible);

            var checkGlyph = FindDescendant<Border>(
                checkBox,
                border => border.Width == 20f && border.Height == 20f);
            Assert.False(checkGlyph.IsHitTestVisible);
            Assert.Equal(VerticalAlignment.Center, checkGlyph.VerticalAlignment);

            var checkMark = FindDescendant<PathShape>(checkBox);
            Assert.Equal(Visibility.Collapsed, checkMark.Visibility);

            var uncheckedBackground = checkGlyph.Background;
            var checkedBackground = ResolveThemeColor("OrangePrimaryBrush");

            var point = Center(contentPresenter.LayoutSlot);
            ClickAt(uiRoot, point);

            Assert.True(checkBox.IsChecked);
            Assert.Equal(checkedBackground, checkGlyph.Background);
            Assert.Equal(Visibility.Visible, checkMark.Visibility);

            ClickAt(uiRoot, point);
            Assert.False(checkBox.IsChecked);
            Assert.Equal(uncheckedBackground, checkGlyph.Background);
            Assert.Equal(Visibility.Collapsed, checkMark.Visibility);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void AppXmlCheckBox_StyleEnablesLayoutRounding_ForCaptionPresenterAndAccessText()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var host = new Canvas
            {
                Width = 640,
                Height = 360
            };

            var checkBox = new CheckBox
            {
                Width = 260,
                Height = 44,
                Content = "Receive updates"
            };
            host.AddChild(checkBox);
            Canvas.SetLeft(checkBox, 48.3f);
            Canvas.SetTop(checkBox, 56.4f);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 640, 360);

            var contentPresenter = FindDescendant<ContentPresenter>(checkBox);
            var accessText = FindDescendant<AccessText>(checkBox);

            Assert.True(checkBox.UseLayoutRounding);
            Assert.Equal(DependencyPropertyValueSource.Style, checkBox.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));

            Assert.True(contentPresenter.UseLayoutRounding);
            Assert.Equal(DependencyPropertyValueSource.Inherited, contentPresenter.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));
            Assert.True(IsRounded(contentPresenter.LayoutSlot.X));
            Assert.True(IsRounded(contentPresenter.LayoutSlot.Y));
            Assert.True(IsRounded(contentPresenter.LayoutSlot.Width));
            Assert.True(IsRounded(contentPresenter.LayoutSlot.Height));

            Assert.True(accessText.UseLayoutRounding);
            Assert.Equal(DependencyPropertyValueSource.Inherited, accessText.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));
            Assert.True(IsRounded(accessText.LayoutSlot.X));
            Assert.True(IsRounded(accessText.LayoutSlot.Y));
            Assert.True(IsRounded(accessText.LayoutSlot.Width));
            Assert.True(IsRounded(accessText.LayoutSlot.Height));
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
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

    private static Color ResolveThemeColor(string key)
    {
        if (!UiApplication.Current.Resources.TryGetValue(key, out var resource))
        {
            throw new InvalidOperationException($"Expected theme resource '{key}' to exist.");
        }

        return resource switch
        {
            Color color => color,
            Brush brush => brush.ToColor(),
            _ => throw new InvalidOperationException($"Resource '{key}' is not a Color or Brush.")
        };
    }

    private static void ClickAt(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftReleased: true));
    }

    private static Vector2 Center(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static T FindDescendant<T>(UIElement root, Func<T, bool>? predicate = null)
        where T : UIElement
    {
        var pending = new Stack<UIElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is T typed && (predicate == null || predicate(typed)))
            {
                return typed;
            }

            foreach (var child in current.GetVisualChildren())
            {
                pending.Push(child);
            }
        }

        throw new InvalidOperationException($"Could not find descendant of type '{typeof(T).Name}'.");
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
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static bool IsRounded(float value)
    {
        return MathF.Abs(value - MathF.Round(value)) < 0.001f;
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

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}
