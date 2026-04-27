using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

    private const string AppResourcesXml = """
            <Application xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Application.Resources>
                <ResourceDictionary>
                  <Color x:Key="PreviewPanelColor">#203A56</Color>
                  <SolidColorBrush x:Key="PreviewPanelBrush" Color="{StaticResource PreviewPanelColor}" />
                </ResourceDictionary>
              </Application.Resources>
            </Application>
            """;

    private const string AppResourceBackedViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Border x:Name="AppResourcePanel"
                        Background="{StaticResource PreviewPanelBrush}" />
            </UserControl>
            """;

    private const string DefaultAppResourcesEditorText = """
            <Application xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Application.Resources>
                    <ResourceDictionary>
                    </ResourceDictionary>
                </Application.Resources>
            </Application>
            """;

    private const string UserReportedButtonStyleResource = """
              <Style x:Key="BaseButtonStyle" TargetType="{x:Type Button}">
                <Setter Property="Background" Value="#2A2A2A" />
                <Setter Property="Foreground" Value="#F0F0F0" />
                <Setter Property="BorderBrush" Value="#3F3F3F" />
                <Setter Property="BorderThickness" Value="1" />
                <Setter Property="Padding" Value="20,10" />
                <Setter Property="FontWeight" Value="Medium" />
                <Setter Property="FontSize" Value="13" />
                <Setter Property="Cursor" Value="Hand" />
                <Setter Property="SnapsToDevicePixels" Value="True" />
                <Setter Property="RenderTransformOrigin" Value="0.5,0.5" />
                <Setter Property="RenderTransform">
                  <Setter.Value>
                    <ScaleTransform ScaleX="1" ScaleY="1" />
                  </Setter.Value>
                </Setter>
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="{x:Type Button}">
                      <Border x:Name="border"
                              Background="{TemplateBinding Background}"
                              BorderBrush="{TemplateBinding BorderBrush}"
                              BorderThickness="{TemplateBinding BorderThickness}"
                              CornerRadius="6"
                              Padding="{TemplateBinding Padding}"
                              SnapsToDevicePixels="True">
                        <Border.Effect>
                          <DropShadowEffect x:Name="shadow"
                                            Color="#FF8C00"
                                            ShadowDepth="0"
                                            BlurRadius="0"
                                            Opacity="0" />
                        </Border.Effect>
                        <ContentPresenter HorizontalAlignment="Center"
                                          VerticalAlignment="Center" />
                      </Border>
                      <ControlTemplate.Triggers>
                        <EventTrigger RoutedEvent="MouseEnter">
                          <BeginStoryboard>
                            <Storyboard>
                              <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX"
                                               To="1.03" Duration="0:0:0.15" />
                              <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY"
                                               To="1.03" Duration="0:0:0.15" />
                              <DoubleAnimation Storyboard.TargetName="shadow"
                                               Storyboard.TargetProperty="BlurRadius"
                                               To="12" Duration="0:0:0.15" />
                              <DoubleAnimation Storyboard.TargetName="shadow"
                                               Storyboard.TargetProperty="Opacity"
                                               To="0.5" Duration="0:0:0.15" />
                            </Storyboard>
                          </BeginStoryboard>
                        </EventTrigger>
                        <EventTrigger RoutedEvent="MouseLeave">
                          <BeginStoryboard>
                            <Storyboard>
                              <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX"
                                               To="1.0" Duration="0:0:0.15" />
                              <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY"
                                               To="1.0" Duration="0:0:0.15" />
                              <DoubleAnimation Storyboard.TargetName="shadow"
                                               Storyboard.TargetProperty="BlurRadius"
                                               To="0" Duration="0:0:0.15" />
                              <DoubleAnimation Storyboard.TargetName="shadow"
                                               Storyboard.TargetProperty="Opacity"
                                               To="0" Duration="0:0:0.15" />
                            </Storyboard>
                          </BeginStoryboard>
                        </EventTrigger>
                        <Trigger Property="IsMouseOver" Value="True">
                          <Setter TargetName="border" Property="Background" Value="#333333" />
                          <Setter TargetName="border" Property="BorderBrush" Value="#FF8C00" />
                          <Setter Property="Foreground" Value="#FFA940" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                          <Setter TargetName="border" Property="Background" Value="#FF8C00" />
                          <Setter TargetName="border" Property="BorderBrush" Value="#CC7000" />
                          <Setter Property="Foreground" Value="#1A1A1A" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                          <Setter TargetName="border" Property="Background" Value="#242424" />
                          <Setter TargetName="border" Property="BorderBrush" Value="#333333" />
                          <Setter Property="Foreground" Value="#5A5A5A" />
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Style>
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
    public void Refresh_ValidPanelRoot_SucceedsAndBuildsPreview()
    {
        const string viewXml = """
                <DockPanel xmlns="urn:inkkslinger-ui"
                           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <TextBlock x:Name="PanelChild"
                               Text="Hello from a panel root" />
                </DockPanel>
                """;
        var controller = new InkkSlinger.Designer.DesignerController();

        var succeeded = controller.Refresh(viewXml);

        Assert.True(succeeded);
        var previewRoot = Assert.IsType<DockPanel>(controller.PreviewRoot);
        Assert.Equal(InkkSlinger.Designer.DesignerPreviewState.Success, controller.PreviewState);
        Assert.True(controller.LastRefreshSucceeded);
        Assert.NotNull(controller.VisualTreeRoot);
        Assert.Equal("DockPanel", controller.VisualTreeRoot!.TypeName);
        Assert.Contains(previewRoot.GetVisualChildren(), child => child is TextBlock textBlock && textBlock.Name == "PanelChild");
        Assert.Equal("DockPanel", controller.Inspector.Properties.Single(property => property.Name == "Type").Value);
        Assert.DoesNotContain(controller.Diagnostics, diagnostic => diagnostic.Code == XamlDiagnosticCode.GeneralFailure);
    }

    [Fact]
    public void Refresh_AppResourcesText_ResolvesStaticResourcesForPreview()
    {
        var controller = new InkkSlinger.Designer.DesignerController();

        var succeeded = controller.Refresh(AppResourceBackedViewXml, AppResourcesXml);

        Assert.True(succeeded);
        var root = Assert.IsType<UserControl>(controller.PreviewRoot);
        var border = Assert.IsType<Border>(root.Content);
        var brush = Assert.IsType<SolidColorBrush>(border.Background);
        Assert.Equal(new Color(0x20, 0x3A, 0x56), brush.Color);
        Assert.Empty(controller.Diagnostics);
    }

    [Fact]
    public void Refresh_InvalidAppResourcesText_FailsWithAppResourcesDiagnostic()
    {
        const string invalidResourcesXml = """
                <Application xmlns="urn:inkkslinger-ui"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <Application.Resources>
                        <ResourceDictionary>
                            <SolidColorBrush x:Key="BrokenBrush" NotAProperty="#203A56" />
                        </ResourceDictionary>
                    </Application.Resources>
                </Application>
                """;
        var controller = new InkkSlinger.Designer.DesignerController();

        var succeeded = controller.Refresh(ValidViewXml, invalidResourcesXml);

        Assert.False(succeeded);
        var diagnostic = Assert.Single(controller.Diagnostics, diagnostic =>
            diagnostic.Code == XamlDiagnosticCode.UnknownProperty &&
            diagnostic.Source == InkkSlinger.Designer.DesignerDiagnosticSource.AppResources &&
            diagnostic.Line.HasValue);
        Assert.Equal(InkkSlinger.Designer.DesignerDiagnosticSource.AppResources, diagnostic.Source);
        Assert.Equal("SolidColorBrush.NotAProperty", diagnostic.TargetDescription);
    }

    [Fact]
    public void AppResourcesEditor_BulkStylePaste_ShouldNotRefreshHighlightingOrInspectorSynchronously()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var appResourcesEditorView = shell.AppResourcesEditorView;
        var appResourcesEditor = shell.AppResourcesEditorControl;
        var insertionIndex = appResourcesEditorView.SourceText.IndexOf(
            "        </ResourceDictionary>",
            StringComparison.Ordinal);
        Assert.True(insertionIndex >= 0);
        appResourcesEditor.Select(insertionIndex, 0);

        var before = appResourcesEditorView.GetDesignerSourceEditorViewSnapshotForDiagnostics();
        var beforeEditor = appResourcesEditor.GetIDEEditorSnapshotForDiagnostics();
        TextClipboard.SetText(UserReportedButtonStyleResource);

        appResourcesEditor.Paste();

        var after = appResourcesEditorView.GetDesignerSourceEditorViewSnapshotForDiagnostics();
        var afterEditor = appResourcesEditor.GetIDEEditorSnapshotForDiagnostics();
        Assert.Equal(before.SourceEditorTextChangedCallCount, after.SourceEditorTextChangedCallCount);
        Assert.Equal(beforeEditor.EditorTextChangedCallCount, afterEditor.EditorTextChangedCallCount);
        Assert.Equal(
            beforeEditor.UpdateLineNumberGutterForcedCount,
            afterEditor.UpdateLineNumberGutterForcedCount);
        Assert.Equal(
            before.SourceEditorTextChangedRefreshHighlightedCallCount,
            after.SourceEditorTextChangedRefreshHighlightedCallCount);
        Assert.Equal(
            before.SourceEditorTextChangedRefreshPropertyInspectorCallCount,
            after.SourceEditorTextChangedRefreshPropertyInspectorCallCount);
        Assert.Contains("BaseButtonStyle", appResourcesEditor.DocumentText, StringComparison.Ordinal);
        Assert.True(Dispatcher.PendingDeferredOperationCount > 0);

        Dispatcher.DrainDeferredOperations();

        var afterDeferred = appResourcesEditorView.GetDesignerSourceEditorViewSnapshotForDiagnostics();
        var afterEditorDeferred = appResourcesEditor.GetIDEEditorSnapshotForDiagnostics();
        Assert.Equal(0, Dispatcher.PendingDeferredOperationCount);
        Assert.Equal(before.SourceEditorTextChangedCallCount + 1, afterDeferred.SourceEditorTextChangedCallCount);
        Assert.True(afterEditorDeferred.EditorTextChangedCallCount > beforeEditor.EditorTextChangedCallCount);
        Assert.Contains("BaseButtonStyle", appResourcesEditorView.SourceText, StringComparison.Ordinal);
        Assert.Contains(
            appResourcesEditorView.SourceOverviewItems,
            item => string.Equals(item.Name, "Style", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceEditor_BulkStylePaste_ShouldNotRefreshHighlightingSynchronously()
    {
        var sourceEditorView = new InkkSlinger.Designer.DesignerSourceEditorView
        {
            SourceText = DefaultAppResourcesEditorText
        };
        var sourceEditor = sourceEditorView.Editor;
        var insertionIndex = sourceEditorView.SourceText.IndexOf(
            "        </ResourceDictionary>",
            StringComparison.Ordinal);
        Assert.True(insertionIndex >= 0);
        sourceEditor.Select(insertionIndex, 0);

        var before = sourceEditorView.GetDesignerSourceEditorViewSnapshotForDiagnostics();
        var beforeEditor = sourceEditor.GetIDEEditorSnapshotForDiagnostics();
        TextClipboard.SetText(UserReportedButtonStyleResource);

        sourceEditor.Paste();

        var after = sourceEditorView.GetDesignerSourceEditorViewSnapshotForDiagnostics();
        var afterEditor = sourceEditor.GetIDEEditorSnapshotForDiagnostics();
        Assert.Equal(before.SourceEditorTextChangedCallCount, after.SourceEditorTextChangedCallCount);
        Assert.Equal(beforeEditor.EditorTextChangedCallCount, afterEditor.EditorTextChangedCallCount);
        Assert.Equal(
            beforeEditor.UpdateLineNumberGutterForcedCount,
            afterEditor.UpdateLineNumberGutterForcedCount);
        Assert.Equal(
            before.SourceEditorTextChangedRefreshHighlightedCallCount,
            after.SourceEditorTextChangedRefreshHighlightedCallCount);
        Assert.Contains("BaseButtonStyle", sourceEditor.DocumentText, StringComparison.Ordinal);
        Assert.True(Dispatcher.PendingDeferredOperationCount > 0);

        Dispatcher.DrainDeferredOperations();

        var afterDeferred = sourceEditorView.GetDesignerSourceEditorViewSnapshotForDiagnostics();
        var afterEditorDeferred = sourceEditor.GetIDEEditorSnapshotForDiagnostics();
        Assert.Equal(0, Dispatcher.PendingDeferredOperationCount);
        Assert.Equal(before.SourceEditorTextChangedCallCount + 1, afterDeferred.SourceEditorTextChangedCallCount);
        Assert.True(afterEditorDeferred.EditorTextChangedCallCount > beforeEditor.EditorTextChangedCallCount);
        Assert.Contains("BaseButtonStyle", sourceEditorView.SourceText, StringComparison.Ordinal);
        Assert.Contains(
            sourceEditorView.SourceOverviewItems,
            item => string.Equals(item.Name, "Style", StringComparison.Ordinal));
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
    public void ShellView_SourceEditor_TabKey_RoutedInput_InsertsTwoSpacesInsteadOfTabCharacter()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "alpha"
        };
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);

        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Editor.SetFocusedFromInput(true);
        sourceEditor.Select(sourceEditor.DocumentText.Length, 0);

        var pointer = new Vector2(sourceEditor.Editor.LayoutSlot.X + 4f, sourceEditor.Editor.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Tab, pointer));

        Assert.Equal("alpha  ", sourceEditor.DocumentText);
        Assert.DoesNotContain('\t', sourceEditor.DocumentText);
    }

    [Fact]
    public void ShellView_SourceEditor_BackspaceKey_RoutedInput_DeletesAdjacentSpacePair()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "alpha  "
        };
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);

        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Editor.SetFocusedFromInput(true);
        sourceEditor.Select(sourceEditor.DocumentText.Length, 0);

        var pointer = new Vector2(sourceEditor.Editor.LayoutSlot.X + 4f, sourceEditor.Editor.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Back, pointer));

        Assert.Equal("alpha", sourceEditor.DocumentText);
        Assert.Equal(sourceEditor.DocumentText.Length, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void SourceEditor_TypingSlashAfterOpenStartTagWithAttributes_InsertsClosingBracket()
    {
        const string initialText = "<TextBlock Text=\"Haha\"";
        const string expectedText = "<TextBlock Text=\"Haha\"/>";

        var sourceEditorView = new InkkSlinger.Designer.DesignerSourceEditorView
        {
            SourceText = initialText
        };
        var sourceEditor = sourceEditorView.Editor;
        var uiRoot = new UiRoot(sourceEditorView);
        RunLayout(uiRoot, 640, 240, 16);
        RunLayout(uiRoot, 640, 240, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(initialText.Length, 0);

        Assert.True(sourceEditor.HandleTextCompositionFromInput("/"));
        RunLayout(uiRoot, 640, 240, 16);

        Assert.Equal(expectedText, sourceEditorView.SourceText);
        Assert.Equal(expectedText, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(expectedText.Length, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void SourceEditor_TypingSlashInsideAttributeValue_DoesNotInsertClosingBracket()
    {
        const string initialText = "<TextBlock Text=\"Haha";
        const string expectedText = "<TextBlock Text=\"Haha/";

        var sourceEditorView = new InkkSlinger.Designer.DesignerSourceEditorView
        {
            SourceText = initialText
        };
        var sourceEditor = sourceEditorView.Editor;
        var uiRoot = new UiRoot(sourceEditorView);
        RunLayout(uiRoot, 640, 240, 16);
        RunLayout(uiRoot, 640, 240, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(initialText.Length, 0);

        Assert.True(sourceEditor.HandleTextCompositionFromInput("/"));
        RunLayout(uiRoot, 640, 240, 16);

        Assert.Equal(expectedText, sourceEditorView.SourceText);
        Assert.Equal(expectedText, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(expectedText.Length, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
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
        var appResourcesTab = Assert.IsType<TabItem>(shell.FindName("AppResourcesTab"));
        var sourceLineNumberBorder = shell.SourceLineNumberBorderControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var appResourcesPropertyInspectorSplitter = Assert.IsType<GridSplitter>(shell.AppResourcesEditorView.FindName("SourcePropertyInspectorSplitter"));
        var appResourcesPropertyInspectorBorder = Assert.IsType<Border>(shell.AppResourcesEditorView.FindName("SourcePropertyInspectorBorder"));
        var diagnosticsItemsControl = Assert.IsType<ItemsControl>(shell.FindName("DiagnosticsItemsControl"));

        _ = Assert.IsType<ContentControl>(shell.FindName("PreviewHost"));
        _ = Assert.IsType<ItemsControl>(shell.FindName("VisualTreeView"));
        _ = Assert.IsAssignableFrom<IDE_Editor>(shell.SourceEditorControl);
        _ = Assert.IsAssignableFrom<IDE_Editor>(shell.AppResourcesEditorControl);
        Assert.NotSame(shell.SourceEditorView, shell.AppResourcesEditorView);
        Assert.NotSame(shell.SourceEditorControl, shell.AppResourcesEditorControl);
        Assert.NotNull(shell.AppResourcesEditorView.Minimap);

        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, previewScrollViewer.VerticalScrollBarVisibility);
        Assert.Equal(0, editorTabControl.SelectedIndex);
        Assert.Equal("App Resources", appResourcesTab.Header);
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

        Assert.Equal(1, Grid.GetColumn(sourcePropertyInspectorSplitter));
        Assert.Equal(GridResizeDirection.Columns, sourcePropertyInspectorSplitter.ResizeDirection);
        Assert.Equal(GridResizeBehavior.PreviousAndNext, sourcePropertyInspectorSplitter.ResizeBehavior);
        Assert.Equal(HorizontalAlignment.Stretch, sourcePropertyInspectorSplitter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Stretch, sourcePropertyInspectorSplitter.VerticalAlignment);
        Assert.Equal(2, Grid.GetColumn(sourcePropertyInspectorBorder));

        Assert.Equal(1, Grid.GetColumn(appResourcesPropertyInspectorSplitter));
        Assert.Equal(GridResizeDirection.Columns, appResourcesPropertyInspectorSplitter.ResizeDirection);
        Assert.Equal(GridResizeBehavior.PreviousAndNext, appResourcesPropertyInspectorSplitter.ResizeBehavior);
        Assert.Equal(HorizontalAlignment.Stretch, appResourcesPropertyInspectorSplitter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Stretch, appResourcesPropertyInspectorSplitter.VerticalAlignment);
        Assert.Equal(2, Grid.GetColumn(appResourcesPropertyInspectorBorder));
    }

    [Fact]
    public void ShellView_RefreshPreview_UsesAppResourcesEditorText()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = AppResourceBackedViewXml,
            AppResourcesText = AppResourcesXml
        };

        Assert.True(shell.RefreshPreview());

        var root = Assert.IsType<UserControl>(shell.Controller.PreviewRoot);
        var border = Assert.IsType<Border>(root.Content);
        var brush = Assert.IsType<SolidColorBrush>(border.Background);
        Assert.Equal(new Color(0x20, 0x3A, 0x56), brush.Color);
        Assert.Empty(shell.Controller.Diagnostics);
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

    [Theory]
    [InlineData(
        "RowDefinition",
        """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     Background="#101820">
          <Grid>
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto"
                             MinHeight="32" />
            </Grid.RowDefinitions>
            <TextBlock Text="Probe" />
          </Grid>
        </UserControl>
        """,
        "Height",
        "MinHeight")]
    [InlineData(
        "ColumnDefinition",
        """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     Background="#101820">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="2*"
                                MinWidth="48" />
            </Grid.ColumnDefinitions>
            <TextBlock Text="Probe" />
          </Grid>
        </UserControl>
        """,
        "Width",
        "MinWidth")]
    public void ShellView_SourcePropertyInspector_SelectingNonVisualTag_ShowsApplicableProperties(
        string elementName,
        string sourceText,
        string expectedPrimaryProperty,
        string expectedSecondaryProperty)
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = sourceText
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, elementName);

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        Assert.Contains(expectedPrimaryProperty, propertyEditors.Keys);
        Assert.Contains(expectedSecondaryProperty, propertyEditors.Keys);

        var header = Assert.IsType<TextBlock>(shell.SourceEditorView.FindName("SourcePropertyInspectorHeaderText"));
        Assert.Equal(elementName, header.Text);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_SelectingGradientStop_ShowsApplicableProperties()
    {
        const string gradientStopViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <Border Width="240"
                      Height="80">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0,0"
                                       EndPoint="1,0">
                    <GradientStop Color="#FF3355"
                                  Offset="0" />
                    <GradientStop Color="#33AAFF"
                                  Offset="1" />
                  </LinearGradientBrush>
                </Border.Background>
              </Border>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = gradientStopViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "GradientStop");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        Assert.Contains("Color", propertyEditors.Keys);
        Assert.Contains("Offset", propertyEditors.Keys);

        var header = Assert.IsType<TextBlock>(shell.SourceEditorView.FindName("SourcePropertyInspectorHeaderText"));
        Assert.Equal("GradientStop", header.Text);
    }

        [Fact]
        public void ShellView_SourcePropertyInspector_SelectingSetterInsideInlineStyle_UsesEnclosingOwnerTypeProperties()
        {
                const string inlineStyleSetterViewXml = """
                        <UserControl xmlns="urn:inkkslinger-ui"
                                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                 Background="#101820">
                            <Grid>
                                <TextBlock Text="Designer Preview"
                                                     Foreground="#E7EDF5"
                                                     FontSize="22"
                                                     FontWeight="SemiBold">
                                    <TextBlock.Style>
                                        <Setter Property="FontStyle" Value="Italic" />
                                    </TextBlock.Style>
                                </TextBlock>
                            </Grid>
                        </UserControl>
                        """;

                var shell = new InkkSlinger.Designer.DesignerShellView
                {
                        SourceText = inlineStyleSetterViewXml
                };

                var sourceEditor = shell.SourceEditorControl;
                var uiRoot = new UiRoot(shell);
                RunLayout(uiRoot, 1280, 840, 16);
                RunLayout(uiRoot, 1280, 840, 16);

                SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Setter");

                var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
                Assert.Contains("Text", propertyEditors.Keys);
                Assert.Contains("FontStyle", propertyEditors.Keys);
                Assert.Contains("FontWeight", propertyEditors.Keys);

                var fontStyleEditor = Assert.IsType<ComboBox>(propertyEditors["FontStyle"]);
                Assert.Equal("Italic", fontStyleEditor.SelectedItem as string);

                var header = Assert.IsType<TextBlock>(shell.SourceEditorView.FindName("SourcePropertyInspectorHeaderText"));
                Assert.Equal("Setter", header.Text);
        }

        [Fact]
        public void ShellView_SourcePropertyInspector_EditingInlineStyleSetterProjectedProperty_UpdatesSetterAttributes()
        {
                const string inlineStyleSetterViewXml = """
                        <UserControl xmlns="urn:inkkslinger-ui"
                                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                 Background="#101820">
                            <Grid>
                                <TextBlock Text="Designer Preview"
                                                     Foreground="#E7EDF5"
                                                     FontSize="22"
                                                     FontWeight="SemiBold">
                                    <TextBlock.Style>
                                        <Setter Property="FontStyle" Value="Italic" />
                                    </TextBlock.Style>
                                </TextBlock>
                            </Grid>
                        </UserControl>
                        """;

                var shell = new InkkSlinger.Designer.DesignerShellView
                {
                        SourceText = inlineStyleSetterViewXml
                };

                var sourceEditor = shell.SourceEditorControl;
                var uiRoot = new UiRoot(shell);
                RunLayout(uiRoot, 1280, 840, 16);
                RunLayout(uiRoot, 1280, 840, 16);

                SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Setter");

                var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
                var fontWeightEditor = Assert.IsType<ComboBox>(propertyEditors["FontWeight"]);
                fontWeightEditor.SelectedItem = "Bold";
                RunLayout(uiRoot, 1280, 840, 16);

                Assert.Contains("<Setter Property=\"FontWeight\" Value=\"Bold\" />", shell.SourceText, StringComparison.Ordinal);
                Assert.DoesNotContain("<Setter Property=\"FontWeight\" Value=\"Bold\" />", shell.Controller.SourceText, StringComparison.Ordinal);
        }

        [Fact]
        public void ShellView_SourcePropertyInspector_TypingProjectedSetterNumericValue_DoesNotCorruptSetterPropertyAttribute()
        {
                const string inlineStyleSetterViewXml = """
                        <UserControl xmlns="urn:inkkslinger-ui"
                                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                 Background="#101820">
                            <Grid>
                                <TextBlock Text="Designer Preview"
                                                     Foreground="#E7EDF5"
                                                     FontSize="22"
                                                     FontWeight="SemiBold">
                                    <TextBlock.Style>
                                        <Setter />
                                    </TextBlock.Style>
                                </TextBlock>
                            </Grid>
                        </UserControl>
                        """;

                var shell = new InkkSlinger.Designer.DesignerShellView
                {
                        SourceText = inlineStyleSetterViewXml
                };

                var sourceEditor = shell.SourceEditorControl;
                var uiRoot = new UiRoot(shell);
                RunLayout(uiRoot, 1280, 840, 16);
                RunLayout(uiRoot, 1280, 840, 16);

                SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Setter");

                var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
                var fontSizeEditor = Assert.IsType<TextBox>(propertyEditors["FontSize"]);

                fontSizeEditor.Text = "1";
                RunLayout(uiRoot, 1280, 840, 16);

                var sourceLines = NormalizeLineEndings(shell.SourceText).Split('\n');
                var setterLine = Assert.Single(sourceLines.Where(static line => line.Contains("<Setter", StringComparison.Ordinal)));
                var propertyLine = Assert.Single(sourceLines.Where(static line => line.Contains("Property=\"FontSize\"", StringComparison.Ordinal)));
                var valueLine = Assert.Single(sourceLines.Where(static line => line.Contains("Value=\"1\"", StringComparison.Ordinal)));

                Assert.Equal("<Setter", setterLine.Trim());
                Assert.Equal("Property=\"FontSize\"", propertyLine.TrimStart());
                Assert.Equal("Value=\"1\" />", valueLine.TrimStart());
                Assert.Equal(GetLeadingWhitespaceCount(propertyLine), GetLeadingWhitespaceCount(valueLine));
                Assert.DoesNotContain("Property=\"FontSize/\"", shell.SourceText, StringComparison.Ordinal);
        }

        [Fact]
        public void ShellView_SourceEditor_TypingIntoSetterValue_DoesNotMoveSelfClosingSlashIntoPropertyName()
        {
                const string inlineStyleSetterViewXml = """
                        <UserControl xmlns="urn:inkkslinger-ui"
                                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                 Background="#101820">
                            <Grid>
                                <TextBlock Text="Designer Preview"
                                                     Foreground="#E7EDF5"
                                                     FontSize="22"
                                                     FontWeight="SemiBold">
                                    <TextBlock.Style>
                                        <Setter />
                                    </TextBlock.Style>
                                </TextBlock>
                            </Grid>
                        </UserControl>
                        """;

                var shell = new InkkSlinger.Designer.DesignerShellView
                {
                        SourceText = inlineStyleSetterViewXml
                };

                var sourceEditor = shell.SourceEditorControl;
                var uiRoot = new UiRoot(shell);
                RunLayout(uiRoot, 1280, 840, 16);
                RunLayout(uiRoot, 1280, 840, 16);

                SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Setter");

                var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
                var fontSizeEditor = Assert.IsType<TextBox>(propertyEditors["FontSize"]);

                fontSizeEditor.Text = "1";
                RunLayout(uiRoot, 1280, 840, 16);
                fontSizeEditor.Text = "12";
                RunLayout(uiRoot, 1280, 840, 16);

                var currentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));
                var sourceLines = currentText.Split('\n');
                var setterLine = Assert.Single(sourceLines, static line => line.Contains("<Setter", StringComparison.Ordinal));
                var propertyLine = Assert.Single(sourceLines, static line => line.Contains("Property=\"FontSize\"", StringComparison.Ordinal));
                var valueLine = Assert.Single(sourceLines, static line => line.Contains("Value=\"12\"", StringComparison.Ordinal));

                Assert.Equal("<Setter", setterLine.Trim());
                Assert.Equal("Property=\"FontSize\"", propertyLine.TrimStart());
                Assert.Equal("Value=\"12\" />", valueLine.TrimStart());
                Assert.Equal(GetLeadingWhitespaceCount(propertyLine), GetLeadingWhitespaceCount(valueLine));
                Assert.DoesNotContain("Property=\"FontSize/\"", currentText, StringComparison.Ordinal);
                Assert.DoesNotContain("Value=\"1\">", currentText, StringComparison.Ordinal);
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
    public void ShellView_SourceEditor_RecordedResizeScrollThenMaximize_ShouldPreserveViewportAndGutter()
    {
        int[] recordedWheelDeltas =
        [
            -120,
            -240,
            -120,
            -120,
            -120,
            -120,
            -360,
            -120,
            -240,
            -120,
            -240,
            -240,
            -240,
            -120,
            -360,
            -240,
            -240,
            -120,
            -360,
            -240,
            -240,
            -120,
            -360,
            -360,
            -120,
            -120,
            -120,
            -120
        ];

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildNumberedSource(160)
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var previewSourceSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewSourceSplitter"));
        var sourcePropertyInspectorSplitter = Assert.IsType<GridSplitter>(shell.SourceEditorView.FindName("SourcePropertyInspectorSplitter"));
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 820, 16);
        RunLayout(uiRoot, 1280, 820, 16);

        var previewStart = GetCenter(previewSourceSplitter.LayoutSlot);
        var previewEnd = previewStart + new Vector2(130f, -265f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(previewStart, pointerMoved: true));
        RunLayout(uiRoot, 1280, 820, 16);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(previewStart, leftPressed: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 820, 16);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(previewEnd, pointerMoved: true));
        RunLayout(uiRoot, 1280, 820, 16);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(previewEnd, leftReleased: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 820, 16);

        var inspectorStart = GetCenter(sourcePropertyInspectorSplitter.LayoutSlot);
        var inspectorEnd = inspectorStart + new Vector2(-203f, 15f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inspectorStart, pointerMoved: true));
        RunLayout(uiRoot, 1280, 820, 16);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inspectorStart, leftPressed: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 820, 16);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inspectorEnd, pointerMoved: true));
        RunLayout(uiRoot, 1280, 820, 16);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(inspectorEnd, leftReleased: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 820, 16);

        var pointer = GetSourceEditorLinePoint(sourceEditor, 1);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 820, 16);

        for (var i = 0; i < recordedWheelDeltas.Length; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, recordedWheelDeltas[i]));
            RunLayout(uiRoot, 1280, 820, 16);
        }

        var verticalOffsetBeforeMaximize = sourceEditor.VerticalOffset;
        Assert.True(verticalOffsetBeforeMaximize > 0f, "Expected the recorded wheel-scroll segment to move the source editor viewport before maximize.");
        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);
        Assert.True(int.TryParse(GetLineNumberText(sourceLineNumberPanel, 0), out var firstRenderedLineNumberBeforeMaximize));
        Assert.Equal(sourceLineNumberPanel.FirstVisibleLine + 1, firstRenderedLineNumberBeforeMaximize);

        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        shell.ClearRenderInvalidationRecursive();

        RunLayout(uiRoot, 1920, 1080, 16);
        RunLayout(uiRoot, 1920, 1080, 16);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
        var verticalOffsetAfterMaximize = sourceEditor.VerticalOffset;
        Assert.InRange(
            MathF.Abs(verticalOffsetAfterMaximize - verticalOffsetBeforeMaximize),
            0f,
            0.5f);
        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);
        Assert.True(int.TryParse(GetLineNumberText(sourceLineNumberPanel, 0), out var firstRenderedLineNumberAfterMaximize));
        Assert.Equal(sourceLineNumberPanel.FirstVisibleLine + 1, firstRenderedLineNumberAfterMaximize);
        Assert.InRange(
            Math.Abs(firstRenderedLineNumberAfterMaximize - firstRenderedLineNumberBeforeMaximize),
            0,
            1);
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
    public void ShellView_SourcePropertyInspector_CornerRadius_UsesFourPartEditorAndExpandsUniformValue()
    {
        const string borderViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                        CornerRadius="16"
                        BorderBrush="#334455"
                        BorderThickness="1" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = borderViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");

        var cornerRadiusEditors = GetSourceInspectorCompositeTextEditors(shell.SourceEditorView, "CornerRadius");
        Assert.Equal(4, cornerRadiusEditors.Count);
        Assert.All(cornerRadiusEditors, editor => Assert.Equal("16", editor.Text));
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_ThicknessLikeProperty_UsesFourPartEditorAndUpdatesSourceWithoutRefreshingPreview()
    {
        const string buttonViewXml = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <StackPanel>
                <Button x:Name="SaveButton"
                        Content="Save"
                        Padding="8" />
              </StackPanel>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = buttonViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Button");

        var paddingEditors = GetSourceInspectorCompositeTextEditors(shell.SourceEditorView, "Padding");
        Assert.Equal(4, paddingEditors.Count);
        Assert.All(paddingEditors, editor => Assert.Equal("8", editor.Text));

        paddingEditors[1].Text = "12";
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("Padding=\"8,12,8,8\"", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("Padding=\"8,12,8,8\"", shell.Controller.SourceText, StringComparison.Ordinal);
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

        var sourceLines = NormalizeLineEndings(shell.SourceText).Split('\n');
        var contentLine = Assert.Single(sourceLines, static line => line.Contains("Content=\"Save\"", StringComparison.Ordinal));
        var widthLine = Assert.Single(sourceLines, static line => line.Contains("Width=\"180\"", StringComparison.Ordinal));

        Assert.Equal("Width=\"180\" />", widthLine.TrimStart());
        Assert.True(GetLeadingWhitespaceCount(widthLine) > 0, $"Expected inserted Width attribute to stay indented on its own line, but line was '{widthLine}'.");
        Assert.True(Array.IndexOf(sourceLines, widthLine) >= Array.IndexOf(sourceLines, contentLine), $"Expected inserted Width attribute to remain with the Button tag block, but contentLine='{contentLine}' widthLine='{widthLine}'.");
        Assert.DoesNotContain("Width=\"180\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_AddingMissingProperty_ToSingleLineSelfClosingTag_InsertsNewIndentedLine()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = ValidViewXml
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "TextBlock");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var widthEditor = Assert.IsType<TextBox>(propertyEditors["Width"]);
        Assert.True(string.IsNullOrEmpty(widthEditor.Text));

        widthEditor.Text = "180";
        RunLayout(uiRoot, 1280, 840, 16);

        var sourceLines = NormalizeLineEndings(shell.SourceText).Split('\n');
        var textBlockLine = Assert.Single(sourceLines.Where(static line => line.Contains("<TextBlock Text=\"Hello designer\"", StringComparison.Ordinal)));
        var widthLine = Assert.Single(sourceLines.Where(static line => line.Contains("Width=\"180\"", StringComparison.Ordinal)));

        Assert.Equal("Width=\"180\" />", widthLine.TrimStart());
        Assert.Equal(textBlockLine.IndexOf("Text=", StringComparison.Ordinal), GetLeadingWhitespaceCount(widthLine));
        Assert.DoesNotContain("Width=\"180\"", shell.Controller.SourceText, StringComparison.Ordinal);
    }

        [Fact]
        public void ShellView_SourcePropertyInspector_GridLengthProperty_UsesHybridTextAndPresetEditors()
        {
                const string rowDefinitionViewXml = """
                        <UserControl xmlns="urn:inkkslinger-ui"
                                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                 Background="#101820">
                            <Grid>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"
                                                                 MinHeight="32" />
                                </Grid.RowDefinitions>
                                <TextBlock Text="Probe" />
                            </Grid>
                        </UserControl>
                        """;

                var shell = new InkkSlinger.Designer.DesignerShellView
                {
                        SourceText = rowDefinitionViewXml
                };

                var sourceEditor = shell.SourceEditorControl;
                var uiRoot = new UiRoot(shell);
                RunLayout(uiRoot, 1280, 840, 16);
                RunLayout(uiRoot, 1280, 840, 16);

                SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "RowDefinition");

                var editors = GetSourceInspectorTextChoiceEditors(shell.SourceEditorView, "Height");
                var presetValues = Assert.IsAssignableFrom<IReadOnlyList<string>>(editors.ComboBox.ItemsSource);

                Assert.Equal("Auto", editors.TextBox.Text);
                Assert.Equal("Auto", editors.ComboBox.SelectedItem as string);
                Assert.Equal(["Auto", "*"], presetValues.ToArray());
        }

        [Fact]
        public void ShellView_SourcePropertyInspector_SelectingGridLengthPreset_UpdatesSourceWithoutRefreshingPreview()
        {
                const string columnDefinitionViewXml = """
                        <UserControl xmlns="urn:inkkslinger-ui"
                                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                 Background="#101820">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="120" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Text="Probe" />
                            </Grid>
                        </UserControl>
                        """;

                var shell = new InkkSlinger.Designer.DesignerShellView
                {
                        SourceText = columnDefinitionViewXml
                };

                var sourceEditor = shell.SourceEditorControl;
                var uiRoot = new UiRoot(shell);
                RunLayout(uiRoot, 1280, 840, 16);
                RunLayout(uiRoot, 1280, 840, 16);

                SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "ColumnDefinition");

                var editors = GetSourceInspectorTextChoiceEditors(shell.SourceEditorView, "Width");
                editors.ComboBox.SelectedItem = "*";
                RunLayout(uiRoot, 1280, 840, 16);

                Assert.Contains("<ColumnDefinition Width=\"*\" />", shell.SourceText, StringComparison.Ordinal);
                Assert.DoesNotContain("<ColumnDefinition Width=\"*\" />", shell.Controller.SourceText, StringComparison.Ordinal);
                Assert.Equal("*", editors.TextBox.Text);
        }

            [Fact]
            public void ShellView_SourcePropertyInspector_FrameworkWidth_UsesHybridTextAndAutoPreset()
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

                var editors = GetSourceInspectorTextChoiceEditors(shell.SourceEditorView, "Width");
                var presetValues = Assert.IsAssignableFrom<IReadOnlyList<string>>(editors.ComboBox.ItemsSource);

                Assert.True(string.IsNullOrEmpty(editors.TextBox.Text));
                Assert.Null(editors.ComboBox.SelectedItem);
                Assert.Equal(["Auto"], presetValues.ToArray());

                editors.ComboBox.SelectedItem = "Auto";
                RunLayout(uiRoot, 1280, 840, 16);

                Assert.Contains("Width=\"Auto\"", shell.SourceText, StringComparison.Ordinal);
                Assert.DoesNotContain("Width=\"Auto\"", shell.Controller.SourceText, StringComparison.Ordinal);
                Assert.Equal("Auto", editors.TextBox.Text);
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
        var fontWeightChoices = Assert.IsAssignableFrom<IReadOnlyList<string>>(fontWeightEditor.ItemsSource);

        Assert.Contains("Normal", fontWeightChoices);
        Assert.Contains("Bold", fontWeightChoices);
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
        var cursorChoices = Assert.IsAssignableFrom<IReadOnlyList<string>>(cursorEditor.ItemsSource);

        Assert.Contains("Arrow", cursorChoices);
        Assert.Contains("Hand", cursorChoices);
        Assert.Contains("IBeam", cursorChoices);
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

        Assert.Equal(new Color(8, 16, 24), fontWeightEditor.Background);
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
        var cursorChoices = Assert.IsAssignableFrom<IReadOnlyList<string>>(cursorEditor.ItemsSource);
        var fontWeightChoices = Assert.IsAssignableFrom<IReadOnlyList<string>>(fontWeightEditor.ItemsSource);

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

        Assert.True(cursorChoices.Count > fontWeightChoices.Count);
        Assert.True(cursorDropDown.LayoutSlot.Height + 0.5f >= fontWeightDropDown.LayoutSlot.Height,
            $"Expected Cursor dropdown to be at least as tall as FontWeight once both choice lists apply the viewport cap. cursorHeight={cursorDropDown.LayoutSlot.Height:0.##} fontWeightHeight={fontWeightDropDown.LayoutSlot.Height:0.##} cursorItems={cursorChoices.Count} fontWeightItems={fontWeightChoices.Count}");
        Assert.True(cursorScrollViewer.ExtentHeight > cursorScrollViewer.ViewportHeight + 40f,
            $"Expected Cursor dropdown content to exceed the viewport cap. extent={cursorScrollViewer.ExtentHeight:0.##} viewport={cursorScrollViewer.ViewportHeight:0.##} items={cursorChoices.Count}");
        Assert.InRange(fontWeightScrollViewer.ViewportHeight - fontWeightScrollViewer.ExtentHeight, -2.5f, 6f);
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
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_WhenRecordedColorPickerClickIsApplied_ShouldSelectClickedPoint()
    {
        var scenario = CreateRecordedColorEditorScenario("#E7EDF5");
        var initialPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        var pointer = new Vector2(
            initialPicker.SpectrumRect.X + (initialPicker.SpectrumRect.Width * 0.20817845f),
            initialPicker.SpectrumRect.Y + (initialPicker.SpectrumRect.Height * 0.6142857f));
        var expectedSaturation = ColorControlUtilities.Clamp01((pointer.X - initialPicker.SpectrumRect.X) / initialPicker.SpectrumRect.Width);
        var expectedValue = 1f - ColorControlUtilities.Clamp01((pointer.Y - initialPicker.SpectrumRect.Y) / initialPicker.SpectrumRect.Height);
        var expectedColor = ColorControlUtilities.FromHsva(initialPicker.Hue, expectedSaturation, expectedValue, initialPicker.Alpha);

        var hovered = VisualTreeHelper.HitTest(scenario.Shell, pointer);
        Assert.NotNull(FindSelfOrAncestor<ColorPicker>(hovered));

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var pressedPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        Assert.True(pressedPicker.IsDragging);
        AssertClose(pointer.X, pressedPicker.SaturationSelector.X, 1.2f);
        AssertClose(pointer.Y, pressedPicker.SaturationSelector.Y, 1.2f);

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var releasedPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        Assert.False(releasedPicker.IsDragging);
        AssertClose(pointer.X, releasedPicker.SaturationSelector.X, 1.2f);
        AssertClose(pointer.Y, releasedPicker.SaturationSelector.Y, 1.2f);
        AssertClose(expectedSaturation, releasedPicker.Saturation, 0.01f);
        AssertClose(expectedValue, releasedPicker.Value, 0.01f);
        Assert.Equal(expectedColor.PackedValue, releasedPicker.SelectedColor.PackedValue);
        Assert.Equal(expectedColor.PackedValue, scenario.EditorHost.SelectedColorValue.PackedValue);
        Assert.Equal(expectedColor.PackedValue, scenario.EditorHost.CurrentSelectedColor.PackedValue);
        Assert.Contains(
            $"Background=\"{FormatDesignerColorValue(expectedColor)}\"",
            scenario.Shell.SourceText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_WhenRecordedHueSpectrumClickIsApplied_ShouldSelectClickedPoint()
    {
        var scenario = CreateRecordedColorEditorScenario("#0B1620");
        var initialPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        var initialHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        var pointer = new Vector2(
            initialHue.SpectrumRect.X + (initialHue.SpectrumRect.Width * 0.62267655f),
            initialHue.SpectrumRect.Y + (initialHue.SpectrumRect.Height * 0.5f));
        var expectedHue = ColorControlUtilities.NormalizeHue(
            ColorControlUtilities.Clamp01((pointer.X - initialHue.SpectrumRect.X) / initialHue.SpectrumRect.Width) * 360f);
        var expectedColor = ColorControlUtilities.FromHsva(expectedHue, initialHue.Saturation, initialHue.Value, initialHue.Alpha);

        var hovered = VisualTreeHelper.HitTest(scenario.Shell, pointer);
        Assert.NotNull(FindSelfOrAncestor<ColorSpectrum>(hovered));

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var pressedHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        Assert.True(pressedHue.IsDragging);
        AssertClose(pointer.X, pressedHue.SelectorPosition, 1.2f);

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var releasedPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        var releasedHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        Assert.False(releasedHue.IsDragging);
        AssertClose(pointer.X, releasedHue.SelectorPosition, 1.2f);
        AssertClose(expectedHue, releasedHue.Hue, 0.5f);
        Assert.NotEqual(initialPicker.SelectedColor.PackedValue, releasedPicker.SelectedColor.PackedValue);
        AssertClose(initialPicker.SaturationSelector.X, releasedPicker.SaturationSelector.X, 0.01f);
        AssertClose(initialPicker.SaturationSelector.Y, releasedPicker.SaturationSelector.Y, 0.01f);
        AssertClose(initialPicker.Saturation, releasedPicker.Saturation, 0.0001f);
        AssertClose(initialPicker.Value, releasedPicker.Value, 0.0001f);
        Assert.Equal(expectedColor.PackedValue, releasedHue.SelectedColor.PackedValue);
        Assert.Equal(expectedColor.PackedValue, releasedPicker.SelectedColor.PackedValue);
        Assert.Equal(expectedColor.PackedValue, scenario.EditorHost.SelectedColorValue.PackedValue);
        Assert.Equal(expectedColor.PackedValue, scenario.EditorHost.CurrentSelectedColor.PackedValue);
        Assert.Contains(
            $"Background=\"{FormatDesignerColorValue(expectedColor)}\"",
            scenario.Shell.SourceText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_WhenRecordedHueSpectrumClickIsReleased_ShouldPreservePointerDerivedHueWithoutExtraPointerUpdate()
    {
        var scenario = CreateRecordedColorEditorScenario("#0B1620");
        var initialHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        var pointer = new Vector2(
            initialHue.SpectrumRect.X + (initialHue.SpectrumRect.Width * 0.62267655f),
            initialHue.SpectrumRect.Y + (initialHue.SpectrumRect.Height * 0.5f));
        var expectedHue = ColorControlUtilities.NormalizeHue(
            ColorControlUtilities.Clamp01((pointer.X - initialHue.SpectrumRect.X) / initialHue.SpectrumRect.Width) * 360f);
        var expectedColor = ColorControlUtilities.FromHsva(expectedHue, initialHue.Saturation, initialHue.Value, initialHue.Alpha);
        ColorControlUtilities.ToHsva(expectedColor, out var byteRoundTripHue, out _, out _, out _);

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var pressedHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
        var postReleaseHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var releasedHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();

        AssertClose(initialHue.SpectrumRect.X, pressedHue.SpectrumRect.X, 0.01f);
        AssertClose(initialHue.SpectrumRect.X, releasedHue.SpectrumRect.X, 0.01f);
        AssertClose(initialHue.SpectrumRect.Width, pressedHue.SpectrumRect.Width, 0.01f);
        AssertClose(initialHue.SpectrumRect.Width, releasedHue.SpectrumRect.Width, 0.01f);
        Assert.Equal(initialHue.UpdateFromPointerCallCount + 1, pressedHue.UpdateFromPointerCallCount);
        Assert.Equal(pressedHue.UpdateFromPointerCallCount, postReleaseHue.UpdateFromPointerCallCount);
        Assert.Equal(pressedHue.UpdateFromPointerCallCount, releasedHue.UpdateFromPointerCallCount);
        AssertClose(expectedHue, pressedHue.Hue, 0.5f);
        Assert.Equal(pressedHue.SyncSelectedColorFromComponentsCallCount + 1, postReleaseHue.SyncSelectedColorFromComponentsCallCount);
        Assert.Equal(pressedHue.SelectedColorChangedComponentWritebackCount + 1, postReleaseHue.SelectedColorChangedComponentWritebackCount);
        Assert.Equal(pressedHue.SelectedColorChangedExternalSyncCount, postReleaseHue.SelectedColorChangedExternalSyncCount);
        Assert.NotInRange(byteRoundTripHue, expectedHue - 0.5f, expectedHue + 0.5f);
        AssertClose(expectedHue, postReleaseHue.Hue, 0.5f);
        AssertClose(expectedHue, releasedHue.Hue, 0.5f);
        AssertClose(postReleaseHue.Hue, releasedHue.Hue, 0.01f);
        Assert.Equal(expectedColor.PackedValue, releasedHue.SelectedColor.PackedValue);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_AfterBottomCornerColorPickerSelection_HueChange_ShouldNotMoveColorPickerSelector()
    {
        var scenario = CreateRecordedColorEditorScenario("#0B1620");
        var initialPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        var colorPickerPointer = new Vector2(
            initialPicker.SpectrumRect.X + (initialPicker.SpectrumRect.Width * 0.95f),
            initialPicker.SpectrumRect.Y + (initialPicker.SpectrumRect.Height * 0.95f));

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(colorPickerPointer, pointerMoved: true));
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(colorPickerPointer, leftPressed: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(colorPickerPointer, leftReleased: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var selectedPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        AssertClose(colorPickerPointer.X, selectedPicker.SaturationSelector.X, 1.2f);
        AssertClose(colorPickerPointer.Y, selectedPicker.SaturationSelector.Y, 1.2f);

        var initialHue = scenario.HueSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        var huePointer = new Vector2(
            initialHue.SpectrumRect.X + (initialHue.SpectrumRect.Width * 0.62267655f),
            initialHue.SpectrumRect.Y + (initialHue.SpectrumRect.Height * 0.5f));

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(huePointer, pointerMoved: true));
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(huePointer, leftPressed: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(huePointer, leftReleased: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var releasedPicker = scenario.ColorPicker.GetColorPickerSnapshotForDiagnostics();
        Assert.NotEqual(selectedPicker.SelectedColor.PackedValue, releasedPicker.SelectedColor.PackedValue);
        AssertClose(selectedPicker.SaturationSelector.X, releasedPicker.SaturationSelector.X, 0.01f);
        AssertClose(selectedPicker.SaturationSelector.Y, releasedPicker.SaturationSelector.Y, 0.01f);
        AssertClose(selectedPicker.Saturation, releasedPicker.Saturation, 0.0001f);
        AssertClose(selectedPicker.Value, releasedPicker.Value, 0.0001f);
    }

    [Fact]
    public void ShellView_SourcePropertyInspector_BorderBackgroundColorEditor_WhenRecordedAlphaSpectrumClickIsApplied_ShouldSelectClickedPoint()
    {
        var scenario = CreateRecordedColorEditorScenario("#0B2011");
        var initialAlpha = scenario.AlphaSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        var pointer = new Vector2(
            initialAlpha.SpectrumRect.X + (initialAlpha.SpectrumRect.Width * 0.33271375f),
            initialAlpha.SpectrumRect.Y + (initialAlpha.SpectrumRect.Height * 0.5f));
        var expectedAlpha = ColorControlUtilities.Clamp01((pointer.X - initialAlpha.SpectrumRect.X) / initialAlpha.SpectrumRect.Width);
        var expectedColor = ColorControlUtilities.FromHsva(initialAlpha.Hue, initialAlpha.Saturation, initialAlpha.Value, expectedAlpha);

        var hovered = VisualTreeHelper.HitTest(scenario.Shell, pointer);
        Assert.NotNull(FindSelfOrAncestor<ColorSpectrum>(hovered));

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var pressedAlpha = scenario.AlphaSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        Assert.True(pressedAlpha.IsDragging);
        AssertClose(pointer.X, pressedAlpha.SelectorPosition, 1.2f);

        scenario.UiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
        RunLayout(scenario.UiRoot, 1280, 820, 16);

        var releasedAlpha = scenario.AlphaSpectrum.GetColorSpectrumSnapshotForDiagnostics();
        Assert.False(releasedAlpha.IsDragging);
        AssertClose(pointer.X, releasedAlpha.SelectorPosition, 1.2f);
        AssertClose(expectedAlpha, releasedAlpha.Alpha, 0.01f);
        Assert.Equal(expectedColor.PackedValue, releasedAlpha.SelectedColor.PackedValue);
        Assert.Equal(expectedColor.PackedValue, scenario.EditorHost.SelectedColorValue.PackedValue);
        Assert.Equal(expectedColor.PackedValue, scenario.EditorHost.CurrentSelectedColor.PackedValue);
        Assert.Contains(
            $"Background=\"{FormatDesignerColorValue(expectedColor)}\"",
            scenario.Shell.SourceText,
            StringComparison.Ordinal);
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

        Assert.True(
            sourceEditor.VerticalOffset > 0f,
            $"Expected wheel scrolling to move the source editor viewport, but offset={sourceEditor.VerticalOffset:0.###}, scrollable={sourceEditor.ScrollableHeight:0.###}, viewport={sourceEditor.ViewportHeight:0.###}, extent={sourceEditor.ExtentHeight:0.###}, pointer=({pointer.X:0.###},{pointer.Y:0.###}).");
        Assert.True(
            MathF.Abs(sourceEditor.ScrollableHeight - sourceEditor.VerticalOffset) <= sourceEditor.EstimatedLineHeight + 0.5f,
            $"Expected repeated wheel scrolling to reach the bounded tail range of the source editor, but offset={sourceEditor.VerticalOffset:0.###} scrollable={sourceEditor.ScrollableHeight:0.###} lineHeight={sourceEditor.EstimatedLineHeight:0.###} iterations={iterations}.");
        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) > 0);
        Assert.True(int.TryParse(GetLineNumberText(sourceLineNumberPanel, 0), out var firstRenderedLineNumber));
        Assert.Equal(sourceLineNumberPanel.FirstVisibleLine + 1, firstRenderedLineNumber);
        Assert.True(
            firstRenderedLineNumber > 10,
            $"Expected the line-number gutter to follow the wheel-scrolled viewport near the end of the document, but the first rendered line stayed at {firstRenderedLineNumber}. offset={sourceEditor.VerticalOffset:0.###} scrollable={sourceEditor.ScrollableHeight:0.###}.");
    }

    [Fact]
    public void ShellView_SourceEditor_WhenWheelScrolledWhileUnfocused_ClickShouldPlaceCaretOnClickedVisibleLine()
    {
        var source = BuildNumberedSource(160);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var pointer = GetSourceEditorLinePoint(sourceEditor, 1);
        uiRoot.SetFocusedElementForTests(null);
        sourceEditor.SetFocusedFromInput(false);
        sourceEditor.Editor.SetFocusedFromInput(false);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        for (var i = 0; i < 10; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        Assert.True(sourceEditor.VerticalOffset > 0f, $"Expected wheel scrolling to move the source editor while unfocused, but offset={sourceEditor.VerticalOffset:0.###}.");
        Assert.False(sourceEditor.IsFocused, "Expected the outer IDE_Editor to remain unfocused before the click repro.");
        Assert.False(sourceEditor.Editor.IsFocused, "Expected the inner RichTextBox to remain unfocused before the click repro.");
        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) >= 6, $"Expected several visible gutter lines after scrolling, but rendered={GetRenderedLineNumberCount(sourceLineNumberPanel)}.");
        Assert.True(sourceEditor.Editor.TryGetViewportLayoutSnapshot(out var viewportSnapshot));
        var visibleLines = GetVisibleViewportLines(viewportSnapshot).ToArray();
        Assert.True(visibleLines.Length >= 6, $"Expected several visible layout lines after scrolling, but visibleCount={visibleLines.Length}, totalCount={viewportSnapshot.Layout.Lines.Count}.");

        var targetLine = visibleLines[3];
        var clickPoint = GetVisibleSourceEditorLineClickPoint(viewportSnapshot, targetLine);
        Click(uiRoot, clickPoint);
        RunLayout(uiRoot, 1280, 840, 16);

        var expectedSelectionStart = targetLine.StartOffset;

        Assert.Equal(expectedSelectionStart, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditor_WhenWheelScrolledWhileFocused_ClickShouldPlaceCaretOnClickedVisibleLine()
    {
        var source = BuildNumberedSource(160);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var pointer = GetSourceEditorLinePoint(sourceEditor, 1);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Editor.SetFocusedFromInput(true);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, 1280, 840, 16);

        for (var i = 0; i < 10; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        Assert.True(sourceEditor.VerticalOffset > 0f, $"Expected wheel scrolling to move the focused source editor, but offset={sourceEditor.VerticalOffset:0.###}.");
        Assert.True(sourceEditor.Editor.IsFocused, "Expected the inner RichTextBox to stay focused for the focused control scenario.");
        Assert.True(GetRenderedLineNumberCount(sourceLineNumberPanel) >= 6, $"Expected several visible gutter lines after scrolling, but rendered={GetRenderedLineNumberCount(sourceLineNumberPanel)}.");
        Assert.True(sourceEditor.Editor.TryGetViewportLayoutSnapshot(out var viewportSnapshot));
        var visibleLines = GetVisibleViewportLines(viewportSnapshot).ToArray();
        Assert.True(visibleLines.Length >= 6, $"Expected several visible layout lines after scrolling, but visibleCount={visibleLines.Length}, totalCount={viewportSnapshot.Layout.Lines.Count}.");

        var targetLine = visibleLines[3];
        var clickPoint = GetVisibleSourceEditorLineClickPoint(viewportSnapshot, targetLine);
        Click(uiRoot, clickPoint);
        RunLayout(uiRoot, 1280, 840, 16);

        var expectedSelectionStart = targetLine.StartOffset;

        Assert.Equal(expectedSelectionStart, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
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
    public void ShellView_SourceEditorLineNumberGutter_WithHorizontalScrollbar_ShouldStayInsideTextViewport()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildWideNumberedSource(80)
        };

        var sourceEditor = shell.SourceEditorControl;
        var sourceLineNumberPanel = shell.SourceLineNumberPanelControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 960, 520, 16);
        RunLayout(uiRoot, 960, 520, 16);

        Assert.True(
            sourceEditor.ScrollableWidth > 0f,
            $"Expected wide Designer source text to show a horizontal scrollbar, but scrollableWidth={sourceEditor.ScrollableWidth:0.###}, viewportWidth={sourceEditor.ViewportWidth:0.###}, extentWidth={sourceEditor.ExtentWidth:0.###}.");
        Assert.True(sourceEditor.Editor.TryGetViewportLayoutSnapshot(out var viewportSnapshot));

        var textViewportBottom = viewportSnapshot.TextRect.Y + viewportSnapshot.TextRect.Height;
        var lineNumberBorderBounds = ResolveRenderedBounds(sourceEditor.LineNumberBorder);
        var overflowingRows = sourceLineNumberPanel.GetVisualChildren()
            .OfType<TextBlock>()
            .Select(ResolveRenderedBounds)
            .Where(bounds => bounds.Y + bounds.Height > textViewportBottom + 0.5f)
            .ToArray();

        Assert.True(
            overflowingRows.Length == 0,
            $"Expected Designer source-editor line numbers to stop at the RichTextBox text viewport when the horizontal scrollbar is visible, but {overflowingRows.Length} gutter rows extended into the scrollbar band. textViewport={FormatRect(viewportSnapshot.TextRect)} gutterBounds={FormatRect(lineNumberBorderBounds)} overflowingRows=[{string.Join("; ", overflowingRows.Select(FormatRect))}].");
    }

    [Fact]
    public void ShellView_SourceEditorLineNumberGutter_WithHorizontalScrollbar_ShouldClipToTextViewport()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = BuildWideNumberedSource(80)
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 960, 520, 16);
        RunLayout(uiRoot, 960, 520, 16);

        Assert.True(
            sourceEditor.ScrollableWidth > 0f,
            $"Expected wide Designer source text to show a horizontal scrollbar, but scrollableWidth={sourceEditor.ScrollableWidth:0.###}, viewportWidth={sourceEditor.ViewportWidth:0.###}, extentWidth={sourceEditor.ExtentWidth:0.###}.");
        Assert.True(sourceEditor.Editor.TryGetViewportLayoutSnapshot(out var viewportSnapshot));

        var textViewportBottom = viewportSnapshot.TextRect.Y + viewportSnapshot.TextRect.Height;
        var lineNumberBorderBounds = ResolveRenderedBounds(sourceEditor.LineNumberBorder);

        Assert.True(
            lineNumberBorderBounds.Y + lineNumberBorderBounds.Height <= textViewportBottom + 0.5f,
            $"Expected the Designer source-editor gutter clip to end at the text viewport instead of covering the horizontal scrollbar band, but gutterBounds={FormatRect(lineNumberBorderBounds)} and textViewport={FormatRect(viewportSnapshot.TextRect)}.");
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

        Assert.Equal(2, editorTabControl.SelectedIndex);
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

        Assert.Same(sourceEditor, FindSelfOrAncestor<IDE_Editor>(uiRoot.GetHoveredElementForDiagnostics()));

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F5, sourceHoverPoint));
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.False(shell.Controller.LastRefreshSucceeded);
        Assert.Equal(2, editorTabControl.SelectedIndex);
        Assert.Contains("(!", diagnosticsTab.Header, StringComparison.Ordinal);
        Assert.NotSame(sourceEditor, FindSelfOrAncestor<IDE_Editor>(uiRoot.GetHoveredElementForDiagnostics()));
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
        Assert.Equal(2, editorTabControl.SelectedIndex);

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
        Assert.Equal(2, editorTabControl.SelectedIndex);

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
    public void ShellView_SourceEditor_DeletingCharacterOnLaterLine_ShouldPreserveEarlierNewlines()
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

        var deleteIndex = source.IndexOf("Line 20", StringComparison.Ordinal);
        Assert.True(deleteIndex >= 0);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(deleteIndex, 1);

        var expected = source.Remove(deleteIndex, 1);

        Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.None));

        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(GetLineText(source, 10), GetLineText(shell.SourceText, 10));
        Assert.Equal(GetLineText(source, 19), GetLineText(shell.SourceText, 19));
        Assert.Equal(CountLogicalLines(source), CountLogicalLines(shell.SourceText));
    }

    [Fact]
    public void ShellView_SourceEditor_DeletingWholePropertyLine_ShouldPreserveEarlierBlankLines()
    {
        var source = NormalizeLineEndings(BuildPropertyLineDeletionSourceXml());
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var propertyLineNumber = 8;
        var propertyLineStart = GetLineStartOffset(source, propertyLineNumber);
        var propertyLineText = GetLineText(source, propertyLineNumber);
        var propertyLineLength = propertyLineText.Length + 1;

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(propertyLineStart, propertyLineLength);

        var expected = source.Remove(propertyLineStart, propertyLineLength);

        Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.None));

        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(GetLineText(source, 4), GetLineText(shell.SourceText, 4));
        Assert.Equal(GetLineText(source, 5), GetLineText(shell.SourceText, 5));
        Assert.Equal(CountLogicalLines(source) - 1, CountLogicalLines(shell.SourceText));
        Assert.Contains("Padding=\"8\"", shell.SourceText, StringComparison.Ordinal);
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
    public void ShellView_DefaultSourceEditorEnterViaUiRootInputAtLineTen_DoesNotCorruptSourceOrShrinkExtent()
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
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        var afterDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));
        var afterMetrics = sourceEditor.GetScrollMetricsSnapshot();

        Assert.Equal(shell.SourceText, afterDocumentText);
        Assert.Contains("Content=\"Preview Action\"", shell.SourceText, StringComparison.Ordinal);
        Assert.Equal(beforeLineCount, CountLogicalLines(shell.SourceText));
        Assert.True(afterMetrics.ExtentHeight >= beforeMetrics.ExtentHeight - 0.01f);
        Assert.True(afterMetrics.ExtentHeight - afterMetrics.ViewportHeight >= beforeMetrics.ExtentHeight - beforeMetrics.ViewportHeight - 0.01f);
    }

    [Fact]
    public void ShellView_DefaultSourceEditorRepeatedEnterViaUiRootInputAtLineTen_DoesNotCorruptSourceOrShrinkExtent()
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
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);
        for (var i = 0; i < 4; i++)
        {
            uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        var afterMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var afterDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));

        Assert.Equal(shell.SourceText, afterDocumentText);
        Assert.Equal(beforeLineCount, CountLogicalLines(shell.SourceText));
        Assert.True(afterMetrics.ExtentHeight >= beforeMetrics.ExtentHeight - 0.01f);
        Assert.True(afterMetrics.ExtentHeight - afterMetrics.ViewportHeight >= beforeMetrics.ExtentHeight - beforeMetrics.ViewportHeight - 0.01f);
    }

    [Fact]
    public void ShellView_DefaultSourceEditorAfterFortyEntersAtLineTen_ShouldScrollPastInsertedBlankRegion()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var source = shell.SourceText;
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var beforeLineCount = CountLogicalLines(source);
        var lineTenStart = GetLineStartOffset(source, 10);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 10);
        Click(uiRoot, clickPoint);
        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(lineTenStart, 0);

        for (var i = 0; i < 40; i++)
        {
            uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        var afterDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));
        var afterMetrics = sourceEditor.GetScrollMetricsSnapshot();
        var expectedLineCount = beforeLineCount + 40;
        var expectedExtentHeight = sourceEditor.EstimatedLineHeight * expectedLineCount;

        Assert.Equal(shell.SourceText, afterDocumentText);
        Assert.Equal(expectedLineCount, CountLogicalLines(shell.SourceText));
        Assert.True(
            afterMetrics.ExtentHeight > afterMetrics.ViewportHeight,
            $"Expected the source editor to remain scrollable after 40 Enter presses, but extent={afterMetrics.ExtentHeight:0.###} viewport={afterMetrics.ViewportHeight:0.###}.");
        Assert.True(
            afterMetrics.ExtentHeight >= expectedExtentHeight - sourceEditor.EstimatedLineHeight,
            $"Expected the scroll extent to include all {expectedLineCount} logical lines after 40 Enter presses, but extent={afterMetrics.ExtentHeight:0.###}, expectedExtent~={expectedExtentHeight:0.###}, lineHeight={sourceEditor.EstimatedLineHeight:0.###}, beforeLineCount={beforeLineCount}.");

        sourceEditor.ScrollToVerticalOffset(100000f);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(sourceEditor.Editor.TryGetViewportLayoutSnapshot(out var viewportSnapshot));
        var visibleLines = GetVisibleViewportLines(viewportSnapshot).ToArray();
        Assert.NotEmpty(visibleLines);
        var lastVisibleLineNumber = visibleLines[^1].Index + 1;

        Assert.True(
            sourceEditor.VerticalOffset > sourceEditor.EstimatedLineHeight * 40f,
            $"Expected bottom scrolling to move past the inserted blank-line region, but offset={sourceEditor.VerticalOffset:0.###}, lineHeight={sourceEditor.EstimatedLineHeight:0.###}, scrollable={sourceEditor.ScrollableHeight:0.###}.");
        Assert.True(
            lastVisibleLineNumber >= expectedLineCount - 1,
            $"Expected bottom scrolling to reveal the document tail after 40 Enter presses, but lastVisibleLine={lastVisibleLineNumber}, expectedLineCount={expectedLineCount}, offset={sourceEditor.VerticalOffset:0.###}, scrollable={sourceEditor.ScrollableHeight:0.###}, extent={sourceEditor.ExtentHeight:0.###}, viewport={sourceEditor.ViewportHeight:0.###}.");
    }

    [Fact]
    public void ShellView_DefaultSourceEditorAfterFortyDirectEntersAtLineTen_ShouldGrowDocumentAndScrollToTail()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var source = shell.SourceText;
        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var beforeLineCount = CountLogicalLines(source);
        var lineTenStart = GetLineStartOffset(source, 10);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(lineTenStart, 0);

        for (var i = 0; i < 40; i++)
        {
            Assert.True(sourceEditor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        var afterDocumentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));
        var expectedLineCount = beforeLineCount + 40;

        Assert.Equal(shell.SourceText, afterDocumentText);
        Assert.Equal(expectedLineCount, CountLogicalLines(shell.SourceText));

        sourceEditor.ScrollToVerticalOffset(100000f);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(sourceEditor.Editor.TryGetViewportLayoutSnapshot(out var viewportSnapshot));
        var visibleLines = GetVisibleViewportLines(viewportSnapshot).ToArray();
        Assert.NotEmpty(visibleLines);
        var lastVisibleLineNumber = visibleLines[^1].Index + 1;

        Assert.True(
            lastVisibleLineNumber >= expectedLineCount - 1,
            $"Expected direct Enter handling to reach the document tail, but lastVisibleLine={lastVisibleLineNumber}, expectedLineCount={expectedLineCount}, offset={sourceEditor.VerticalOffset:0.###}, scrollable={sourceEditor.ScrollableHeight:0.###}, extent={sourceEditor.ExtentHeight:0.###}, viewport={sourceEditor.ViewportHeight:0.###}.");
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
    public void ShellView_SourceEditorCtrlSpaceAfterPropertyElementOwnerPrefix_OpensFilteredCompletionPopup()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<TextBlock."
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);
        Click(uiRoot, clickPoint);
        sourceEditor.Select(shell.SourceText.Length, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Space, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(shell.SourceEditorView.IsControlCompletionOpen);
        Assert.Contains("Style", shell.SourceEditorView.ControlCompletionItems);
        Assert.DoesNotContain("TextBlock.Style", shell.SourceEditorView.ControlCompletionItems);
        Assert.True(shell.SourceEditorView.ControlCompletionSelectedIndex >= 0);
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
            scrollTelemetry.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the fixed completion popup to stay on the arrange-only virtualizing SetOffsets branch, but telemetry was {scrollTelemetry}.");
        Assert.Equal(0, scrollTelemetry.SetOffsetsVirtualizingMeasureInvalidationPathCount);
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
            designerPopup.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected designer completion popup scrolling to remain on the arrange-only virtualizing SetOffsets branch, but telemetry was {designerPopup.Scroll}.");
        Assert.Equal(0, designerPopup.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.Equal(0, designerPopup.Scroll.SetOffsetsTransformInvalidationPathCount);
        Assert.Equal(0, designerPopup.Scroll.SetOffsetsManualArrangePathCount);
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
            scrollTelemetry.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the stable-anchor completion popup to keep using the arrange-only virtualizing SetOffsets branch, but telemetry was {scrollTelemetry}.");
        Assert.Equal(0, scrollTelemetry.SetOffsetsVirtualizingMeasureInvalidationPathCount);
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
            standalone.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the standalone source editor view to keep using the arrange-only virtualizing SetOffsets branch, but telemetry was {standalone.Scroll}.");
        Assert.Equal(0, standalone.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.True(
            fullShell.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the full shell source editor completion popup to keep using the arrange-only virtualizing SetOffsets branch, but telemetry was {fullShell.Scroll}.");
        Assert.Equal(0, fullShell.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
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
            baseline.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the default completion list to keep using the arrange-only virtualizing SetOffsets branch, but telemetry was {baseline.Scroll}.");
        Assert.Equal(0, baseline.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.Equal(0, baseline.Scroll.SetOffsetsTransformInvalidationPathCount);
        Assert.Equal(0, withoutVirtualization.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.Equal(0, withoutVirtualization.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount);
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
            virtualizedRuntime.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the default completion list to take the arrange-only VirtualizingStackPanel branch, but runtime was {virtualizedRuntime}.");
        Assert.Equal(
            0,
            virtualizedRuntime.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.Equal(
            0,
            virtualizedRuntime.SetOffsetsTransformInvalidationPathCount);
        Assert.Equal(
            0,
            virtualizedRuntime.SetOffsetsManualArrangePathCount);

        Assert.True(
            nonVirtualizedRuntime.SetOffsetsVirtualizingMeasureInvalidationPathCount <= 1,
            $"Expected disabling completion-list virtualization to largely avoid the virtualizing measure-invalidation path, but runtime was {nonVirtualizedRuntime}.");
        Assert.Equal(
            0,
            nonVirtualizedRuntime.SetOffsetsVirtualizingArrangeOnlyPathCount);
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
            virtualized.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the fixed designer completion popup to stay on the arrange-only virtualizing SetOffsets branch, but telemetry was {virtualized.Scroll}.");
        Assert.Equal(0, virtualized.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount <= 1,
            $"Expected disabling completion-list virtualization in the standalone source editor view to largely avoid the virtualizing SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.Equal(0, withoutVirtualization.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount);
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsTransformInvalidationPathCount > 0,
            $"Expected disabling completion-list virtualization to move the same repro into the transform-scrolling SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.True(
            virtualized.Control.GetVisualChildrenCallCount < withoutVirtualization.Control.GetVisualChildrenCallCount,
            $"Expected the fixed virtualized completion path to stay cheaper than the non-virtualized fallback in this repro. virtualized={virtualized.Control.GetVisualChildrenCallCount} nonVirtualized={withoutVirtualization.Control.GetVisualChildrenCallCount}");
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
            baseline.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount > 0,
            $"Expected the standalone source editor completion popup to keep using the arrange-only virtualizing SetOffsets branch, but telemetry was {baseline.Scroll}.");
        Assert.Equal(0, baseline.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Assert.True(
            withoutVirtualization.Scroll.SetOffsetsVirtualizingMeasureInvalidationPathCount <= 1,
            $"Expected disabling completion-list virtualization in the standalone source editor view to largely avoid the virtualizing SetOffsets branch, but telemetry was {withoutVirtualization.Scroll}.");
        Assert.Equal(0, withoutVirtualization.Scroll.SetOffsetsVirtualizingArrangeOnlyPathCount);
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
    public void ShellView_SourceEditorTypingGreaterThanAfterCompleteControlTag_InsertsClosingTag()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<TextBlock"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(shell.SourceText.Length, 0);

        Assert.True(sourceEditor.HandleTextInputFromInput('>'));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<TextBlock></TextBlock>", shell.SourceText);
        Assert.Equal("<TextBlock></TextBlock>", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal("<TextBlock>".Length, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorEnterBetweenEmptyControlTags_ViaUiRootInput_MovesClosingTagToIndentedNextLine()
    {
        var source = NormalizeLineEndings(
            """
            <Grid>
              <TextBlock></TextBlock>
            </Grid>
            """);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 2);
        Click(uiRoot, clickPoint);

        var caretIndex = source.IndexOf("<TextBlock>", StringComparison.Ordinal) + "<TextBlock>".Length;
        Assert.True(caretIndex >= "<TextBlock>".Length);
        sourceEditor.Select(caretIndex, 0);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        var expected = NormalizeLineEndings(
            """
            <Grid>
              <TextBlock>
              </TextBlock>
            </Grid>
            """);

        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
    }

    [Fact]
    public void ShellView_SourceEditorTypingGreaterThanAfterCompletePropertyElementTag_InsertsClosingTag()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid.RowDefinitions"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(shell.SourceText.Length, 0);

        Assert.True(sourceEditor.HandleTextInputFromInput('>'));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<Grid.RowDefinitions></Grid.RowDefinitions>", shell.SourceText);
        Assert.Equal("<Grid.RowDefinitions></Grid.RowDefinitions>", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal("<Grid.RowDefinitions>".Length, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorTypingSlashAfterCompleteControlTag_InsertsSelfClosingBracket()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<TextBlock"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(shell.SourceText.Length, 0);

        Assert.True(sourceEditor.HandleTextInputFromInput('/'));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<TextBlock/>", shell.SourceText);
        Assert.Equal("<TextBlock/>", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorTypingSlashAfterLessThan_InfersClosingTagFromNearestOpenControl()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<TextBlock><"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(shell.SourceText.Length, 0);

        Assert.True(sourceEditor.HandleTextInputFromInput('/'));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<TextBlock></TextBlock>", shell.SourceText);
        Assert.Equal("<TextBlock></TextBlock>", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorTypingSlashAfterLessThan_ConvertsNearestSelfClosingControlIntoOpenClosePair()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<TextBlock/><"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(shell.SourceText.Length, 0);

        Assert.True(sourceEditor.HandleTextInputFromInput('/'));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<TextBlock></TextBlock>", shell.SourceText);
        Assert.Equal("<TextBlock></TextBlock>", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorTypingSlashAfterLessThan_IndentsInferredClosingTagToOpeningTagLine()
    {
        var source = NormalizeLineEndings(
            """
            <Grid>
              <TextBlock Text="Designer Preview"
                         Foreground="#E7EDF5"
                         FontSize="22"
                         FontWeight="SemiBold">
            <
            </Grid>
            """);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var insertionIndex = shell.SourceText.IndexOf("\n<\n", StringComparison.Ordinal) + 1;
        Assert.True(insertionIndex >= 1);
        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(insertionIndex + 1, 0);

        Assert.True(sourceEditor.HandleTextInputFromInput('/'));
        RunLayout(uiRoot, 1280, 840, 16);

        var expected = NormalizeLineEndings(
            """
            <Grid>
              <TextBlock Text="Designer Preview"
                         Foreground="#E7EDF5"
                         FontSize="22"
                         FontWeight="SemiBold">
              </TextBlock>
            </Grid>
            """);

        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(0, sourceEditor.SelectionLength);

        var runs = GetDocumentRuns(sourceEditor.Document);
        Assert.Contains(runs, run => run.Text == "Grid" && run.Foreground == InkkSlinger.Designer.DesignerXmlSyntaxColors.Default.ControlTypeForeground);
        Assert.Contains(runs, run => run.Text == "TextBlock" && run.Foreground == InkkSlinger.Designer.DesignerXmlSyntaxColors.Default.ControlTypeForeground);
        Assert.Contains(runs, run => run.Text == "Text" && run.Foreground == InkkSlinger.Designer.DesignerXmlSyntaxColors.Default.PropertyForeground);
        Assert.Contains(runs, run => run.Text == "Foreground" && run.Foreground == InkkSlinger.Designer.DesignerXmlSyntaxColors.Default.PropertyForeground);
    }

    [Fact]
    public void ShellView_SourceEditor_TabAndShiftTab_IndentAndOutdentSelectedLines()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid>\n<TextBlock />\n<Button />\n</Grid>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var selectionStart = shell.SourceText.IndexOf("<TextBlock", StringComparison.Ordinal);
        var selectionLength = shell.SourceText.IndexOf("</Grid>", StringComparison.Ordinal) - selectionStart;
        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(selectionStart, selectionLength);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 2);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Tab, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("\n  <TextBlock />\n  <Button />\n", shell.SourceText, StringComparison.Ordinal);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Tab, clickPoint, heldModifiers: [Keys.LeftShift]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<Grid>\n<TextBlock />\n<Button />\n</Grid>", shell.SourceText);
    }

    [Fact]
    public void ShellView_SourceEditor_CommandKeys_HandleCommentFormatMoveDuplicateAndDeleteLine()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid>\n<TextBlock />\n<Button />\n</Grid>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(shell.SourceText.IndexOf("<TextBlock", StringComparison.Ordinal), 0);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 2);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.OemQuestion, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.Contains("<!-- <TextBlock /> -->", shell.SourceText, StringComparison.Ordinal);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.OemQuestion, clickPoint, heldModifiers: [Keys.LeftControl]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.Contains("<TextBlock />", shell.SourceText, StringComparison.Ordinal);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down, clickPoint, heldModifiers: [Keys.LeftAlt]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.True(shell.SourceText.IndexOf("<Button />", StringComparison.Ordinal) < shell.SourceText.IndexOf("<TextBlock />", StringComparison.Ordinal));

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down, clickPoint, heldModifiers: [Keys.LeftAlt, Keys.LeftShift]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.True(shell.SourceText.Split("<TextBlock />").Length - 1 >= 2);

        sourceEditor.Select(shell.SourceText.IndexOf("<Button />", StringComparison.Ordinal), 0);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.K, clickPoint, heldModifiers: [Keys.LeftControl, Keys.LeftShift]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.DoesNotContain("<Button />", shell.SourceText, StringComparison.Ordinal);
        Assert.Contains("<TextBlock />", shell.SourceText, StringComparison.Ordinal);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, clickPoint, heldModifiers: [Keys.LeftControl, Keys.LeftShift]));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.Contains("\n  <TextBlock />", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourceEditor_PairedQuotesAndSmartEnter_InsertExpectedXmlEditingText()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(shell.SourceText.Length, 0);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.Equal("<Grid>\n  ", shell.SourceText);

        Assert.True(sourceEditor.HandleTextCompositionFromInput("<TextBlock Text="));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.True(sourceEditor.HandleTextInputFromInput('"'));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<Grid>\n  <TextBlock Text=\"\"", shell.SourceText);
        Assert.Equal(shell.SourceText.Length - 1, sourceEditor.SelectionStart);
    }

    [Fact]
    public void ShellView_SourceEditorTypingAtEndOfClosingTagName_ShouldAdvanceCaretWithTypedText()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<TextBlock></TextBlock>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var closingNameEnd = shell.SourceText.IndexOf("</TextBlock>", StringComparison.Ordinal) + "</TextBlock".Length;
        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(closingNameEnd, 0);

        foreach (var character in "Suffix")
        {
            Assert.True(sourceEditor.HandleTextInputFromInput(character));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        const string expected = "<TextBlockSuffix></TextBlockSuffix>";
        var expectedCaret = expected.IndexOf("</TextBlockSuffix>", StringComparison.Ordinal) + "</TextBlockSuffix".Length;
        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(expectedCaret, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorTypingPropertyElementInsideTextBlock_ShouldPlaceClosingTagOnNewLine()
    {
        var source = NormalizeLineEndings(string.Join('\n',
            "<Grid>",
            "  <TextBlock Text=\"Designer Preview\">",
            "    ",
            "  </TextBlock>",
            "</Grid>"));
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var insertionIndex = shell.SourceText.IndexOf("    \n", StringComparison.Ordinal) + "    ".Length;
        Assert.True(insertionIndex >= "    ".Length);
        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(insertionIndex, 0);

        foreach (var character in "<TextBlock.Style>")
        {
            Assert.True(sourceEditor.HandleTextInputFromInput(character));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        var expected = NormalizeLineEndings(string.Join('\n',
            "<Grid>",
            "  <TextBlock Text=\"Designer Preview\">",
            "    <TextBlock.Style>",
            "      ",
            "    </TextBlock.Style>",
            "  </TextBlock>",
            "</Grid>"));
        var expectedCaret = expected.IndexOf("      \n", StringComparison.Ordinal) + "      ".Length;
        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(expectedCaret, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorTypingClosingQuoteInsidePairedAttributeValue_ShouldSkipExistingQuote()
    {
        var source = NormalizeLineEndings(
            """
            <TextBlock.Style>
              <Style TargetType=""
            </TextBlock.Style>
            """);
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var insertionIndex = shell.SourceText.IndexOf("TargetType=\"\"", StringComparison.Ordinal) + "TargetType=\"".Length;
        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(insertionIndex, 0);

        foreach (var character in "TextBlock")
        {
            Assert.True(sourceEditor.HandleTextInputFromInput(character));
            RunLayout(uiRoot, 1280, 840, 16);
        }

        Assert.True(sourceEditor.HandleTextInputFromInput('"'));
        RunLayout(uiRoot, 1280, 840, 16);

        var expected = NormalizeLineEndings(
            """
            <TextBlock.Style>
              <Style TargetType="TextBlock"
            </TextBlock.Style>
            """);
        var expectedCaret = expected.IndexOf("TargetType=\"TextBlock\"", StringComparison.Ordinal) + "TargetType=\"TextBlock\"".Length;
        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(expectedCaret, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorEnterAfterStyleInsidePropertyElement_ShouldAlignCaretWithStyleElement()
    {
        var source = NormalizeLineEndings(string.Join('\n',
            "<TextBlock.Style>",
            "  <Style TargetType=\"TextBlock\">",
            "</TextBlock.Style>"));
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var insertionIndex = shell.SourceText.IndexOf("<Style TargetType=\"TextBlock\">", StringComparison.Ordinal) +
            "<Style TargetType=\"TextBlock\">".Length;
        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(insertionIndex, 0);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 2);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        var expected = NormalizeLineEndings(string.Join('\n',
            "<TextBlock.Style>",
            "  <Style TargetType=\"TextBlock\">",
            "  ",
            "</TextBlock.Style>"));
        var expectedCaret = expected.IndexOf("  \n", StringComparison.Ordinal) + "  ".Length;
        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(expectedCaret, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditorEnterAfterOpeningTagWithExistingChild_ShouldUseChildIndent()
    {
        var source = NormalizeLineEndings(string.Join('\n',
            "<Border>",
            "    <StackPanel>",
            "",
            "        <TextBlock Text=\"Manual refresh\" />",
            "    </StackPanel>",
            "</Border>"));
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = source
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var insertionIndex = shell.SourceText.IndexOf("<StackPanel>", StringComparison.Ordinal) + "<StackPanel>".Length;
        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(insertionIndex, 0);

        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 2);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        var expected = NormalizeLineEndings(string.Join('\n',
            "<Border>",
            "    <StackPanel>",
            "        ",
            "",
            "        <TextBlock Text=\"Manual refresh\" />",
            "    </StackPanel>",
            "</Border>"));
        var expectedCaret = expected.IndexOf("        \n", StringComparison.Ordinal) + "        ".Length;
        Assert.Equal(expected, shell.SourceText);
        Assert.Equal(expected, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal(expectedCaret, sourceEditor.SelectionStart);
        Assert.Equal(0, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditor_EnterInLargeDocument_PreservesMostHighlightedParagraphs()
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("<Grid>");
        for (var i = 0; i < 900; i++)
        {
            sourceBuilder.AppendLine($"  <TextBlock Grid.Row=\"{i}\" Text=\"Line {i:000}\" />");
        }

        sourceBuilder.Append("</Grid>");
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = NormalizeLineEndings(sourceBuilder.ToString())
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var originalParagraphs = sourceEditor.Document.Blocks.OfType<Paragraph>().ToArray();
        var targetLine = "  <TextBlock Grid.Row=\"450\" Text=\"Line 450\" />";
        var insertionIndex = shell.SourceText.IndexOf(targetLine, StringComparison.Ordinal) + targetLine.Length;
        Assert.True(insertionIndex > targetLine.Length);

        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(insertionIndex, 0);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);

        var updatedParagraphs = sourceEditor.Document.Blocks.OfType<Paragraph>().ToArray();
        var preservedCount = originalParagraphs.Count(updatedParagraphs.Contains);

        Assert.Equal(originalParagraphs.Length + 1, updatedParagraphs.Length);
        Assert.True(
            preservedCount >= originalParagraphs.Length - 4,
            $"Enter rebuilt too many highlighted paragraphs. Preserved {preservedCount} of {originalParagraphs.Length}.");
    }

    [Fact]
    public void ShellView_SourceEditor_RenamingXmlTagName_UpdatesPairedTag()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid>\n</Grid>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(1, "Grid".Length);

        Assert.True(sourceEditor.HandleTextCompositionFromInput("StackPanel"));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<StackPanel>\n</StackPanel>", shell.SourceText);
        Assert.Equal(shell.SourceText, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
    }

    [Fact]
    public void ShellView_SourceEditor_FoldCommands_CollapseAndExpandXmlElementProjection()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid>\n  <TextBlock />\n  <Button />\n</Grid>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(1, 0);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.OemOpenBrackets, clickPoint, heldModifiers: [Keys.LeftControl, Keys.LeftShift]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal(1, shell.SourceEditorView.CollapsedXmlFoldCount);
        Assert.Equal("<Grid>\n  ...\n</Grid>", NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
        Assert.Equal("<Grid>\n  <TextBlock />\n  <Button />\n</Grid>", shell.SourceText);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.OemCloseBrackets, clickPoint, heldModifiers: [Keys.LeftControl, Keys.LeftShift]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal(0, shell.SourceEditorView.CollapsedXmlFoldCount);
        Assert.Equal(shell.SourceText, NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document)));
    }

    [Fact]
    public void ShellView_SourceEditor_SourceOverview_PopulatesXmlDocumentOutline()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid>\n  <StackPanel>\n    <TextBlock />\n  </StackPanel>\n</Grid>"
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var overview = shell.SourceEditorView.SourceOverviewItems;

        Assert.Contains(overview, item => item.Name == "Grid" && item.LineNumber == 1);
        Assert.Contains(overview, item => item.Name == "StackPanel" && item.LineNumber == 2);
        Assert.Contains(overview, item => item.Name == "TextBlock" && item.LineNumber == 3);
    }

    [Fact]
    public void ShellView_SourceEditor_MinimapClick_NavigatesToDocumentLine()
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("<Grid>");
        for (var i = 1; i <= 120; i++)
        {
            sourceBuilder.AppendLine($"  <TextBlock Text=\"Line {i:00}\" />");
        }

        sourceBuilder.Append("</Grid>");
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = NormalizeLineEndings(sourceBuilder.ToString())
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.Select(0, 0);
        var originalSelectionStart = sourceEditor.SelectionStart;
        var originalSelectionLength = sourceEditor.SelectionLength;

        Assert.True(shell.SourceEditorView.Minimap.LineCount > 30);
        var minimapSlot = shell.SourceEditorView.Minimap.LayoutSlot;
        Assert.True(minimapSlot.Width > 0f && minimapSlot.Height > 0f, $"Expected minimap to be arranged, but slot={FormatRect(minimapSlot)}.");
        var pointer = new Vector2(
            minimapSlot.X + (minimapSlot.Width * 0.5f),
            minimapSlot.Y + (minimapSlot.Height * 0.8f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true, pointerMoved: false));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.True(shell.SourceEditorView.Minimap.NavigateRequestCount > 0);
        Assert.True(sourceEditor.VerticalOffset > 0f);
        Assert.Equal(originalSelectionStart, sourceEditor.SelectionStart);
        Assert.Equal(originalSelectionLength, sourceEditor.SelectionLength);
    }

    [Fact]
    public void ShellView_SourceEditor_ConfigurableIndent_AppliesToEnterTabAndFormat()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = "<Grid>"
        };
        shell.SourceEditorView.EditorIndentText = "    ";

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(sourceEditor.Editor);
        sourceEditor.Select(shell.SourceText.Length, 0);
        var clickPoint = GetSourceEditorLinePoint(sourceEditor, 1);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.Equal("<Grid>\n    ", shell.SourceText);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Tab, clickPoint));
        RunLayout(uiRoot, 1280, 840, 16);
        Assert.Equal("<Grid>\n        ", shell.SourceText);

        shell.SourceText = "<Grid>\n<TextBlock />\n</Grid>";
        sourceEditor.Select(0, 0);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, clickPoint, heldModifiers: [Keys.LeftControl, Keys.LeftShift]));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Equal("<Grid>\n    <TextBlock />\n</Grid>", shell.SourceText);
    }

    [Fact]
    public void ShellView_SourceEditorTypingSlashAfterLessThan_PrefersImmediateUpwardSelfClosingControlOverOuterAncestor()
    {
        const string sourceText = """
            <Grid>
              <TextBlock Text="Designer Preview"
                         Foreground="#E7EDF5"
                         FontSize="22"
                         FontWeight="SemiBold" />
            <
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = NormalizeLineEndings(sourceText)
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(shell.SourceText.Length, 0);

        Assert.True(sourceEditor.HandleTextInputFromInput('/'));
        RunLayout(uiRoot, 1280, 840, 16);

        Assert.Contains("<TextBlock Text=\"Designer Preview\"", shell.SourceText, StringComparison.Ordinal);
        Assert.Contains("</TextBlock>", shell.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("</Grid>", shell.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellView_SourceEditor_TypingSelfClosingNestedElement_ColorizesTypedControlName()
    {
        const string sourceText = """
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Grid>
              </Grid>
            </UserControl>
            """;

        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = sourceText
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var insertionIndex = shell.SourceText.IndexOf("  </Grid>", StringComparison.Ordinal);
        Assert.True(insertionIndex >= 0);

        sourceEditor.SetFocusedFromInput(true);
        sourceEditor.Select(insertionIndex, 0);

        DocumentEditing.InsertTextAt(sourceEditor.Document, insertionIndex, "    <TextBlock/>\n");
        RunLayout(uiRoot, 1280, 840, 16);

        var currentText = NormalizeLineEndings(DocumentEditing.GetText(sourceEditor.Document));
        Assert.Contains("    <TextBlock/>", currentText, StringComparison.Ordinal);

        var typedControlRun = GetDocumentRuns(sourceEditor.Document)
            .FirstOrDefault(run => string.Equals(run.Text, "TextBlock", StringComparison.Ordinal));
        Assert.NotNull(typedControlRun);
        Assert.Equal(InkkSlinger.Designer.DesignerXmlSyntaxColors.Default.ControlTypeForeground, typedControlRun!.Foreground);
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
    public void ShellView_ToolbarExposesSaveAndRefreshOnlyForDocumentWorkflow()
    {
        var fileStore = new FakeDocumentFileStore();
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", fileStore);
        documentController.SaveToPath("C:/designer/current.xml");
        var shell = new InkkSlinger.Designer.DesignerShellView(documentController);

        var saveButton = Assert.IsType<Button>(shell.FindName("SaveButton"));
        _ = Assert.IsType<Button>(shell.FindName("RefreshButton"));
        Assert.Null(shell.FindName("NewButton"));
        Assert.Null(shell.FindName("OpenButton"));
        Assert.Null(shell.FindName("SaveAsButton"));
        Assert.Null(shell.FindName("WorkflowPromptSecondaryButton"));

        shell.SourceText = ValidViewXml.Replace("Hello designer", "Saved from shell", StringComparison.Ordinal);

        Assert.True(CommandSourceExecution.TryExecute(saveButton, shell));
        Assert.Equal(shell.SourceText, fileStore.WrittenTexts["C:/designer/current.xml"]);
        Assert.False(shell.DocumentController.IsDirty);
    }

    [Fact]
    public void ShellView_RootTemplatePicker_CreatesBarebonesRootDocument()
    {
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl><TextBlock Text=\"Debug\" /></UserControl>");
        var shell = new InkkSlinger.Designer.DesignerShellView(documentController);
        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));

        var gridTemplate = shell.ViewModel.RootTemplates.Single(template => template.ElementName == "Grid");
        picker.SelectedItem = gridTemplate;

        Assert.Equal(
            "<Grid xmlns=\"urn:inkkslinger-ui\"\n" +
            "             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "</Grid>\n",
            shell.SourceText);
        Assert.True(shell.DocumentController.IsDirty);
        Assert.Equal("New root...", shell.ViewModel.SelectedRootTemplate?.DisplayText);
    }

    [Fact]
    public void ShellView_RootTemplatePicker_DockPanelScaffold_CanRefreshPreview()
    {
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl><TextBlock Text=\"Debug\" /></UserControl>");
        var shell = new InkkSlinger.Designer.DesignerShellView(documentController);
        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));

        var dockPanelTemplate = shell.ViewModel.RootTemplates.Single(template => template.ElementName == "DockPanel");
        picker.SelectedItem = dockPanelTemplate;

        Assert.True(shell.RefreshPreview());
        Assert.IsType<DockPanel>(shell.Controller.PreviewRoot);
        Assert.Equal(InkkSlinger.Designer.DesignerPreviewState.Success, shell.Controller.PreviewState);
        Assert.Equal(Visibility.Visible, shell.ViewModel.PreviewContentVisibility);
        Assert.Equal(Visibility.Collapsed, shell.ViewModel.PreviewPlaceholderVisibility);
        Assert.DoesNotContain(shell.Controller.Diagnostics, diagnostic => diagnostic.Message.Contains("UserControl root", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShellView_RootTemplatePicker_ClickOpen_ShouldNotEagerlyMaterializeEveryControlItem()
    {
        _ = ComboBox.GetTelemetryAndReset();

        var shell = new InkkSlinger.Designer.DesignerShellView();
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));
        var templateCount = shell.ViewModel.RootTemplates.Count;
        Assert.True(templateCount > 40, $"Expected a large enough root template list to reproduce the dropdown open cost, got {templateCount}.");

        Click(uiRoot, GetCenter(picker.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        var snapshot = picker.GetComboBoxSnapshotForDiagnostics();
        var aggregate = ComboBox.GetTelemetryAndReset();
        var dropDown = GetComboBoxDropDownList(picker);

        Assert.True(snapshot.HandlePointerDownHitCount > 0, "Expected the root picker click to hit the ComboBox.");
        Assert.True(snapshot.IsDropDownOpen, "Expected the root picker click to open the dropdown.");
        Assert.True(
            snapshot.RefreshDropDownItemsProjectedItemCount >= templateCount,
            $"Expected root picker telemetry to show at least one full dropdown population. templates={templateCount} projected={snapshot.RefreshDropDownItemsProjectedItemCount}.");
        Assert.True(
            aggregate.RefreshDropDownItemsProjectedItemCount >= templateCount,
            $"Expected aggregate telemetry to show at least one full dropdown population. templates={templateCount} aggregateProjected={aggregate.RefreshDropDownItemsProjectedItemCount}.");
        Assert.Equal(templateCount, dropDown.Items.Count);
        Assert.True(
            snapshot.DropDownItemCount < templateCount,
            $"Expected opening the root picker to avoid eagerly materializing every control item. " +
            $"templates={templateCount} dropdownItems={snapshot.DropDownItemCount} " +
            $"projected={snapshot.RefreshDropDownItemsProjectedItemCount} " +
            $"openMs={snapshot.OpenDropDownMilliseconds:0.###} refreshMs={snapshot.RefreshDropDownItemsMilliseconds:0.###}.");
    }

    [Fact]
    public void ShellView_RootTemplatePicker_ClickOpen_ShouldBuildDropDownShellsOnlyOncePerOpen()
    {
        _ = ComboBox.GetTelemetryAndReset();

        var shell = new InkkSlinger.Designer.DesignerShellView();
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));
        var templateCount = shell.ViewModel.RootTemplates.Count;
        Assert.True(templateCount > 40, $"Expected a large enough root template list to reproduce the dropdown open churn, got {templateCount}.");

        Click(uiRoot, GetCenter(picker.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        var snapshot = picker.GetComboBoxSnapshotForDiagnostics();
        var aggregate = ComboBox.GetTelemetryAndReset();

        Assert.True(snapshot.IsDropDownOpen, "Expected the root picker click to open the dropdown.");
        Assert.Equal(
            templateCount,
            snapshot.BuildDropDownContainerCallCount);
        Assert.True(
            snapshot.DropDownItemCount < templateCount,
            $"Expected virtualization to limit the visible dropdown slice after reusing the owner containers. templates={templateCount} visible={snapshot.DropDownItemCount} refreshCalls={snapshot.RefreshDropDownItemsCallCount}.");
        Assert.InRange(
            snapshot.RefreshDropDownItemsProjectedItemCount,
            templateCount,
            templateCount * 2);
        Assert.True(
            aggregate.RefreshDropDownItemsCallCount >= snapshot.RefreshDropDownItemsCallCount,
            $"Expected aggregate telemetry to retain the repeated refresh evidence. snapshotRefreshCalls={snapshot.RefreshDropDownItemsCallCount} aggregateRefreshCalls={aggregate.RefreshDropDownItemsCallCount}.");
    }

    [Fact]
    public void ShellView_RootTemplatePicker_ScrollingToBottom_ShouldNotLeaveBlankSpaceAfterLastItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));
        Click(uiRoot, GetCenter(picker.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        var dropDown = GetComboBoxDropDownList(picker);
        var scrollViewer = FindListBoxScrollViewer(dropDown);
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot, 1280, 840, 16);

        var lastItem = Assert.IsType<ComboBoxItem>(GetLastViewportIntersectingListBoxItem(dropDown, scrollViewer));
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var gapAfterLastVisibleItem = viewportBottom - (lastItem.LayoutSlot.Y + lastItem.LayoutSlot.Height);

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.True(
            gapAfterLastVisibleItem <= 0.5f,
            $"Expected the last visible root template dropdown item to reach the viewport bottom after scrolling to the end. lastItem={DescribeElement(lastItem)} gap={gapAfterLastVisibleItem:0.##} viewportBottom={viewportBottom:0.##}, itemBottom={lastItem.LayoutSlot.Y + lastItem.LayoutSlot.Height:0.##}, Offset={scrollViewer.VerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.True(
            ComboBoxItemHasRenderedText(lastItem),
            $"Expected the last visible root template dropdown item to still have rendered text content after scrolling to the end. lastItem={DescribeElement(lastItem)}, Offset={scrollViewer.VerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
    }

    [Fact]
    public void ShellView_RootTemplatePicker_ScrollingToBottom_ShouldFullyRevealLastLogicalItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));
        Click(uiRoot, GetCenter(picker.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        var dropDown = GetComboBoxDropDownList(picker);
        var scrollViewer = FindListBoxScrollViewer(dropDown);
        scrollViewer.ScrollToVerticalOffset(10000f);
        RunLayout(uiRoot, 1280, 840, 16);

        var lastRealized = GetHighestRealizedIndexListBoxItem(dropDown);
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 0.5f,
            $"Expected bottom-clamped vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.Equal(
            dropDown.Items.Count - 1,
            lastRealized.Index);
        Assert.True(
            lastRealized.Element.LayoutSlot.Y + lastRealized.Element.LayoutSlot.Height <= viewportBottom + 0.5f,
            $"Expected the last root template dropdown item to be fully visible after scrolling to the end. index={lastRealized.Index} itemTop={lastRealized.Element.LayoutSlot.Y:0.##} itemBottom={lastRealized.Element.LayoutSlot.Y + lastRealized.Element.LayoutSlot.Height:0.##} itemHeight={lastRealized.Element.LayoutSlot.Height:0.##} viewportBottom={viewportBottom:0.##} Offset={scrollViewer.VerticalOffset:0.##} Extent={scrollViewer.ExtentHeight:0.##} Viewport={scrollViewer.ViewportHeight:0.##}.");
    }

    [Fact]
    public void ShellView_RootTemplatePicker_DraggingScrollBarThumbToBottom_ShouldFullyRevealLastLogicalItem()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));
        Click(uiRoot, GetCenter(picker.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        var dropDown = GetComboBoxDropDownList(picker);
        var scrollViewer = FindListBoxScrollViewer(dropDown);
        var verticalBar = GetPrivateScrollBar(scrollViewer, "_verticalBar");
        var thumb = FindNamedVisualChild<Thumb>(verticalBar, "PART_Thumb");
        Assert.NotNull(thumb);

        var start = GetCenter(verticalBar.GetThumbRectForInput());
        var end = new Vector2(start.X, verticalBar.LayoutSlot.Y + verticalBar.LayoutSlot.Height - 18f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        Assert.Same(thumb, FocusManager.GetCapturedPointerElement());

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var lastRealized = GetHighestRealizedIndexListBoxItem(dropDown);
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var maxVerticalOffset = MathF.Max(0f, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        var lastText = FindDescendantOrDefault<TextBlock>(lastRealized.Element, static textBlock => !string.IsNullOrWhiteSpace(textBlock.Text));

        Assert.Null(FocusManager.GetCapturedPointerElement());
        Assert.True(
            MathF.Abs(scrollViewer.VerticalOffset - maxVerticalOffset) <= 1.5f,
            $"Expected thumb drag to bottom-clamp the vertical offset. Offset={scrollViewer.VerticalOffset:0.##}, Max={maxVerticalOffset:0.##}, Extent={scrollViewer.ExtentHeight:0.##}, Viewport={scrollViewer.ViewportHeight:0.##}.");
        Assert.Equal(
            dropDown.Items.Count - 1,
            lastRealized.Index);
        Assert.True(
            lastRealized.Element.LayoutSlot.Y + lastRealized.Element.LayoutSlot.Height <= viewportBottom + 0.5f,
            $"Expected dragging the root template scrollbar thumb to the bottom to fully reveal the last logical item. index={lastRealized.Index} itemTop={lastRealized.Element.LayoutSlot.Y:0.##} itemBottom={lastRealized.Element.LayoutSlot.Y + lastRealized.Element.LayoutSlot.Height:0.##} itemHeight={lastRealized.Element.LayoutSlot.Height:0.##} viewportBottom={viewportBottom:0.##} Offset={scrollViewer.VerticalOffset:0.##} Extent={scrollViewer.ExtentHeight:0.##} Viewport={scrollViewer.ViewportHeight:0.##} thumbStart={start} thumbEnd={end}.");
        Assert.True(
            lastRealized.Element.TryGetRenderBoundsInRootSpace(out var itemRenderBounds),
            $"Expected the last realized root template item to expose render bounds. index={lastRealized.Index}.");
        Assert.True(
            itemRenderBounds.Y + itemRenderBounds.Height <= viewportBottom + 0.5f,
            $"Expected the last realized root template item render bounds to remain inside the viewport after dragging to the end. index={lastRealized.Index} renderBottom={itemRenderBounds.Y + itemRenderBounds.Height:0.##} viewportBottom={viewportBottom:0.##} renderBounds={FormatRect(itemRenderBounds)}.");
        Assert.NotNull(lastText);
        Assert.True(
            lastText!.TryGetRenderBoundsInRootSpace(out var textRenderBounds),
            $"Expected the last realized root template item text to expose render bounds. index={lastRealized.Index}.");
        Assert.True(
            textRenderBounds.Y + textRenderBounds.Height <= viewportBottom + 0.5f,
            $"Expected the last realized root template item text to remain fully visible after dragging to the end. index={lastRealized.Index} textBottom={textRenderBounds.Y + textRenderBounds.Height:0.##} viewportBottom={viewportBottom:0.##} textBounds={FormatRect(textRenderBounds)} itemBounds={FormatRect(itemRenderBounds)}.");
    }

    [Fact]
    public void ShellView_RootTemplatePicker_DraggingScrollBarThumbToBottom_ShouldFullyRevealVirtualizingStackPanelRow()
    {
        var shell = new InkkSlinger.Designer.DesignerShellView();
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 840, 16);
        RunLayout(uiRoot, 1280, 840, 16);

        var picker = Assert.IsType<ComboBox>(shell.FindName("RootTemplateComboBox"));
        Click(uiRoot, GetCenter(picker.LayoutSlot));
        RunLayout(uiRoot, 1280, 840, 16);

        var dropDown = GetComboBoxDropDownList(picker);
        var scrollViewer = FindListBoxScrollViewer(dropDown);
        var verticalBar = GetPrivateScrollBar(scrollViewer, "_verticalBar");
        var thumb = FindNamedVisualChild<Thumb>(verticalBar, "PART_Thumb");
        Assert.NotNull(thumb);

        var start = GetCenter(verticalBar.GetThumbRectForInput());
        var end = new Vector2(start.X, verticalBar.LayoutSlot.Y + verticalBar.LayoutSlot.Height - 18f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
        RunLayout(uiRoot, 1280, 840, 16);

        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        var virtualizingStackPanelText = FindDescendantOrDefault<TextBlock>(
            dropDown,
            static textBlock => string.Equals(textBlock.Text, "VirtualizingStackPanel", StringComparison.Ordinal));

        Assert.NotNull(virtualizingStackPanelText);
        Assert.True(
            virtualizingStackPanelText!.TryGetRenderBoundsInRootSpace(out var textRenderBounds),
            "Expected the VirtualizingStackPanel row text to expose render bounds.");
        Assert.True(
            textRenderBounds.Y + textRenderBounds.Height <= viewportBottom + 0.5f,
            $"Expected the VirtualizingStackPanel row to be fully visible after dragging the root template scrollbar thumb to the bottom. textBottom={textRenderBounds.Y + textRenderBounds.Height:0.##} viewportBottom={viewportBottom:0.##} textBounds={FormatRect(textRenderBounds)} thumbStart={start} thumbEnd={end}.");
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

    private static string BuildWideNumberedSource(int lineCount)
    {
        var wideValue = new string('x', 240);
        return string.Join(
            "\n",
            Enumerable.Range(1, lineCount).Select(line => string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"<Line Number=\"{line}\" Text=\"{wideValue}\" />")));
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

        private static string BuildPropertyLineDeletionSourceXml()
        {
                return """
                                <UserControl xmlns="urn:inkkslinger-ui"
                                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

                                    <Grid>

                                        <Border
                                                Background="#223344"
                                                Margin="24"
                                                Padding="8">
                                            <TextBlock Text="Designer Preview" />
                                        </Border>
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

    private static Vector2 GetSourceEditorLinePoint(IDE_Editor sourceEditor, int oneBasedLineNumber)
    {
        var textHost = sourceEditor.Editor;
        var lineHeight = UiTextRenderer.GetLineHeight(textHost, textHost.FontSize);
        return new Vector2(
            textHost.LayoutSlot.X + 1f + 8f,
            textHost.LayoutSlot.Y + 1f + 5f + ((oneBasedLineNumber - 1) * lineHeight) + 2f);
    }

    private static Vector2 GetVisibleSourceEditorLineClickPoint(RichTextBoxViewportLayoutSnapshot viewportSnapshot, DocumentLayoutLine line)
    {
        return new Vector2(
            viewportSnapshot.TextRect.X + line.Bounds.X - viewportSnapshot.HorizontalOffset + 1f,
            viewportSnapshot.TextRect.Y + line.Bounds.Y - viewportSnapshot.VerticalOffset + (line.Bounds.Height * 0.5f));
    }

    private static IEnumerable<DocumentLayoutLine> GetVisibleViewportLines(RichTextBoxViewportLayoutSnapshot viewportSnapshot)
    {
        var top = viewportSnapshot.VerticalOffset;
        var bottom = top + viewportSnapshot.TextRect.Height;
        return viewportSnapshot.Layout.Lines.Where(line =>
            line.Bounds.Y + line.Bounds.Height > top && line.Bounds.Y < bottom);
    }

    private static Vector2 GetDiagnosticsTabHeaderPoint(TabControl tabControl)
    {
        var sourceHeaderWidth = MathF.Max(
            36f,
            tabControl.HeaderPadding.Horizontal + UiTextRenderer.MeasureWidth(tabControl, "Source", tabControl.FontSize));
        var appResourcesHeaderWidth = MathF.Max(
            36f,
            tabControl.HeaderPadding.Horizontal + UiTextRenderer.MeasureWidth(tabControl, "App Resources", tabControl.FontSize));
        return new Vector2(tabControl.LayoutSlot.X + sourceHeaderWidth + appResourcesHeaderWidth + 8f, tabControl.LayoutSlot.Y + 8f);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static IReadOnlyList<InkkOopsInteractionRecorder.RecordedAction> LoadRecordedActions(string recordingPath)
    {
        Assert.True(File.Exists(recordingPath), $"Expected recording to exist at '{recordingPath}'.");

        using var document = JsonDocument.Parse(File.ReadAllText(recordingPath));
        if (!document.RootElement.TryGetProperty("actions", out var actionsElement) || actionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Recording '{recordingPath}' does not contain an actions array.");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var actions = new List<InkkOopsInteractionRecorder.RecordedAction>();
        foreach (var actionElement in actionsElement.EnumerateArray())
        {
            var action = actionElement.Deserialize<InkkOopsInteractionRecorder.RecordedAction>(options)
                ?? throw new InvalidOperationException($"Could not deserialize a recorded action from '{recordingPath}'.");
            actions.Add(action);
        }

        return actions;
    }

    private static void ReplayRecordedDesignerSession(
        UiRoot uiRoot,
        IReadOnlyList<InkkOopsInteractionRecorder.RecordedAction> actions,
        int? maxActionCount = null)
    {
        var pointer = Vector2.Zero;
        var hasPointer = false;
        var viewportWidth = 1280;
        var viewportHeight = 820;
        var heldKeys = new HashSet<Keys>();
        var actionCount = Math.Clamp(maxActionCount ?? actions.Count, 0, actions.Count);

        for (var index = 0; index < actionCount; index++)
        {
            var action = actions[index];
            switch (action.Kind)
            {
                case InkkOopsInteractionRecorder.RecordedActionKind.WaitFrames:
                {
                    var frameCount = Math.Max(1, action.FrameCount ?? 1);
                    for (var frame = 0; frame < frameCount; frame++)
                    {
                        RunLayout(uiRoot, viewportWidth, viewportHeight, 16);
                    }

                    break;
                }
                case InkkOopsInteractionRecorder.RecordedActionKind.ResizeWindow:
                    viewportWidth = Math.Max(1, action.Width ?? viewportWidth);
                    viewportHeight = Math.Max(1, action.Height ?? viewportHeight);
                    break;
                case InkkOopsInteractionRecorder.RecordedActionKind.MovePointer:
                {
                    var previous = hasPointer ? pointer : new Vector2(action.X ?? 0, action.Y ?? 0);
                    pointer = new Vector2(action.X ?? 0, action.Y ?? 0);
                    hasPointer = true;
                    var keyboard = CreateReplayKeyboardState(heldKeys);
                    uiRoot.RunInputDeltaForTests(CreateRecordedInputDelta(previous, pointer, keyboard, keyboard, pointerMoved: true));
                    break;
                }
                case InkkOopsInteractionRecorder.RecordedActionKind.PointerDown:
                {
                    var previous = hasPointer ? pointer : new Vector2(action.X ?? 0, action.Y ?? 0);
                    pointer = new Vector2(action.X ?? 0, action.Y ?? 0);
                    hasPointer = true;
                    var keyboard = CreateReplayKeyboardState(heldKeys);
                    uiRoot.RunInputDeltaForTests(CreateRecordedInputDelta(previous, pointer, keyboard, keyboard, buttonPressed: MouseButton.Left));
                    break;
                }
                case InkkOopsInteractionRecorder.RecordedActionKind.PointerUp:
                {
                    var previous = hasPointer ? pointer : new Vector2(action.X ?? 0, action.Y ?? 0);
                    pointer = new Vector2(action.X ?? 0, action.Y ?? 0);
                    hasPointer = true;
                    var keyboard = CreateReplayKeyboardState(heldKeys);
                    uiRoot.RunInputDeltaForTests(CreateRecordedInputDelta(previous, pointer, keyboard, keyboard, buttonReleased: MouseButton.Left));
                    break;
                }
                case InkkOopsInteractionRecorder.RecordedActionKind.Wheel:
                {
                    var keyboard = CreateReplayKeyboardState(heldKeys);
                    uiRoot.RunInputDeltaForTests(CreateRecordedInputDelta(pointer, pointer, keyboard, keyboard, wheelDelta: action.WheelDelta ?? 0));
                    break;
                }
                case InkkOopsInteractionRecorder.RecordedActionKind.KeyDown:
                {
                    if (action.Key is not Keys keyDown)
                    {
                        break;
                    }

                    var previousKeyboard = CreateReplayKeyboardState(heldKeys);
                    heldKeys.Add(keyDown);
                    var currentKeyboard = CreateReplayKeyboardState(heldKeys);
                    uiRoot.RunInputDeltaForTests(CreateRecordedInputDelta(pointer, pointer, previousKeyboard, currentKeyboard, pressedKeys: [keyDown]));
                    break;
                }
                case InkkOopsInteractionRecorder.RecordedActionKind.KeyUp:
                {
                    if (action.Key is not Keys keyUp)
                    {
                        break;
                    }

                    var previousKeyboard = CreateReplayKeyboardState(heldKeys);
                    heldKeys.Remove(keyUp);
                    var currentKeyboard = CreateReplayKeyboardState(heldKeys);
                    uiRoot.RunInputDeltaForTests(CreateRecordedInputDelta(pointer, pointer, previousKeyboard, currentKeyboard, releasedKeys: [keyUp]));
                    break;
                }
                case InkkOopsInteractionRecorder.RecordedActionKind.TextInput:
                {
                    if (action.Character is not char character)
                    {
                        break;
                    }

                    var keyboard = CreateReplayKeyboardState(heldKeys);
                    uiRoot.RunInputDeltaForTests(CreateRecordedInputDelta(pointer, pointer, keyboard, keyboard, textInput: [character]));
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unsupported recorded action kind '{action.Kind}' at index {index}.");
            }
        }
    }

    private static InputDelta CreateRecordedInputDelta(
        Vector2 previous,
        Vector2 current,
        KeyboardState previousKeyboard,
        KeyboardState currentKeyboard,
        IReadOnlyList<Keys>? pressedKeys = null,
        IReadOnlyList<Keys>? releasedKeys = null,
        IReadOnlyList<char>? textInput = null,
        bool pointerMoved = false,
        MouseButton? buttonPressed = null,
        MouseButton? buttonReleased = null,
        int wheelDelta = 0)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(previousKeyboard, default, previous),
            Current = new InputSnapshot(currentKeyboard, default, current),
            PressedKeys = pressedKeys ?? Array.Empty<Keys>(),
            ReleasedKeys = releasedKeys ?? Array.Empty<Keys>(),
            TextInput = textInput ?? Array.Empty<char>(),
            PointerMoved = pointerMoved || buttonPressed != null || buttonReleased != null,
            WheelDelta = wheelDelta,
            LeftPressed = buttonPressed == MouseButton.Left,
            LeftReleased = buttonReleased == MouseButton.Left,
            RightPressed = buttonPressed == MouseButton.Right,
            RightReleased = buttonReleased == MouseButton.Right,
            MiddlePressed = buttonPressed == MouseButton.Middle,
            MiddleReleased = buttonReleased == MouseButton.Middle
        };
    }

    private static KeyboardState CreateReplayKeyboardState(HashSet<Keys> heldKeys)
    {
        return heldKeys.Count == 0 ? default : new KeyboardState([.. heldKeys.OrderBy(static key => (int)key)]);
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

    private static void AssertLineNumberRowsDisjoint(IDEEditorLineNumberPresenter panel, IDE_Editor editor, string recordingPath)
    {
        var rowBounds = panel.GetVisualChildren()
            .OfType<TextBlock>()
            .Select(ResolveRenderedBounds)
            .OrderBy(static bounds => bounds.Y)
            .ToArray();

        Assert.True(
            rowBounds.Length > 2,
            $"Expected the recording replay from '{recordingPath}' to render multiple gutter rows, but only found {rowBounds.Length}. firstVisible={panel.FirstVisibleLine} visibleCount={panel.VisibleLineCount} verticalOffset={editor.VerticalOffset:0.###}.");

        for (var index = 1; index < rowBounds.Length; index++)
        {
            Assert.True(
                rowBounds[index].Y + 0.25f >= rowBounds[index - 1].Y,
                $"Expected gutter rows to remain vertically ordered after replay '{recordingPath}', but row {index - 1}={FormatRect(rowBounds[index - 1])} and row {index}={FormatRect(rowBounds[index])}.");
            Assert.True(
                rowBounds[index].Y >= rowBounds[index - 1].Y + rowBounds[index - 1].Height - 0.5f,
                $"Expected gutter rows to stay disjoint after replay '{recordingPath}', but row {index - 1}={FormatRect(rowBounds[index - 1])} overlaps row {index}={FormatRect(rowBounds[index])}. firstVisible={panel.FirstVisibleLine} visibleCount={panel.VisibleLineCount} lineHeight={panel.LineHeight:0.###} verticalLineOffset={panel.VerticalLineOffset:0.###} editorOffset={editor.VerticalOffset:0.###}.");
        }
    }

    private static void AssertLineNumberGutterWidthRemainsReadable(IDEEditorLineNumberPresenter panel, float lineNumberBorderWidth, string recordingPath)
    {
        var textBounds = panel.GetVisualChildren()
            .OfType<TextBlock>()
            .Select(ResolveRenderedBounds)
            .ToArray();

        Assert.NotEmpty(textBounds);

        var widestTextWidth = textBounds.Max(static bounds => bounds.Width);
        Assert.True(
            lineNumberBorderWidth + 0.5f >= widestTextWidth,
            $"Expected the line-number gutter width to remain readable after replay '{recordingPath}', but borderWidth={lineNumberBorderWidth:0.###} and widestRenderedTextWidth={widestTextWidth:0.###}." );
    }

    private static LayoutRect GetPrivateCaretRenderRect(RichTextBox editor)
    {
        Assert.True(editor.TryGetViewportLayoutSnapshot(out var viewportSnapshot));

        var method = typeof(RichTextBox).GetMethod("TryGetCaretRenderRect", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var args = new object?[]
        {
            viewportSnapshot.TextRect,
            viewportSnapshot.Layout,
            viewportSnapshot.HorizontalOffset,
            viewportSnapshot.VerticalOffset,
            null
        };

        var returned = method!.Invoke(editor, args);
        Assert.True(returned is bool success && success, "Expected private TryGetCaretRenderRect to succeed for the replayed source editor.");
        Assert.NotNull(args[4]);
        return Assert.IsType<LayoutRect>(args[4]);
    }

    private static Matrix GetCombinedRenderTransformToRoot(UIElement element)
    {
        var transform = Matrix.Identity;
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalRenderTransformSnapshot(out var localTransform))
            {
                continue;
            }

            transform *= localTransform;
        }

        return transform;
    }

    private static float GetXAxisScale(Matrix transform)
    {
        return MathF.Sqrt((transform.M11 * transform.M11) + (transform.M12 * transform.M12));
    }

    private static float GetYAxisScale(Matrix transform)
    {
        return MathF.Sqrt((transform.M21 * transform.M21) + (transform.M22 * transform.M22));
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
        IDE_Editor sourceEditor,
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

    private static CompletionWheelTelemetryResult RunDesignerCompletionWheelTelemetryScenario(Action<InkkSlinger.Designer.DesignerShellView, IDE_Editor, UiRoot>? configure = null)
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

    private static CompletionWheelTelemetryResult RunStandaloneSourceEditorCompletionWheelTelemetryScenario(Action<InkkSlinger.Designer.DesignerSourceEditorView, IDE_Editor, UiRoot>? configure = null)
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
        IDE_Editor sourceEditor,
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
        return Assert.IsType<Popup>(sourceEditorView.FindName("CompletionPopup"));
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

    private static IReadOnlyList<Run> GetDocumentRuns(FlowDocument document)
    {
        return document.Blocks
            .OfType<Paragraph>()
            .SelectMany(static paragraph => paragraph.Inlines.OfType<Run>())
            .Where(static run => !string.IsNullOrEmpty(run.Text))
            .ToArray();
    }

    private static int GetLeadingWhitespaceCount(string text)
    {
        var count = 0;
        while (count < text.Length && char.IsWhiteSpace(text[count]))
        {
            count++;
        }

        return count;
    }

    private static IReadOnlyList<TextBox> GetSourceInspectorCompositeTextEditors(
        InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView,
        string propertyName)
    {
        var row = GetSourceInspectorPropertyRow(sourceEditorView, propertyName);
        var editors = new List<TextBox>();
        CollectDescendants(row, editors);
        editors.Sort(static (left, right) => left.LayoutSlot.X.CompareTo(right.LayoutSlot.X));

        if (editors.Count != 4)
        {
            throw new Xunit.Sdk.XunitException($"Expected four composite text editors for '{propertyName}', but found {editors.Count}.");
        }

        return editors;
    }

    private static (TextBox TextBox, ComboBox ComboBox) GetSourceInspectorTextChoiceEditors(
        InkkSlinger.Designer.DesignerSourceEditorView sourceEditorView,
        string propertyName)
    {
        var row = GetSourceInspectorPropertyRow(sourceEditorView, propertyName);
        var textBox = FindDescendantOrDefault<TextBox>(row);
        var comboBox = FindDescendantOrDefault<ComboBox>(row);

        Assert.NotNull(textBox);
        Assert.NotNull(comboBox);

        return (textBox!, comboBox!);
    }

    private static Popup GetSourceInspectorColorEditorPopup(ComboBox editor)
    {
        return Assert.IsType<Popup>(GetSourceInspectorColorEditorHost(editor).FindName("InteractivePopup"));
    }

    private static RecordedColorEditorScenario CreateRecordedColorEditorScenario(string initialBackgroundHex)
    {
        var shell = new InkkSlinger.Designer.DesignerShellView
        {
            SourceText = CreateBorderOnlyViewXml(initialBackgroundHex)
        };

        var sourceEditor = shell.SourceEditorControl;
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, 1280, 820, 16);
        RunLayout(uiRoot, 1280, 820, 16);

        SelectControlTagForSourceInspector(shell, sourceEditor, uiRoot, "Border");

        var propertyEditors = GetSourceInspectorPropertyEditors(shell.SourceEditorView);
        var backgroundEditor = Assert.IsAssignableFrom<ComboBox>(propertyEditors["Background"]);

        EnsureSourceInspectorPropertyVisible(shell.SourceEditorView, uiRoot, backgroundEditor, "Background");
        OpenSourceInspectorColorEditor(uiRoot, backgroundEditor);
        RunLayout(uiRoot, 1280, 820, 16);
        RunLayout(uiRoot, 1280, 820, 16);

        var popup = GetSourceInspectorColorEditorPopup(backgroundEditor);
        Assert.True(popup.IsOpen);

        return new RecordedColorEditorScenario(
            shell,
            uiRoot,
            GetSourceInspectorColorEditorHost(backgroundEditor),
            backgroundEditor,
            popup,
            GetSourceInspectorColorEditorColorPicker(backgroundEditor),
            GetSourceInspectorColorEditorHueSpectrum(backgroundEditor),
            GetSourceInspectorColorEditorAlphaSpectrum(backgroundEditor));
    }

    private static string CreateBorderOnlyViewXml(string backgroundHex)
    {
        return $$"""
            <UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         Background="#101820">
              <StackPanel>
                <Border x:Name="ChromeBorder"
                        Background="{{backgroundHex}}"
                        BorderBrush="#334455"
                        BorderThickness="1" />
              </StackPanel>
            </UserControl>
            """;
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

    private static void AssertClose(float expected, float actual, float tolerance)
    {
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    private static string FormatDesignerColorValue(Color color)
    {
        return color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
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

    private static FrameworkElement GetLastViewportIntersectingListBoxItem(ListBox listBox, ScrollViewer scrollViewer)
    {
        var host = FindListBoxItemsHostPanel(listBox);
        var viewportTop = scrollViewer.LayoutSlot.Y;
        var viewportBottom = scrollViewer.LayoutSlot.Y + scrollViewer.ViewportHeight;
        FrameworkElement? best = null;
        var bestBottom = float.NegativeInfinity;

        foreach (var child in host.Children)
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
                best = element;
                bestBottom = elementBottom;
            }
        }

        return best ?? throw new Xunit.Sdk.XunitException("Expected ListBox items host to contain an item intersecting the viewport.");
    }

    private static bool ComboBoxItemHasRenderedText(ComboBoxItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Text))
        {
            return true;
        }

        var textBlocks = new List<TextBlock>();
        CollectDescendants(item, textBlocks, static textBlock => !string.IsNullOrWhiteSpace(textBlock.Text));
        return textBlocks.Count > 0;
    }

    private static (FrameworkElement Element, int Index) GetHighestRealizedIndexListBoxItem(ListBox listBox)
    {
        var host = FindListBoxItemsHostPanel(listBox);
        FrameworkElement? bestElement = null;
        var bestIndex = -1;

        foreach (var child in host.Children)
        {
            if (child is not FrameworkElement element)
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
            : throw new Xunit.Sdk.XunitException("Expected ListBox items host to contain at least one realized item container with generated item info.");
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TElement? FindAncestorOrSelf<TElement>(UIElement? element)
        where TElement : UIElement
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
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

    private readonly record struct RecordedColorEditorScenario(
        InkkSlinger.Designer.DesignerShellView Shell,
        UiRoot UiRoot,
        InkkSlinger.Designer.DesignerSourceColorPropertyEditor EditorHost,
        ComboBox BackgroundEditor,
        Popup Popup,
        ColorPicker ColorPicker,
        ColorSpectrum HueSpectrum,
        ColorSpectrum AlphaSpectrum);

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
        IDE_Editor sourceEditor,
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
        AnimationManager.Current.ResetForTests();
        UiApplication.Current.ResetForTests();
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();
        InputGestureService.Clear();
        Popup.ResetForTests();
        TextClipboard.ResetForTests();
        UiTextRenderer.ConfigureRuntimeServicesForTests();
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

    private static string GetLineNumberText(IDEEditorLineNumberPresenter panel, int index)
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

    private static int GetRenderedLineNumberCount(IDEEditorLineNumberPresenter panel)
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
        public Dictionary<string, string> WrittenTexts { get; } = new(StringComparer.Ordinal);

        public bool Exists(string path)
        {
            return WrittenTexts.ContainsKey(path);
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
