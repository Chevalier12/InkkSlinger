using System;
using System.Collections.Generic;
using System.Linq;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ComboBoxPopupEdgeParityTests
{
    [Fact]
    public void OpenDropDown_ShouldCreatePopup_WithDismissOnOutsideClick()
    {
        var (uiRoot, comboBox) = CreateFixture();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        Assert.True(comboBox.IsDropDownOpen);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void CollapsedSelectionText_ShouldCenterVisibleInkVertically()
    {
        var catalog = new ComboBoxTextAlignmentFontCatalog();
        var rasterizer = new ComboBoxTextAlignmentFontRasterizer();
        UiTextRenderer.ConfigureRuntimeServicesForTests(catalog, rasterizer);

        try
        {
            var host = new Canvas
            {
                Width = 240f,
                Height = 120f
            };
            var comboBox = new ComboBox
            {
                Width = 180f,
                Height = 36f,
                FontFamily = "ComboBox Alignment Probe",
                FontSize = 10f
            };
            comboBox.Items.Add("A");
            comboBox.SelectedIndex = 0;
            host.AddChild(comboBox);
            Canvas.SetLeft(comboBox, 24f);
            Canvas.SetTop(comboBox, 32f);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot);

            var textPosition = comboBox.GetSelectedTextRenderPositionForTests("A");
            var typography = UiTextRenderer.ResolveTypography(comboBox, comboBox.FontSize);
            var inkBounds = UiTextRenderer.GetInkBoundsForTests(typography, "A");
            var inkCenterY = textPosition.Y + inkBounds.Y + (inkBounds.Height / 2f);
            var comboBoxCenterY = comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f);
            var lineBoxTextY = comboBox.LayoutSlot.Y + ((comboBox.LayoutSlot.Height - UiTextRenderer.GetLineHeight(comboBox, comboBox.FontSize)) / 2f);
            var lineBoxInkCenterY = lineBoxTextY + inkBounds.Y + (inkBounds.Height / 2f);

            Assert.Equal(comboBoxCenterY, inkCenterY, 3);
            Assert.True(
                MathF.Abs(lineBoxInkCenterY - comboBoxCenterY) > 1f,
                $"Expected the repro font to prove line-box centering is visibly off. lineBoxInkCenter={lineBoxInkCenterY:0.###} comboCenter={comboBoxCenterY:0.###}.");
        }
        finally
        {
            UiTextRenderer.ConfigureRuntimeServicesForTests();
        }
    }

    [Fact]
    public void ClickOnComboBox_ShouldOpenDropDown()
    {
        var (uiRoot, comboBox) = CreateFixture();
        Assert.False(comboBox.IsDropDownOpen);

        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));
        Click(uiRoot, clickPoint);

        Assert.True(comboBox.IsDropDownOpen);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void ClickOnComboBox_WhenOpen_ShouldCloseDropDown()
    {
        var (uiRoot, comboBox) = CreateFixture();
        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));

        Click(uiRoot, clickPoint);
        Assert.True(comboBox.IsDropDownOpen);

        Click(uiRoot, clickPoint);
        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void OutsideClick_ShouldCloseDropDown_AndSyncIsDropDownOpenFalse()
    {
        var (uiRoot, comboBox) = CreateFixture();
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);

        Click(uiRoot, new Vector2(6f, 6f));

        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void OutsideClick_AfterClickOpeningDropDown_ShouldCloseDropDown()
    {
        var (uiRoot, comboBox) = CreateFixture();
        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));

        Click(uiRoot, clickPoint);
        Assert.True(comboBox.IsDropDownOpen);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);

        Click(uiRoot, new Vector2(6f, 6f));

        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void SelectionFromDropDown_ShouldCloseDropDown_AndPersistSelection()
    {
        var (uiRoot, comboBox) = CreateFixture();
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);
        dropDown!.SelectedIndex = 1;

        Assert.Equal(1, comboBox.SelectedIndex);
        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void DropDown_ShouldUseComboBoxItemContainers_AndApplyItemContainerStyle()
    {
        var (uiRoot, comboBox) = CreateFixture();
        var expectedBackground = new Color(0x22, 0x55, 0x99);
        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, expectedBackground));
        comboBox.ItemContainerStyle = style;

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);

        var hostPanel = FindItemsHostPanel(dropDown!);
        var firstItem = Assert.IsType<ComboBoxItem>(hostPanel.Children[0]);
        Assert.Equal(expectedBackground, firstItem.Background);
    }

    [Fact]
    public void DropDown_ShouldApplyItemTemplateToGeneratedComboBoxItemContainers()
    {
        var (uiRoot, comboBox) = CreateFixture();
        var template = new DataTemplate(static item => new TextBlock
        {
            Text = item?.ToString() ?? string.Empty,
            TextWrapping = TextWrapping.Wrap
        });
        comboBox.ItemTemplate = template;

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var hostPanel = FindItemsHostPanel(dropDown);
        var firstItem = Assert.IsType<ComboBoxItem>(hostPanel.Children[0]);
        Assert.Same(template, firstItem.ContentTemplate);
        Assert.Equal("Alpha", firstItem.Content);
    }

    [Fact]
    public void DropDown_WithTemplatedItems_ShouldOnlyBuildVisualContentForRealizedContainers()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 260f
        };
        var comboBox = new ComboBox
        {
            Width = 190f,
            Height = 36f,
            MaxDropDownHeight = 180f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item?.ToString() ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            })
        };

        for (var i = 0; i < 80; i++)
        {
            comboBox.Items.Add($"Item {i:00} - Deferred dropdown content realization probe");
        }

        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 16f);
        Canvas.SetTop(comboBox, 16f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var hostPanel = Assert.IsType<VirtualizingStackPanel>(FindItemsHostPanel(dropDown));
        var materializedContentCount = hostPanel.Children
            .OfType<ComboBoxItem>()
            .Count(item => item.GetVisualChildren().Any());

        Assert.True(hostPanel.RealizedChildrenCount > 0, "Expected dropdown virtualization to realize at least one item.");
        Assert.Equal(hostPanel.RealizedChildrenCount, materializedContentCount);
        Assert.True(
            materializedContentCount < hostPanel.Children.Count,
            $"Expected offscreen dropdown containers to defer content creation. realized={hostPanel.RealizedChildrenCount} materialized={materializedContentCount} children={hostPanel.Children.Count}.");
    }

    [Fact]
    public void DropDown_WhenTemplatedTextWraps_ShouldGiveComboBoxItemEnoughHeight()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 220f
        };
        var comboBox = new ComboBox
        {
            Width = 190f,
            Height = 36f,
            MaxDropDownHeight = 180f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item?.ToString() ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };
        comboBox.Items.Add("HeaderedItemsControl");
        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 16f);
        Canvas.SetTop(comboBox, 16f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var hostPanel = FindItemsHostPanel(dropDown);
        var item = Assert.IsType<ComboBoxItem>(Assert.Single(hostPanel.Children));
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(item.GetVisualChildren()));
        var innerHeight = item.LayoutSlot.Height - item.Padding.Vertical;

        Assert.True(
            innerHeight >= textBlock.DesiredSize.Y,
            $"Expected dropdown item inner height to fit wrapped text. itemHeight={item.LayoutSlot.Height:0.##} innerHeight={innerHeight:0.##} textDesired={textBlock.DesiredSize.Y:0.##} textSlot={textBlock.LayoutSlot.Height:0.##} text='{textBlock.Text}'.");
    }

    [Fact]
    public void ComboBoxItem_WhenTemplatedTextWraps_ShouldMeasureContentWithPaddingAdjustedWidth()
    {
        var item = new ComboBoxItem
        {
            Width = 190f,
            Padding = new Thickness(8f, 6f, 8f, 6f),
            Content = "HeaderedItemsControl",
            ContentTemplate = new DataTemplate(static content => new TextBlock
            {
                Text = content?.ToString() ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        item.Measure(new Vector2(item.Width, 500f));
        item.Arrange(new LayoutRect(0f, 0f, item.Width, item.DesiredSize.Y));

        var textBlock = Assert.IsType<TextBlock>(Assert.Single(item.GetVisualChildren()));
        var measuredInnerHeight = item.DesiredSize.Y - item.Padding.Vertical;

        Assert.True(
            measuredInnerHeight >= textBlock.DesiredSize.Y,
            $"Expected ComboBoxItem measure to reserve height for wrapped content at padded width. itemDesired={item.DesiredSize.Y:0.##} measuredInner={measuredInnerHeight:0.##} textDesired={textBlock.DesiredSize.Y:0.##}.");
    }

    [Fact]
    public void OpenDropDown_WithFewItems_ShouldSizeViewportToContent()
    {
        var (uiRoot, comboBox) = CreateFixture();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var scrollViewer = FindScrollViewer(dropDown);

        Assert.True(scrollViewer.ExtentHeight > 0f, $"Expected dropdown extent height to be positive, got {scrollViewer.ExtentHeight:0.##}.");
        Assert.InRange(
            scrollViewer.ViewportHeight - scrollViewer.ExtentHeight,
            -0.5f,
            6f);
        Assert.True(
            dropDown.LayoutSlot.Height < comboBox.MaxDropDownHeight - 20f,
            $"Expected dropdown with few items to size below the max cap. height={dropDown.LayoutSlot.Height:0.##} max={comboBox.MaxDropDownHeight:0.##} viewport={scrollViewer.ViewportHeight:0.##} extent={scrollViewer.ExtentHeight:0.##}");
    }

    [Fact]
    public void ClickOpen_WithLargeChoiceSet_ShouldBuildDropDownShellsOnlyOncePerOpen()
    {
        _ = ComboBox.GetTelemetryAndReset();

        var host = new Canvas
        {
            Width = 420f,
            Height = 300f
        };
        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 180f
        };

        for (var i = 0; i < 96; i++)
        {
            comboBox.Items.Add($"Choice {i:00} - shared ComboBox open churn probe");
        }

        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 96f);
        Canvas.SetTop(comboBox, 48f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var clickPoint = new Vector2(
            comboBox.LayoutSlot.X + (comboBox.LayoutSlot.Width / 2f),
            comboBox.LayoutSlot.Y + (comboBox.LayoutSlot.Height / 2f));
        Click(uiRoot, clickPoint);
        RunLayout(uiRoot);

        var snapshot = comboBox.GetComboBoxSnapshotForDiagnostics();
        var aggregate = ComboBox.GetTelemetryAndReset();

        Assert.True(snapshot.IsDropDownOpen, "Expected the ComboBox click to open the dropdown.");
        Assert.Equal(96, snapshot.ItemContainerCount);
        Assert.Equal(
            snapshot.ItemContainerCount,
            snapshot.BuildDropDownContainerCallCount);
        Assert.True(
            snapshot.DropDownItemCount < snapshot.ItemContainerCount,
            $"Expected virtualization to limit the visible dropdown slice after reusing the owner containers. items={snapshot.ItemContainerCount} visible={snapshot.DropDownItemCount} refreshCalls={snapshot.RefreshDropDownItemsCallCount}.");
        Assert.InRange(
            snapshot.RefreshDropDownItemsProjectedItemCount,
            snapshot.ItemContainerCount,
            snapshot.ItemContainerCount * 2);
        Assert.True(
            aggregate.RefreshDropDownItemsCallCount >= snapshot.RefreshDropDownItemsCallCount,
            $"Expected aggregate telemetry to retain the repeated refresh evidence. snapshotRefreshCalls={snapshot.RefreshDropDownItemsCallCount} aggregateRefreshCalls={aggregate.RefreshDropDownItemsCallCount}.");
    }

    [Fact]
    public void OpenDropDown_WithRawCursorAndFontWeightChoiceSets_HitsSameFrameworkViewportCap()
    {
        var host = new Canvas
        {
            Width = 520f,
            Height = 360f
        };

        var cursorComboBox = CreateComboBox(
            40f,
            60f,
            [
                "Arrow",
                "Hand",
                "IBeam",
                "Cross",
                "Help",
                "Wait",
                "AppStarting",
                "No",
                "SizeAll",
                "SizeNESW",
                "SizeNS",
                "SizeNWSE",
                "SizeWE",
                "UpArrow"
            ]);
        host.AddChild(cursorComboBox);

        var fontWeightComboBox = CreateComboBox(
            280f,
            60f,
            [
                "Thin",
                "Light",
                "Normal",
                "Medium",
                "SemiBold",
                "Bold",
                "ExtraBold",
                "Black"
            ]);
        host.AddChild(fontWeightComboBox);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        cursorComboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var cursorDropDown = Assert.IsType<ListBox>(cursorComboBox.DropDownListForTesting);
        var cursorScrollViewer = FindScrollViewer(cursorDropDown);
        var cursorAverageRowHeight = GetAverageVisibleListItemHeight(cursorDropDown);

        cursorComboBox.IsDropDownOpen = false;
        RunLayout(uiRoot);

        fontWeightComboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var fontWeightDropDown = Assert.IsType<ListBox>(fontWeightComboBox.DropDownListForTesting);
        var fontWeightScrollViewer = FindScrollViewer(fontWeightDropDown);
        var fontWeightAverageRowHeight = GetAverageVisibleListItemHeight(fontWeightDropDown);

        Assert.True(cursorScrollViewer.ExtentHeight > cursorScrollViewer.ViewportHeight + 40f,
            $"Expected the larger choice set to exceed the viewport cap. extent={cursorScrollViewer.ExtentHeight:0.##} viewport={cursorScrollViewer.ViewportHeight:0.##}");
        Assert.True(cursorScrollViewer.ExtentHeight > fontWeightScrollViewer.ExtentHeight,
            $"Expected the raw Cursor choice set to have more total content height than the raw FontWeight choice set. cursorExtent={cursorScrollViewer.ExtentHeight:0.##} fontWeightExtent={fontWeightScrollViewer.ExtentHeight:0.##}");
        Assert.InRange(fontWeightScrollViewer.ExtentHeight - fontWeightScrollViewer.ViewportHeight, 0f, 48f);
        Assert.InRange(MathF.Abs(cursorDropDown.LayoutSlot.Height - fontWeightDropDown.LayoutSlot.Height), 0f, 0.5f);
        Assert.InRange(MathF.Abs(cursorAverageRowHeight - fontWeightAverageRowHeight), 0f, 2f);
    }

    [Fact]
    public void OpenDropDown_WithRawCursorChoiceSet_ScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastItem()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 360f
        };

        var comboBox = CreateComboBox(
            40f,
            60f,
            [
                "Arrow",
                "Hand",
                "IBeam",
                "Cross",
                "Help",
                "Wait",
                "AppStarting",
                "No",
                "SizeAll",
                "SizeNESW",
                "SizeNS",
                "SizeNWSE",
                "SizeWE",
                "UpArrow"
            ]);
        host.AddChild(comboBox);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var scrollViewer = FindScrollViewer(dropDown);
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot);

        var lastItem = GetLastVisibleItem(dropDown);
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        var probe = new Vector2(
            scrollViewer.LayoutSlot.X + 24f,
            (scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight) - 2f);
        var hit = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.HitTest(host, probe));

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.True(
            IsDescendantOrSelf(lastItem, hit),
            $"Expected the viewport-bottom hit to land on the last dropdown item after scrolling to the end. hit={hit.GetType().Name}, lastItem={lastItem.GetType().Name}, probe={probe}, Offset={scrollViewer.VerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
    }

    [Fact]
    public void OpenDropDown_WithLargeChoiceSet_ScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastItem()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f
        };

        for (var i = 0; i < 96; i++)
        {
            comboBox.Items.Add($"Choice {i:00} - Root template style bottom-scroll blank-space repro");
        }

        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var scrollViewer = FindScrollViewer(dropDown);
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot);

        var lastItem = GetLastVisibleItem(dropDown);
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        var probe = new Vector2(
            scrollViewer.LayoutSlot.X + 24f,
            (scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight) - 2f);
        var hit = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.HitTest(host, probe));

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.True(
            IsDescendantOrSelf(lastItem, hit),
            $"Expected the viewport-bottom hit to land on the last large-choice dropdown item after scrolling to the end. hit={hit.GetType().Name}, lastItem={lastItem.GetType().Name}, probe={probe}, Offset={scrollViewer.VerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
    }

    [Fact]
    public void OpenDropDown_WithLargeTemplatedObjectChoiceSet_ScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastItem()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item is ComboBoxTemplateOption option ? option.DisplayText : string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        for (var i = 0; i < 96; i++)
        {
            comboBox.Items.Add(new ComboBoxTemplateOption($"Choice {i:00} - Root template style bottom-scroll blank-space repro"));
        }

        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var scrollViewer = FindScrollViewer(dropDown);
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot);

        var lastItem = GetLastVisibleItem(dropDown);
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        var probe = new Vector2(
            scrollViewer.LayoutSlot.X + 24f,
            (scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight) - 2f);
        var hit = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.HitTest(host, probe));

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.True(
            IsDescendantOrSelf(lastItem, hit),
            $"Expected the viewport-bottom hit to land on the last large templated dropdown item after scrolling to the end. hit={hit.GetType().Name}, lastItem={lastItem.GetType().Name}, probe={probe}, Offset={scrollViewer.VerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
    }

    [Fact]
    public void OpenDropDown_WithLargeTemplatedObjectChoiceSet_ScrollingToBottom_ShouldFullyRevealLastLogicalItem()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item is ComboBoxTemplateOption option ? option.DisplayText : string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        for (var i = 0; i < 96; i++)
        {
            comboBox.Items.Add(new ComboBoxTemplateOption($"Choice {i:00} - Root template style bottom-scroll partial-last-item repro"));
        }

        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var scrollViewer = FindScrollViewer(dropDown);
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot);

        var lastRealized = GetHighestRealizedIndexItem(dropDown);
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.Equal(
            dropDown.Items.Count - 1,
            lastRealized.Index);
        Assert.True(
            lastRealized.Element.LayoutSlot.Y + lastRealized.Element.LayoutSlot.Height <= viewportBottom + 1f,
            $"Expected the last logical dropdown item to be fully visible after scrolling to the end. index={lastRealized.Index} itemBottom={lastRealized.Element.LayoutSlot.Y + lastRealized.Element.LayoutSlot.Height:0.##} viewportBottom={viewportBottom:0.##} itemTop={lastRealized.Element.LayoutSlot.Y:0.##} itemHeight={lastRealized.Element.LayoutSlot.Height:0.##} Offset={scrollViewer.VerticalOffset:0.##} Extent={scrollViewer.ExtentHeight:0.##} Viewport={scrollViewer.ViewportHeight:0.##}.");
    }

    [Fact]
    public void OpenDropDown_WithLargeVirtualizedChoiceSet_ScrollingBottomThenTop_ShouldRestoreTopRows()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item is ComboBoxTemplateOption option ? option.DisplayText : string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        for (var i = 0; i < 98; i++)
        {
            comboBox.Items.Add(new ComboBoxTemplateOption($"Choice {i:00} - bottom then top slot restore repro"));
        }

        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var scrollViewer = FindScrollViewer(dropDown);
        ScrollDropDownToBottom(uiRoot, scrollViewer);

        scrollViewer.ScrollToVerticalOffset(scrollViewer.ViewportHeight);
        RunLayout(uiRoot);
        scrollViewer.ScrollToVerticalOffset(0f);
        RunLayout(uiRoot);

        var snapshot = comboBox.GetComboBoxDropDownSnapshotForDiagnostics();
        var topProbe = new Vector2(
            scrollViewer.LayoutSlot.X + 24f,
            scrollViewer.LayoutSlot.Y + 8f);
        var hit = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.HitTest(host, topProbe));
        var firstVisibleItem = Assert.IsType<ComboBoxItem>(GetFirstViewportIntersectingItem(dropDown, scrollViewer));

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset) <= 0.5f,
            $"Expected the dropdown ScrollViewer to return to the top. Offset={scrollViewer.VerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.Equal(0, snapshot.VirtualizingStackPanelFirstRealizedIndex);
        Assert.True(
            snapshot.NonZeroItemContainerSlotCount >= snapshot.ViewportIntersectingItemContainerCount,
            $"Expected every viewport-intersecting dropdown item to have a non-zero layout slot after returning to top. {FormatComboBoxDropDownSnapshot(snapshot)}");
        Assert.True(
            snapshot.ViewportIntersectingItemContainerCount > 0,
            $"Expected top dropdown rows to intersect the viewport after returning to top. {FormatComboBoxDropDownSnapshot(snapshot)}");
        Assert.True(
            IsDescendantOrSelf(firstVisibleItem, hit),
            $"Expected hit testing near the top of the reopened viewport to land on the restored first visible dropdown item. hit={hit.GetType().Name}, firstVisibleItem={firstVisibleItem.GetType().Name}, probe={topProbe}, {FormatComboBoxDropDownSnapshot(snapshot)}");
    }

    [Fact]
    public void ReopenDropDown_AfterScrollingToBottom_ShouldResetScrollViewerToTop()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item is ComboBoxTemplateOption option ? option.DisplayText : string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        for (var i = 0; i < 95; i++)
        {
            comboBox.Items.Add(new ComboBoxTemplateOption($"Choice {i:00} - reopen scroll state repro"));
        }

        comboBox.Items.Add(new ComboBoxTemplateOption("VirtualizingStackPanel"));
        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var firstOpenDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var firstOpenScrollViewer = FindScrollViewer(firstOpenDropDown);
        ScrollDropDownToBottom(uiRoot, firstOpenScrollViewer);

        Assert.True(
            firstOpenScrollViewer.VerticalOffset > firstOpenScrollViewer.ViewportHeight,
            $"Expected the first open to retain a substantial downward dropdown scroll offset before reopening. Offset={firstOpenScrollViewer.VerticalOffset:0.##}, Extent={firstOpenScrollViewer.ExtentHeight:0.##}, Viewport={firstOpenScrollViewer.ViewportHeight:0.##}.");

        comboBox.IsDropDownOpen = false;
        RunLayout(uiRoot);
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var reopenedDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var reopenedScrollViewer = FindScrollViewer(reopenedDropDown);

        Assert.True(
            reopenedScrollViewer.VerticalOffset <= 0.5f,
            $"Expected reopening the dropdown to reset the ScrollViewer to the top. Offset={reopenedScrollViewer.VerticalOffset:0.##}, Extent={reopenedScrollViewer.ExtentHeight:0.##}, Viewport={reopenedScrollViewer.ViewportHeight:0.##}, SelectedIndex={comboBox.SelectedIndex}.");
    }

    [Fact]
    public void ReopenDropDown_AfterScrollingToBottom_ClickingVisibleItem_ShouldSelectThatVisibleItem()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item is ComboBoxTemplateOption option ? option.DisplayText : string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        for (var i = 0; i < 95; i++)
        {
            comboBox.Items.Add(new ComboBoxTemplateOption($"Choice {i:00} - visible selection repro"));
        }

        comboBox.Items.Add(new ComboBoxTemplateOption("VirtualizingStackPanel"));
        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var firstOpenDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var firstOpenScrollViewer = FindScrollViewer(firstOpenDropDown);
        ScrollDropDownToBottom(uiRoot, firstOpenScrollViewer);

        comboBox.IsDropDownOpen = false;
        RunLayout(uiRoot);
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var reopenedDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var reopenedScrollViewer = FindScrollViewer(reopenedDropDown);
        var clickedItem = Assert.IsType<ComboBoxItem>(GetLastViewportIntersectingItem(reopenedDropDown, reopenedScrollViewer));
        var expectedText = GetVisibleDisplayText(clickedItem);
        var visibleTop = MathF.Max(clickedItem.LayoutSlot.Y, reopenedScrollViewer.LayoutSlot.Y);
        var visibleBottom = MathF.Min(
            clickedItem.LayoutSlot.Y + clickedItem.LayoutSlot.Height,
            reopenedScrollViewer.LayoutSlot.Y + reopenedScrollViewer.ViewportHeight);
        var clickPoint = new Vector2(
            clickedItem.LayoutSlot.X + (clickedItem.LayoutSlot.Width / 2f),
            visibleTop + ((visibleBottom - visibleTop) / 2f));

        Click(uiRoot, clickPoint);
        RunLayout(uiRoot);

        var selectedOption = Assert.IsType<ComboBoxTemplateOption>(comboBox.SelectedItem);

        Assert.Equal(
            expectedText,
            selectedOption.DisplayText);
        Assert.False(comboBox.IsDropDownOpen);
    }

    [Fact]
    public void ReopenDropDown_AfterScrollingToBottom_ShouldRetainVisibleItemTextBeforeAnyFurtherScroll()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item is ComboBoxTemplateOption option ? option.DisplayText : string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        for (var i = 0; i < 95; i++)
        {
            comboBox.Items.Add(new ComboBoxTemplateOption($"Choice {i:00} - retained visible text repro"));
        }

        comboBox.Items.Add(new ComboBoxTemplateOption("VirtualizingStackPanel"));
        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var firstOpenDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var firstOpenScrollViewer = FindScrollViewer(firstOpenDropDown);
        ScrollDropDownToBottom(uiRoot, firstOpenScrollViewer);

        comboBox.IsDropDownOpen = false;
        RunLayout(uiRoot);
        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var reopenedDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var reopenedScrollViewer = FindScrollViewer(reopenedDropDown);
        var visibleItem = Assert.IsType<ComboBoxItem>(GetLastViewportIntersectingItem(reopenedDropDown, reopenedScrollViewer));
        var visibleText = GetVisibleDisplayTextBlock(visibleItem);

        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Contains(visibleText, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    [Fact]
    public void ReopenDropDown_AfterSelectingBottomItem_ShouldRetainVisibleItemTextBeforeAnyFurtherScroll()
    {
        var host = new Canvas
        {
            Width = 360f,
            Height = 420f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 36f,
            MaxDropDownHeight = 260f,
            ItemTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = item is ComboBoxTemplateOption option ? option.DisplayText : string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = "Consolas",
                FontSize = 12f
            })
        };

        for (var i = 0; i < 95; i++)
        {
            comboBox.Items.Add(new ComboBoxTemplateOption($"Choice {i:00} - selected bottom retained visible text repro"));
        }

        comboBox.Items.Add(new ComboBoxTemplateOption("VirtualizingStackPanel"));
        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 40f);
        Canvas.SetTop(comboBox, 40f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var firstOpenDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var firstOpenScrollViewer = FindScrollViewer(firstOpenDropDown);
        ScrollDropDownToBottom(uiRoot, firstOpenScrollViewer);

        var bottomItem = Assert.IsType<ComboBoxItem>(GetLastViewportIntersectingItem(firstOpenDropDown, firstOpenScrollViewer));
        var bottomItemText = GetVisibleDisplayText(bottomItem);
        Assert.True(
            bottomItemText == "VirtualizingStackPanel",
            $"Expected scrolling to the bottom to reveal the logical last item. Actual={bottomItemText}. {FormatListBoxSlotsForFailure(firstOpenDropDown, firstOpenScrollViewer)}");
        var clickPoint = new Vector2(
            bottomItem.LayoutSlot.X + (bottomItem.LayoutSlot.Width / 2f),
            bottomItem.LayoutSlot.Y + (bottomItem.LayoutSlot.Height / 2f));

        Click(uiRoot, clickPoint);
        RunLayout(uiRoot);

        Assert.False(comboBox.IsDropDownOpen);
        var selectedOption = Assert.IsType<ComboBoxTemplateOption>(comboBox.SelectedItem);
        Assert.Equal("VirtualizingStackPanel", selectedOption.DisplayText);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var reopenedDropDown = Assert.IsType<ListBox>(comboBox.DropDownListForTesting);
        var reopenedScrollViewer = FindScrollViewer(reopenedDropDown);
        var visibleItem = Assert.IsType<ComboBoxItem>(GetLastViewportIntersectingItem(reopenedDropDown, reopenedScrollViewer));
        var visibleText = GetVisibleDisplayTextBlock(visibleItem);

        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Contains(visibleText, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    [Fact]
    public void DropDown_InNestedScrollViewerUserControl_ShouldAnchorToComboBoxInsteadOfFlowingAfterSiblingContent()
    {
        var rootView = new UserControl();
        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360f, GridUnitType.Pixel) });
        rootView.Content = rootGrid;

        var filler = new Border();
        Grid.SetColumn(filler, 0);
        rootGrid.AddChild(filler);

        var scrollViewer = new ScrollViewer();
        Grid.SetColumn(scrollViewer, 1);
        rootGrid.AddChild(scrollViewer);

        var sidebar = new StackPanel();
        scrollViewer.Content = sidebar;
        sidebar.AddChild(new Label { Content = "Payload lab" });
        sidebar.AddChild(new Label { Content = "Round-trip full documents or the active selection." });

        var comboBox = new ComboBox
        {
            Width = 320f,
            Height = 40f,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };
        comboBox.Items.Add("Flow XML");
        comboBox.Items.Add("XAML");
        comboBox.Items.Add("XamlPackage");
        comboBox.Items.Add("Rich Text Format");
        comboBox.Items.Add("Plain Text");
        comboBox.SelectedIndex = 0;
        sidebar.AddChild(comboBox);

        var buttonRow = new WrapPanel { Margin = new Thickness(0f, 8f, 0f, 0f) };
        buttonRow.AddChild(new Button { Content = "Export Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Export Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        sidebar.AddChild(buttonRow);
        sidebar.AddChild(new TextBox { Width = 320f, Height = 220f, Margin = new Thickness(0f, 8f, 0f, 0f) });

        var uiRoot = new UiRoot(rootView);
        RunLayout(uiRoot, 1200, 900);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 1200, 900);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);
        Assert.InRange(dropDown!.LayoutSlot.X - comboBox.LayoutSlot.X, 0f, 4f);
        Assert.InRange(dropDown.LayoutSlot.Y - (comboBox.LayoutSlot.Y + comboBox.LayoutSlot.Height), 0f, 8f);
    }

    [Fact]
    public void DropDown_InScrolledScrollViewer_ShouldAnchorToRenderedComboBoxBounds()
    {
        var (uiRoot, scrollViewer, comboBox) = CreateScrolledSidebarFixture();

        scrollViewer.ScrollToVerticalOffset(96f);
        RunLayout(uiRoot, 1200, 900);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 1200, 900);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);

        var renderedComboBoxY = comboBox.LayoutSlot.Y;
        var renderedDropDownY = dropDown!.LayoutSlot.Y;
        Assert.InRange(dropDown.LayoutSlot.X - comboBox.LayoutSlot.X, 0f, 4f);
        Assert.InRange(renderedDropDownY - (renderedComboBoxY + comboBox.LayoutSlot.Height), 0f, 8f);
    }

    [Fact]
    public void ScrollViewerScroll_ShouldCloseOpenComboBoxDropDown()
    {
        var (uiRoot, scrollViewer, comboBox) = CreateScrolledSidebarFixture();

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 1200, 900);
        Assert.True(comboBox.IsDropDownPopupOpenForTesting);

        scrollViewer.ScrollToVerticalOffset(64f);
        RunLayout(uiRoot, 1200, 900);

        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(comboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void OpenDropDown_ShouldNotReflowSiblingRows_InLocalGrid()
    {
        var root = new Panel
        {
            Width = 840f,
            Height = 520f
        };

        var rightColumn = new Grid();
        rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.AddChild(rightColumn);

        var comboBox = new ComboBox
        {
            Width = 260f,
            Height = 32f,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 0;
        Grid.SetRow(comboBox, 0);
        rightColumn.AddChild(comboBox);

        var siblingButton = new Button
        {
            Content = "Open Combo Dropdown",
            Height = 30f,
            Margin = new Thickness(0f, 10f, 0f, 0f)
        };
        Grid.SetRow(siblingButton, 1);
        rightColumn.AddChild(siblingButton);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        var siblingYBefore = siblingButton.LayoutSlot.Y;

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);
        var siblingYAfter = siblingButton.LayoutSlot.Y;

        Assert.True(comboBox.IsDropDownPopupOpenForTesting);
        Assert.Equal(siblingYBefore, siblingYAfter);
    }

    [Fact]
    public void ClickingDropDownItem_OverlappingButton_ShouldNotClickUnderlyingButton()
    {
        var host = new Canvas
        {
            Width = 480f,
            Height = 280f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 32f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 2;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 24f);
        Canvas.SetTop(comboBox, 24f);

        var openButtonClicks = 0;
        var openDropDownButton = new Button
        {
            Content = "Open Combo Dropdown",
            Width = 240f,
            Height = 32f
        };
        openDropDownButton.Click += (_, _) =>
        {
            openButtonClicks++;
            comboBox.IsDropDownOpen = true;
        };
        host.AddChild(openDropDownButton);
        Canvas.SetLeft(openDropDownButton, 24f);
        Canvas.SetTop(openDropDownButton, 58f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        // First click opens the dropdown through the underlying button, mirroring the lab interaction.
        Click(uiRoot, new Vector2(openDropDownButton.LayoutSlot.X + 8f, openDropDownButton.LayoutSlot.Y + 8f));
        Assert.Equal(1, openButtonClicks);
        Assert.True(comboBox.IsDropDownOpen);

        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);

        // Click inside the first row (Alpha). This point intentionally overlaps the button's bounds.
        var clickPoint = new Vector2(dropDown!.LayoutSlot.X + 12f, dropDown.LayoutSlot.Y + 12f);
        Click(uiRoot, clickPoint);

        Assert.Equal(1, openButtonClicks);
        Assert.Equal(0, comboBox.SelectedIndex);
    }

    [Fact]
    public void ClickingDropDownItem_OverlappingComboBox_ShouldNotOpenUnderlyingComboBox()
    {
        var host = new Canvas
        {
            Width = 480f,
            Height = 280f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 32f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 2;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 24f);
        Canvas.SetTop(comboBox, 24f);

        var underlyingComboBox = new ComboBox
        {
            Width = 220f,
            Height = 32f
        };
        underlyingComboBox.Items.Add("One");
        underlyingComboBox.Items.Add("Two");
        underlyingComboBox.Items.Add("Three");
        underlyingComboBox.SelectedIndex = 1;
        host.AddChild(underlyingComboBox);
        Canvas.SetLeft(underlyingComboBox, 24f);
        Canvas.SetTop(underlyingComboBox, 58f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);
        Assert.True(comboBox.IsDropDownOpen);
        Assert.False(underlyingComboBox.IsDropDownOpen);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);

        var clickPoint = new Vector2(dropDown!.LayoutSlot.X + 12f, dropDown.LayoutSlot.Y + 12f);
        Click(uiRoot, clickPoint);

        Assert.Equal(0, comboBox.SelectedIndex);
        Assert.False(comboBox.IsDropDownOpen);
        Assert.False(underlyingComboBox.IsDropDownOpen);
        Assert.False(underlyingComboBox.IsDropDownPopupOpenForTesting);
        Assert.Equal(1, underlyingComboBox.SelectedIndex);
    }

    [Fact]
    public void AfterDropDownCloses_ClickingFormerItemArea_ShouldHitUnderlyingButton()
    {
        var host = new Canvas
        {
            Width = 520f,
            Height = 320f
        };

        var comboBox = new ComboBox
        {
            Width = 220f,
            Height = 32f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 2;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 24f);
        Canvas.SetTop(comboBox, 24f);

        var buttonClicks = 0;
        var underneathButton = new Button
        {
            Content = "Underneath",
            Width = 260f,
            Height = 32f
        };
        underneathButton.Click += (_, _) => buttonClicks++;
        host.AddChild(underneathButton);
        Canvas.SetLeft(underneathButton, 24f);
        Canvas.SetTop(underneathButton, 58f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);
        var firstItemPoint = new Vector2(dropDown!.LayoutSlot.X + 12f, dropDown.LayoutSlot.Y + 12f);

        // Select Alpha (dropdown should close).
        Click(uiRoot, firstItemPoint);
        RunLayout(uiRoot);
        Assert.False(comboBox.IsDropDownOpen);
        Assert.Equal(0, comboBox.SelectedIndex);
        var hit = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.HitTest(host, firstItemPoint));
        Assert.True(IsDescendantOrSelf(underneathButton, hit));

        // Clicking the same coordinates again should now hit the underlying button.
        Click(uiRoot, firstItemPoint);

        Assert.Equal(1, buttonClicks);
    }

    private static (UiRoot UiRoot, ComboBox ComboBox) CreateFixture()
    {
        var host = new Canvas
        {
            Width = 420f,
            Height = 260f
        };

        var comboBox = new ComboBox
        {
            Width = 180f,
            Height = 36f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");
        comboBox.SelectedIndex = 0;
        host.AddChild(comboBox);
        Canvas.SetLeft(comboBox, 120f);
        Canvas.SetTop(comboBox, 90f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, comboBox);
    }

    private sealed class ComboBoxTextAlignmentFontCatalog : IUiFontCatalog
    {
        public UiResolvedTypeface Resolve(UiTypography typography)
        {
            return new UiResolvedTypeface(typography.Family, typography.Weight, typography.Style, "combobox-alignment-probe.ttf", 400);
        }
    }

    private sealed class ComboBoxTextAlignmentFontRasterizer : IUiFontRasterizer
    {
        public UiTextMetrics Measure(UiResolvedTypeface typeface, float fontSize, string text, UiTextStyleOverride styleOverride)
        {
            _ = typeface;
            _ = fontSize;
            _ = styleOverride;
            return new UiTextMetrics(text.Length * 7f, 8f, 20f, 14f, 6f);
        }

        public UiGlyphRasterized Rasterize(UiResolvedTypeface typeface, float fontSize, int codePoint, UiTextAntialiasMode antialiasMode)
        {
            _ = typeface;
            _ = fontSize;
            return new UiGlyphRasterized([], 7, 8, (uint)codePoint, 0f, 12f, 7f, antialiasMode);
        }

        public float GetKerning(UiResolvedTypeface typeface, float fontSize, uint leftGlyphIndex, uint rightGlyphIndex)
        {
            _ = typeface;
            _ = fontSize;
            _ = leftGlyphIndex;
            _ = rightGlyphIndex;
            return 0f;
        }

        public void Dispose()
        {
        }
    }

    private sealed record ComboBoxTemplateOption(string DisplayText);

    private static ComboBox CreateComboBox(float left, float top, IReadOnlyList<string> items)
    {
        var comboBox = new ComboBox
        {
            Width = 180f,
            Height = 36f
        };

        for (var i = 0; i < items.Count; i++)
        {
            comboBox.Items.Add(items[i]);
        }

        comboBox.SelectedIndex = 0;
        Canvas.SetLeft(comboBox, left);
        Canvas.SetTop(comboBox, top);
        return comboBox;
    }

    private static (UiRoot UiRoot, ScrollViewer ScrollViewer, ComboBox ComboBox) CreateScrolledSidebarFixture()
    {
        var rootView = new UserControl();
        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360f, GridUnitType.Pixel) });
        rootView.Content = rootGrid;

        var filler = new Border();
        Grid.SetColumn(filler, 0);
        rootGrid.AddChild(filler);

        var scrollViewer = new ScrollViewer();
        Grid.SetColumn(scrollViewer, 1);
        rootGrid.AddChild(scrollViewer);

        var sidebar = new StackPanel();
        scrollViewer.Content = sidebar;
        sidebar.AddChild(new Border { Height = 140f });
        sidebar.AddChild(new Label { Content = "Payload lab" });
        sidebar.AddChild(new Label { Content = "Round-trip full documents or the active selection." });

        var comboBox = new ComboBox
        {
            Width = 320f,
            Height = 40f,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };
        comboBox.Items.Add("Flow XML");
        comboBox.Items.Add("XAML");
        comboBox.Items.Add("XamlPackage");
        comboBox.Items.Add("Rich Text Format");
        comboBox.Items.Add("Plain Text");
        comboBox.SelectedIndex = 0;
        sidebar.AddChild(comboBox);

        var buttonRow = new WrapPanel { Margin = new Thickness(0f, 8f, 0f, 0f) };
        buttonRow.AddChild(new Button { Content = "Export Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Export Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Doc", Width = 112f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        buttonRow.AddChild(new Button { Content = "Load Selection", Width = 144f, Height = 36f, Margin = new Thickness(0f, 0f, 8f, 8f) });
        sidebar.AddChild(buttonRow);
        sidebar.AddChild(new TextBox { Width = 320f, Height = 220f, Margin = new Thickness(0f, 8f, 0f, 0f) });
        sidebar.AddChild(new Border { Height = 420f });

        var uiRoot = new UiRoot(rootView);
        RunLayout(uiRoot, 1200, 900);
        return (uiRoot, scrollViewer, comboBox);
    }

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement candidate)
    {
        for (UIElement? current = candidate; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
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

    private static ScrollViewer FindScrollViewer(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }
        }

        throw new InvalidOperationException("Could not resolve ListBox ScrollViewer.");
    }

    private static float GetAverageVisibleListItemHeight(ListBox listBox)
    {
        var hostPanel = FindItemsHostPanel(listBox);
        var totalHeight = 0f;
        var itemCount = 0;
        foreach (var child in hostPanel.Children)
        {
            if (child is FrameworkElement element)
            {
                totalHeight += element.LayoutSlot.Height;
                itemCount++;
            }
        }

        Assert.True(itemCount > 0, "Expected ListBox items host to contain visible item containers.");
        return totalHeight / itemCount;
    }

    private static FrameworkElement GetLastVisibleItem(ListBox listBox)
    {
        var hostPanel = FindItemsHostPanel(listBox);
        for (var i = hostPanel.Children.Count - 1; i >= 0; i--)
        {
            if (hostPanel.Children[i] is FrameworkElement element &&
                element.LayoutSlot.Width > 0f &&
                element.LayoutSlot.Height > 0f &&
                TryGetVisibleDisplayTextBlock(element) != null)
            {
                return element;
            }
        }

        throw new InvalidOperationException("Expected ListBox items host to contain at least one visible item container.");
    }

    private static FrameworkElement GetLastViewportIntersectingItem(ListBox listBox, ScrollViewer scrollViewer)
    {
        var hostPanel = FindItemsHostPanel(listBox);
        var viewportTop = scrollViewer.LayoutSlot.Y;
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        FrameworkElement? bestElement = null;
        var bestBottom = float.NegativeInfinity;

        foreach (var child in hostPanel.Children)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var elementTop = element.LayoutSlot.Y;
            var elementBottom = element.LayoutSlot.Y + element.LayoutSlot.Height;
            if (elementBottom <= viewportTop || elementTop >= viewportBottom)
            {
                continue;
            }

            if (elementBottom > bestBottom)
            {
                bestElement = element;
                bestBottom = elementBottom;
            }
        }

        return bestElement ?? throw new InvalidOperationException(
            $"Expected ListBox items host to contain an item intersecting the viewport. {FormatListBoxSlotsForFailure(listBox, scrollViewer)}");
    }

    private static FrameworkElement GetFirstViewportIntersectingItem(ListBox listBox, ScrollViewer scrollViewer)
    {
        var hostPanel = FindItemsHostPanel(listBox);
        var viewportTop = scrollViewer.LayoutSlot.Y;
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        FrameworkElement? bestElement = null;
        var bestTop = float.PositiveInfinity;

        foreach (var child in hostPanel.Children)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var elementTop = element.LayoutSlot.Y;
            var elementBottom = element.LayoutSlot.Y + element.LayoutSlot.Height;
            if (element.LayoutSlot.Width <= 0f ||
                element.LayoutSlot.Height <= 0f ||
                elementBottom <= viewportTop ||
                elementTop >= viewportBottom)
            {
                continue;
            }

            if (elementTop < bestTop)
            {
                bestElement = element;
                bestTop = elementTop;
            }
        }

        return bestElement ?? throw new InvalidOperationException("Expected ListBox items host to contain an item intersecting the viewport.");
    }

    private static (FrameworkElement Element, int Index) GetHighestRealizedIndexItem(ListBox listBox)
    {
        var hostPanel = FindItemsHostPanel(listBox);
        FrameworkElement? bestElement = null;
        var bestIndex = -1;

        for (var i = 0; i < hostPanel.Children.Count; i++)
        {
            if (hostPanel.Children[i] is not FrameworkElement element)
            {
                continue;
            }

            if (!listBox.TryGetGeneratedItemInfo(element, out _, out var index))
            {
                continue;
            }

            if (index > bestIndex)
            {
                bestIndex = index;
                bestElement = element;
            }
        }

        return bestElement != null
            ? (bestElement, bestIndex)
            : throw new InvalidOperationException("Expected ListBox items host to contain at least one realized item container with generated item info.");
    }

    private static string FormatListBoxSlotsForFailure(ListBox listBox, ScrollViewer scrollViewer)
    {
        var hostPanel = FindItemsHostPanel(listBox);
        var parts = new List<string>();
        for (var i = 0; i < hostPanel.Children.Count && parts.Count < 16; i++)
        {
            if (hostPanel.Children[i] is not FrameworkElement element)
            {
                continue;
            }

            listBox.TryGetGeneratedItemInfo(element, out _, out var generatedIndex);
            parts.Add($"{i}->{generatedIndex}@({element.LayoutSlot.X:0.#},{element.LayoutSlot.Y:0.#},{element.LayoutSlot.Width:0.#},{element.LayoutSlot.Height:0.#})/{GetVisibleDisplayTextOrEmpty(element)}");
        }

        return $"offset={scrollViewer.VerticalOffset:0.##}, viewport=({scrollViewer.LayoutSlot.X:0.#},{scrollViewer.LayoutSlot.Y:0.#},{scrollViewer.ViewportWidth:0.#},{scrollViewer.ViewportHeight:0.#}), children={hostPanel.Children.Count}, slots={string.Join(" | ", parts)}";
    }

    private static string GetVisibleDisplayTextOrEmpty(FrameworkElement element)
    {
        return TryGetVisibleDisplayTextBlock(element)?.Text ?? string.Empty;
    }

    private static string FormatComboBoxDropDownSnapshot(ComboBoxDropDownRuntimeDiagnosticsSnapshot snapshot)
    {
        return
            $"offset={snapshot.ScrollViewerVerticalOffset:0.##}, viewport={snapshot.ScrollViewerViewportHeight:0.##}, " +
            $"realized={snapshot.VirtualizingStackPanelFirstRealizedIndex}..{snapshot.VirtualizingStackPanelLastRealizedIndex}, " +
            $"containers={snapshot.ListItemContainerCount}, realizedContainers={snapshot.RealizedItemContainerCount}, " +
            $"nonZeroSlots={snapshot.NonZeroItemContainerSlotCount}, viewportHits={snapshot.ViewportIntersectingItemContainerCount}, " +
            $"firstSlots={snapshot.FirstContainerSlotSummary}, viewportSlots={snapshot.ViewportIntersectingContainerSlotSummary}, " +
            $"lastViewerOwned={snapshot.VirtualizingStackPanelLastTryArrangeForViewerOwnedOffsetResult}/{snapshot.VirtualizingStackPanelLastTryArrangeForViewerOwnedOffsetReason}, " +
            $"lastArrangeRange={snapshot.VirtualizingStackPanelLastArrangeRangeFirst}..{snapshot.VirtualizingStackPanelLastArrangeRangeLast}@{snapshot.VirtualizingStackPanelLastArrangeRangeViewportOffset:0.##}, " +
            $"lastArrangeOrTranslate={snapshot.VirtualizingStackPanelLastArrangeOrTranslateFirst}..{snapshot.VirtualizingStackPanelLastArrangeOrTranslateLast}@{snapshot.VirtualizingStackPanelLastArrangeOrTranslateViewportOffset:0.##}";
    }

    private static string GetVisibleDisplayText(FrameworkElement element)
    {
        return GetVisibleDisplayTextBlock(element).Text;
    }

    private static TextBlock GetVisibleDisplayTextBlock(FrameworkElement element)
    {
        return TryGetVisibleDisplayTextBlock(element) ??
            throw new InvalidOperationException($"Expected {element.GetType().Name} to contain visible text content.");
    }

    private static TextBlock? TryGetVisibleDisplayTextBlock(FrameworkElement element)
    {
        if (element is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
        {
            return textBlock;
        }

        foreach (var child in element.GetVisualChildren())
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var descendantTextBlock = TryGetVisibleDisplayTextBlock(frameworkChild);
            if (descendantTextBlock != null)
            {
                return descendantTextBlock;
            }
        }

        return null;
    }

    private static void ScrollDropDownToBottom(UiRoot uiRoot, ScrollViewer scrollViewer)
    {
        var priorOffset = -1f;
        var priorExtent = -1f;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            scrollViewer.ScrollToVerticalOffset(float.MaxValue);
            RunLayout(uiRoot);

            var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
            if (MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f)
            {
                return;
            }

            if (MathF.Abs(scrollViewer.VerticalOffset - priorOffset) <= 0.5f &&
                MathF.Abs(scrollViewer.ExtentHeight - priorExtent) <= 0.5f)
            {
                break;
            }

            priorOffset = scrollViewer.VerticalOffset;
            priorExtent = scrollViewer.ExtentHeight;
        }
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false)
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
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width = 420, int height = 260)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}

