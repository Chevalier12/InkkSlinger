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
        var previewScrollViewer = Assert.IsType<ScrollViewer>(shell.FindName("PreviewScrollViewer"));
        var editorTabControl = Assert.IsType<TabControl>(shell.FindName("EditorTabControl"));
        var diagnosticsTab = Assert.IsType<TabItem>(shell.FindName("DiagnosticsTab"));
        var sourceLineNumberBorder = Assert.IsType<Border>(shell.FindName("SourceLineNumberBorder"));
        var sourceLineNumberPanel = Assert.IsType<StackPanel>(shell.FindName("SourceLineNumberPanel"));
        var diagnosticsItemsControl = Assert.IsType<ItemsControl>(shell.FindName("DiagnosticsItemsControl"));

        _ = Assert.IsType<ContentControl>(shell.FindName("PreviewHost"));
        _ = Assert.IsType<TreeView>(shell.FindName("VisualTreeView"));
        _ = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));

        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.VerticalScrollBarVisibility);
        Assert.Equal(0, editorTabControl.SelectedIndex);
        Assert.Equal("Diagnostics", diagnosticsTab.Header);
        Assert.NotNull(diagnosticsItemsControl);
        Assert.NotNull(sourceLineNumberBorder.Child);
        Assert.NotEmpty(sourceLineNumberPanel.Children);

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
    }

    [Fact]
    public void ShellView_SourceEditorLineNumberGutter_FollowsVerticalScroll()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildNumberedSource(80)
        };

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
        var sourceLineNumberPanel = Assert.IsType<StackPanel>(shell.FindName("SourceLineNumberPanel"));
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(sourceLineNumberPanel.Children.Count > 0);
        Assert.Equal("1", GetLineNumberText(sourceLineNumberPanel, 0));

        sourceEditor.SetFocusedFromInput(true);
        var lineHeight = UiTextRenderer.GetLineHeight(sourceEditor, sourceEditor.FontSize);
        var scrollMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var desiredVerticalOffset = Math.Max(0f, (18f * lineHeight) - (scrollMetrics.ViewportHeight * 0.5f));
        sourceEditor.ScrollToVerticalOffset(desiredVerticalOffset);
        Assert.True(sourceEditor.VerticalOffset > 0f);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.NotEqual("1", GetLineNumberText(sourceLineNumberPanel, 0));
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

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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
        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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
    Assert.Equal(new Color(41, 72, 102), Assert.IsType<SolidColorBrush>(hoverChrome.BorderBrush).Color);
    }

    [Fact]
    public void ShellView_SourceEditorTyping_PreservesVerticalScrollOffsetDuringRehighlight()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildLongSourceXml()
        };

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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

        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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
        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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
        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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
        var sourceEditor = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
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
    public void ShellView_FileCommands_WireToolbarButtonsAndUseDocumentWorkflow()
    {
        var fileStore = new FakeDocumentFileStore();
        fileStore.ReadTexts["C:/designer/open.xml"] = ValidViewXml;
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", fileStore);
        var shell = new InkkSlinger.Designer.DesignerShellView(documentController);

        var openButton = Assert.IsType<Button>(shell.FindName("OpenButton"));
        var saveButton = Assert.IsType<Button>(shell.FindName("SaveButton"));
        var promptPrimaryButton = Assert.IsType<Button>(shell.FindName("WorkflowPromptPrimaryButton"));
        var pathEditor = Assert.IsType<TextBox>(shell.FindName("DocumentPathTextBox"));

        Assert.True(CommandSourceExecution.TryExecute(openButton, shell));
        pathEditor.Text = "C:/designer/open.xml";
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

    [Fact]
    public void ShellView_AppExitRequest_WhenDirty_ShowsPromptAndAllowsDeferredCloseAfterDiscard()
    {
        var deferredCloseRequested = false;
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />");
        var shell = new InkkSlinger.Designer.DesignerShellView(
            documentController,
            requestAppExit: () => deferredCloseRequested = true)
        {
            SourceText = ValidViewXml
        };

        var allowedImmediately = shell.TryRequestAppExit();

        Assert.False(allowedImmediately);
        Assert.Equal(Visibility.Visible, Assert.IsType<Border>(shell.FindName("WorkflowPromptBorder")).Visibility);

        var discardButton = Assert.IsType<Button>(shell.FindName("WorkflowPromptSecondaryButton"));
        Assert.True(CommandSourceExecution.TryExecute(discardButton, shell));
        Assert.True(deferredCloseRequested);

        var allowedAfterDiscard = shell.TryRequestAppExit();

        Assert.True(allowedAfterDiscard);
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

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
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

    private static InputDelta CreateKeyDownDelta(Keys key, Vector2 pointer)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(), default, pointer),
            Current = new InputSnapshot(new KeyboardState(key), default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
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
    private static string GetLineNumberText(StackPanel panel, int index)
    {
        var container = Assert.IsType<Border>(panel.Children[index]);
        var textBlock = Assert.IsType<TextBlock>(container.Child);
        return textBlock.Text;
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
}