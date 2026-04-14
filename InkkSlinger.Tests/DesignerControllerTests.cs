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
        Assert.Contains(controller.Diagnostics, diagnostic => diagnostic.TargetDescription == "Button.UnknownProperty");
        Assert.Contains(controller.Diagnostics, diagnostic => diagnostic.LocationText.StartsWith("Line ", StringComparison.Ordinal));
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

        _ = Assert.IsType<ContentControl>(shell.FindName("PreviewHost"));
        _ = Assert.IsType<TreeView>(shell.FindName("VisualTreeView"));
        _ = Assert.IsAssignableFrom<RichTextBox>(shell.FindName("SourceEditor"));
        _ = Assert.IsType<StackPanel>(shell.FindName("DiagnosticsPanel"));

        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.VerticalScrollBarVisibility);
        Assert.Equal(0, editorTabControl.SelectedIndex);
        Assert.Equal("Diagnostics", diagnosticsTab.Header);
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

    private static string GetLineNumberText(StackPanel panel, int index)
    {
        var container = Assert.IsType<Border>(panel.Children[index]);
        var textBlock = Assert.IsType<TextBlock>(container.Child);
        return textBlock.Text;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
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