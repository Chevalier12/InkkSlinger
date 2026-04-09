using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AppStyledDataGridSampleTests
{
    [Fact]
    public void AppStyledDataGridSample_CellsUseTemplatePresenterBackedByCellContentAlias()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new DataGridView();
            var uiRoot = new UiRoot(view);
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 1280, 820));

            var dataGrid = FindDescendant<DataGrid>(view);
            var rows = dataGrid.RowsForTesting;
            Assert.NotEmpty(rows);
            var firstCell = Assert.IsType<DataGridCell>(rows[0].Cells[0]);
            Assert.Equal(new Color(0, 0, 0, 0), firstCell.Background);
            Assert.Equal("101", firstCell.Value?.ToString());
            Assert.Equal("101", firstCell.Content?.ToString());

            var border = Assert.IsType<Border>(Assert.Single(firstCell.GetVisualChildren()));
            var presenter = Assert.IsType<ContentPresenter>(Assert.Single(border.GetVisualChildren()));
            var label = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
            Assert.Equal("101", label.GetContentText());
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void AppStyledDataGridSample_InlineEditorMatchesDisplayedCellTypography()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new DataGridView();
            var uiRoot = new UiRoot(view);
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 1280, 820));

            var dataGrid = FindDescendant<DataGrid>(view);
            dataGrid.SelectionUnit = DataGridSelectionUnit.Cell;
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 1280, 820));

            var firstCell = Assert.IsType<DataGridCell>(dataGrid.RowsForTesting[0].Cells[1]);
            Click(uiRoot, Center(firstCell.LayoutSlot));
            var border = Assert.IsType<Border>(Assert.Single(firstCell.GetVisualChildren()));
            var presenter = Assert.IsType<ContentPresenter>(Assert.Single(border.GetVisualChildren()));
            var label = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
            var expectedFontFamily = label.FontFamily;
            var expectedFontSize = label.FontSize;
            var expectedFontWeight = label.FontWeight;
            var expectedFontStyle = label.FontStyle;
            var expectedForeground = label.Foreground;

            PressKey(uiRoot, Keys.F2);

            var editor = Assert.IsType<TextBox>(firstCell.EditingElement);
            Assert.Equal(expectedFontFamily, editor.FontFamily);
            Assert.Equal(expectedFontSize, editor.FontSize);
            Assert.Equal(expectedFontWeight, editor.FontWeight);
            Assert.Equal(expectedFontStyle, editor.FontStyle);
            Assert.Equal(expectedForeground, editor.Foreground);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void AppStyledDataGridSample_ColumnHeaderFontSize_AffectsHeaderHeight()
    {
        var smaller = new DataGridColumnHeader
        {
            Content = "Name",
            FontSize = 12f,
            Padding = new Thickness(12f, 8f, 12f, 8f)
        };
        var larger = new DataGridColumnHeader
        {
            Content = "Name",
            FontSize = 20f,
            Padding = new Thickness(12f, 8f, 12f, 8f)
        };

        smaller.Measure(new Vector2(300f, 60f));
        larger.Measure(new Vector2(300f, 60f));

        Assert.True(larger.DesiredSize.Y > smaller.DesiredSize.Y);
    }

    [Fact]
    public void AppStyledDataGridSample_HorizontalThumbDrag_KeepsTemplateLabelAlignedWithNameCell()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new DataGridView
            {
                Width = 780f,
                Height = 520f
            };
            var host = new Canvas
            {
                Width = 780f,
                Height = 520f
            };
            host.AddChild(view);

            var uiRoot = new UiRoot(host);
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 780, 520));

            var dataGrid = FindDescendant<DataGrid>(view);
            var row = dataGrid.RowsForTesting[0];
            var ticketCell = Assert.IsType<DataGridCell>(row.Cells[0]);
            var nameCell = Assert.IsType<DataGridCell>(dataGrid.RowsForTesting[0].Cells[1]);
            var label = FindDescendant<Label>(nameCell);
            Assert.True(nameCell.TryGetRenderBoundsInRootSpace(out var cellBoundsBefore));
            Assert.True(label.TryGetRenderBoundsInRootSpace(out var labelBoundsBefore));
            var labelOffsetBefore = labelBoundsBefore.X - cellBoundsBefore.X;
            var horizontalBar = GetPrivateScrollBar(dataGrid.ScrollViewerForTesting, "_horizontalBar");
            var thumb = FindDescendant<Thumb>(horizontalBar);
            var thumbCenter = Center(thumb.LayoutSlot);
            var rightPointer = new Vector2(horizontalBar.LayoutSlot.X + horizontalBar.LayoutSlot.Width - 2f, thumbCenter.Y);

            Assert.True(thumb.HandlePointerDownFromInput(thumbCenter));
            Assert.True(thumb.HandlePointerMoveFromInput(rightPointer));
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 780, 520));

            Assert.True(dataGrid.ScrollViewerForTesting.HorizontalOffset > 0f);
            Assert.True(nameCell.TryGetRenderBoundsInRootSpace(out var cellBoundsAfter));
            Assert.True(label.TryGetRenderBoundsInRootSpace(out var labelBoundsAfter));
            Assert.Equal(cellBoundsAfter.X + labelOffsetBefore, labelBoundsAfter.X);

            var overlapPoint = new Vector2(
                ticketCell.LayoutSlot.X + ticketCell.LayoutSlot.Width - 6f,
                nameCell.LayoutSlot.Y + (nameCell.LayoutSlot.Height * 0.5f));
            Assert.Same(ticketCell, FindAncestor<DataGridCell>(VisualTreeHelper.HitTest(host, overlapPoint)));

            Assert.True(thumb.HandlePointerUpFromInput());
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static void PressKey(UiRoot uiRoot, Keys key)
    {
        uiRoot.RunInputDeltaForTests(new InputDelta
        {
            Previous = new InputSnapshot(default, default, Vector2.Zero),
            Current = new InputSnapshot(default, default, Vector2.Zero),
            PressedKeys = [key],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        });
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved = false, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
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

    private static Vector2 Center(LayoutRect rect) => new(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static void LoadRootAppResources()
    {
        TestApplicationResources.LoadDemoAppResources();
    }

    private static TElement FindDescendant<TElement>(UIElement root)
        where TElement : UIElement
    {
        var pending = new Stack<UIElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is TElement typed)
            {
                return typed;
            }

            foreach (var child in current.GetVisualChildren())
            {
                pending.Push(child);
            }
        }

        throw new InvalidOperationException($"Could not find descendant of type '{typeof(TElement).Name}'.");
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }
}
