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
            Assert.Equal("1", firstCell.Value?.ToString());
            Assert.Equal("1", firstCell.Content?.ToString());

            var border = Assert.IsType<Border>(Assert.Single(firstCell.GetVisualChildren()));
            var presenter = Assert.IsType<ContentPresenter>(Assert.Single(border.GetVisualChildren()));
            var label = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
            Assert.Equal("1", label.Text);
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
            var expectedFont = label.Font;
            var expectedFontFamily = label.FontFamily;
            var expectedFontSize = label.FontSize;
            var expectedForeground = label.Foreground;

            PressKey(uiRoot, Keys.F2);

            var editor = Assert.IsType<TextBox>(firstCell.EditingElement);
            Assert.Same(expectedFont, editor.Font);
            Assert.Equal(expectedFontFamily, editor.FontFamily);
            Assert.Equal(expectedFontSize, editor.FontSize);
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
        if (!IsScalableTextRendererEnabled())
        {
            return;
        }

        var smaller = new DataGridColumnHeader
        {
            Text = "Name",
            FontSize = 12f,
            Padding = new Thickness(12f, 8f, 12f, 8f)
        };
        var larger = new DataGridColumnHeader
        {
            Text = "Name",
            FontSize = 20f,
            Padding = new Thickness(12f, 8f, 12f, 8f)
        };

        smaller.Measure(new Vector2(300f, 60f));
        larger.Measure(new Vector2(300f, 60f));

        Assert.True(larger.DesiredSize.Y > smaller.DesiredSize.Y);
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

    private static bool IsScalableTextRendererEnabled()
    {
        var rendererType = typeof(DataGridView).Assembly.GetType("InkkSlinger.FontStashTextRenderer");
        var property = rendererType?.GetProperty("IsEnabled", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        return property?.GetValue(null) as bool? == true;
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        UiApplication.Current.Resources.Clear();
        foreach (var entry in snapshot)
        {
            UiApplication.Current.Resources[entry.Key] = entry.Value;
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
}
