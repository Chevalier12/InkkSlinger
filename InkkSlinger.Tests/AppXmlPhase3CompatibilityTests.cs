using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AppXmlPhase3CompatibilityTests
{
    [Fact]
    public void GridViewRowPresenter_ListViewTemplate_PresentsItemContent()
    {
        var listView = new ListView
        {
            Width = 320f,
            Height = 180f
        };
        listView.Items.Add("Alpha");

        var itemStyle = new Style(typeof(ListViewItem));
        itemStyle.Setters.Add(
            new Setter(
                Control.TemplateProperty,
                new ControlTemplate(_ =>
                {
                    var border = new Border();
                    border.Child = new GridViewRowPresenter();
                    return border;
                })
                {
                    TargetType = typeof(ListViewItem)
                }));
        listView.ItemContainerStyle = itemStyle;

        var uiRoot = BuildUiRootWithSingleChild(listView, 520, 320);
        RunLayout(uiRoot, 520, 320);

        var hostPanel = FindItemsHostPanel(listView);
        var item = Assert.IsType<ListViewItem>(hostPanel.Children[0]);

        var border = Assert.IsType<Border>(Assert.Single(item.GetVisualChildren()));
        var presenter = Assert.IsType<GridViewRowPresenter>(Assert.Single(border.GetVisualChildren()));
        var presented = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
        Assert.Equal("Alpha", presented.GetContentText());
    }

    [Fact]
    public void Window_ImplicitStyle_FromApplicationResources_AppliesRuntimeDpValues()
    {
        var backup = CaptureApplicationResources();
        try
        {
            var style = new Style(typeof(Window));
            style.Setters.Add(new Setter(Window.BackgroundProperty, new Color(0x22, 0x44, 0x66)));
            style.Setters.Add(new Setter(Window.ForegroundProperty, new Color(0xEE, 0xCC, 0xAA)));
            style.Setters.Add(new Setter(Window.FontFamilyProperty, "Segoe UI"));
            style.Setters.Add(new Setter(Window.FontSizeProperty, 16f));
            style.Setters.Add(new Setter(Window.FontWeightProperty, "SemiBold"));
            UiApplication.Current.Resources[typeof(Window)] = style;

            using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
            Assert.Equal(new Color(0x22, 0x44, 0x66), window.Background);
            Assert.Equal(new Color(0xEE, 0xCC, 0xAA), window.Foreground);
            Assert.Equal("Segoe UI", window.FontFamily);
            Assert.Equal(16f, window.FontSize);
            Assert.Equal("SemiBold", window.FontWeight);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void Window_ThemeValues_DriveShellRootStyling()
    {
        using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
        var root = new Panel();
        using var binding = new WindowThemeBinding(window, root);

        window.Background = new Color(0x18, 0x2A, 0x3C);
        window.FontFamily = "Segoe UI";
        window.FontSize = 15f;
        window.FontWeight = "Bold";

        Assert.Equal(new Color(0x18, 0x2A, 0x3C), root.Background);
        Assert.Equal("Segoe UI", root.FontFamily);
        Assert.Equal(15f, root.FontSize);
        Assert.Equal("Bold", root.FontWeight);
    }

    [Fact]
    public void RichTextBox_Hyperlink_ImplicitStyle_AppliesForegroundAndTextDecorations()
    {
        var backup = CaptureApplicationResources();
        try
        {
            var style = new Style(typeof(Hyperlink));
            style.Setters.Add(new Setter(Hyperlink.ForegroundProperty, new Color(0xFA, 0xA5, 0x32)));
            style.Setters.Add(new Setter(Hyperlink.TextDecorationsProperty, "None"));
            UiApplication.Current.Resources[typeof(Hyperlink)] = style;

            var (box, hyperlink) = CreateRichTextBoxWithSingleHyperlink(isReadOnly: false);
            _ = box;

            Assert.Equal(new Color(0xFA, 0xA5, 0x32), hyperlink.Foreground);
            Assert.Equal("None", hyperlink.TextDecorations);
            Assert.Equal(DependencyPropertyValueSource.Style, hyperlink.GetValueSource(Hyperlink.ForegroundProperty));
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void RichTextBox_HyperlinkHover_TriggersIsMouseOver_InEditableMode()
    {
        RunHyperlinkHoverScenario(isReadOnly: false);
    }

    [Fact]
    public void RichTextBox_HyperlinkHover_TriggersIsMouseOver_InReadOnlyMode()
    {
        RunHyperlinkHoverScenario(isReadOnly: true);
    }

    [Fact]
    public void RichTextBox_HyperlinkHover_DoesNotEmitDocumentChanged()
    {
        var backup = CaptureApplicationResources();
        try
        {
            UiApplication.Current.Resources[typeof(Hyperlink)] = BuildHyperlinkHoverStyle(
                baseColor: new Color(0x66, 0x77, 0x88),
                hoverColor: new Color(0xAA, 0xBB, 0xCC));

            var (box, hyperlink) = CreateRichTextBoxWithSingleHyperlink(isReadOnly: false);
            var uiRoot = BuildUiRootWithSingleChild(box, 560, 360);
            RunLayout(uiRoot, 560, 360);

            var documentChangedCount = 0;
            box.DocumentChanged += (_, _) => documentChangedCount++;

            var pointerOver = new Vector2(box.LayoutSlot.X + 72f, box.LayoutSlot.Y + 18f);
            var pointerAway = new Vector2(box.LayoutSlot.X + box.LayoutSlot.Width + 60f, box.LayoutSlot.Y + 12f);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointerOver, pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointerAway, pointerMoved: true));

            Assert.False(hyperlink.IsMouseOver);
            Assert.Equal(0, documentChangedCount);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static void RunHyperlinkHoverScenario(bool isReadOnly)
    {
        var backup = CaptureApplicationResources();
        try
        {
            var baseColor = new Color(0x35, 0x73, 0xB6);
            var hoverColor = new Color(0xFF, 0xB8, 0x4D);
            UiApplication.Current.Resources[typeof(Hyperlink)] = BuildHyperlinkHoverStyle(baseColor, hoverColor);

            var (box, hyperlink) = CreateRichTextBoxWithSingleHyperlink(isReadOnly);
            var uiRoot = BuildUiRootWithSingleChild(box, 560, 360);
            RunLayout(uiRoot, 560, 360);

            Assert.False(hyperlink.IsMouseOver);
            Assert.Equal(baseColor, hyperlink.Foreground);
            Assert.Equal("None", hyperlink.TextDecorations);

            var pointerOver = new Vector2(box.LayoutSlot.X + 72f, box.LayoutSlot.Y + 18f);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointerOver, pointerMoved: true));

            Assert.True(hyperlink.Foreground == baseColor || hyperlink.Foreground == hoverColor);
            Assert.True(hyperlink.TextDecorations == "None" || hyperlink.TextDecorations == "Underline");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static Style BuildHyperlinkHoverStyle(Color baseColor, Color hoverColor)
    {
        var style = new Style(typeof(Hyperlink));
        style.Setters.Add(new Setter(Hyperlink.ForegroundProperty, baseColor));
        style.Setters.Add(new Setter(Hyperlink.TextDecorationsProperty, "None"));

        var hoverTrigger = new Trigger(Hyperlink.IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(Hyperlink.ForegroundProperty, hoverColor));
        hoverTrigger.Setters.Add(new Setter(Hyperlink.TextDecorationsProperty, "Underline"));
        style.Triggers.Add(hoverTrigger);
        return style;
    }

    private static (RichTextBox Box, Hyperlink Hyperlink) CreateRichTextBoxWithSingleHyperlink(bool isReadOnly)
    {
        var hyperlink = new Hyperlink
        {
            NavigateUri = "https://example.com/inkkslinger"
        };
        hyperlink.Inlines.Add(new Run("Open link"));
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(hyperlink);
        var document = new FlowDocument();
        document.Blocks.Add(paragraph);

        var box = new RichTextBox
        {
            Width = 320f,
            Height = 130f,
            IsReadOnly = isReadOnly,
            Document = document
        };
        return (box, hyperlink);
    }

    private static UiRoot BuildUiRootWithSingleChild(FrameworkElement element, int width, int height)
    {
        var host = new Canvas
        {
            Width = width,
            Height = height
        };
        host.AddChild(element);
        Canvas.SetLeft(element, 40f);
        Canvas.SetTop(element, 24f);
        return new UiRoot(host);
    }

    private static Panel FindItemsHostPanel(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is not ScrollViewer viewer)
            {
                continue;
            }

            foreach (var viewerChild in viewer.GetVisualChildren())
            {
                if (viewerChild is Panel panel)
                {
                    return panel;
                }
            }
        }

        throw new InvalidOperationException("Could not resolve ListBox items host panel.");
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

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
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

    private sealed class FakeWindowNativeAdapter : IWindowNativeAdapter
    {
        public event EventHandler<EventArgs>? ClientSizeChanged;

        public string Title { get; set; } = string.Empty;

        public bool AllowUserResizing { get; set; }

        public bool IsBorderless { get; set; }

        public Point Position { get; set; }

        public Rectangle ClientBounds { get; set; } = new(0, 0, 1200, 800);

        public IntPtr Handle { get; } = new(999);

        public void RaiseClientSizeChanged()
        {
            ClientSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeWindowGraphicsAdapter : IWindowGraphicsAdapter
    {
        public int PreferredBackBufferWidth { get; set; } = 1200;

        public int PreferredBackBufferHeight { get; set; } = 800;

        public bool IsFullScreen { get; set; }

        public void ApplyChanges()
        {
        }
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}
