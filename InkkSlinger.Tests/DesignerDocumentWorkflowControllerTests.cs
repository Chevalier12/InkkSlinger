using Xunit;

namespace InkkSlinger.Tests;

public class DesignerDocumentWorkflowControllerTests
{
    private const string DefaultDocument = "<UserControl />";

    [Fact]
    public void BeginClose_WhenDocumentIsClean_AllowsCurrentCloseRequest()
    {
        var workflow = CreateWorkflow();

        var result = workflow.Workflow.BeginClose();

        Assert.Equal(InkkSlinger.Designer.DesignerWorkflowCloseAction.AllowCurrentRequest, result.CloseAction);
        Assert.Equal(InkkSlinger.Designer.DesignerDocumentPromptKind.None, workflow.Workflow.Prompt.Kind);
    }

    [Fact]
    public void SaveWithoutPath_ShowsSavePrompt_AndSubmitPathSavesDocument()
    {
        var workflow = CreateWorkflow();
        workflow.DocumentController.UpdateText("<UserControl><TextBlock Text=\"Hello\" /></UserControl>");

        var saveResult = workflow.Workflow.Save();

        Assert.True(saveResult.PromptChanged);
        Assert.Equal(InkkSlinger.Designer.DesignerDocumentPromptKind.SavePath, workflow.Workflow.Prompt.Kind);

        var submitResult = workflow.Workflow.SubmitPromptPath("C:/designer/saved.xml");

        Assert.False(submitResult.ReloadEditor);
        Assert.True(submitResult.PromptChanged);
        Assert.Equal("<UserControl><TextBlock Text=\"Hello\" /></UserControl>", workflow.Store.WrittenTexts["C:/designer/saved.xml"]);
        Assert.False(workflow.DocumentController.IsDirty);
        Assert.Equal("C:/designer/saved.xml", workflow.DocumentController.CurrentPath);
    }

    [Fact]
    public void SaveWithoutPath_WhenTargetExists_ShowsOverwritePrompt_AndCancelReturnsToPathPrompt()
    {
        var workflow = CreateWorkflow();
        workflow.Store.ExistingPaths.Add("C:/designer/existing.xml");
        workflow.DocumentController.UpdateText("<UserControl><TextBlock Text=\"Hello\" /></UserControl>");

        _ = workflow.Workflow.Save();

        var submitResult = workflow.Workflow.SubmitPromptPath("C:/designer/existing.xml");

        Assert.Equal(InkkSlinger.Designer.DesignerDocumentPromptKind.OverwriteConfirmation, workflow.Workflow.Prompt.Kind);
        Assert.Equal(InkkSlinger.Designer.DesignerWorkflowCloseAction.None, submitResult.CloseAction);

        var cancelResult = workflow.Workflow.CancelPrompt();

        Assert.True(cancelResult.PromptChanged);
        Assert.Equal(InkkSlinger.Designer.DesignerDocumentPromptKind.SavePath, workflow.Workflow.Prompt.Kind);
        Assert.Equal("C:/designer/existing.xml", workflow.Workflow.Prompt.PathText);
    }

    private static WorkflowHarness CreateWorkflow()
    {
        var store = new FakeDocumentFileStore();
        var documentController = new InkkSlinger.Designer.DesignerDocumentController(DefaultDocument, store);
        var workflow = new InkkSlinger.Designer.DesignerDocumentWorkflowController(documentController);
        return new WorkflowHarness(documentController, workflow, store);
    }

    private sealed record WorkflowHarness(
        InkkSlinger.Designer.DesignerDocumentController DocumentController,
        InkkSlinger.Designer.DesignerDocumentWorkflowController Workflow,
        FakeDocumentFileStore Store);

    private sealed class FakeDocumentFileStore : InkkSlinger.Designer.IDesignerDocumentFileStore
    {
        public HashSet<string> ExistingPaths { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> WrittenTexts { get; } = new(StringComparer.Ordinal);

        public bool Exists(string path)
        {
            return ExistingPaths.Contains(path) || WrittenTexts.ContainsKey(path);
        }

        public void WriteAllText(string path, string text)
        {
            WrittenTexts[path] = text;
            ExistingPaths.Add(path);
        }
    }
}