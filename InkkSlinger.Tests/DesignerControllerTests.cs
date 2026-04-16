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
    }

    [Fact]
    public void ShellView_SourceEditorLineNumberGutter_FollowsVerticalScroll()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildNumberedSource(80)
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);
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
        Assert.Equal(0, frameworkTelemetry.UpdateLayoutMaxPassExitCount);
        Assert.InRange(frameworkTelemetry.MeasureCallCount, 1, FixedCompletionScrollFrameworkMeasureCallCount);
        Assert.InRange(scrollTelemetry.MeasureOverrideCallCount, 0, 1);
        Assert.InRange(controlTelemetry.GetVisualChildrenCallCount, 1, FixedCompletionScrollVisualChildrenTraversalCount * 2);
        Assert.True(
            scrollTelemetry.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected the fixed completion popup to stay on the virtualizing SetOffsets branch, but telemetry was {scrollTelemetry}.");
        Assert.Equal(0, scrollTelemetry.SetOffsetsTransformInvalidationPathCount);
        Assert.Equal(0, scrollTelemetry.SetOffsetsManualArrangePathCount);
        Assert.Equal("control.get_visual_children", pathologySignal.Name);
        Assert.Equal(controlTelemetry.GetVisualChildrenCallCount, pathologySignal.Value);
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
        Assert.Equal(0, plainPopup.Framework.UpdateLayoutMaxPassExitCount);
        Assert.Equal(0, designerPopup.Framework.UpdateLayoutMaxPassExitCount);
        Assert.InRange(designerPopup.Framework.MeasureCallCount, 1, FixedCompletionScrollFrameworkMeasureCallCount);
        Assert.True(
            designerPopup.Framework.MeasureCallCount <= plainPopup.Framework.MeasureCallCount,
            $"Expected designer completion popup scrolling to stay no more expensive than the plain absolute popup baseline for framework measure churn. plain={plainPopup.Framework.MeasureCallCount} designer={designerPopup.Framework.MeasureCallCount}");
        Assert.InRange(designerPopup.Scroll.MeasureOverrideCallCount, 0, 1);
        Assert.InRange(designerPopup.Control.GetVisualChildrenCallCount, 1, FixedCompletionScrollVisualChildrenTraversalCount * 2);
        Assert.True(
            designerPopup.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount > 0,
            $"Expected designer completion popup scrolling to remain on the virtualizing SetOffsets branch, but telemetry was {designerPopup.Scroll}.");
        Assert.True(
            designerPopup.Control.GetVisualChildrenCallCount < plainPopup.Control.GetVisualChildrenCallCount * 2,
            $"Expected designer completion popup scrolling to stay in the same cheap visual-traversal envelope as a plain absolute popup list after the fix. plain={plainPopup.Control.GetVisualChildrenCallCount} designer={designerPopup.Control.GetVisualChildrenCallCount}");
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

        Assert.Equal(0, frameworkTelemetry.UpdateLayoutMaxPassExitCount);
        Assert.InRange(frameworkTelemetry.MeasureCallCount, 1, FixedCompletionScrollFrameworkMeasureCallCount);
        Assert.InRange(scrollTelemetry.MeasureOverrideCallCount, 0, 1);
        Assert.InRange(controlTelemetry.GetVisualChildrenCallCount, 1, FixedCompletionScrollVisualChildrenTraversalCount * 2);
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
        Assert.Equal(0, plainPopup.Framework.UpdateLayoutMaxPassExitCount);
        Assert.Equal(0, standalone.Framework.UpdateLayoutMaxPassExitCount);
        Assert.Equal(0, fullShell.Framework.UpdateLayoutMaxPassExitCount);
        Assert.True(
            standalone.Framework.MeasureCallCount <= plainPopup.Framework.MeasureCallCount,
            $"Expected the standalone source editor view to stay no more expensive than the plain absolute popup baseline for framework measure churn. plain={plainPopup.Framework.MeasureCallCount} standalone={standalone.Framework.MeasureCallCount}");
        Assert.True(
            fullShell.Framework.MeasureCallCount <= plainPopup.Framework.MeasureCallCount,
            $"Expected the full designer shell completion popup to stay no more expensive than the plain absolute popup baseline for framework measure churn. plain={plainPopup.Framework.MeasureCallCount} shell={fullShell.Framework.MeasureCallCount}");
        Assert.InRange(fullShell.Framework.MeasureCallCount, 1, FixedCompletionScrollFrameworkMeasureCallCount);
        Assert.InRange(standalone.Scroll.MeasureOverrideCallCount, 0, 1);
        Assert.InRange(fullShell.Scroll.MeasureOverrideCallCount, 0, 1);
        Assert.InRange(fullShell.Control.GetVisualChildrenCallCount, 1, FixedCompletionScrollVisualChildrenTraversalCount * 2);
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

        Assert.Equal(0, baseline.Framework.UpdateLayoutMaxPassExitCount);
        Assert.Equal(0, withoutVirtualization.Framework.UpdateLayoutMaxPassExitCount);
        Assert.InRange(baseline.Framework.MeasureCallCount, 1, FixedCompletionScrollFrameworkMeasureCallCount);
        Assert.InRange(baseline.Scroll.MeasureOverrideCallCount, 0, 1);
        Assert.InRange(baseline.Control.GetVisualChildrenCallCount, 1, FixedCompletionScrollVisualChildrenTraversalCount * 2);
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

        Assert.Equal(0, plainPopup.Framework.UpdateLayoutMaxPassExitCount);
        Assert.Equal(0, virtualized.Framework.UpdateLayoutMaxPassExitCount);
        Assert.InRange(virtualized.Framework.MeasureCallCount, 1, FixedCompletionScrollFrameworkMeasureCallCount);
        Assert.True(
            virtualized.Framework.MeasureCallCount <= plainPopup.Framework.MeasureCallCount,
            $"Expected the fixed virtualized completion path to stay no more expensive than the plain absolute popup baseline for framework measure churn. plain={plainPopup.Framework.MeasureCallCount} virtualized={virtualized.Framework.MeasureCallCount}");
        Assert.InRange(virtualized.Scroll.MeasureOverrideCallCount, 0, 1);
        Assert.InRange(virtualized.Control.GetVisualChildrenCallCount, 1, FixedCompletionScrollVisualChildrenTraversalCount * 2);
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
            virtualized.Control.GetVisualChildrenCallCount < plainPopup.Control.GetVisualChildrenCallCount * 2,
            $"Expected the fixed virtualized completion path to stay near plain popup traversal cost without turning virtualization off. plain={plainPopup.Control.GetVisualChildrenCallCount} virtualized={virtualized.Control.GetVisualChildrenCallCount}");
        Assert.True(
            virtualized.Control.GetVisualChildrenCallCount < withoutVirtualization.Control.GetVisualChildrenCallCount,
            $"Expected the fixed virtualized completion path to stay cheaper than the non-virtualized fallback for visual-tree traversal cost. virtualized={virtualized.Control.GetVisualChildrenCallCount} nonVirtualized={withoutVirtualization.Control.GetVisualChildrenCallCount}");
    }

    [Fact]
    public void StandaloneSourceEditorView_ControlCompletionWheelScroll_DisablingCompletionListVirtualization_NoLongerChangesCostEnvelope()
    {
        var baseline = RunStandaloneSourceEditorCompletionWheelTelemetryScenario();
        var withoutVirtualization = RunStandaloneSourceEditorCompletionWheelTelemetryScenario(static (sourceEditorView, _, _) =>
            GetCompletionListBox(sourceEditorView).IsVirtualizing = false);

        Assert.Equal(0, baseline.Framework.UpdateLayoutMaxPassExitCount);
        Assert.Equal(0, withoutVirtualization.Framework.UpdateLayoutMaxPassExitCount);
        Assert.InRange(baseline.Framework.MeasureCallCount, 1, FixedCompletionScrollFrameworkMeasureCallCount);
        Assert.InRange(baseline.Scroll.MeasureOverrideCallCount, 0, 1);
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
        var field = typeof(InkkSlinger.Designer.DesignerSourceEditorView).GetField("_completionListBox", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ListBox>(field!.GetValue(sourceEditorView));
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
        if (item.Content is InkkSlinger.Designer.DesignerControlCompletionItem completionItem &&
            string.Equals(completionItem.ElementName, itemName, StringComparison.Ordinal))
        {
            return true;
        }

        if (item.Content is Label label && string.Equals(label.Content as string, itemName, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(item.Content as string, itemName, StringComparison.Ordinal);
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