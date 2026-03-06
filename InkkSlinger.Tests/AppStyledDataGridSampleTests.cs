using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
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
