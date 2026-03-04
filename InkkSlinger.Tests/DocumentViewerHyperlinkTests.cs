using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentViewerHyperlinkTests
{
    [Fact]
    public void CtrlEnter_ActivatesHyperlink()
    {
        var (viewer, hyperlink) = CreateViewerWithHyperlink();
        _ = hyperlink;
        var hits = 0;
        viewer.HyperlinkNavigate += (_, args) =>
        {
            if (args.NavigateUri == "https://example.com")
            {
                hits++;
            }
        };

        viewer.SetFocusedFromInput(true);
        Assert.True(viewer.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.Control));
        Assert.Equal(1, hits);
    }

    [Fact]
    public void UiRootPointerMove_UpdatesHyperlinkHoverState()
    {
        var (viewer, hyperlink) = CreateViewerWithHyperlink();
        var root = new Grid();
        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 420, 160));
        uiRoot.RunInputDeltaForTests(CreatePointerMoveDelta(new Vector2(16f, 16f)));

        Assert.True(hyperlink.IsMouseOver);
    }

    private static (DocumentViewer Viewer, Hyperlink Hyperlink) CreateViewerWithHyperlink()
    {
        var viewer = new DocumentViewer();
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 420f, 160f));

        var doc = new FlowDocument();
        var p = new Paragraph();
        var hyperlink = new Hyperlink { NavigateUri = "https://example.com" };
        hyperlink.Inlines.Add(new Run("link"));
        p.Inlines.Add(hyperlink);
        doc.Blocks.Add(p);
        viewer.Document = doc;
        viewer.Measure(new Vector2(420f, 160f));

        return (viewer, hyperlink);
    }

    private static InputDelta CreatePointerMoveDelta(Vector2 pointer)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }
}
