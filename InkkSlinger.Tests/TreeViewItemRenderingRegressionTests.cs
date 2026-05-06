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
