using Xunit;

namespace InkkSlinger.Tests;

public class DesignerDocumentControllerTests
{
    private const string DefaultDocument = "<UserControl />";

    [Fact]
    public void UpdateText_MarksDocumentDirtyWhenTextChanges()
    {
        var controller = new InkkSlinger.Designer.DesignerDocumentController(DefaultDocument, new FakeDocumentFileStore());

        var changed = controller.UpdateText("<UserControl><Grid /></UserControl>");

        Assert.True(changed);
        Assert.True(controller.IsDirty);
        Assert.Equal("<UserControl><Grid /></UserControl>", controller.CurrentText);
        Assert.Null(controller.CurrentPath);
    }

    [Fact]
    public void New_RestoresTemplateClearsPathAndDirtyState()
    {
        var store = new FakeDocumentFileStore();
        var controller = new InkkSlinger.Designer.DesignerDocumentController(DefaultDocument, store);
        controller.UpdateText("<UserControl><Button /></UserControl>");
        controller.SaveAs("C:/temp/view.xml");
        controller.UpdateText("<UserControl><Grid /></UserControl>");

        controller.New();

        Assert.Equal(DefaultDocument, controller.CurrentText);
        Assert.Null(controller.CurrentPath);
        Assert.False(controller.IsDirty);
        Assert.Equal("Untitled.xml", controller.DisplayName);
    }

    [Fact]
    public void Open_LoadsTextPathAndClearsDirtyState()
    {
        var store = new FakeDocumentFileStore();
        store.ReadTexts["C:/samples/view.xml"] = "<UserControl>\r\n  <Grid />\r\n</UserControl>";
        var controller = new InkkSlinger.Designer.DesignerDocumentController(DefaultDocument, store);
        controller.UpdateText("<UserControl><Button /></UserControl>");

        controller.Open("C:/samples/view.xml");

        Assert.Equal("<UserControl>\n  <Grid />\n</UserControl>", controller.CurrentText);
        Assert.Equal("C:/samples/view.xml", controller.CurrentPath);
        Assert.False(controller.IsDirty);
        Assert.Equal("view.xml", controller.DisplayName);
    }

    [Fact]
    public void Save_WritesCurrentTextToExistingPathAndClearsDirtyState()
    {
        var store = new FakeDocumentFileStore();
        var controller = new InkkSlinger.Designer.DesignerDocumentController(DefaultDocument, store);
        controller.SaveAs("C:/temp/view.xml");
        controller.UpdateText("<UserControl><Grid /></UserControl>");

        var saved = controller.Save();

        Assert.True(saved);
        Assert.Equal("<UserControl><Grid /></UserControl>", store.WrittenTexts["C:/temp/view.xml"]);
        Assert.False(controller.IsDirty);
        Assert.Equal("C:/temp/view.xml", controller.CurrentPath);
    }

    [Fact]
    public void Save_WithoutCurrentPath_ReturnsFalseAndDoesNotWrite()
    {
        var store = new FakeDocumentFileStore();
        var controller = new InkkSlinger.Designer.DesignerDocumentController(DefaultDocument, store);
        controller.UpdateText("<UserControl><Grid /></UserControl>");

        var saved = controller.Save();

        Assert.False(saved);
        Assert.Empty(store.WrittenTexts);
        Assert.True(controller.IsDirty);
    }

    [Fact]
    public void SaveAs_WritesTextUpdatesPathAndClearsDirtyState()
    {
        var store = new FakeDocumentFileStore();
        var controller = new InkkSlinger.Designer.DesignerDocumentController(DefaultDocument, store);
        controller.UpdateText("<UserControl><TextBlock Text=\"Hello\" /></UserControl>");

        controller.SaveAs("C:/temp/other-view.xml");

        Assert.Equal("<UserControl><TextBlock Text=\"Hello\" /></UserControl>", store.WrittenTexts["C:/temp/other-view.xml"]);
        Assert.Equal("C:/temp/other-view.xml", controller.CurrentPath);
        Assert.False(controller.IsDirty);
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