using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class DesignerControllerTests
{
    private const string ValidViewXml = """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     Background="#101820">
          <Grid x:Name="RootGrid">
            <StackPanel x:Name="HostPanel">
              <TextBlock Text="Hello designer" />
              <Button x:Name="SaveButton"
                      Content="Save" />
            </StackPanel>
          </Grid>
        </UserControl>
        """;

    private const string InvalidViewXml = """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Grid>
            <Button UnknownProperty="Boom"
                    Content="Broken" />
          </Grid>
        </UserControl>
        """;

        private const string LateInvalidViewXml = """
                <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <StackPanel>
                        <TextBlock Text="Line 01" />
                        <TextBlock Text="Line 02" />
                        <TextBlock Text="Line 03" />
                        <TextBlock Text="Line 04" />
                        <TextBlock Text="Line 05" />
                        <TextBlock Text="Line 06" />
                        <TextBlock Text="Line 07" />
                        <TextBlock Text="Line 08" />
                        <TextBlock Text="Line 09" />
                        <Button UnknownProperty="Boom"
                                        Content="Broken" />
                    </StackPanel>
                </UserControl>
                """;

        private const string MalformedViewXml = """
                <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <Grid>
                        <Button Content="Broken">
                    </Grid>
                </UserControl>
                """;

    private const int FixedCompletionScrollFrameworkMeasureCallCount = 36;
    private const int FixedCompletionScrollVisualChildrenTraversalCount = 5874;

    public DesignerControllerTests()
    {
        ResetDesignerTestState();
    }

    [Fact]
    public void Refresh_ValidUserControl_SucceedsAndBuildsVisualTreeAndInspector()
    {
        var controller = new InkkSlinger.Designer.DesignerController();

        var succeeded = controller.Refresh(ValidViewXml);

        Assert.True(succeeded);
        Assert.True(controller.LastRefreshSucceeded);
        Assert.Equal(InkkSlinger.Designer.DesignerPreviewState.Success, controller.PreviewState);
        Assert.NotNull(controller.PreviewRoot);
        Assert.NotNull(controller.VisualTreeRoot);
        Assert.Equal("UserControl", controller.VisualTreeRoot!.TypeName);
        Assert.Equal("UserControl", controller.Inspector.Header);
        Assert.Contains(controller.Inspector.Properties, property => property.Name == "Type" && property.Value == "UserControl");
        Assert.Contains(controller.Inspector.Properties, property => property.Name == "Background" && property.Value.Contains("Local", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectVisualNode_KnownNode_UpdatesInspectorState()
    {
        var controller = new InkkSlinger.Designer.DesignerController();
        Assert.True(controller.Refresh(ValidViewXml));

        var buttonNodeId = FindNodeId(controller.VisualTreeRoot!, "SaveButton", "Button");

        var selected = controller.SelectVisualNode(buttonNodeId);

        Assert.True(selected);
        Assert.Equal(buttonNodeId, controller.SelectedNodeId);
        Assert.Equal("SaveButton : Button", controller.Inspector.Header);
        Assert.Contains(controller.Inspector.Properties, property => property.Name == "Name" && property.Value == "SaveButton");
        Assert.Contains(controller.Inspector.Properties, property => property.Name == "Type" && property.Value == "Button");
        Assert.Contains(controller.Inspector.Properties, property => property.Name == "Content" && property.Value.Contains("Save", StringComparison.Ordinal));
    }

    [Fact]
    public void Refresh_InvalidXml_FailsClearsPreviewStateAndCapturesDiagnostics()
    {
        var controller = new InkkSlinger.Designer.DesignerController();
        Assert.True(controller.Refresh(ValidViewXml));

        var succeeded = controller.Refresh(InvalidViewXml);

        Assert.False(succeeded);
        Assert.Equal(InkkSlinger.Designer.DesignerPreviewState.Error, controller.PreviewState);
        Assert.False(controller.LastRefreshSucceeded);
        Assert.Null(controller.PreviewRoot);
        Assert.Null(controller.VisualTreeRoot);
        Assert.Null(controller.SelectedNodeId);
        Assert.Equal(InkkSlinger.Designer.DesignerInspectorModel.Empty, controller.Inspector);
        Assert.NotEmpty(controller.Diagnostics);
        Assert.Contains(controller.Diagnostics, diagnostic => diagnostic.Code == XamlDiagnosticCode.UnknownProperty);
        Assert.DoesNotContain(controller.Diagnostics, diagnostic => diagnostic.Code == XamlDiagnosticCode.GeneralFailure);
        Assert.Contains(controller.Diagnostics, diagnostic => diagnostic.TargetDescription == "Button.UnknownProperty");
        Assert.Contains(controller.Diagnostics, diagnostic => diagnostic.LocationText.StartsWith("Line ", StringComparison.Ordinal));
        Assert.DoesNotContain(controller.Diagnostics, diagnostic => diagnostic.LocationText.Contains("Col", StringComparison.Ordinal));
    }

    [Fact]
    public void Refresh_MalformedXml_CapturesFallbackDiagnosticLineInformation()
    {
        var controller = new InkkSlinger.Designer.DesignerController();

        var succeeded = controller.Refresh(MalformedViewXml);

        Assert.False(succeeded);
        var diagnostic = Assert.Single(controller.Diagnostics);
        Assert.Equal(XamlDiagnosticCode.GeneralFailure, diagnostic.Code);
        Assert.Equal(5, diagnostic.Line);
        Assert.NotNull(diagnostic.Position);
        Assert.Equal("Line 5", diagnostic.LocationText);
        Assert.DoesNotContain("Position", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Diagnostic:", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectVisualNode_UnknownNode_ClearsInspectorState()
    {
        var controller = new InkkSlinger.Designer.DesignerController();
        Assert.True(controller.Refresh(ValidViewXml));

        var selected = controller.SelectVisualNode("missing-node");

        Assert.False(selected);
        Assert.Null(controller.SelectedNodeId);
        Assert.Equal(InkkSlinger.Designer.DesignerInspectorModel.Empty, controller.Inspector);
    }

    [Fact]
    public void ShellView_RefreshCommand_WiresToolbarButtonAndF5Shortcut()
    {
        InputGestureService.Clear();
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var refreshButton = Assert.IsType<Button>(shell.FindName("RefreshButton"));

        Assert.True(CommandSourceExecution.TryExecute(refreshButton, shell));
        Assert.True(shell.Controller.LastRefreshSucceeded);

        shell.SourceText = ValidViewXml.Replace("Save", "Save Again");
        var executed = InputGestureService.Execute(Keys.F5, ModifierKeys.None, shell, shell);

        Assert.True(executed);
        Assert.True(shell.Controller.LastRefreshSucceeded);
    }

    [Fact]
    public void ShellView_ContainsRequiredPreviewSplitters()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();

        var previewDockSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewDockSplitter"));
        var previewSourceSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewSourceSplitter"));
        var sourcePropertyInspectorSplitter = Assert.IsType<GridSplitter>(shell.SourceEditorView.FindName("SourcePropertyInspectorSplitter"));
        var sourcePropertyInspectorBorder = Assert.IsType<Border>(shell.SourceEditorView.FindName("SourcePropertyInspectorBorder"));
        var previewScrollViewer = Assert.IsType<ScrollViewer>(shell.FindName("PreviewScrollViewer"));
        var editorTabControl = Assert.IsType<TabControl>(shell.FindName("EditorTabControl"));
        var diagnosticsTab = Assert.IsType<TabItem>(shell.FindName("DiagnosticsTab"));
        var sourceLineNumberBorder = shell.SourceLineNumberBorderControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var diagnosticsItemsControl = Assert.IsType<ItemsControl>(shell.FindName("DiagnosticsItemsControl"));

        _ = Assert.IsType<ContentControl>(shell.FindName("PreviewHost"));
        _ = Assert.IsType<ItemsControl>(shell.FindName("VisualTreeView"));
        _ = Assert.IsAssignableFrom<RichTextBox>(shell.SourceEditorControl);

        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.VerticalScrollBarVisibility);
        Assert.Equal(0, editorTabControl.SelectedIndex);
        Assert.Equal("Diagnostics", diagnosticsTab.Header);
        Assert.NotNull(diagnosticsItemsControl);
        Assert.NotNull(sourceLineNumberBorder.Child);
        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);

        Assert.Equal(1, Grid.GetColumn(previewDockSplitter));
        Assert.Equal(GridResizeDirection.Columns, previewDockSplitter.ResizeDirection);
        Assert.Equal(GridResizeBehavior.PreviousAndNext, previewDockSplitter.ResizeBehavior);
        Assert.Equal(HorizontalAlignment.Stretch, previewDockSplitter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Stretch, previewDockSplitter.VerticalAlignment);

        Assert.Equal(3, Grid.GetRow(previewSourceSplitter));
        Assert.Equal(GridResizeDirection.Rows, previewSourceSplitter.ResizeDirection);
        Assert.Equal(GridResizeBehavior.PreviousAndNext, previewSourceSplitter.ResizeBehavior);
        Assert.Equal(HorizontalAlignment.Stretch, previewSourceSplitter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, previewSourceSplitter.VerticalAlignment);

        Assert.Equal(3, Grid.GetColumn(sourcePropertyInspectorSplitter));
        Assert.Equal(GridResizeDirection.Columns, sourcePropertyInspectorSplitter.ResizeDirection);
        Assert.Equal(GridResizeBehavior.PreviousAndNext, sourcePropertyInspectorSplitter.ResizeBehavior);
        Assert.Equal(HorizontalAlignment.Stretch, sourcePropertyInspectorSplitter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Stretch, sourcePropertyInspectorSplitter.VerticalAlignment);
        Assert.Equal(4, Grid.GetColumn(sourcePropertyInspectorBorder));
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_SelectingControlTag_ShowsApplicableProperties()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var buttonIndex = shell.SourceText.IndexOf("<Button", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(buttonIndex + 2, 0);
        RunLayout(uiRoot, 1280, 840, 16);

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        Assert.Contains("Content", propertyEditors.Keys);
        Assert.Contains("Width", propertyEditors.Keys);

        var header = Assert.IsType<TextBlock>(shell.SourceEditorView.FindName("SourcePropertyInspectorHeaderText"));
        Assert.Equal("Button", header.Text);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_HidesTypographyForBorder_ButKeepsItForButton()
    {
        const string borderAndButtonViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 Background="#101820">
              <StackPanel>
            <Border x:Name="ChromeBorder"
                BorderBrush="#334455"
                BorderThickness="1" />
            <Button x:Name="SaveButton"
                Content="Save" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = borderAndButtonViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");
        var borderPropertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);

        Assert.DoesNotContain("FontFamily", borderPropertyEditors.Keys);
        Assert.DoesNotContain("FontSize", borderPropertyEditors.Keys);
        Assert.DoesNotContain("FontWeight", borderPropertyEditors.Keys);
        Assert.DoesNotContain("FontStyle", borderPropertyEditors.Keys);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");
        var buttonPropertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);

        Assert.Contains("FontFamily", buttonPropertyEditors.Keys);
        Assert.Contains("FontSize", buttonPropertyEditors.Keys);
        Assert.Contains("FontWeight", buttonPropertyEditors.Keys);
        Assert.Contains("FontStyle", buttonPropertyEditors.Keys);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_PropertyDescriptions_RemainRetainedAndRedrawAfterSwitchingControlTypes()
    {
        const string multiTypeViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                    BorderBrush="#334455"
                    BorderThickness="1" />
                <Button x:Name="SaveButton"
                    Content="Save" />
                <TextBlock x:Name="StatusText"
                    Text="Ready" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = multiTypeViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        var viewport = new Viewport(0, 0, 1280, 840);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");
        var buttonContentDescription = GetSourceInspectorPropertyDescription(shell.SourceEditorView, "Content");
        Assert.Contains("set in source", buttonContentDescription.Text, StringComparison.Ordinal);
        uiRoot.SynchronizeRetainedRenderListForTests();
        Assert.Contains(buttonContentDescription, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var borderIndex = shell.SourceText.IndexOf("<Border", StringComparison.Ordinal);
        Assert.True(borderIndex >= 0);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(borderIndex + 2, 0);

        var borderSwitchShouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(borderSwitchShouldDraw);
        Assert.True(
            (uiRoot.LastShouldDrawReasons & (UiRedrawReason.LayoutInvalidated | UiRedrawReason.RenderInvalidated)) != 0,
            $"Expected switching the Property Inspector from Button to Border to schedule a redraw for rebuilt subtitle rows, but reasons were {uiRoot.LastShouldDrawReasons}.");

        RunLayout(uiRoot, 1280, 840, 16);
        var borderBrushDescription = GetSourceInspectorPropertyDescription(shell.SourceEditorView, "BorderBrush");
        Assert.Contains("set in source", borderBrushDescription.Text, StringComparison.Ordinal);
        uiRoot.SynchronizeRetainedRenderListForTests();
        Assert.Contains(borderBrushDescription, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var textBlockIndex = shell.SourceText.IndexOf("<TextBlock", StringComparison.Ordinal);
        Assert.True(textBlockIndex >= 0);
        sourceEditor.Select(textBlockIndex + 2, 0);

        var textBlockSwitchShouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(textBlockSwitchShouldDraw);
        Assert.True(
            (uiRoot.LastShouldDrawReasons & (UiRedrawReason.LayoutInvalidated | UiRedrawReason.RenderInvalidated)) != 0,
            $"Expected switching the Property Inspector from Border to TextBlock to schedule a redraw for rebuilt subtitle rows, but reasons were {uiRoot.LastShouldDrawReasons}.");

        RunLayout(uiRoot, 1280, 840, 16);
        var textDescription = GetSourceInspectorPropertyDescription(shell.SourceEditorView, "Text");
        Assert.Contains("set in source", textDescription.Text, StringComparison.Ordinal);
        uiRoot.SynchronizeRetainedRenderListForTests();
        Assert.Contains(textDescription, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var buttonIndex = shell.SourceText.IndexOf("<Button", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        sourceEditor.Select(buttonIndex + 2, 0);

        var buttonSwitchShouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(48), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(buttonSwitchShouldDraw);
        Assert.True(
            (uiRoot.LastShouldDrawReasons & (UiRedrawReason.LayoutInvalidated | UiRedrawReason.RenderInvalidated)) != 0,
            $"Expected switching the Property Inspector back to Button to schedule a redraw for rebuilt subtitle rows, but reasons were {uiRoot.LastShouldDrawReasons}.");

        RunLayout(uiRoot, 1280, 840, 16);
        var buttonContentDescriptionAfterRoundTrip = GetSourceInspectorPropertyDescription(shell.SourceEditorView, "Content");
        Assert.Contains("set in source", buttonContentDescriptionAfterRoundTrip.Text, StringComparison.Ordinal);
        uiRoot.SynchronizeRetainedRenderListForTests();
        Assert.Contains(buttonContentDescriptionAfterRoundTrip, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_SwitchingSourceSelection_ShouldKeepDescriptionVisible()
    {
        const string multiTypeViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                    BorderBrush="#334455"
                    BorderThickness="1" />
                <Button x:Name="SaveButton"
                    Content="Save" />
                <TextBlock x:Name="StatusText"
                    Text="Ready" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = multiTypeViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 1280f, 840f));

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        shell.ClearRenderInvalidationRecursive();

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");
        uiRoot.SynchronizeRetainedRenderListForTests();

        var borderBrushDescription = GetSourceInspectorPropertyDescription(shell.SourceEditorView, "BorderBrush");
        var borderBrushDescriptionBounds = ResolveRenderedBounds(borderBrushDescription);

        Assert.False(string.IsNullOrWhiteSpace(borderBrushDescription.Text));
        Assert.Contains(borderBrushDescription, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
        Assert.True(
            borderBrushDescriptionBounds.Height > 0f,
            $"Expected switching the source selection from Button to Border to keep the rebuilt subtitle visible, but its rendered height was {borderBrushDescriptionBounds.Height:0.###}. descriptionBounds={FormatRect(borderBrushDescriptionBounds)}, descriptionText='{borderBrushDescription.Text}'.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_FilterEdit_RestoresDescriptionVisibilityAfterControlSwitch()
    {
        const string multiTypeViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                    BorderBrush="#334455"
                    BorderThickness="1" />
                <Button x:Name="SaveButton"
                    Content="Save" />
                <TextBlock x:Name="StatusText"
                    Text="Ready" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = multiTypeViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 1280f, 840f));

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        shell.ClearRenderInvalidationRecursive();

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");
        uiRoot.SynchronizeRetainedRenderListForTests();

        var borderBrushDescription = GetSourceInspectorPropertyDescription(shell.SourceEditorView, "BorderBrush");
        var borderBrushDescriptionBounds = ResolveRenderedBounds(borderBrushDescription);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        shell.ClearRenderInvalidationRecursive();

        var filterTextBox = GetSourceInspectorFilterTextBox(shell.SourceEditorView);
        filterTextBox.Text = "Border";
        RunLayout(uiRoot, 1280, 840, 16);

        var borderBrushDescriptionAfterFilter = GetSourceInspectorPropertyDescription(shell.SourceEditorView, "BorderBrush");
        var borderBrushDescriptionBoundsAfterFilter = ResolveRenderedBounds(borderBrushDescriptionAfterFilter);

        Assert.False(string.IsNullOrWhiteSpace(borderBrushDescriptionAfterFilter.Text));
        Assert.True(
            borderBrushDescriptionBoundsAfterFilter.Height > 0f,
            $"Expected editing the Property Inspector filter after a control switch to restore the subtitle visibility, but its rendered height stayed {borderBrushDescriptionBoundsAfterFilter.Height:0.###}. beforeFilterBounds={FormatRect(borderBrushDescriptionBounds)}, afterFilterBounds={FormatRect(borderBrushDescriptionBoundsAfterFilter)}, descriptionText='{borderBrushDescriptionAfterFilter.Text}'.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_ViewportExpandAfterSourceSelection_ShouldKeepPropertyRowVisible()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 960, 640, 16);
        RunLayout(uiRoot, 960, 640, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var contentRowBeforeResize = GetSourceInspectorPropertyRow(shell.SourceEditorView, "Content");
        var contentRowBoundsBeforeResize = ResolveRenderedBounds(contentRowBeforeResize);

        Assert.True(
            contentRowBoundsBeforeResize.Height > 0f,
            $"Expected the Content property row to be visible before viewport expansion, but bounds were {FormatRect(contentRowBoundsBeforeResize)}.");

        RunLayout(uiRoot, 1920, 1080, 16);
        RunLayout(uiRoot, 1920, 1080, 16);

        var contentRowAfterResize = GetSourceInspectorPropertyRow(shell.SourceEditorView, "Content");
        var contentRowBoundsAfterResize = ResolveRenderedBounds(contentRowAfterResize);

        Assert.True(
            contentRowBoundsAfterResize.Height > 0f,
            $"Expected expanding the viewport after selecting a source tag to keep the Content property row visible, but bounds were {FormatRect(contentRowBoundsAfterResize)}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_FilterEdit_RestoresPropertyRowVisibilityAfterViewportExpand()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 960, 640, 16);
        RunLayout(uiRoot, 960, 640, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");
        RunLayout(uiRoot, 1920, 1080, 16);
        RunLayout(uiRoot, 1920, 1080, 16);

        var contentRowBeforeFilter = GetSourceInspectorPropertyRow(shell.SourceEditorView, "Content");
        var contentRowBoundsBeforeFilter = ResolveRenderedBounds(contentRowBeforeFilter);

        var filterTextBox = GetSourceInspectorFilterTextBox(shell.SourceEditorView);
        filterTextBox.Text = "con";
        RunLayout(uiRoot, 1920, 1080, 16);

        var contentRowAfterFilter = GetSourceInspectorPropertyRow(shell.SourceEditorView, "Content");
        var contentRowBoundsAfterFilter = ResolveRenderedBounds(contentRowAfterFilter);

        Assert.True(
            contentRowBoundsAfterFilter.Height > 0f,
            $"Expected editing the Property Inspector filter after viewport expansion to restore the Content property row visibility, but beforeFilterBounds={FormatRect(contentRowBoundsBeforeFilter)} and afterFilterBounds={FormatRect(contentRowBoundsAfterFilter)}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_EditingExistingProperty_UpdatesSourceWithoutRefreshingPreview()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var buttonIndex = shell.SourceText.IndexOf("<Button", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(buttonIndex + 2, 0);
        RunLayout(uiRoot, 1280, 840, 16);

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var contentEditor = Assert.IsType<TextBox>(propertyEditors["Content"]);

        contentEditor.Text = "Save Later";
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("Content=\"Save Later\"", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Save Later\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_AddingMissingProperty_InsertsAttributeWithoutRefreshingPreview()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var buttonIndex = shell.SourceText.IndexOf("<Button", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(buttonIndex + 2, 0);
        RunLayout(uiRoot, 1280, 840, 16);

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var widthEditor = Assert.IsType<TextBox>(propertyEditors["Width"]);
        Assert.True(string.IsNullOrEmpty(widthEditor.Text));

        widthEditor.Text = "180";
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("Width=\"180\"", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"180\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_EditingProperty_F5RefreshesPreviewOnDemand()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var buttonIndex = shell.SourceText.IndexOf("<Button", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(buttonIndex + 2, 0);
        RunLayout(uiRoot, 1280, 840, 16);

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var contentEditor = Assert.IsType<TextBox>(propertyEditors["Content"]);

        contentEditor.Text = "Save Later";
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("Content=\"Save Later\"", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Save Later\"", shell.Controller.SourceText, StringComparison.Ordinal);

        var executed = InputGestureService.Execute(Keys.F5, ModifierKeys.None, shell, shell);

        Assert.True(executed);
        Assert.True(shell.Controller.LastRefreshSucceeded);
        Assert.Contains("Content=\"Save Later\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_EditingProperty_PreservesInspectorScrollOffset()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var buttonIndex = shell.SourceText.IndexOf("<Button", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(buttonIndex + 2, 0);
        RunLayout(uiRoot, 1280, 840, 16);

        var scrollViewer = GetSourceInspectorScrollViewer(shell.SourceEditorView);
        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var widthEditor = Assert.IsType<TextBox>(propertyEditors["Width"]);

        scrollViewer.ScrollToVerticalOffset(180f);
        RunLayout(uiRoot, 1280, 840, 16);
        var verticalOffsetBefore = scrollViewer.VerticalOffset;
        Assert.True(verticalOffsetBefore > 0f, $"Expected the source property inspector to scroll before editing, but offset stayed {verticalOffsetBefore:0.###}.");

        widthEditor.Text = "180";
        RunLayout(uiRoot, 1280, 840, 16);

        var verticalOffsetAfter = scrollViewer.VerticalOffset;
        Assert.Equal(verticalOffsetBefore, verticalOffsetAfter, 3);
        Assert.Contains("Width=\"180\"", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_FilterText_FiltersVisiblePropertiesCaseInsensitively()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var filterTextBox = GetSourceInspectorFilterTextBox(shell.SourceEditorView);
        filterTextBox.Text = "wid";
        RunLayout(uiRoot, 1280, 840, 16);

        var visibleProperties = GetVisibleSourceInspectorPropertyNames(shell.SourceEditorView);
        Assert.Contains("Width", visibleProperties);
        Assert.DoesNotContain("Content", visibleProperties);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_FilterText_NoMatches_ShowsFilterEmptyState()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var filterTextBox = GetSourceInspectorFilterTextBox(shell.SourceEditorView);
        var emptyState = Assert.IsType<TextBlock>(shell.SourceEditorView.FindName("SourcePropertyInspectorEmptyState"));
        var scrollViewer = GetSourceInspectorScrollViewer(shell.SourceEditorView);

        filterTextBox.Text = "zzzz-no-match";
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal(Visibility.Visible, emptyState.Visibility);
        Assert.Contains("No properties match", emptyState.Text, StringComparison.Ordinal);
        Assert.Equal(Visibility.Collapsed, scrollViewer.Visibility);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_FontWeight_UsesChoiceEditor()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);

        Assert.Contains("Normal", fontWeightEditor.Items.Cast<object>());
        Assert.Contains("Bold", fontWeightEditor.Items.Cast<object>());
        Assert.Equal(-1, fontWeightEditor.SelectedIndex);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_FontWeightChoice_UpdatesSourceWithoutRefreshingPreview()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);

        fontWeightEditor.SelectedItem = "Bold";
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("FontWeight=\"Bold\"", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("FontWeight=\"Bold\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_Cursor_UsesChoiceEditor()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var cursorEditor = Assert.IsType<ComboBox>(propertyEditors["Cursor"]);

        Assert.Contains("Arrow", cursorEditor.Items.Cast<object>());
        Assert.Contains("Hand", cursorEditor.Items.Cast<object>());
        Assert.Contains("IBeam", cursorEditor.Items.Cast<object>());
        Assert.Equal(-1, cursorEditor.SelectedIndex);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_CursorChoice_UpdatesSourceWithoutRefreshingPreview()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var cursorEditor = Assert.IsType<ComboBox>(propertyEditors["Cursor"]);

        cursorEditor.SelectedItem = "Hand";
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("Cursor=\"Hand\"", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("Cursor=\"Hand\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_FontWeightComboBoxDropdown_UsesDesignerPalette()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);

        Assert.Equal(new Color(8, 15, 24), fontWeightEditor.Background);
        Assert.Equal(new Color(216, 227, 238), fontWeightEditor.Foreground);
        Assert.Equal(new Color(36, 51, 66), fontWeightEditor.BorderBrush);
        Assert.Equal(new FontFamily("Consolas"), fontWeightEditor.FontFamily);

        fontWeightEditor.IsDropDownOpen = true;
        RunLayout(uiRoot, 1280, 840, 16);

        var dropDownPopupField = typeof(ComboBox).GetField("_dropDownPopup", BindingFlags.Instance | BindingFlags.NonPublic);
        var dropDownListField = typeof(ComboBox).GetField("_dropDownList", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(dropDownPopupField);
        Assert.NotNull(dropDownListField);

        var dropDownPopup = Assert.IsType<Popup>(dropDownPopupField!.GetValue(fontWeightEditor));
        var dropDownList = Assert.IsType<ListBox>(dropDownListField!.GetValue(fontWeightEditor));

        Assert.True(dropDownPopup.IsOpen);
        Assert.Same(fontWeightEditor, dropDownPopup.PlacementTarget);
        Assert.Equal(new Color(9, 13, 19), dropDownPopup.Background);
        Assert.Equal(new Color(35, 52, 73), dropDownPopup.BorderBrush);
        Assert.Equal(new Color(9, 13, 19), dropDownList.Background);
        Assert.Equal(new Color(216, 227, 238), dropDownList.Foreground);
        Assert.Equal(new Color(35, 52, 73), dropDownList.BorderBrush);
        Assert.Equal(new FontFamily("Consolas"), dropDownList.FontFamily);
        Assert.Equal(12f, dropDownList.FontSize);
        Assert.NotNull(dropDownList.ItemContainerStyle);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_CursorComboBoxDropdown_UsesMoreHeightBecauseItsChoiceListHitsTheViewportCap()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var cursorEditor = Assert.IsType<ComboBox>(propertyEditors["Cursor"]);
        var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);

        cursorEditor.IsDropDownOpen = true;
        RunLayout(uiRoot, 1280, 840, 16);

        var cursorDropDown = GetComboBoxDropDownList(cursorEditor);
        var cursorScrollViewer = FindListBoxScrollViewer(cursorDropDown);
        var cursorAverageRowHeight = GetAverageVisibleListItemHeight(cursorDropDown);

        cursorEditor.IsDropDownOpen = false;
        RunLayout(uiRoot, 1280, 840, 16);

        fontWeightEditor.IsDropDownOpen = true;
        RunLayout(uiRoot, 1280, 840, 16);

        var fontWeightDropDown = GetComboBoxDropDownList(fontWeightEditor);
        var fontWeightScrollViewer = FindListBoxScrollViewer(fontWeightDropDown);
        var fontWeightAverageRowHeight = GetAverageVisibleListItemHeight(fontWeightDropDown);

        Assert.True(cursorEditor.Items.Count > fontWeightEditor.Items.Count);
        Assert.True(cursorDropDown.LayoutSlot.Height + 0.5f >= fontWeightDropDown.LayoutSlot.Height,
            $"Expected Cursor dropdown to be at least as tall as FontWeight once both choice lists apply the viewport cap. cursorHeight={cursorDropDown.LayoutSlot.Height:0.##} fontWeightHeight={fontWeightDropDown.LayoutSlot.Height:0.##} cursorItems={cursorEditor.Items.Count} fontWeightItems={fontWeightEditor.Items.Count}");
        Assert.True(cursorScrollViewer.ExtentHeight > cursorScrollViewer.ViewportHeight + 40f,
            $"Expected Cursor dropdown content to exceed the viewport cap. extent={cursorScrollViewer.ExtentHeight:0.##} viewport={cursorScrollViewer.ViewportHeight:0.##} items={cursorEditor.Items.Count}");
        Assert.InRange(fontWeightScrollViewer.ViewportHeight - fontWeightScrollViewer.ExtentHeight, -0.5f, 6f);
        Assert.InRange(MathF.Abs(cursorAverageRowHeight - fontWeightAverageRowHeight), 0f, 2f);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_CursorComboBoxDropdown_ScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var cursorEditor = Assert.IsType<ComboBox>(propertyEditors["Cursor"]);

        cursorEditor.IsDropDownOpen = true;
        RunLayout(uiRoot, 1280, 840, 16);

        var cursorDropDown = GetComboBoxDropDownList(cursorEditor);
        var scrollViewer = FindListBoxScrollViewer(cursorDropDown);
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot, 1280, 840, 16);

        var lastItem = GetLastVisibleListBoxItem(cursorDropDown);
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        var probe = new Vector2(
            scrollViewer.LayoutSlot.X + 24f,
            (scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight) - 2f);
        var hit = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.HitTest(shell, probe));

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.True(
            IsVisualAncestorOrSelf(lastItem, hit),
            $"Expected the viewport-bottom hit to land on the last Cursor dropdown item after scrolling to the end. hit={DescribeElement(hit)}, lastItem={DescribeElement(lastItem)}, probe={probe}, Offset={scrollViewer.VerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspectorHoveringFontWeightComboBox_ResolvesHoveredElementToComboBox()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);
    EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, fontWeightEditor, "FontWeight");
        var pointer = GetCenter(fontWeightEditor.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var hovered = uiRoot.GetHoveredElementForDiagnostics();

        Assert.Same(
            fontWeightEditor,
            hovered);
        Assert.True(
            hovered is ComboBox,
            $"Expected hovering FontWeight combo box to resolve the combo box itself, but hovered={DescribeElement(hovered)}, comboSlot={fontWeightEditor.LayoutSlot}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspectorMouseDownOnFontWeightComboBox_ResolvesClickDownTargetToComboBox()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);
    EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, fontWeightEditor, "FontWeight");
        var pointer = GetCenter(fontWeightEditor.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        var clickDownTarget = uiRoot.GetLastClickDownTargetForDiagnostics();

        Assert.Same(fontWeightEditor, clickDownTarget);
        Assert.True(
            clickDownTarget is ComboBox,
            $"Expected mouse down on FontWeight combo box to resolve click-down target to the combo box, but clickDown={DescribeElement(clickDownTarget)}, comboSlot={fontWeightEditor.LayoutSlot}, pointer={pointer}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_Background_UsesColorDropdownEditorWithColorControls()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var backgroundEditor = Assert.IsAssignableFrom<ComboBox>(propertyEditors["Background"]);

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, backgroundEditor, "Background");
        var pointer = GetCenter(backgroundEditor.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        var backgroundPopup = GetSourceInspectorColorEditorPopup(backgroundEditor);
        var backgroundPopupItemsHost = GetSourceInspectorColorEditorPopupItemsHost(backgroundEditor);

        Assert.True(backgroundPopup.IsOpen);
        Assert.Same(backgroundEditor, backgroundPopup.PlacementTarget);
        Assert.Equal(PopupPlacementMode.Bottom, backgroundPopup.PlacementMode);
        Assert.Equal(3, backgroundPopupItemsHost.Children.Count);
        Assert.All(backgroundPopupItemsHost.Children, child => Assert.IsType<ComboBoxItem>(child));
        Assert.IsType<ColorPicker>(GetSourceInspectorColorEditorColorPicker(backgroundEditor));
        Assert.IsType<ColorSpectrum>(GetSourceInspectorColorEditorHueSpectrum(backgroundEditor));
        Assert.IsType<ColorSpectrum>(GetSourceInspectorColorEditorAlphaSpectrum(backgroundEditor));
        Assert.Equal(Orientation.Horizontal, GetSourceInspectorColorEditorHueSpectrum(backgroundEditor).Orientation);
        Assert.Equal(ColorSpectrumMode.Hue, GetSourceInspectorColorEditorHueSpectrum(backgroundEditor).Mode);
        Assert.Equal(Orientation.Horizontal, GetSourceInspectorColorEditorAlphaSpectrum(backgroundEditor).Orientation);
        Assert.Equal(ColorSpectrumMode.Alpha, GetSourceInspectorColorEditorAlphaSpectrum(backgroundEditor).Mode);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BackgroundColorEditor_UpdatesArgbSourceWithoutRefreshingPreview()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var backgroundEditor = Assert.IsAssignableFrom<ComboBox>(propertyEditors["Background"]);
        var colorPicker = GetSourceInspectorColorEditorColorPicker(backgroundEditor);
        var alphaSpectrum = GetSourceInspectorColorEditorAlphaSpectrum(backgroundEditor);

        colorPicker.SelectedColor = new Color(0x33, 0x66, 0x99);
        RunLayout(uiRoot, 1280, 840, 16);

        alphaSpectrum.Alpha = 0.5f;
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("Background=\"#", shell.SourceText, StringComparison.Ordinal);
        Assert.Contains("336699\"", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("336699\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_WhenHoveringColorPicker_ShouldResolveColorPicker()
    {
        const string borderOnlyViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                        Background="#223344"
                        BorderBrush="#334455"
                        BorderThickness="1" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = borderOnlyViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var backgroundEditor = Assert.IsAssignableFrom<ComboBox>(propertyEditors["Background"]);

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, backgroundEditor, "Background");
        OpenSourceInspectorColorEditor(uiRoot, backgroundEditor);
        RunLayout(uiRoot, 1280, 840, 16);

        var pointer = FindRenderedPoint(
            shell,
            shell.LayoutSlot,
            element => FindSelfOrAncestor<ColorPicker>(element) != null);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var hovered = uiRoot.GetHoveredElementForDiagnostics();

        Assert.NotNull(FindSelfOrAncestor<ColorPicker>(hovered));
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_ManualHitTest_ResolvesColorPickerSubtree()
    {
        const string borderOnlyViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                        Background="#223344"
                        BorderBrush="#334455"
                        BorderThickness="1" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = borderOnlyViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var backgroundEditor = Assert.IsAssignableFrom<ComboBox>(propertyEditors["Background"]);

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, backgroundEditor, "Background");
        OpenSourceInspectorColorEditor(uiRoot, backgroundEditor);
        RunLayout(uiRoot, 1280, 840, 16);

        var pointer = FindRenderedPoint(
            shell,
            shell.LayoutSlot,
            element => FindSelfOrAncestor<ColorPicker>(element) != null);

        var hit = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.HitTest(shell, pointer));

        var picker = Assert.IsType<ColorPicker>(FindSelfOrAncestor<ColorPicker>(hit));
        Assert.True(hit == picker || FindSelfOrAncestor<ColorSpectrum>(hit) != null);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_WhenClickingColorPicker_ShouldUpdateBackgroundSource()
    {
        const string borderOnlyViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                        Background="#223344"
                        BorderBrush="#334455"
                        BorderThickness="1" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = borderOnlyViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var backgroundEditor = Assert.IsAssignableFrom<ComboBox>(propertyEditors["Background"]);

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, backgroundEditor, "Background");
        OpenSourceInspectorColorEditor(uiRoot, backgroundEditor);
        RunLayout(uiRoot, 1280, 840, 16);

        var colorPicker = GetSourceInspectorColorEditorColorPicker(backgroundEditor);
        var beforeSnapshot = colorPicker.GetColorPickerSnapshotForDiagnostics();
        var beforeColor = colorPicker.SelectedColor;
        var clickPoint = new Vector2(
            beforeSnapshot.SpectrumRect.X + (beforeSnapshot.SpectrumRect.Width * 0.8f),
            beforeSnapshot.SpectrumRect.Y + (beforeSnapshot.SpectrumRect.Height * 0.2f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftReleased: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.NotEqual(
            beforeColor,
            colorPicker.SelectedColor);
        Assert.Contains(
            "Background=\"#",
            shell.SourceText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Background=\"#223344\"",
            shell.SourceText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_ColorPickerPointerPress_ShouldCaptureAndStartDragging()
    {
        const string borderOnlyViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                        Background="#223344"
                        BorderBrush="#334455"
                        BorderThickness="1" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = borderOnlyViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var backgroundEditor = Assert.IsAssignableFrom<ComboBox>(propertyEditors["Background"]);

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, backgroundEditor, "Background");
        OpenSourceInspectorColorEditor(uiRoot, backgroundEditor);
        RunLayout(uiRoot, 1280, 840, 16);

        var colorPicker = GetSourceInspectorColorEditorColorPicker(backgroundEditor);
        var snapshot = colorPicker.GetColorPickerSnapshotForDiagnostics();
        var pressPoint = new Vector2(
            snapshot.SpectrumRect.X + (snapshot.SpectrumRect.Width * 0.8f),
            snapshot.SpectrumRect.Y + (snapshot.SpectrumRect.Height * 0.2f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pressPoint, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pressPoint, leftPressed: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        var manualHit = VisualTreeHelper.HitTest(shell, pressPoint);
        var clickDownTarget = uiRoot.GetLastClickDownTargetForDiagnostics();
        var hovered = uiRoot.GetHoveredElementForDiagnostics();
        var captured = FocusManager.GetCapturedPointerElement();
        var afterSnapshot = colorPicker.GetColorPickerSnapshotForDiagnostics();

        Assert.NotNull(FindSelfOrAncestor<ColorPicker>(manualHit));
        Assert.NotNull(FindSelfOrAncestor<ColorPicker>(hovered));
        Assert.NotNull(FindSelfOrAncestor<ColorPicker>(clickDownTarget));
        Assert.Same(colorPicker, captured);
        Assert.True(
            afterSnapshot.IsDragging,
            $"Expected pressing the embedded ColorPicker to start dragging, but manualHit={DescribeElement(manualHit)}, clickDown={DescribeElement(clickDownTarget)}, hovered={DescribeElement(hovered)}, captured={DescribeElement(captured)}, spectrumRect={FormatRect(afterSnapshot.SpectrumRect)}, pointer={pressPoint}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspectorPropertyEditors_AreArrangedWithinInspectorViewport()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var contentEditor = Assert.IsType<TextBox>(propertyEditors["Content"]);
        var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, contentEditor, "Content");
        var viewport = GetSourceInspectorScrollViewer(shell.SourceEditorView).LayoutSlot;

        Assert.True(
            IsWithinViewport(contentEditor.LayoutSlot, viewport),
            $"Expected Content editor to be arranged within the Property Inspector viewport, but contentSlot={FormatRect(contentEditor.LayoutSlot)}, viewport={FormatRect(viewport)}.");

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, fontWeightEditor, "FontWeight");
        viewport = GetSourceInspectorScrollViewer(shell.SourceEditorView).LayoutSlot;

        Assert.True(
            IsWithinViewport(fontWeightEditor.LayoutSlot, viewport),
            $"Expected FontWeight combo box to be arranged within the Property Inspector viewport, but comboSlot={FormatRect(fontWeightEditor.LayoutSlot)}, viewport={FormatRect(viewport)}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspectorSplitter_WhenDragged_ShouldCapturePointerAndResizeAdjacentColumns()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var splitter = Assert.IsType<GridSplitter>(shell.SourceEditorView.FindName("SourcePropertyInspectorSplitter"));
        var inspectorBorder = Assert.IsType<Border>(shell.SourceEditorView.FindName("SourcePropertyInspectorBorder"));
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);
        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var start = GetCenter(splitter.LayoutSlot);
        var end = new Vector2(start.X - 72f, start.Y);
        var initialEditorWidth = sourceEditor.LayoutSlot.Width;
        var initialInspectorWidth = inspectorBorder.LayoutSlot.Width;

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var hoverBeforePress = uiRoot.GetHoveredElementForDiagnostics();
        var manualHitBeforePress = VisualTreeHelper.HitTest(shell, start);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var hoverAfterPress = uiRoot.GetHoveredElementForDiagnostics();
        var capturedAfterPress = FocusManager.GetCapturedPointerElement();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(
            splitter.IsDragging,
            $"Expected dragging to start when pressing SourcePropertyInspectorSplitter, but IsDragging was false. manualHitBeforePress={DescribeElement(manualHitBeforePress)}, hoverBeforePress={DescribeElement(hoverBeforePress)}, hoverAfterPress={DescribeElement(hoverAfterPress)}, capturedAfterPress={DescribeElement(capturedAfterPress)}.");
        Assert.Same(splitter, FocusManager.GetCapturedPointerElement());
        Assert.True(
            MathF.Abs(sourceEditor.LayoutSlot.Width - initialEditorWidth) > 0.01f ||
            MathF.Abs(inspectorBorder.LayoutSlot.Width - initialInspectorWidth) > 0.01f,
            $"Expected adjacent widths to change after dragging SourcePropertyInspectorSplitter, but sourceEditorWidth stayed {sourceEditor.LayoutSlot.Width:0.###} from {initialEditorWidth:0.###} and inspectorWidth stayed {inspectorBorder.LayoutSlot.Width:0.###} from {initialInspectorWidth:0.###}.");
    }

    [Fact]
    public void ShellView_SourcePropertyInspectorSplitter_PointerPress_HitTestsToSplitterButDoesNotReachGridSplitterHandler()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var splitter = Assert.IsType<GridSplitter>(shell.SourceEditorView.FindName("SourcePropertyInspectorSplitter"));
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);
        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var start = GetCenter(splitter.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var manualHitBeforePress = VisualTreeHelper.HitTest(shell, start);
        var hoverBeforePress = uiRoot.GetHoveredElementForDiagnostics();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var hoverAfterPress = uiRoot.GetHoveredElementForDiagnostics();
        var runtime = splitter.GetGridSplitterSnapshotForDiagnostics();

        Assert.Same(splitter, manualHitBeforePress);
        Assert.Same(splitter, hoverBeforePress);
        Assert.Same(splitter, hoverAfterPress);
        Assert.True(splitter.IsDragging);
        Assert.Same(splitter, FocusManager.GetCapturedPointerElement());
        Assert.Equal(1L, runtime.PointerDownCallCount);
        Assert.Equal(0L, runtime.PointerDownHitTestRejectCount);
        Assert.Equal(0L, runtime.PointerDownDisabledRejectCount);
        Assert.Equal(1L, runtime.PointerDownBeginDragSuccessCount);
        Assert.Equal(0L, runtime.PointerDownBeginDragFailureCount);
        Assert.Equal(1L, runtime.BeginDragCallCount);
        Assert.Equal(1L, runtime.BeginDragSuccessCount);
        Assert.Equal(0L, runtime.BeginDragRejectedMissingGridCount);
        Assert.Equal(0L, runtime.BeginDragRejectedTargetResolutionCount);
    }

    [Fact]
    public void ShellView_SourceEditorLineNumberGutter_RemainsRenderedAfterVerticalScroll()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildNumberedSource(80)
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        var viewport = new Viewport(0, 0, 1280, 840);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);
        Assert.Equal("1", GetLineNumberText(sourceLineNumberPanel, 0));

        sourceEditor.SetFocusedFromInput(true);
        var lineHeight = UiTextRenderer.GetLineHeight(sourceEditor, sourceEditor.FontSize);
        var scrollMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var desiredVerticalOffset = Math.Max(lineHeight * 3f, (18f * lineHeight) - (scrollMetrics.ViewportHeight * 0.5f));
        sourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        Assert.True(sourceEditor.VerticalOffset > 0f);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);
        Assert.True(int.TryParse(GetLineNumberText(sourceLineNumberPanel, 0), out var firstRenderedLineNumber));
        Assert.Equal(sourceLineNumberPanel.FirstVisibleLine + 1, firstRenderedLineNumber);
    }

    [Fact]
    public void ShellView_SourceEditorLineNumberGutter_PartialVerticalScroll_InvalidatesPresenterVisual()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildNumberedSource(80)
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        var viewport = new Viewport(0, 0, 1280, 840);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal(0, sourceLineNumberPanel.FirstVisibleLine);
        Assert.Equal("1", GetLineNumberText(sourceLineNumberPanel, 0));

        var beforeDiagnostics = sourceLineNumberPanel.GetFrameworkElementSnapshotForDiagnostics();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        sourceEditor.SetFocusedFromInput(true);
        var lineHeight = UiTextRenderer.GetLineHeight(sourceEditor, sourceEditor.FontSize);
        var desiredVerticalOffset = MathF.Min(
            MathF.Max(1f, lineHeight * 0.1f),
            MathF.Max(1f, sourceEditor.ScrollableHeight));

        sourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True(sourceEditor.VerticalOffset > 0f);
        Assert.Equal(0, sourceLineNumberPanel.FirstVisibleLine);
        Assert.Equal("1", GetLineNumberText(sourceLineNumberPanel, 0));
        Assert.True(sourceLineNumberPanel.VerticalLineOffset > 0f);

        var afterDiagnostics = sourceLineNumberPanel.GetFrameworkElementSnapshotForDiagnostics();
        Assert.True(
            afterDiagnostics.InvalidateArrangeCallCount > beforeDiagnostics.InvalidateArrangeCallCount,
            $"Expected partial vertical scroll to invalidate gutter arrange, but counts were before={beforeDiagnostics.InvalidateArrangeCallCount}, after={afterDiagnostics.InvalidateArrangeCallCount}.");
        Assert.True(
            afterDiagnostics.InvalidateVisualCallCount > beforeDiagnostics.InvalidateVisualCallCount,
            $"Expected partial vertical scroll to invalidate gutter visual so the retained render tree redraws shifted line numbers, but counts were before={beforeDiagnostics.InvalidateVisualCallCount}, after={afterDiagnostics.InvalidateVisualCallCount}.");
    }

    [Fact]
    public void ShellView_SourceEditorLineNumberGutter_WheelScrollToEnd_FollowsEditorViewport()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildNumberedSource(160)
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var pointer = GetSourceEditorLinePoint(sourceEditor, 1);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var iterations = 0;
        var previousOffset = -1f;
        while (iterations < 120)
        {
            iterations++;
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);

            if (MathF.Abs(sourceEditor.VerticalOffset - previousOffset) <= 0.01f &&
                MathF.Abs(sourceEditor.ScrollableHeight - sourceEditor.VerticalOffset) <= 0.5f)
            {
                break;
            }

            previousOffset = sourceEditor.VerticalOffset;
        }

        Assert.True(sourceEditor.VerticalOffset > 0f, "Expected wheel scrolling to move the source editor viewport.");
        Assert.True(
            MathF.Abs(sourceEditor.ScrollableHeight - sourceEditor.VerticalOffset) <= 0.5f,
            $"Expected to reach the end of the source editor after repeated wheel scrolling, but offset={sourceEditor.VerticalOffset:0.###} scrollable={sourceEditor.ScrollableHeight:0.###} iterations={iterations}.");
        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);
        Assert.True(int.TryParse(GetLineNumberText(sourceLineNumberPanel, 0), out var firstRenderedLineNumber));
        Assert.Equal(sourceLineNumberPanel.FirstVisibleLine + 1, firstRenderedLineNumber);
        Assert.True(
            firstRenderedLineNumber > 10,
            $"Expected the line-number gutter to follow the wheel-scrolled viewport near the end of the document, but the first rendered line stayed at {firstRenderedLineNumber}. offset={sourceEditor.VerticalOffset:0.###} scrollable={sourceEditor.ScrollableHeight:0.###}.");
    }

    [Fact]
    public void ShellView_SourceEditorLineNumberGutter_StartupUpdate_PopulatesViewportLineRange()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildNumberedSource(160)
        };

        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        var viewport = new Viewport(0, 0, 1280, 840);

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)), viewport);

        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 2,
            $"Expected startup layout to populate more than the first two gutter rows, but VisibleLineCount was {GetRenderedLineNumberCount(sourceLineNumberPanel)} and first text was '{GetLineNumberText(sourceLineNumberPanel, 0)}'.");
        Assert.Equal("1", GetLineNumberText(sourceLineNumberPanel, 0));
    }

    [Fact]
    public void ShellView_RefreshError_SelectsDiagnosticsTabAndShowsErrorCountInHeader()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = InvalidViewXml
        };

        Assert.True(shell.RefreshPreview() == false);

        var editorTabControl = Assert.IsType<TabControl>(shell.FindName("EditorTabControl"));
        var diagnosticsTab = Assert.IsType<TabItem>(shell.FindName("DiagnosticsTab"));
        var diagnosticsSummary = Assert.IsType<TextBlock>(shell.FindName("DiagnosticsSummaryText"));

        Assert.Equal(1, editorTabControl.SelectedIndex);
        Assert.Contains("(!", diagnosticsTab.Header, StringComparison.Ordinal);
        Assert.Contains("error", diagnosticsSummary.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellView_F5RefreshError_AutoSwitchToDiagnostics_ShouldNotLeaveHoverOnSourceEditor()
    {
        InputGestureService.Clear();
        var source = NormalizeLineEndings(LateInvalidViewXml);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var editorTabControl = Assert.IsType<TabControl>(shell.FindName("EditorTabControl"));
        var diagnosticsTab = Assert.IsType<TabItem>(shell.FindName("DiagnosticsTab"));
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var sourceHoverPoint = GetSourceEditorLinePoint(sourceEditor, 2);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(sourceHoverPoint, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Same(sourceEditor, uiRoot.GetHoveredElementForDiagnostics());

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F5, sourceHoverPoint));
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.False(shell.Controller.LastRefreshSucceeded);
        Assert.Equal(1, editorTabControl.SelectedIndex);
        Assert.Contains("(!", diagnosticsTab.Header, StringComparison.Ordinal);
        Assert.NotSame(sourceEditor, uiRoot.GetHoveredElementForDiagnostics());
    }

    [Fact]
    public void ShellView_DiagnosticCard_Click_SelectsReportedSourceLine()
    {
        var source = NormalizeLineEndings(LateInvalidViewXml);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        Assert.False(shell.RefreshPreview());

        var editorTabControl = Assert.IsType<TabControl>(shell.FindName("EditorTabControl"));
        var diagnosticsItemsControl = Assert.IsType<ItemsControl>(shell.FindName("DiagnosticsItemsControl"));
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);
        var diagnosticsCardButtons = FindDiagnosticsCardButtons(diagnosticsItemsControl);

        var diagnosticIndex = -1;
        for (var i = 0; i < shell.Controller.Diagnostics.Count; i++)
        {
            if (shell.Controller.Diagnostics[i].Code == XamlDiagnosticCode.UnknownProperty)
            {
                diagnosticIndex = i;
                break;
            }
        }

        Assert.True(diagnosticIndex >= 0);
    var diagnosticButton = diagnosticsCardButtons[diagnosticIndex];
        Assert.Equal(1, editorTabControl.SelectedIndex);

    Click(uiRoot, GetCenter(diagnosticButton.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        var highlightedLineNumber = GetLineNumberContaining(source, "UnknownProperty=\"Boom\"");
        var expectedStart = GetLineStartOffset(source, highlightedLineNumber);
        var expectedLineText = GetLineText(source, highlightedLineNumber);
        var selectedText = source.Substring(sourceEditor.SelectionStart, sourceEditor.SelectionLength);

        Assert.Equal(0, editorTabControl.SelectedIndex);
        Assert.Equal(expectedStart, sourceEditor.SelectionStart);
        Assert.Equal(expectedLineText, selectedText);
        Assert.Contains("UnknownProperty=\"Boom\"", selectedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_ClickingSameDiagnosticAgain_AfterReturningToDiagnostics_ShouldNavigateBackToSource()
    {
        var source = NormalizeLineEndings(LateInvalidViewXml);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        Assert.False(shell.RefreshPreview());

        var editorTabControl = Assert.IsType<TabControl>(shell.FindName("EditorTabControl"));
        var diagnosticsItemsControl = Assert.IsType<ItemsControl>(shell.FindName("DiagnosticsItemsControl"));
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var diagnosticsCardButtons = FindDiagnosticsCardButtons(diagnosticsItemsControl);
        var diagnosticIndex = -1;
        for (var i = 0; i < shell.Controller.Diagnostics.Count; i++)
        {
            if (shell.Controller.Diagnostics[i].Code == XamlDiagnosticCode.UnknownProperty)
            {
                diagnosticIndex = i;
                break;
            }
        }

        Assert.True(diagnosticIndex >= 0);
        var diagnosticButton = diagnosticsCardButtons[diagnosticIndex];

        Click(uiRoot, GetCenter(diagnosticButton.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal(0, editorTabControl.SelectedIndex);
        var firstSelectionStart = sourceEditor.SelectionStart;
        var firstSelectionLength = sourceEditor.SelectionLength;

        Click(uiRoot, GetDiagnosticsTabHeaderPoint(editorTabControl));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.Equal(1, editorTabControl.SelectedIndex);

        sourceEditor.Select(0, 0);

        Click(uiRoot, GetCenter(diagnosticButton.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal(0, editorTabControl.SelectedIndex);
        Assert.Equal(firstSelectionStart, sourceEditor.SelectionStart);
        Assert.Equal(firstSelectionLength, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_DiagnosticCard_Hover_SetsButtonMouseOver()
    {
        var source = NormalizeLineEndings(LateInvalidViewXml);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        Assert.False(shell.RefreshPreview());

        var diagnosticsItemsControl = Assert.IsType<ItemsControl>(shell.FindName("DiagnosticsItemsControl"));
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);
        var diagnosticsCardButtons = FindDiagnosticsCardButtons(diagnosticsItemsControl);

        var diagnosticIndex = -1;
        for (var i = 0; i < shell.Controller.Diagnostics.Count; i++)
        {
            if (shell.Controller.Diagnostics[i].Code == XamlDiagnosticCode.UnknownProperty)
            {
                diagnosticIndex = i;
                break;
            }
        }

        Assert.True(diagnosticIndex >= 0);
    var diagnosticButton = diagnosticsCardButtons[diagnosticIndex];
    Assert.False(diagnosticButton.IsMouseOver);
    Assert.Equal(Color.Transparent, diagnosticButton.Background);
    Assert.Equal(Color.Transparent, diagnosticButton.BorderBrush);

    uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(diagnosticButton.LayoutSlot), pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

    Assert.True(diagnosticButton.IsMouseOver);

    var hoverChrome = FindDescendant<Border>(diagnosticButton, border => border.Name == "HoverChrome");
    Assert.Equal(new Color(19, 33, 49), Assert.IsType<SolidColorBrush>(hoverChrome.Background).Color);
    }

    [Fact]
    public void ShellView_SourceEditorTyping_PreservesVerticalScrollOffsetDuringRehighlight()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildLongSourceXml()
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        var targetOffset = shell.SourceText.IndexOf("Line 18", StringComparison.Ordinal);
        Assert.True(targetOffset >= 0);

        sourceEditor.Select(targetOffset, 0);

        var lineHeight = UiTextRenderer.GetLineHeight(sourceEditor, sourceEditor.FontSize);
        var metricsBeforeScroll = sourceEditor.GetScrollMetricsSnapshot();
        var desiredVerticalOffset = Math.Max(0f, (18f * lineHeight) - (metricsBeforeScroll.ViewportHeight * 0.5f));
        sourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        RunLayout(uiRoot, 1280, 840, 16);

        var before = sourceEditor.GetScrollMetricsSnapshot();

        Assert.True(sourceEditor.HandleTextInputFromInput('X'));
        RunLayout(uiRoot, 1280, 840, 16);

        var after = sourceEditor.GetScrollMetricsSnapshot();

        Assert.Equal(before.VerticalOffset, after.VerticalOffset, 3);
        Assert.Contains("XLine 18", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourceEditorEnterAtLineTen_ShouldOnlyInsertOneNewlineAtCaret()
    {
        var source = NormalizeLineEndings(BuildLongSourceXml());
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var lineTenStart = GetLineStartOffset(source, 10);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);

        var expected = source.Insert(lineTenStart, "\n");

        Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        Assert.Equal(expected, shell.SourceText);
        Assert.Contains("Line 39", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourceEditorBackspaceAtLineTen_ShouldOnlyDeleteTheImmediatePreviousNewline()
    {
        var source = NormalizeLineEndings(BuildLongSourceXml());
        var lineTenStart = GetLineStartOffset(source, 10);
        var sourceWithInsertedLineBreak = source.Insert(lineTenStart, "\n");
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = sourceWithInsertedLineBreak
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(lineTenStart > 0);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart + 1, 0);

        var expected = source;

        Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.Equal(expected, shell.SourceText);
        Assert.Contains("Line 39", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourceEditorEnterOnBlankLineAtLineTen_ShouldPreserveDocumentTail()
    {
        var source = NormalizeLineEndings(BuildBlankLineAtLineTenSourceXml());
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var lineTenStart = GetLineStartOffset(source, 10);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);

        var expected = source.Insert(lineTenStart, "\n");

        Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        Assert.Equal(expected, shell.SourceText);
        Assert.Contains("</UserControl>", shell.SourceText, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"Designer Preview\" />", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourceEditorRepeatedEnterOnBlankLineAtLineTen_ShouldPreserveDocumentTail()
    {
        var source = NormalizeLineEndings(BuildBlankLineAtLineTenSourceXml());
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var lineTenStart = GetLineStartOffset(source, 10);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);

        var expected = source;
        for (var i = 0; i < 3; i++)
        {
            expected = expected.Insert(lineTenStart, "\n");
            Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        }

        Assert.Equal(expected, shell.SourceText);
        Assert.Contains("</UserControl>", shell.SourceText, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"Designer Preview\" />", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourceEditorEnter_ShouldPreserveTrailingBlankLineAtEndOfDocument()
    {
        var source = NormalizeLineEndings(BuildLongSourceXml()) + "\n";
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var lineTenStart = GetLineStartOffset(source, 10);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);

        var expected = source.Insert(lineTenStart, "\n");

        Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));

        Assert.Equal(expected, shell.SourceText);
        Assert.EndsWith("\n", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_DefaultSourceEditorEnterAtLineTen_ShouldPreserveTailAndScrollableExtent()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var source = shell.SourceText;
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var beforeMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var beforeDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));
        Assert.Equal(source, beforeDocumentText);

        var lineTenStart = GetLineStartOffset(source, 10);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);

        var expected = source.Insert(lineTenStart, "\n");

        Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        RunLayout(uiRoot, 1280, 840, 16);

        var afterMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var afterDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));

        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, afterDocumentText);
        Assert.Contains("Content=\"Preview Action\"", shell.SourceText, StringComparison.Ordinal);
        Assert.True(afterMetrics.ExtentHeight - afterMetrics.ViewportHeight >= beforeMetrics.ExtentHeight - beforeMetrics.ViewportHeight);
        Assert.Equal(CountLogicalLines(expected), CountLogicalLines(afterDocumentText));
    }

    [Fact]
    public void ShellView_DefaultSourceEditorEnterViaUiRootInputAtLineTen_ShouldPreserveTailAndScrollableExtent()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var source = shell.SourceText;
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var beforeMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var beforeLineCount = CountLogicalLines(source);
        var lineTenStart = GetLineStartOffset(source, 10);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 10);
        Click(uiRoot, clickPoint);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        var afterDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));
        var afterMetrics = sourceEditor.GetScrollMetricsSnapshot();

        Assert.Equal(shell.SourceText, afterDocumentText);
        Assert.Contains("Content=\"Preview Action\"", shell.SourceText, StringComparison.Ordinal);
        Assert.Equal(beforeLineCount + 1, CountLogicalLines(shell.SourceText));
        Assert.True(afterMetrics.ExtentHeight - afterMetrics.ViewportHeight >= beforeMetrics.ExtentHeight - beforeMetrics.ViewportHeight);
    }

    [Fact]
    public void ShellView_DefaultSourceEditorRepeatedEnterViaUiRootInputAtLineTen_ShouldIncreaseExtentHeight()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var source = shell.SourceText;
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var beforeMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var beforeLineCount = CountLogicalLines(source);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 10);
        Click(uiRoot, clickPoint);
        for (var i = 0; i < 4; i++)
        {
            uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        var afterMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var afterDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));

        Assert.Equal(shell.SourceText, afterDocumentText);
        Assert.Equal(beforeLineCount + 4, CountLogicalLines(shell.SourceText));
        Assert.True(afterMetrics.ExtentHeight > beforeMetrics.ExtentHeight + 0.01f);
        Assert.True(afterMetrics.ExtentHeight - afterMetrics.ViewportHeight > beforeMetrics.ExtentHeight - beforeMetrics.ViewportHeight + 0.01f);
    }

    [Fact]
    public void ShellView_SourceEditorCtrlSpaceAfterLessThan_OpensControlCompletionPopup()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Contains("Button", shell.SourceEditorView.ControlCompletionItems);
        Assert.True(shell.SourceEditorView.ControlCompletionSelectedIndex >= 0);
        Assert.True(shell.SourceEditorView.ControlCompletionBounds.Width > 0f);
        Assert.True(shell.SourceEditorView.ControlCompletionBounds.Height > 0f);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_WritesHotTelemetryReport_ForFixedVirtualizedBranchBehavior()
    {
        _ = ScrollViewer.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();
        _ = Control.GetTelemetryAndReset();
        _ = Panel.GetTelemetryAndReset();
        _ = TextBlock.GetTelemetryAndReset();
        _ = Label.GetTelemetryAndReset();
        UiTextRenderer.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);

        var completionList = FindDescendant<ListBox>(shell);
        var completionScrollViewer = FindDescendant<ScrollViewer>(completionList);
        Assert.True(
            completionScrollViewer.ExtentHeight > completionScrollViewer.ViewportHeight + 0.01f,
            $"Expected completion popup to be scrollable, but extent={completionScrollViewer.ExtentHeight:0.###} viewport={completionScrollViewer.ViewportHeight:0.###}.");

        var pointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        _ = ScrollViewer.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();
        _ = Control.GetTelemetryAndReset();
        _ = Panel.GetTelemetryAndReset();
        _ = TextBlock.GetTelemetryAndReset();
        _ = Label.GetTelemetryAndReset();
        UiTextRenderer.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();

        const int wheelTicks = 12;
        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        var scrollTelemetry = ScrollViewer.GetTelemetryAndReset();
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        var controlTelemetry = Control.GetTelemetryAndReset();
        var panelTelemetry = Panel.GetTelemetryAndReset();
        var textBlockTelemetry = TextBlock.GetTelemetryAndReset();
        var labelTelemetry = Label.GetTelemetryAndReset();
        var uiPerformanceTelemetry = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var uiRenderTelemetry = uiRoot.GetRenderTelemetrySnapshotForTests();
        var textRendererTelemetry = UiTextRenderer.GetTimingSnapshotForTests();
        var textLayoutTelemetry = TextLayout.GetMetricsSnapshot();

        var suspiciousSignals = new (string Name, long Value)[]
        {
            ("framework.update_layout_max_pass_exit", frameworkTelemetry.UpdateLayoutMaxPassExitCount),
            ("framework.update_layout_passes", frameworkTelemetry.UpdateLayoutPassCount),
            ("framework.layout_updated_raise", frameworkTelemetry.LayoutUpdatedRaiseCount),
            ("framework.measure_calls", frameworkTelemetry.MeasureCallCount),
            ("framework.arrange_calls", frameworkTelemetry.ArrangeCallCount),
            ("control.get_visual_children", controlTelemetry.GetVisualChildrenCallCount),
            ("control.measure_override", controlTelemetry.MeasureOverrideCallCount),
            ("scrollviewer.measure_override", scrollTelemetry.MeasureOverrideCallCount),
            ("scrollviewer.arrange_override", scrollTelemetry.ArrangeOverrideCallCount),
            ("textblock.resolve_layout", textBlockTelemetry.ResolveLayoutCallCount),
            ("textblock.render", textBlockTelemetry.RenderCallCount),
            ("textlayout.build", textLayoutTelemetry.BuildCount),
            ("uitextrenderer.measure_width", textRendererTelemetry.MeasureWidthCallCount),
            ("uitextrenderer.draw_string", textRendererTelemetry.DrawStringCallCount)
        };

        var hottestSignal = suspiciousSignals
            .OrderByDescending(static entry => entry.Value)
            .First();
        var pathologySignal = frameworkTelemetry.UpdateLayoutMaxPassExitCount > 0
            ? (Name: "framework.update_layout_max_pass_exit", Value: frameworkTelemetry.UpdateLayoutMaxPassExitCount)
            : hottestSignal;

        var frameworkMeasureCallsPerWheel = frameworkTelemetry.MeasureCallCount / (double)wheelTicks;
        var scrollViewerMeasureOverridesPerWheel = scrollTelemetry.MeasureOverrideCallCount / (double)wheelTicks;
        var updateLayoutPassesPerWheel = frameworkTelemetry.UpdateLayoutPassCount / (double)wheelTicks;
        var visualChildrenTraversalsPerWheel = controlTelemetry.GetVisualChildrenCallCount / (double)wheelTicks;

        var repoRoot = TestApplicationResources.GetRepositoryRoot();
        var artifactsDir = Path.Combine(repoRoot, "artifacts");
        Directory.CreateDirectory(artifactsDir);
        var reportPath = Path.Combine(artifactsDir, "designer-source-completion-scroll-telemetry.txt");

        var report = new StringBuilder();
        report.AppendLine("DESIGNER_SOURCE_COMPLETION_SCROLL_TELEMETRY");
        report.AppendLine($"completion_items={shell.SourceEditorView.ControlCompletionItems.Count}");
        report.AppendLine($"wheel_ticks={wheelTicks}");
        report.AppendLine($"completion_vertical_offset={completionScrollViewer.VerticalOffset:0.###}");
        report.AppendLine($"completion_extent_height={completionScrollViewer.ExtentHeight:0.###}");
        report.AppendLine($"completion_viewport_height={completionScrollViewer.ViewportHeight:0.###}");
        report.AppendLine($"hottest_signal={hottestSignal.Name}:{hottestSignal.Value}");
        report.AppendLine($"pathology_signal={pathologySignal.Name}:{pathologySignal.Value}");
        report.AppendLine($"framework_measure_calls_per_wheel={frameworkMeasureCallsPerWheel:0.###}");
        report.AppendLine($"scrollviewer_measure_overrides_per_wheel={scrollViewerMeasureOverridesPerWheel:0.###}");
        report.AppendLine($"update_layout_passes_per_wheel={updateLayoutPassesPerWheel:0.###}");
        report.AppendLine($"visual_children_traversals_per_wheel={visualChildrenTraversalsPerWheel:0.###}");
        report.AppendLine();
        report.AppendLine($"scrollviewer={scrollTelemetry}");
        report.AppendLine($"framework={frameworkTelemetry}");
        report.AppendLine($"control={controlTelemetry}");
        report.AppendLine($"panel={panelTelemetry}");
        report.AppendLine($"textblock={textBlockTelemetry}");
        report.AppendLine($"label={labelTelemetry}");
        report.AppendLine($"ui_performance={uiPerformanceTelemetry}");
        report.AppendLine($"ui_render={uiRenderTelemetry}");
        report.AppendLine($"text_renderer={textRendererTelemetry}");
        report.AppendLine($"text_layout={textLayoutTelemetry}");
        File.WriteAllText(reportPath, report.ToString());

        Assert.True(completionScrollViewer.VerticalOffset > 0f, $"Expected completion popup to scroll, but offset stayed {completionScrollViewer.VerticalOffset:0.###}.");
        Assert.Equal(wheelTicks, scrollTelemetry.WheelHandled);
        Assert.True(frameworkTelemetry.MeasureCallCount > 0);
        Assert.True(scrollTelemetry.MeasureOverrideCallCount >= 0);
        Assert.True(controlTelemetry.GetVisualChildrenCallCount > 0);
        Assert.True(
            scrollTelemetry.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the fixed completion popup to stay on the virtualizing SetOffsets branch, but telemetry was {scrollTelemetry}.");
        Assert.Equal(0, scrollTelemetry.SetOffsetsTransformInvalidationPathCount);
        Assert.Equal(0, scrollTelemetry.SetOffsetsManualArrangePathCount);
        Assert.True(
            pathologySignal.Name is "framework.update_layout_max_pass_exit" or "control.get_visual_children",
            $"Expected the hot-path signal to be either the framework max-pass exit counter or the control visual-children traversal counter, but got {pathologySignal.Name}.");
        Assert.Equal(
            pathologySignal.Name == "framework.update_layout_max_pass_exit"
                ? frameworkTelemetry.UpdateLayoutMaxPassExitCount
                : controlTelemetry.GetVisualChildrenCallCount,
            pathologySignal.Value);
        Assert.True(File.Exists(reportPath), $"Expected telemetry report at '{reportPath}'.");
    }

    [Fact]
    public void AbsolutePopupListWheelScroll_StaysInSameCheapEnvelopeAsDesignerCompletionPopup()
    {
        var plainPopup = RunPopupListWheelTelemetryScenario(includeRichTextBoxSibling: false);
        var designerPopup = RunDesignerCompletionWheelTelemetryScenario();

        Assert.True(plainPopup.ScrollViewer.VerticalOffset > 0f);
        Assert.True(designerPopup.ScrollViewer.VerticalOffset > 0f);
        Assert.Equal(plainPopup.Scroll.PopupCloseCallCount, designerPopup.Scroll.PopupCloseCallCount);
        Assert.True(designerPopup.Framework.MeasureCallCount > 0);
        Assert.True(designerPopup.Scroll.MeasureOverrideCallCount >= 0);
        Assert.True(designerPopup.Control.GetVisualChildrenCallCount > 0);
        Assert.True(
            designerPopup.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected designer completion popup scrolling to remain on the virtualizing SetOffsets branch, but telemetry was {designerPopup.Scroll}.");
    }

    [Fact]
    public void AbsolutePopupListWheelScroll_WithRichTextBoxSibling_RemainsNearPlainPopupCost()
    {
        var plainPopup = RunPopupListWheelTelemetryScenario(includeRichTextBoxSibling: false);
        var popupWithRichTextBox = RunPopupListWheelTelemetryScenario(includeRichTextBoxSibling: true);

        Assert.True(popupWithRichTextBox.ScrollViewer.VerticalOffset > 0f);
        Assert.Equal(plainPopup.Scroll.PopupCloseCallCount, popupWithRichTextBox.Scroll.PopupCloseCallCount);
        Assert.True(
            popupWithRichTextBox.Framework.UpdateLayoutMaxPassExitCount <= plainPopup.Framework.UpdateLayoutMaxPassExitCount + 8,
            $"Expected adding a sibling RichTextBox to stay near plain popup cost, but max-pass exits jumped from {plainPopup.Framework.UpdateLayoutMaxPassExitCount} to {popupWithRichTextBox.Framework.UpdateLayoutMaxPassExitCount}.");
        Assert.True(
            popupWithRichTextBox.Framework.MeasureCallCount < plainPopup.Framework.MeasureCallCount * 3,
            $"Expected adding a sibling RichTextBox to avoid designer-scale measure churn, but measure calls jumped from {plainPopup.Framework.MeasureCallCount} to {popupWithRichTextBox.Framework.MeasureCallCount}.");
        Assert.True(
            popupWithRichTextBox.Control.GetVisualChildrenCallCount < plainPopup.Control.GetVisualChildrenCallCount * 3,
            $"Expected adding a sibling RichTextBox to avoid designer-scale visual traversal churn, but get-visual-children calls jumped from {plainPopup.Control.GetVisualChildrenCallCount} to {popupWithRichTextBox.Control.GetVisualChildrenCallCount}.");
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_ScrollsCheaplyWithoutMovingEditorViewportOrPopupAnchor()
    {
        const int wheelTicks = 12;
        ResetCompletionScrollTelemetry();

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);

        var completionList = FindDescendant<ListBox>(shell);
        var completionScrollViewer = FindDescendant<ScrollViewer>(completionList);
        var pointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        ResetCompletionScrollTelemetry();

        var editorVerticalOffsetBefore = sourceEditor.VerticalOffset;
        var popupBoundsBefore = shell.SourceEditorView.ControlCompletionBounds;
        var caretBoundsAvailableBefore = sourceEditor.TryGetCaretBounds(out var caretBoundsBefore);

        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        var scrollTelemetry = ScrollViewer.GetTelemetryAndReset();
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        var controlTelemetry = Control.GetTelemetryAndReset();

        var editorVerticalOffsetAfter = sourceEditor.VerticalOffset;
        var popupBoundsAfter = shell.SourceEditorView.ControlCompletionBounds;
        var caretBoundsAvailableAfter = sourceEditor.TryGetCaretBounds(out var caretBoundsAfter);

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Equal(editorVerticalOffsetBefore, editorVerticalOffsetAfter);
        Assert.True(AreRectsEffectivelyEqual(popupBoundsBefore, popupBoundsAfter));
        Assert.Equal(caretBoundsAvailableBefore, caretBoundsAvailableAfter);
        if (caretBoundsAvailableBefore && caretBoundsAvailableAfter)
        {
            Assert.True(AreRectsEffectivelyEqual(caretBoundsBefore, caretBoundsAfter));
        }

        Assert.True(frameworkTelemetry.MeasureCallCount > 0);
        Assert.True(scrollTelemetry.MeasureOverrideCallCount >= 0);
        Assert.True(controlTelemetry.GetVisualChildrenCallCount > 0);
        Assert.True(
            scrollTelemetry.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the stable-anchor completion popup to keep using the virtualizing SetOffsets branch, but telemetry was {scrollTelemetry}.");
        Assert.Equal(0, scrollTelemetry.SetOffsetsTransformInvalidationPathCount);
        Assert.Equal(0, scrollTelemetry.SetOffsetsManualArrangePathCount);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_DoesNotRebuildLineNumberGutterWhenViewportStaysStable()
    {
        const int wheelTicks = 12;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        var completionList = FindDescendant<ListBox>(shell);
        var pointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var lineNumberPanel = shell.SourceLineNumberPanelControl;
        var gutterItemsSourceBefore = lineNumberPanel.VisibleLineTexts;
        var firstLineBefore = GetLineNumberText(lineNumberPanel, 0);
        var renderedLineCountBefore = GetRenderedLineNumberCount(lineNumberPanel);
        var editorVerticalOffsetBefore = sourceEditor.VerticalOffset;
        var popupBoundsBefore = shell.SourceEditorView.ControlCompletionBounds;

        var gutterRebuildCount = 0;
        var previousItemsSource = gutterItemsSourceBefore;
        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);

            var currentItemsSource = lineNumberPanel.VisibleLineTexts;
            if (!ReferenceEquals(previousItemsSource, currentItemsSource))
            {
                gutterRebuildCount++;
                previousItemsSource = currentItemsSource;
            }
        }

        var firstLineAfter = GetLineNumberText(lineNumberPanel, 0);
        var renderedLineCountAfter = GetRenderedLineNumberCount(lineNumberPanel);
        var editorVerticalOffsetAfter = sourceEditor.VerticalOffset;
        var popupBoundsAfter = shell.SourceEditorView.ControlCompletionBounds;

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Equal(0, gutterRebuildCount);
        Assert.Equal(firstLineBefore, firstLineAfter);
        Assert.Equal(renderedLineCountBefore, renderedLineCountAfter);
        Assert.Equal(editorVerticalOffsetBefore, editorVerticalOffsetAfter);
        Assert.True(AreRectsEffectivelyEqual(popupBoundsBefore, popupBoundsAfter));
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_DetachingSourceEditorHandlers_DoesNotCollapseLayoutChurn()
    {
        var baseline = RunDesignerCompletionWheelTelemetryScenario();
        var withoutViewportChanged = RunDesignerCompletionWheelTelemetryScenario(static (shell, _, _) =>
            DetachSourceEditorEventHandler(shell.SourceEditorView, shell.SourceEditorControl, eventName: "ViewportChanged", methodName: "OnSourceEditorViewportChanged"));
        var withoutLayoutUpdated = RunDesignerCompletionWheelTelemetryScenario(static (shell, _, _) =>
            DetachSourceEditorEventHandler(shell.SourceEditorView, shell.SourceEditorControl, eventName: "LayoutUpdated", methodName: "OnSourceEditorLayoutUpdated"));

        Assert.True(
            withoutLayoutUpdated.Framework.UpdateLayoutMaxPassExitCount >= baseline.Framework.UpdateLayoutMaxPassExitCount * 0.9,
            $"Expected detaching the LayoutUpdated handler to leave most layout churn intact. baseline={baseline.Framework.UpdateLayoutMaxPassExitCount} withoutLayoutUpdated={withoutLayoutUpdated.Framework.UpdateLayoutMaxPassExitCount}");
        Assert.True(
            withoutLayoutUpdated.Framework.MeasureCallCount >= baseline.Framework.MeasureCallCount * 0.9,
            $"Expected detaching the LayoutUpdated handler to leave most measure churn intact. baseline={baseline.Framework.MeasureCallCount} withoutLayoutUpdated={withoutLayoutUpdated.Framework.MeasureCallCount}");
        Assert.True(
            withoutViewportChanged.Framework.UpdateLayoutMaxPassExitCount >= baseline.Framework.UpdateLayoutMaxPassExitCount * 0.9,
            $"Expected detaching the ViewportChanged handler to leave most layout churn intact. baseline={baseline.Framework.UpdateLayoutMaxPassExitCount} withoutViewportChanged={withoutViewportChanged.Framework.UpdateLayoutMaxPassExitCount}");
        Assert.True(
            withoutViewportChanged.Framework.MeasureCallCount >= baseline.Framework.MeasureCallCount * 0.9,
            $"Expected detaching the ViewportChanged handler to leave most measure churn intact. baseline={baseline.Framework.MeasureCallCount} withoutViewportChanged={withoutViewportChanged.Framework.MeasureCallCount}");
        Assert.True(
            withoutLayoutUpdated.Control.GetVisualChildrenCallCount >= baseline.Control.GetVisualChildrenCallCount * 0.9,
            $"Expected detaching the LayoutUpdated handler to leave most visual-tree traversal churn intact. baseline={baseline.Control.GetVisualChildrenCallCount} withoutLayoutUpdated={withoutLayoutUpdated.Control.GetVisualChildrenCallCount}");
    }

    [Fact]
    public void StandaloneSourceEditorView_ControlCompletionWheelScroll_MatchesShellFixedBehaviorWithoutReintroducingStorm()
    {
        var plainPopup = RunPopupListWheelTelemetryScenario(includeRichTextBoxSibling: false);
        var standalone = RunStandaloneSourceEditorCompletionWheelTelemetryScenario();
        var fullShell = RunDesignerCompletionWheelTelemetryScenario();

        Assert.True(plainPopup.ScrollViewer.VerticalOffset > 0f);
        Assert.True(standalone.ScrollViewer.VerticalOffset > 0f);
        Assert.True(fullShell.ScrollViewer.VerticalOffset > 0f);
        Assert.True(standalone.Framework.MeasureCallCount > 0);
        Assert.True(fullShell.Framework.MeasureCallCount > 0);
        Assert.True(standalone.Scroll.MeasureOverrideCallCount >= 0);
        Assert.True(fullShell.Scroll.MeasureOverrideCallCount >= 0);
        Assert.True(fullShell.Control.GetVisualChildrenCallCount > 0);
        Assert.True(
            standalone.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the standalone source editor view to keep using the virtualizing SetOffsets branch, but telemetry was {standalone.Scroll}.");
        Assert.True(
            fullShell.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the full shell source editor completion popup to keep using the virtualizing SetOffsets branch, but telemetry was {fullShell.Scroll}.");
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_DoesNotCloseOrReopenPopup()
    {
        const int wheelTicks = 12;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        var completionPopup = GetCompletionPopup(shell.SourceEditorView);
        var opened = 0;
        var closed = 0;
        completionPopup.Opened += (_, _) => opened++;
        completionPopup.Closed += (_, _) => closed++;

        var completionList = FindDescendant<ListBox>(shell);
        var pointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Equal(0, opened);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_PopupHostLayoutUpdatedFeedback_DoesNotDriveMostChurn()
    {
        var baseline = RunDesignerCompletionWheelTelemetryScenario();
        var withoutPopupHostLayoutUpdated = RunDesignerCompletionWheelTelemetryScenario(static (shell, _, _) =>
            DetachPopupHostLayoutUpdatedHandler(GetCompletionPopup(shell.SourceEditorView)));

        Assert.True(
            withoutPopupHostLayoutUpdated.Framework.UpdateLayoutMaxPassExitCount >= baseline.Framework.UpdateLayoutMaxPassExitCount * 0.9,
            $"Expected detaching the popup host LayoutUpdated handler to leave most max-pass exits intact. baseline={baseline.Framework.UpdateLayoutMaxPassExitCount} withoutPopupHostLayoutUpdated={withoutPopupHostLayoutUpdated.Framework.UpdateLayoutMaxPassExitCount}");
        Assert.True(
            withoutPopupHostLayoutUpdated.Framework.MeasureCallCount >= baseline.Framework.MeasureCallCount * 0.9,
            $"Expected detaching the popup host LayoutUpdated handler to leave most framework measure churn intact. baseline={baseline.Framework.MeasureCallCount} withoutPopupHostLayoutUpdated={withoutPopupHostLayoutUpdated.Framework.MeasureCallCount}");
        Assert.True(
            withoutPopupHostLayoutUpdated.Control.GetVisualChildrenCallCount >= baseline.Control.GetVisualChildrenCallCount * 0.9,
            $"Expected detaching the popup host LayoutUpdated handler to leave most visual-tree traversal churn intact. baseline={baseline.Control.GetVisualChildrenCallCount} withoutPopupHostLayoutUpdated={withoutPopupHostLayoutUpdated.Control.GetVisualChildrenCallCount}");
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_DoesNotFireSelectionChangedPaths()
    {
        const int wheelTicks = 12;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        var completionList = GetCompletionListBox(shell.SourceEditorView);
        var sourceEditorSelectionChanged = 0;
        var completionSelectionChanged = 0;
        sourceEditor.SelectionChanged += (_, _) => sourceEditorSelectionChanged++;
        completionList.SelectionChanged += (_, _) => completionSelectionChanged++;

        var pointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Equal(0, sourceEditorSelectionChanged);
        Assert.Equal(0, completionSelectionChanged);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_DisablingCompletionListVirtualization_NoLongerChangesCostEnvelope()
    {
        var baseline = RunDesignerCompletionWheelTelemetryScenario();
        var withoutVirtualization = RunDesignerCompletionWheelTelemetryScenario(static (shell, _, _) =>
            GetCompletionListBox(shell.SourceEditorView).IsVirtualizing = false);

        Assert.True(baseline.Framework.MeasureCallCount > 0);
        Assert.True(baseline.Scroll.MeasureOverrideCallCount >= 0);
        Assert.True(baseline.Control.GetVisualChildrenCallCount > 0);
        Assert.True(
            baseline.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the default completion list to keep using the virtualizing SetOffsets branch, but telemetry was {baseline.Scroll}.");
        Assert.Equal(0, withoutVirtualization.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsTransformInvalidationPathCount > 0,
            $"Expected disabling completion-list virtualization to move the same repro into the transform-scrolling SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.Equal(0, withoutVirtualization.Scroll.SetOffsetsManualArrangePathCount);
        Assert.True(
            withoutVirtualization.Framework.MeasureCallCount <= baseline.Framework.MeasureCallCount * 2,
            $"Expected disabling completion-list virtualization to stay in the same cheap measure envelope after the fix. baseline={baseline.Framework.MeasureCallCount} withoutVirtualization={withoutVirtualization.Framework.MeasureCallCount}");
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_VirtualizationSwitchesScrollViewerIntoExactSetOffsetsBranch()
    {
        var virtualized = RunDesignerCompletionWheelTelemetryScenario();
        var nonVirtualized = RunDesignerCompletionWheelTelemetryScenario(static (shell, _, _) =>
            GetCompletionListBox(shell.SourceEditorView).IsVirtualizing = false);

        var virtualizedRuntime = virtualized.ScrollViewer.GetScrollViewerSnapshotForDiagnostics();
        var nonVirtualizedRuntime = nonVirtualized.ScrollViewer.GetScrollViewerSnapshotForDiagnostics();

        Assert.True(
            virtualizedRuntime.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the default completion list to take the VirtualizingStackPanel measure-invalidation branch, but runtime was {virtualizedRuntime}.");
        Assert.Equal(
            0,
            virtualizedRuntime.SetOffsetsTransformInvalidationPathCount);
        Assert.Equal(
            0,
            virtualizedRuntime.SetOffsetsManualArrangePathCount);

        Assert.True(
            nonVirtualizedRuntime.SetOffsetsVirtualizingMeasureInvalidationPathCount <= 1,
            $"Expected disabling completion-list virtualization to largely avoid the virtualizing measure-invalidation path, but runtime was {nonVirtualizedRuntime}.");
        Assert.True(
            nonVirtualizedRuntime.SetOffsetsTransformInvalidationPathCount > 0,
            $"Expected disabling completion-list virtualization to move the same wheel-scroll scenario into the transform-scrolling SetOffsets branch, but runtime was {nonVirtualizedRuntime}.");
        Assert.Equal(
            0,
            nonVirtualizedRuntime.SetOffsetsManualArrangePathCount);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionWheelScroll_VirtualizedFixedPath_StaysNearPlainPopupWithoutDisablingVirtualization()
    {
        var plainPopup = RunPopupListWheelTelemetryScenario(includeRichTextBoxSibling: false);
        var virtualized = RunDesignerCompletionWheelTelemetryScenario();
        var withoutVirtualization = RunDesignerCompletionWheelTelemetryScenario(static (shell, _, _) =>
            GetCompletionListBox(shell.SourceEditorView).IsVirtualizing = false);

        Assert.True(virtualized.Framework.MeasureCallCount > 0);
        Assert.True(virtualized.Scroll.MeasureOverrideCallCount >= 0);
        Assert.True(virtualized.Control.GetVisualChildrenCallCount > 0);
        Assert.True(
            virtualized.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the fixed designer completion popup to stay on the virtualizing SetOffsets branch, but telemetry was {virtualized.Scroll}.");
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount <= 1,
            $"Expected disabling completion-list virtualization in the standalone source editor view to largely avoid the virtualizing SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsTransformInvalidationPathCount > 0,
            $"Expected disabling completion-list virtualization to move the same repro into the transform-scrolling SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.True(
            virtualized.Control.GetVisualChildrenCallCount > withoutVirtualization.Control.GetVisualChildrenCallCount,
            $"Expected the current virtualized completion path to show higher visual-tree traversal telemetry than the non-virtualized fallback in this repro. virtualized={virtualized.Control.GetVisualChildrenCallCount} nonVirtualized={withoutVirtualization.Control.GetVisualChildrenCallCount}");
    }

    [Fact]
    public void StandaloneSourceEditorView_ControlCompletionWheelScroll_DisablingCompletionListVirtualization_NoLongerChangesCostEnvelope()
    {
        var baseline = RunStandaloneSourceEditorCompletionWheelTelemetryScenario();
        var withoutVirtualization = RunStandaloneSourceEditorCompletionWheelTelemetryScenario(static (sourceEditorView, _, _) =>
            GetCompletionListBox(sourceEditorView).IsVirtualizing = false);

        Assert.True(baseline.Framework.MeasureCallCount > 0);
        Assert.True(baseline.Scroll.MeasureOverrideCallCount >= 0);
        Assert.True(
            baseline.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the standalone source editor completion popup to keep using the virtualizing SetOffsets branch, but telemetry was {baseline.Scroll}.");
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount <= 1,
            $"Expected disabling completion-list virtualization in the standalone source editor view to largely avoid the virtualizing SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsTransformInvalidationPathCount > 0,
            $"Expected disabling completion-list virtualization in the standalone source editor view to move the same repro into the transform-scrolling SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.Equal(0, withoutVirtualization.Scroll.SetOffsetsManualArrangePathCount);
        Assert.True(
            withoutVirtualization.Framework.MeasureCallCount <= baseline.Framework.MeasureCallCount * 2,
            $"Expected disabling completion-list virtualization in the standalone source editor view to stay in the same cheap measure envelope after the fix. baseline={baseline.Framework.MeasureCallCount} withoutVirtualization={withoutVirtualization.Framework.MeasureCallCount}");
    }

    [Fact]
    public void ShellView_SourceEditorCtrlSpaceAfterLessThan_ShouldNotInsertSpaceBeforeOpeningControlCompletion()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, textInput: [' '], heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Equal("<", shell.SourceText);
        Assert.Equal("<", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(1, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionAccept_InsertsFullElementAndPlacesCaretBetweenTags()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<But"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(4, 0);

        Assert.True(shell.SourceEditorView.TryOpenControlCompletion());
        Assert.True(shell.SourceEditorView.TryAcceptControlCompletion());

        Assert.Equal("<Button></Button>", shell.SourceText);
        Assert.Equal("<Button></Button>", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(8, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
        Assert.False(shell.SourceEditorView.IsControlCompletionOpen);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionAccept_ReplacesPartialEmptyElementSkeleton()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<But></But>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(4, 0);

        Assert.True(shell.SourceEditorView.TryOpenControlCompletion());
        Assert.True(shell.SourceEditorView.TryAcceptControlCompletion());

        Assert.Equal("<Button></Button>", shell.SourceText);
        Assert.Equal(8, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionClickAccept_InsertsSelectedElement()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<But"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(4, 0);

        Assert.True(shell.SourceEditorView.TryOpenControlCompletion());
        RunLayout(uiRoot, 1280, 840, 16);

        var listBoxItem = FindDescendant<ListBoxItem>(shell, static item => IsCompletionItem(item, "Button"));

        Click(uiRoot, GetCenter(listBoxItem.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<Button></Button>", shell.SourceText);
        Assert.False(shell.SourceEditorView.IsControlCompletionOpen);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionHoveringScrolledCheckBox_ResolvesHoveredElementToListBoxItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var checkBoxItem = OpenCompletionAndScrollToItem(shell, sourceEditor, uiRoot, "CheckBox");
        var pointer = GetCenter(checkBoxItem.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Same(checkBoxItem, uiRoot.GetHoveredElementForDiagnostics());
        Assert.IsType<ListBoxItem>(uiRoot.GetHoveredElementForDiagnostics());
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionScrolledCheckBox_HitTestAtItemCenter_ResolvesWithinListBoxItemSubtree()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var checkBoxItem = OpenCompletionAndScrollToItem(shell, sourceEditor, uiRoot, "CheckBox");
        var pointer = GetCenter(checkBoxItem.LayoutSlot);
        var hit = VisualTreeHelper.HitTest(shell, pointer);

        Assert.NotNull(hit);
        Assert.Same(checkBoxItem, FindSelfOrAncestor<ListBoxItem>(hit));
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionMouseDownOnScrolledCheckBox_SelectsCheckBoxItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var checkBoxItem = OpenCompletionAndScrollToItem(shell, sourceEditor, uiRoot, "CheckBox");
        var pointer = GetCenter(checkBoxItem.LayoutSlot);
        var expectedIndex = GetCompletionItemIndex(shell.SourceEditorView.ControlCompletionItems, "CheckBox");

        Assert.True(expectedIndex >= 0);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal(expectedIndex, shell.SourceEditorView.ControlCompletionSelectedIndex);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionMouseDownOnScrolledCheckBox_ResolvesClickDownTargetToListBoxItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var checkBoxItem = OpenCompletionAndScrollToItem(shell, sourceEditor, uiRoot, "CheckBox");
        var pointer = GetCenter(checkBoxItem.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Same(checkBoxItem, uiRoot.GetLastClickDownTargetForDiagnostics());
        Assert.IsType<ListBoxItem>(uiRoot.GetLastClickDownTargetForDiagnostics());
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionMouseUpOnScrolledCheckBox_ResolvesClickUpTargetToListBoxItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var checkBoxItem = OpenCompletionAndScrollToItem(shell, sourceEditor, uiRoot, "CheckBox");
        var pointer = GetCenter(checkBoxItem.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Same(checkBoxItem, uiRoot.GetLastClickUpTargetForDiagnostics());
        Assert.IsType<ListBoxItem>(uiRoot.GetLastClickUpTargetForDiagnostics());
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionClickAccept_AfterScrollingToCheckBox_InsertsSelectedElement()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var checkBoxItem = OpenCompletionAndScrollToItem(shell, sourceEditor, uiRoot, "CheckBox");
        var pointer = GetCenter(checkBoxItem.LayoutSlot);

        Click(uiRoot, pointer);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<CheckBox></CheckBox>", shell.SourceText);
        Assert.False(shell.SourceEditorView.IsControlCompletionOpen);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletionEscape_DismissesPopup()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.False(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Equal("<", shell.SourceText);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletion_OutsideClickDismissesAndClearsState()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.NotEmpty(shell.SourceEditorView.ControlCompletionItems);

        Click(uiRoot, GetDiagnosticsTabHeaderPoint(Assert.IsType<TabControl>(shell.FindName("EditorTabControl"))));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.False(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Empty(shell.SourceEditorView.ControlCompletionItems);
        Assert.Equal(-1, shell.SourceEditorView.ControlCompletionSelectedIndex);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletion_InvalidClosingTagContext_DoesNotOpen()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "</"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(2, 0);

        Assert.False(shell.SourceEditorView.TryOpenControlCompletion());
        Assert.False(shell.SourceEditorView.IsControlCompletionOpen);
    }

    [Fact]
    public void ShellView_SourceEditorControlCompletion_ScrollRepositionsPopupWithCaret()
    {
        var source = NormalizeLineEndings(BuildLongSourceXml()).Replace(
            "    <TextBlock Text=\"Line 20\" />",
            "    <But",
            StringComparison.Ordinal);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var caretOffset = source.IndexOf("<But", StringComparison.Ordinal) + 4;
        Assert.True(caretOffset >= 4);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(caretOffset, 0);
        RunLayout(uiRoot, 1280, 840, 16);

        var lineHeight = UiTextRenderer.GetLineHeight(sourceEditor, sourceEditor.FontSize);
        Assert.True(shell.SourceEditorView.TryOpenControlCompletion());
        RunLayout(uiRoot, 1280, 840, 16);
        var before = shell.SourceEditorView.ControlCompletionBounds;
        var beforeMetrics = sourceEditor.GetScrollMetricsSnapshot();

        sourceEditor.ScrollToVerticalOffset(beforeMetrics.VerticalOffset + (3f * lineHeight));
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);
        var after = shell.SourceEditorView.ControlCompletionBounds;

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.NotEqual(before.Y, after.Y);
    }

    [Fact]
    public void ShellView_FileCommands_WireToolbarButtonsAndUseDocumentWorkflow()
    {
        var fileStore = new FakeDocumentFileStore();
        fileStore.ReadTexts["C:/designer/open.xml"] = ValidViewXml;
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", fileStore);
        var shell = new InkkSlinger.Designer.DesignerShellView(documentController);

        var openButton = Assert.IsType<Button>(shell.FindName("OpenButton"));
        var saveButton = Assert.IsType<Button>(shell.FindName("SaveButton"));
        var promptPrimaryButton = Assert.IsType<Button>(shell.FindName("WorkflowPromptPrimaryButton"));
        _ = Assert.IsType<TextBox>(shell.FindName("DocumentPathTextBox"));

        Assert.True(CommandSourceExecution.TryExecute(openButton, shell));
        shell.ViewModel.PromptPathText = "C:/designer/open.xml";
        Assert.True(CommandSourceExecution.TryExecute(promptPrimaryButton, shell));
        Assert.Equal(NormalizeLineEndings(ValidViewXml), shell.SourceText);
        Assert.False(shell.DocumentController.IsDirty);

        shell.SourceText = ValidViewXml.Replace("Hello designer", "Saved from shell", StringComparison.Ordinal);

        Assert.True(CommandSourceExecution.TryExecute(saveButton, shell));
        Assert.Equal(shell.SourceText, fileStore.WrittenTexts["C:/designer/open.xml"]);
        Assert.False(shell.DocumentController.IsDirty);
    }

    [Fact]
    public void ShellView_NewCommand_ShowsUnsavedChangesPromptAndDiscardResetsDocument()
    {
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />");
        var shell = new InkkSlinger.Designer.DesignerShellView(documentController)
        {
            SourceText = ValidViewXml
        };

        var newButton = Assert.IsType<Button>(shell.FindName("NewButton"));
        var discardButton = Assert.IsType<Button>(shell.FindName("WorkflowPromptSecondaryButton"));
        var promptBorder = Assert.IsType<Border>(shell.FindName("WorkflowPromptBorder"));

        Assert.True(CommandSourceExecution.TryExecute(newButton, shell));
        Assert.Equal(Visibility.Visible, promptBorder.Visibility);

        Assert.True(CommandSourceExecution.TryExecute(discardButton, shell));
        Assert.Equal("<UserControl />", shell.SourceText);
        Assert.False(shell.DocumentController.IsDirty);
    }

    private static string FindNodeId(InkkSlinger.Designer.DesignerVisualNode node, string? elementName, string typeName)
    {
        if (node.ElementName == elementName && node.TypeName == typeName)
        {
            return node.Id;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            var match = FindNodeIdOrNull(child, elementName, typeName);
            if (match != null)
            {
                return match;
            }
        }

        throw new Xunit.Sdk.XunitException($"Could not find node '{elementName ?? "(unnamed)"}' of type '{typeName}'.");
    }

    private static string? FindNodeIdOrNull(InkkSlinger.Designer.DesignerVisualNode node, string? elementName, string typeName)
    {
        if (node.ElementName == elementName && node.TypeName == typeName)
        {
            return node.Id;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            var match = FindNodeIdOrNull(node.Children[i], elementName, typeName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string BuildLongSourceXml()
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("<UserControl xmlns=\"urn:inkkslinger-ui\"");
        lines.AppendLine("             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        lines.AppendLine("  <StackPanel>");
        for (var i = 0; i < 40; i++)
        {
            lines.Append("    <TextBlock Text=\"Line ");
            lines.Append(i.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            lines.AppendLine("\" />");
        }

        lines.AppendLine("  </StackPanel>");
        lines.Append("</UserControl>");
        return lines.ToString();
    }

    private static string BuildNumberedSource(int lineCount)
    {
        return string.Join(
            "\n",
            Enumerable.Range(1, lineCount).Select(static line => string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"<Line Number=\"{line}\" />")));
    }

        private static string BuildBlankLineAtLineTenSourceXml()
        {
                return """
                        <UserControl xmlns="urn:inkkslinger-ui"
                                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                 Background="#101820">
                            <Grid Margin="24">
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="18" />
                                    <RowDefinition Height="*" />
                                </Grid.RowDefinitions>

                                <TextBlock Text="Designer Preview" />
                            </Grid>
                        </UserControl>
                        """;
        }

    private static int GetLineStartOffset(string text, int oneBasedLineNumber)
    {
        Assert.True(oneBasedLineNumber >= 1);

        if (oneBasedLineNumber == 1)
        {
            return 0;
        }

        var currentLine = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            currentLine++;
            if (currentLine == oneBasedLineNumber)
            {
                return index + 1;
            }
        }

        throw new Xunit.Sdk.XunitException($"Could not find line {oneBasedLineNumber}.");
    }

    private static string GetLineText(string text, int oneBasedLineNumber)
    {
        var lineStart = GetLineStartOffset(text, oneBasedLineNumber);
        var lineEnd = lineStart;
        while (lineEnd < text.Length && text[lineEnd] != '\n')
        {
            lineEnd++;
        }

        return text.Substring(lineStart, lineEnd - lineStart);
    }

    private static int GetLineNumberContaining(string text, string fragment)
    {
        var index = text.IndexOf(fragment, StringComparison.Ordinal);
        Assert.True(index >= 0);

        var lineNumber = 1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static int CountLogicalLines(string text)
    {
        if (text.Length == 0)
        {
            return 1;
        }

        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static Vector2 GetSourceEditorLinePoint(RichTextBox sourceEditor, int oneBasedLineNumber)
    {
        var lineHeight = UiTextRenderer.GetLineHeight(sourceEditor, sourceEditor.FontSize);
        return new Vector2(
            sourceEditor.LayoutSlot.X + 1f + 8f,
            sourceEditor.LayoutSlot.Y + 1f + 5f + ((oneBasedLineNumber - 1) * lineHeight) + 2f);
    }

    private static Vector2 GetDiagnosticsTabHeaderPoint(TabControl tabControl)
    {
        var sourceHeaderWidth = MathF.Max(
            36f,
            tabControl.HeaderPadding.Horizontal + UiTextRenderer.MeasureWidth(tabControl, "Source", tabControl.FontSize));
        return new Vector2(tabControl.LayoutSlot.X + sourceHeaderWidth + 8f, tabControl.LayoutSlot.Y + 8f);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static bool IsWithinViewport(LayoutRect slot, LayoutRect viewport)
    {
        return slot.X >= viewport.X &&
               slot.Y >= viewport.Y &&
               slot.X + slot.Width <= viewport.X + viewport.Width &&
               slot.Y + slot.Height <= viewport.Y + viewport.Height;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"x={rect.X:0.###},y={rect.Y:0.###},w={rect.Width:0.###},h={rect.Height:0.###}";
    }

    private static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "<null>";
        }

        if (element is FrameworkElement frameworkElement && !string.IsNullOrEmpty(frameworkElement.Name))
        {
            return $"{element.GetType().Name}#{frameworkElement.Name}";
        }

        return element.GetType().Name;
    }

    private static IReadOnlyList<Button> FindDiagnosticsCardButtons(ItemsControl itemsControl)
    {
        var buttons = new List<Button>();
        CollectDescendants(itemsControl, buttons, button => button.DataContext is InkkSlinger.Designer.DesignerDiagnosticEntry);

        if (buttons.Count == 0)
        {
            throw new InvalidOperationException("Expected diagnostics items control to expose button-backed cards.");
        }

        return buttons;
    }

    private static void CollectDescendants<TElement>(UIElement root, List<TElement> matches, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement match && (predicate == null || predicate(match)))
            {
                matches.Add(match);
            }

            CollectDescendants(child, matches, predicate);
        }
    }

    private static TElement FindDescendant<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement match && (predicate == null || predicate(match)))
            {
                return match;
            }

            var descendant = FindDescendantOrDefault(child, predicate);
            if (descendant != null)
            {
                return descendant;
            }
        }

        throw new InvalidOperationException($"Expected descendant of type '{typeof(TElement).Name}'.");
    }

    private static TElement? FindDescendantOrDefault<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement match && (predicate == null || predicate(match)))
            {
                return match;
            }

            var descendant = FindDescendantOrDefault(child, predicate);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static void SelectControlTagForSourceInspector(
        InkkSlinger.Designer.DesignerShellView shell,
        RichTextBox sourceEditor,
        UiRoot uiRoot,
        string elementName)
    {
        var elementIndex = shell.SourceText.IndexOf($"<{elementName}", StringComparison.Ordinal);
        Assert.True(elementIndex >= 0, $"Expected source text to contain start tag for '{elementName}'.");

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(elementIndex + 2, 0);
        RunLayout(uiRoot, 1280, 840, 16);
    }

    private static void EnsureSourceInspectorPropertyVisible(
        InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView,
        UiRoot uiRoot,
        FrameworkElement editor,
        string propertyName)
    {
        var filterTextBox = GetSourceInspectorFilterTextBox(sourceEditorView);
        filterTextBox.Text = propertyName;
        RunLayout(uiRoot, 1280, 840, 16);

        var visibleProperties = GetVisibleSourceInspectorPropertyNames(sourceEditorView);
        Assert.True(
            visibleProperties.Contains(propertyName, StringComparer.Ordinal),
            $"Expected Property Inspector filter to show '{propertyName}', but visible properties were [{string.Join(", ", visibleProperties)}].");

        var scrollViewer = GetSourceInspectorScrollViewer(sourceEditorView);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var viewport = scrollViewer.LayoutSlot;
            if (IsWithinViewport(editor.LayoutSlot, viewport))
            {
                return;
            }

            var targetTop = editor.LayoutSlot.Y - viewport.Y;
            var targetBottom = (editor.LayoutSlot.Y + editor.LayoutSlot.Height) - (viewport.Y + viewport.Height);
            var delta = targetTop < 0f ? targetTop : Math.Max(0f, targetBottom);

            if (Math.Abs(delta) < 0.01f)
            {
                break;
            }

            scrollViewer.ScrollToVerticalOffset(Math.Max(0f, scrollViewer.VerticalOffset + delta));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        Assert.True(
            IsWithinViewport(editor.LayoutSlot, scrollViewer.LayoutSlot),
            $"Expected {propertyName} editor to be visible within the Property Inspector viewport after filtering, but editorSlot={FormatRect(editor.LayoutSlot)}, viewport={FormatRect(scrollViewer.LayoutSlot)}, verticalOffset={scrollViewer.VerticalOffset:0.###}.");
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false, bool pointerMoved = true)
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

    private static InputDelta CreateKeyDownDelta(Keys key, Vector2 pointer, char[]? textInput = null, Keys[]? heldModifiers = null)
    {
        var keyboard = heldModifiers == null || heldModifiers.Length == 0
            ? new KeyboardState(key)
            : new KeyboardState(heldModifiers.Concat([key]).ToArray());

        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(heldModifiers ?? Array.Empty<Keys>()), default, pointer),
            Current = new InputSnapshot(keyboard, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = textInput == null ? new List<char>() : new List<char>(textInput),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerWheelDelta(Vector2 pointer, int wheelDelta)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = wheelDelta,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static CompletionWheelTelemetryResult RunPopupListWheelTelemetryScenario(bool includeRichTextBoxSibling)
    {
        const int wheelTicks = 12;
        ResetCompletionScrollTelemetry();

        var host = new Canvas
        {
            Width = 1280f,
            Height = 840f
        };

        if (includeRichTextBoxSibling)
        {
            var editor = new RichTextBox
            {
                Width = 620f,
                Height = 360f,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };
            DocumentEditing.ReplaceAllText(editor.Document, string.Join("\n", Enumerable.Range(1, 80).Select(static i => $"Line {i:000} <Button Content=\"Probe\" />")));
            host.AddChild(editor);
            Canvas.SetLeft(editor, 80f);
            Canvas.SetTop(editor, 80f);
        }

        var listBox = CreateCompletionProbeListBox();
        var popup = CreateCompletionProbePopup(listBox);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1280, 840, 16);
        popup.Open(host);
        Assert.True(popup.TrySetRootSpacePosition(300f, 140f));
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var completionScrollViewer = FindDescendant<ScrollViewer>(listBox);
        var pointer = GetCenter(listBox.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        ResetCompletionScrollTelemetry();

        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        return new CompletionWheelTelemetryResult(
            includeRichTextBoxSibling ? "popup-with-richtextbox" : "plain-popup",
            completionScrollViewer,
            popup.LayoutSlot,
            ScrollViewer.GetTelemetryAndReset(),
            FrameworkElement.GetTelemetryAndReset(),
            Control.GetTelemetryAndReset(),
            Panel.GetTelemetryAndReset(),
            TextBlock.GetTelemetryAndReset(),
            Label.GetTelemetryAndReset(),
            UiTextRenderer.GetTimingSnapshotForTests(),
            TextLayout.GetMetricsSnapshot());
    }

    private static CompletionWheelTelemetryResult RunDesignerCompletionWheelTelemetryScenario(Action<InkkSlinger.Designer.DesignerShellView, RichTextBox, UiRoot>? configure = null)
    {
        const int wheelTicks = 12;
        ResetCompletionScrollTelemetry();

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(1, 0);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        configure?.Invoke(shell, sourceEditor, uiRoot);

        var completionList = FindDescendant<ListBox>(shell);
        var completionScrollViewer = FindDescendant<ScrollViewer>(completionList);
        var pointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        ResetCompletionScrollTelemetry();

        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        return new CompletionWheelTelemetryResult(
            "designer-completion",
            completionScrollViewer,
            shell.SourceEditorView.ControlCompletionBounds,
            ScrollViewer.GetTelemetryAndReset(),
            FrameworkElement.GetTelemetryAndReset(),
            Control.GetTelemetryAndReset(),
            Panel.GetTelemetryAndReset(),
            TextBlock.GetTelemetryAndReset(),
            Label.GetTelemetryAndReset(),
            UiTextRenderer.GetTimingSnapshotForTests(),
            TextLayout.GetMetricsSnapshot());
    }

    private static CompletionWheelTelemetryResult RunStandaloneSourceEditorCompletionWheelTelemetryScenario(Action<InkkSlinger.Designer.DesignerSourceEditorView, RichTextBox, UiRoot>? configure = null)
    {
        const int wheelTicks = 12;
        ResetCompletionScrollTelemetry();

        var host = new Canvas
        {
            Width = 1280f,
            Height = 840f
        };

        var sourceEditorView = new InkkSlinger.Designer.DesignerSourceEditorView
        {
            Width = 760f,
            Height = 420f,
            SourceText = "<"
        };
        host.AddChild(sourceEditorView);
        Canvas.SetLeft(sourceEditorView, 80f);
        Canvas.SetTop(sourceEditorView, 80f);

        var sourceEditor = sourceEditorView.Editor;
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(1, 0);
        Assert.True(sourceEditorView.TryOpenControlCompletion());
        RunLayout(uiRoot, 1280, 840, 16);

        configure?.Invoke(sourceEditorView, sourceEditor, uiRoot);

        var completionList = FindDescendant<ListBox>(host);
        var completionScrollViewer = FindDescendant<ScrollViewer>(completionList);
        var pointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        ResetCompletionScrollTelemetry();

        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        return new CompletionWheelTelemetryResult(
            "standalone-source-editor-view",
            completionScrollViewer,
            sourceEditorView.ControlCompletionBounds,
            ScrollViewer.GetTelemetryAndReset(),
            FrameworkElement.GetTelemetryAndReset(),
            Control.GetTelemetryAndReset(),
            Panel.GetTelemetryAndReset(),
            TextBlock.GetTelemetryAndReset(),
            Label.GetTelemetryAndReset(),
            UiTextRenderer.GetTimingSnapshotForTests(),
            TextLayout.GetMetricsSnapshot());
    }

    private static void DetachSourceEditorEventHandler(
        InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView,
        RichTextBox sourceEditor,
        string eventName,
        string methodName)
    {
        var handlerMethod = typeof(InkkSlinger.Designer.DesignerSourceEditorView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handlerMethod);
        var handler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), sourceEditorView, handlerMethod!);

        switch (eventName)
        {
            case "LayoutUpdated":
                sourceEditor.LayoutUpdated -= handler;
                break;
            case "ViewportChanged":
                sourceEditor.ViewportChanged -= handler;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventName), eventName, "Unsupported RichTextBox event.");
        }
    }

    private static Popup GetCompletionPopup(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView)
    {
        var field = typeof(InkkSlinger.Designer.DesignerSourceEditorView).GetField("_completionPopup", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Popup>(field!.GetValue(sourceEditorView));
    }

    private static ListBox GetCompletionListBox(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView)
    {
        return Assert.IsType<ListBox>(sourceEditorView.FindName("CompletionListBox"));
    }

    private static IReadOnlyDictionary<string, FrameworkElement> GetSourceInspectorPropertyEditors(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView)
    {
        var visiblePropertyNames = GetVisibleSourceInspectorPropertyNames(sourceEditorView);
        var editors = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        foreach (var propertyName in visiblePropertyNames)
        {
            var row = GetSourceInspectorPropertyRow(sourceEditorView, propertyName);
            FrameworkElement? editor = FindDescendantOrDefault<TextBox>(row);
            editor ??= FindDescendantOrDefault<ComboBox>(row);
            if (editor == null)
            {
                var colorEditorHost = FindDescendantOrDefault<InkkSlinger.Designer.DesignerSourceColorPropertyEditor>(row);
                editor = colorEditorHost == null
                    ? null
                    : Assert.IsType<ComboBox>(colorEditorHost.FindName("EditorComboBox"));
            }
            if (editor == null)
            {
                throw new Xunit.Sdk.XunitException($"Expected Property Inspector editor for '{propertyName}'.");
            }

            editors[propertyName] = editor;
        }

        return editors;
    }

    private static Popup GetSourceInspectorColorEditorPopup(ComboBox editor)
    {
        return Assert.IsType<Popup>(GetSourceInspectorColorEditorHost(editor).FindName("InteractivePopup"));
    }

    private static ListBox GetComboBoxDropDownList(ComboBox editor)
    {
        var field = typeof(ComboBox).GetField("_dropDownList", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ListBox>(field!.GetValue(editor));
    }

    private static StackPanel GetSourceInspectorColorEditorPopupItemsHost(ComboBox editor)
    {
        return Assert.IsType<StackPanel>(GetSourceInspectorColorEditorHost(editor).FindName("InteractivePopupItemsHost"));
    }

    private static ColorPicker GetSourceInspectorColorEditorColorPicker(ComboBox editor)
    {
        return Assert.IsType<ColorPicker>(GetSourceInspectorColorEditorHost(editor).FindName("ColorPickerControl"));
    }

    private static ColorSpectrum GetSourceInspectorColorEditorHueSpectrum(ComboBox editor)
    {
        return Assert.IsType<ColorSpectrum>(GetSourceInspectorColorEditorHost(editor).FindName("HueSpectrumControl"));
    }

    private static ColorSpectrum GetSourceInspectorColorEditorAlphaSpectrum(ComboBox editor)
    {
        return Assert.IsType<ColorSpectrum>(GetSourceInspectorColorEditorHost(editor).FindName("AlphaSpectrumControl"));
    }

    private static InkkSlinger.Designer.DesignerSourceColorPropertyEditor GetSourceInspectorColorEditorHost(ComboBox editor)
    {
        return Assert.IsType<InkkSlinger.Designer.DesignerSourceColorPropertyEditor>(FindSelfOrAncestor<InkkSlinger.Designer.DesignerSourceColorPropertyEditor>(editor));
    }

    private static void OpenSourceInspectorColorEditor(UiRoot uiRoot, ComboBox editor)
    {
        var pointer = GetCenter(editor.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
    }

    private static LayoutRect ResolveRenderedBounds(UIElement element)
    {
        var bounds = element.LayoutSlot;
        if (!TryGetTransformFromThisToRoot(element, out var transform))
        {
            return bounds;
        }

        return TransformRect(bounds, transform);
    }

    private static bool TryGetTransformFromThisToRoot(UIElement? element, out Matrix transform)
    {
        transform = Matrix.Identity;
        var hasTransform = false;
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalRenderTransformSnapshot(out var localTransform))
            {
                continue;
            }

            transform *= localTransform;
            hasTransform = true;
        }

        return hasTransform;
    }

    private static LayoutRect TransformRect(LayoutRect rect, Matrix transform)
    {
        var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
        var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
        return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static bool Intersects(LayoutRect first, LayoutRect second)
    {
        return first.X < second.X + second.Width &&
               first.X + first.Width > second.X &&
               first.Y < second.Y + second.Height &&
               first.Y + first.Height > second.Y;
    }

    private static Vector2 FindRenderedPoint(UIElement root, LayoutRect bounds, Func<UIElement, bool> predicate)
    {
        const float step = 2f;

        for (var y = bounds.Y + step; y < bounds.Y + bounds.Height - step; y += step)
        {
            for (var x = bounds.X + step; x < bounds.X + bounds.Width - step; x += step)
            {
                var point = new Vector2(x, y);
                var hit = VisualTreeHelper.HitTest(root, point);
                if (hit != null && predicate(hit))
                {
                    return point;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"Could not find a rendered point within bounds {FormatRect(bounds)} that satisfied the requested predicate.");
    }

    private static ScrollViewer FindListBoxScrollViewer(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }
        }

        throw new Xunit.Sdk.XunitException("Could not resolve ListBox ScrollViewer.");
    }

    private static float GetAverageVisibleListItemHeight(ListBox listBox)
    {
        var host = FindListBoxItemsHostPanel(listBox);
        var totalHeight = 0f;
        var itemCount = 0;
        foreach (var child in host.Children)
        {
            if (child is FrameworkElement element)
            {
                totalHeight += element.LayoutSlot.Height;
                itemCount++;
            }
        }

        Assert.True(itemCount > 0, "Expected ListBox items host to contain at least one item.");
        return totalHeight / itemCount;
    }

    private static FrameworkElement GetLastVisibleListBoxItem(ListBox listBox)
    {
        var host = FindListBoxItemsHostPanel(listBox);
        for (var i = host.Children.Count - 1; i >= 0; i--)
        {
            if (host.Children[i] is FrameworkElement element)
            {
                return element;
            }
        }

        throw new Xunit.Sdk.XunitException("Expected ListBox items host to contain at least one item.");
    }

    private static bool IsVisualAncestorOrSelf(UIElement ancestor, UIElement? candidate)
    {
        for (var current = candidate; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static Panel FindListBoxItemsHostPanel(ListBox listBox)
    {
        var scrollViewer = FindListBoxScrollViewer(listBox);
        foreach (var child in scrollViewer.GetVisualChildren())
        {
            if (child is Panel panel)
            {
                return panel;
            }
        }

        throw new Xunit.Sdk.XunitException("Could not resolve ListBox items host panel.");
    }

    private static ScrollViewer GetSourceInspectorScrollViewer(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView)
    {
        return Assert.IsType<ScrollViewer>(sourceEditorView.FindName("SourcePropertyInspectorScrollViewer"));
    }

    private static TextBox GetSourceInspectorFilterTextBox(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView)
    {
        return Assert.IsType<TextBox>(sourceEditorView.FindName("SourcePropertyInspectorFilterTextBox"));
    }

    private static IReadOnlyList<string> GetVisibleSourceInspectorPropertyNames(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView)
    {
        var host = Assert.IsType<ItemsControl>(sourceEditorView.FindName("SourcePropertyInspectorPropertiesHost"));
        var names = new List<string>();
        foreach (var child in host.GetVisualChildren())
        {
            if (child is not Border { Visibility: Visibility.Visible, Child: StackPanel rowStack })
            {
                continue;
            }

            if (rowStack.Children.Count == 0 || rowStack.Children[0] is not TextBlock header)
            {
                continue;
            }

            names.Add(header.Text);
        }

        return names;
    }

    private static TextBlock GetSourceInspectorPropertyDescription(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView, string propertyName)
    {
        var host = Assert.IsType<ItemsControl>(sourceEditorView.FindName("SourcePropertyInspectorPropertiesHost"));
        foreach (var child in host.GetVisualChildren())
        {
            if (child is not Border { Child: StackPanel rowStack })
            {
                continue;
            }

            if (rowStack.Children.Count < 2 ||
                rowStack.Children[0] is not TextBlock header ||
                rowStack.Children[1] is not TextBlock description ||
                !string.Equals(header.Text, propertyName, StringComparison.Ordinal))
            {
                continue;
            }

            return description;
        }

        throw new Xunit.Sdk.XunitException($"Expected Property Inspector description row for '{propertyName}'.");
    }

    private static Border GetSourceInspectorPropertyRow(InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView, string propertyName)
    {
        var host = Assert.IsType<ItemsControl>(sourceEditorView.FindName("SourcePropertyInspectorPropertiesHost"));
        foreach (var child in host.GetVisualChildren())
        {
            if (child is not Border { Child: StackPanel rowStack } row)
            {
                continue;
            }

            if (rowStack.Children.Count == 0 || rowStack.Children[0] is not TextBlock header)
            {
                continue;
            }

            if (string.Equals(header.Text, propertyName, StringComparison.Ordinal))
            {
                return row;
            }
        }

        throw new Xunit.Sdk.XunitException($"Expected Property Inspector row for '{propertyName}'.");
    }

    private static ListBoxItem OpenCompletionAndScrollToItem(
        InkkSlinger.Designer.DesignerShellView shell,
        RichTextBox sourceEditor,
        UiRoot uiRoot,
        string itemName)
    {
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(1, 0);

        Assert.True(shell.SourceEditorView.TryOpenControlCompletion());
        RunLayout(uiRoot, 1280, 840, 16);

        var completionList = GetCompletionListBox(shell.SourceEditorView);
        var completionScrollViewer = FindDescendant<ScrollViewer>(completionList);
        var completionPointer = GetCenter(completionList.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(completionPointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        ListBoxItem? targetItem = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            targetItem = FindDescendantOrDefault<ListBoxItem>(completionList, item => IsCompletionItem(item, itemName));
            if (targetItem != null && completionScrollViewer.VerticalOffset > 0f)
            {
                var targetPointer = GetCenter(targetItem.LayoutSlot);
                uiRoot.RunInputDeltaForTests(CreatePointerDelta(targetPointer, pointerMoved: true));
                RunLayout(uiRoot, 1280, 840, 16);

                targetItem = FindDescendantOrDefault<ListBoxItem>(completionList, item => IsCompletionItem(item, itemName)) ?? targetItem;
                var hit = VisualTreeHelper.HitTest(shell, GetCenter(targetItem.LayoutSlot));
                if (ReferenceEquals(FindSelfOrAncestor<ListBoxItem>(hit), targetItem))
                {
                    break;
                }
            }

            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(completionPointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        targetItem ??= FindDescendantOrDefault<ListBoxItem>(completionList, item => IsCompletionItem(item, itemName));
        Assert.NotNull(targetItem);
        Assert.True(completionScrollViewer.VerticalOffset > 0f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(targetItem!.LayoutSlot), pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);
        return targetItem!;
    }

    private static bool IsCompletionItem(ListBoxItem item, string itemName)
    {
        if (TryGetCompletionItemElementName(item.Content, out var completionElementName) &&
            string.Equals(completionElementName, itemName, StringComparison.Ordinal))
        {
            return true;
        }

        if (item.Content is Label label && string.Equals(label.Content as string, itemName, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(item.Content as string, itemName, StringComparison.Ordinal);
    }

    private static bool TryGetCompletionItemElementName(object? content, out string? elementName)
    {
        elementName = null;
        if (content == null)
        {
            return false;
        }

        var contentType = content.GetType();
        if (!string.Equals(contentType.Name, "DesignerControlCompletionItem", StringComparison.Ordinal))
        {
            return false;
        }

        var property = contentType.GetProperty("ElementName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return false;
        }

        elementName = property.GetValue(content) as string;
        return !string.IsNullOrEmpty(elementName);
    }

    private static int GetCompletionItemIndex(IReadOnlyList<string> items, string itemName)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i], itemName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static TElement? FindSelfOrAncestor<TElement>(UIElement? element)
        where TElement : UIElement
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static void DetachPopupHostLayoutUpdatedHandler(Popup popup)
    {
        var hostField = typeof(Popup).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(hostField);
        var host = Assert.IsAssignableFrom<Panel>(hostField!.GetValue(popup));

        var handlerMethod = typeof(Popup).GetMethod("OnHostLayoutUpdated", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handlerMethod);
        var handler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), popup, handlerMethod!);
        host.LayoutUpdated -= handler;
    }

    private static void ResetCompletionScrollTelemetry()
    {
        _ = ScrollViewer.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();
        _ = Control.GetTelemetryAndReset();
        _ = Panel.GetTelemetryAndReset();
        _ = TextBlock.GetTelemetryAndReset();
        _ = Label.GetTelemetryAndReset();
        UiTextRenderer.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();
    }

    private static void ResetDesignerTestState()
    {
        Dispatcher.ResetForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        ResetCompletionScrollTelemetry();
    }

    private static ListBox CreateCompletionProbeListBox()
    {
        var listBox = new ListBox
        {
            Background = new Color(11, 16, 24),
            BorderBrush = new Color(35, 52, 73),
            BorderThickness = 0f,
            Padding = new Thickness(0f),
            SelectionMode = SelectionMode.Single,
            IsVirtualizing = true,
            MaxHeight = 260f,
            MinWidth = 180f,
            MaxWidth = 420f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        for (var i = 0; i < 90; i++)
        {
            listBox.Items.Add($"Completion Item {i:000}");
        }

        listBox.SelectedIndex = 0;
        return listBox;
    }

    private static Popup CreateCompletionProbePopup(ListBox listBox)
    {
        return new Popup
        {
            Title = string.Empty,
            TitleBarHeight = 0f,
            CanClose = false,
            CanDragMove = false,
            DismissOnOutsideClick = true,
            BorderThickness = 1f,
            BorderBrush = new Color(35, 52, 73),
            Background = new Color(9, 13, 19),
            Padding = new Thickness(0f),
            PlacementMode = PopupPlacementMode.Absolute,
            Content = listBox
        };
    }

    private static string GetLineNumberText(InkkSlinger.Designer.DesignerSourceLineNumberPresenter panel, int index)
    {
        return panel.VisibleLineTexts[index];
    }

    private static bool AreRectsEffectivelyEqual(LayoutRect left, LayoutRect right)
    {
        return MathF.Abs(left.X - right.X) < 0.001f &&
               MathF.Abs(left.Y - right.Y) < 0.001f &&
               MathF.Abs(left.Width - right.Width) < 0.001f &&
               MathF.Abs(left.Height - right.Height) < 0.001f;
    }

    private static int GetRenderedLineNumberCount(InkkSlinger.Designer.DesignerSourceLineNumberPresenter panel)
    {
        return panel.VisibleLineCount;
    }

    private static List<TElement> FindDescendants<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        var matches = new List<TElement>();
        CollectDescendants(root, matches, predicate);
        return matches;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        _ = elapsedMs;
        uiRoot.RunLayoutForTests(new Viewport(0, 0, width, height));
    }

    private sealed class FakeDocumentFileStore : InkkSlinger.Designer.IDesignerDocumentFileStore
    {
        public Dictionary<string, string> ReadTexts { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> WrittenTexts { get; } = new(StringComparer.Ordinal);

        public bool Exists(string path)
        {
            return ReadTexts.ContainsKey(path) || WrittenTexts.ContainsKey(path);
        }

        public string ReadAllText(string path)
        {
            return ReadTexts[path];
        }

        public void WriteAllText(string path, string text)
        {
            WrittenTexts[path] = text;
        }
    }

    private readonly record struct CompletionWheelTelemetryResult(
        string Scenario,
        ScrollViewer ScrollViewer,
        LayoutRect PopupBounds,
        ScrollViewerTelemetrySnapshot Scroll,
        FrameworkElementTelemetrySnapshot Framework,
        ControlTelemetrySnapshot Control,
        PanelTelemetrySnapshot Panel,
        TextBlockTelemetrySnapshot TextBlock,
        LabelTelemetrySnapshot Label,
        UiTextRendererTimingSnapshot TextRenderer,
        TextLayoutMetricsSnapshot TextLayout);
}