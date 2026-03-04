using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentViewerCommandingTests
{
    [Fact]
    public void EditingCommands_CopyAndSelectAll_AreSupported()
    {
        var viewer = CreateViewer("copy me");
        viewer.SelectAll();

        Assert.True(CommandManager.CanExecute(EditingCommands.Copy, null, viewer));
        Assert.True(CommandManager.CanExecute(EditingCommands.SelectAll, null, viewer));
    }

    [Fact]
    public void EditingMutationCommands_AreNotSupported()
    {
        var viewer = CreateViewer("cannot edit");

        Assert.False(CommandManager.CanExecute(EditingCommands.Cut, null, viewer));
        Assert.False(CommandManager.CanExecute(EditingCommands.Paste, null, viewer));
        Assert.False(CommandManager.CanExecute(EditingCommands.ToggleBold, null, viewer));
    }

    [Fact]
    public void NavigationCommands_ZoomCommands_UpdateState()
    {
        var viewer = CreateViewer("line one\nline two\nline three\nline four\nline five");

        var before = viewer.Zoom;
        Assert.True(CommandManager.CanExecute(NavigationCommands.IncreaseZoom, null, viewer));
        CommandManager.Execute(NavigationCommands.IncreaseZoom, null, viewer);
        Assert.True(viewer.Zoom > before);
    }

    [Fact]
    public void HyperlinkCommand_RoutedCommand_UsesFocusedFallbackTarget()
    {
        FocusManager.ClearFocus();
        try
        {
            var viewer = CreateViewer("doc");
            var target = new TextBox();
            var command = new RoutedCommand("Probe", typeof(DocumentViewerCommandingTests));
            var executions = 0;

            target.CommandBindings.Add(new CommandBinding(command, (_, _) => executions++));
            FocusManager.SetFocus(target);

            var hyperlink = new Hyperlink
            {
                Command = command
            };

            var handled = InvokeHyperlinkActivation(viewer, hyperlink);

            Assert.True(handled);
            Assert.Equal(1, executions);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void HyperlinkCommand_RoutedCommand_ExplicitTarget_Wins()
    {
        FocusManager.ClearFocus();
        try
        {
            var viewer = CreateViewer("doc");
            var focusedTarget = new TextBox();
            var explicitTarget = new TextBox();
            var command = new RoutedCommand("Probe", typeof(DocumentViewerCommandingTests));
            var focusedExecutions = 0;
            var explicitExecutions = 0;

            focusedTarget.CommandBindings.Add(new CommandBinding(command, (_, _) => focusedExecutions++));
            explicitTarget.CommandBindings.Add(new CommandBinding(command, (_, _) => explicitExecutions++));
            FocusManager.SetFocus(focusedTarget);

            var hyperlink = new Hyperlink
            {
                Command = command,
                CommandTarget = explicitTarget
            };

            var handled = InvokeHyperlinkActivation(viewer, hyperlink);

            Assert.True(handled);
            Assert.Equal(0, focusedExecutions);
            Assert.Equal(1, explicitExecutions);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    private static DocumentViewer CreateViewer(string text)
    {
        var viewer = new DocumentViewer();
        viewer.SetLayoutSlot(new LayoutRect(0f, 0f, 360f, 120f));
        var doc = new FlowDocument();
        foreach (var line in text.Split('\n'))
        {
            var p = new Paragraph();
            p.Inlines.Add(new Run(line));
            doc.Blocks.Add(p);
        }

        viewer.Document = doc;
        viewer.Measure(new Microsoft.Xna.Framework.Vector2(360f, 120f));
        return viewer;
    }

    private static bool InvokeHyperlinkActivation(DocumentViewer viewer, Hyperlink hyperlink)
    {
        var method = typeof(DocumentViewer).GetMethod("TryActivateHyperlink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (bool)method!.Invoke(viewer, new object[] { hyperlink })!;
    }
}
