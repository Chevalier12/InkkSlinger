using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TreeViewItemRenderingRegressionTests
{
    [Fact]
    public void HeaderText_ShouldContributeToDesiredWidth_WhenFontIsNotExplicitlySet()
    {
        var item = new TreeViewItem
        {
            Header = "Root Node"
        };

        item.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(
            item.DesiredSize.X > 20f,
            $"Expected header text width to contribute to desired width when Font is null. DesiredWidth={item.DesiredSize.X:0.###}");
    }

    [Fact]
    public void VirtualizedDisplaySnapshot_UsesSnapshotSelectionForRendering()
    {
        var item = new TreeViewItem
        {
            Header = "Selected container",
            IsSelected = true
        };

        item.ApplyVirtualizedDisplaySnapshot(
            "Different row",
            hasChildren: false,
            isExpanded: false,
            isSelected: false,
            depth: 0,
            rowIndex: 12);

        Assert.False(item.IsSelectedForRenderDiagnostics);

        item.ApplyVirtualizedDisplaySnapshot(
            "Selected row",
            hasChildren: false,
            isExpanded: false,
            isSelected: true,
            depth: 0,
            rowIndex: 13);

        Assert.True(item.IsSelectedForRenderDiagnostics);

        item.ClearVirtualizedDisplaySnapshot();

        Assert.True(item.IsSelectedForRenderDiagnostics);
    }

    [Fact]
    public void VirtualizedDisplaySnapshot_WithTemplatedExpander_ReservesAndRendersSnapshotGlyph()
    {
        var item = new TreeViewItem
        {
            Header = "Old leaf",
            ShowsBuiltInExpander = false,
            Template = CreateTemplatedExpanderTemplate(),
            CollapsedExpanderGlyph = ">",
            ExpandedExpanderGlyph = "v"
        };

        item.ApplyTemplate();
        item.Measure(new Vector2(240f, 24f));
        item.Arrange(new LayoutRect(0f, 0f, 240f, item.DesiredSize.Y));

        item.ApplyVirtualizedDisplaySnapshot(
            "Runtime",
            hasChildren: true,
            isExpanded: true,
            isSelected: false,
            depth: 1,
            rowIndex: 42);

        Assert.True(item.HasVirtualizedDisplaySnapshotForDiagnostics);
        Assert.True(item.RendersTemplateExpanderSnapshotForDiagnostics);
        Assert.True(
            item.HeaderTextOffsetForDiagnostics >= item.Indent + 14f,
            $"Snapshot header text should reserve the templated expander slot. offset={item.HeaderTextOffsetForDiagnostics:0.###}");
    }

    [Fact]
    public void VirtualizedDisplaySnapshot_WithNonTextTemplatedExpander_ReservesSlotAndUsesGraphicalSnapshotGlyph()
    {
        var item = new TreeViewItem
        {
            Header = "Old leaf",
            ShowsBuiltInExpander = false,
            Template = CreateNonTextTemplatedExpanderTemplate(),
            CollapsedExpanderGlyph = "^",
            ExpandedExpanderGlyph = "v"
        };

        item.ApplyTemplate();
        item.Measure(new Vector2(240f, 24f));
        item.Arrange(new LayoutRect(0f, 0f, 240f, item.DesiredSize.Y));

        item.ApplyVirtualizedDisplaySnapshot(
            "Runtime",
            hasChildren: true,
            isExpanded: true,
            isSelected: false,
            depth: 1,
            rowIndex: 42);

        Assert.True(item.HasVirtualizedDisplaySnapshotForDiagnostics);
        Assert.True(item.RendersTemplateExpanderSnapshotForDiagnostics);
        Assert.False(item.RendersTemplateExpanderTextSnapshotForDiagnostics);
        Assert.True(
            item.HeaderTextOffsetForDiagnostics >= item.Indent + 14f,
            $"Snapshot header text should still reserve the templated expander slot. offset={item.HeaderTextOffsetForDiagnostics:0.###}");
    }

    [Fact]
    public void VirtualizedDisplaySnapshot_WithTemplatedHeader_UsesSettledTemplateRowHeightAndHeaderTextTypography()
    {
        var item = new TreeViewItem
        {
            Header = "Old leaf",
            ShowsBuiltInExpander = false,
            Template = CreateTemplatedExpanderTemplate(headerFontSize: 9f),
            FontSize = 22f
        };

        item.ApplyTemplate();
        item.Measure(new Vector2(240f, 40f));
        item.Arrange(new LayoutRect(0f, 0f, 240f, 30f));

        item.ApplyVirtualizedDisplaySnapshot(
            "Runtime",
            hasChildren: true,
            isExpanded: true,
            isSelected: false,
            depth: 1,
            rowIndex: 42);
        item.Arrange(new LayoutRect(0f, 0f, 240f, 30f));

        Assert.Equal(33f, item.RenderRowHeightForDiagnostics);
        Assert.Equal(9f, item.VirtualizedHeaderRenderFontSizeForDiagnostics);
        Assert.Equal(10.5f, item.SnapshotHeaderTextRelativeYForDiagnostics, precision: 3);
        Assert.Equal(item.LayoutSlot.Y + item.SnapshotHeaderTextRelativeYForDiagnostics, item.VirtualizedHeaderRenderYForDiagnostics, precision: 3);
    }

    [Fact]
    public void VirtualizedDisplaySnapshot_WithTemplatedExpander_UsesTemplateExpanderTypography()
    {
        var item = new TreeViewItem
        {
            Header = "Old leaf",
            ShowsBuiltInExpander = false,
            Template = CreateTemplatedExpanderTemplate(expanderFontSize: 9f),
            FontSize = 22f
        };

        item.ApplyTemplate();
        item.Measure(new Vector2(240f, 40f));
        item.Arrange(new LayoutRect(0f, 0f, 240f, 30f));

        item.ApplyVirtualizedDisplaySnapshot(
            "Runtime",
            hasChildren: true,
            isExpanded: true,
            isSelected: false,
            depth: 1,
            rowIndex: 42);

        Assert.Equal(9f, item.VirtualizedExpanderRenderFontSizeForDiagnostics);
        Assert.Equal(0f, item.SnapshotExpanderTextRelativeXForDiagnostics);
    }

    [Fact]
    public void RecycledVirtualizedFolderContainer_ReusedForLeaf_ClearsExpanderPresentation()
    {
        var item = new TreeViewItem
        {
            ShowsBuiltInExpander = false,
            Template = CreateTemplatedExpanderTemplate(),
            CollapsedExpanderGlyph = ">",
            ExpandedExpanderGlyph = "v"
        };

        item.ApplyVirtualizedBranchState(hasChildren: true, isExpanded: true);

        Assert.Equal(Visibility.Visible, item.ExpanderGlyphVisibility);

        item.ClearVirtualizedBranchStateForRecycle();
        item.ApplyVirtualizedBranchState(hasChildren: false, isExpanded: false);

        Assert.False(item.HasItems);
        Assert.Equal(Visibility.Collapsed, item.ExpanderGlyphVisibility);
    }

    private static ControlTemplate CreateTemplatedExpanderTemplate(float headerFontSize = 12f, float expanderFontSize = 12f)
    {
        var template = new ControlTemplate(_ =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14f, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });

            var expander = new TextBlock
            {
                Name = "PART_Expander",
                Width = 14f,
                FontSize = expanderFontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var headerText = new TextBlock
            {
                Name = "HeaderText",
                FontSize = headerFontSize,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(expander, 0);
            Grid.SetColumn(headerText, 1);
            grid.AddChild(expander);
            grid.AddChild(headerText);
            return grid;
        })
        {
            TargetType = typeof(TreeViewItem)
        };

        template.BindTemplate("PART_Expander", TextBlock.TextProperty, TreeViewItem.CurrentExpanderGlyphProperty);
        template.BindTemplate("PART_Expander", TextBlock.VisibilityProperty, TreeViewItem.ExpanderGlyphVisibilityProperty);
        template.BindTemplate("HeaderText", TextBlock.TextProperty, TreeViewItem.HeaderProperty);
        return template;
    }

    private static ControlTemplate CreateNonTextTemplatedExpanderTemplate(float headerFontSize = 12f)
    {
        var template = new ControlTemplate(_ =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14f, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });

            var expander = new Grid
            {
                Name = "PART_Expander",
                Width = 14f,
                Height = 14f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var headerText = new TextBlock
            {
                Name = "HeaderText",
                FontSize = headerFontSize,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(expander, 0);
            Grid.SetColumn(headerText, 1);
            grid.AddChild(expander);
            grid.AddChild(headerText);
            return grid;
        })
        {
            TargetType = typeof(TreeViewItem)
        };

        template.BindTemplate("PART_Expander", Grid.VisibilityProperty, TreeViewItem.ExpanderGlyphVisibilityProperty);
        template.BindTemplate("HeaderText", TextBlock.TextProperty, TreeViewItem.HeaderProperty);
        return template;
    }
}
