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

        _ = Assert.IsType<ContentControl>(shell.FindName("PreviewHost"));
        _ = Assert.IsType<TreeView>(shell.FindName("VisualTreeView"));
        _ = Assert.IsType<RichTextBox>(shell.FindName("SourceEditor"));

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