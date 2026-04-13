using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class FontDependencyPropertyIdentityTests
{
    [Fact]
    public void ControlDerivedTypes_InheritFrameworkTypographyProperties()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            ClearApplicationResources();

            var expectedFamily = new FontFamily("Segoe UI");
            var host = new StackPanel
            {
                FontFamily = expectedFamily,
                FontSize = 18f,
                FontWeight = "SemiBold",
                FontStyle = "Italic"
            };

            var descendants = new FrameworkElement[]
            {
                new Button(),
                new TextBox(),
                new PasswordBox(),
                new RichTextBox(),
                new ComboBox(),
                new MenuItem(),
                new TabControl(),
                new Popup(),
                new GroupBox(),
                new Expander(),
                new StatusBarItem(),
                new DocumentViewer(),
                new DataGrid(),
                new DataGridCell(),
                new DataGridRowHeader(),
                new TreeView(),
                new TreeViewItem()
            };

            foreach (var descendant in descendants)
            {
                host.AddChild(descendant);

                Assert.Equal(expectedFamily, descendant.FontFamily);
                Assert.Equal(18f, descendant.FontSize);
                Assert.Equal("SemiBold", descendant.FontWeight);
                Assert.Equal("Italic", descendant.FontStyle);
                Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontFamilyProperty));
                Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontSizeProperty));
                Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontWeightProperty));
                Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontStyleProperty));
            }
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void Window_KeepsDistinctTypographyDependencyProperties()
    {
        Assert.NotSame(FrameworkElement.FontFamilyProperty, Window.FontFamilyProperty);
        Assert.NotSame(FrameworkElement.FontSizeProperty, Window.FontSizeProperty);
        Assert.NotSame(FrameworkElement.FontWeightProperty, Window.FontWeightProperty);
        Assert.NotSame(FrameworkElement.FontStyleProperty, Window.FontStyleProperty);
    }

    [Fact]
    public void InheritedTypographyLookup_CachesEffectiveValues_OnDescendantWithoutLocalEntries()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            ClearApplicationResources();

            var host = new StackPanel
            {
                FontFamily = new FontFamily("Cascadia Code"),
                FontSize = 17f,
                FontWeight = "SemiBold",
                FontStyle = "Italic"
            };

            var descendant = new TextBlock
            {
                Text = "Typography cache regression"
            };

            host.AddChild(descendant);

            var typography = UiTypography.FromElement(descendant);

            Assert.Equal("Cascadia Code", typography.Family);
            Assert.Equal(17f, typography.Size);
            Assert.Equal("SemiBold", typography.Weight);
            Assert.Equal("Italic", typography.Style);

            AssertCachedEffectiveValue(descendant, FrameworkElement.FontFamilyProperty, new FontFamily("Cascadia Code"), DependencyPropertyValueSource.Inherited);
            AssertCachedEffectiveValue(descendant, FrameworkElement.FontSizeProperty, 17f, DependencyPropertyValueSource.Inherited);
            AssertCachedEffectiveValue(descendant, FrameworkElement.FontWeightProperty, "SemiBold", DependencyPropertyValueSource.Inherited);
            AssertCachedEffectiveValue(descendant, FrameworkElement.FontStyleProperty, "Italic", DependencyPropertyValueSource.Inherited);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void InheritedTypographyCache_Recomputes_WhenAncestorValueChanges()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            ClearApplicationResources();

            var host = new StackPanel
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 16f,
                FontWeight = "Normal",
                FontStyle = "Normal"
            };

            var descendant = new TextBlock
            {
                Text = "Typography cache invalidation regression"
            };

            host.AddChild(descendant);

            _ = UiTypography.FromElement(descendant);

            host.FontFamily = new FontFamily("Consolas");
            host.FontSize = 20f;
            host.FontWeight = "Bold";
            host.FontStyle = "Italic";

            var typography = UiTypography.FromElement(descendant);

            Assert.Equal("Consolas", typography.Family);
            Assert.Equal(20f, typography.Size);
            Assert.Equal("Bold", typography.Weight);
            Assert.Equal("Italic", typography.Style);

            AssertCachedEffectiveValue(descendant, FrameworkElement.FontFamilyProperty, new FontFamily("Consolas"), DependencyPropertyValueSource.Inherited);
            AssertCachedEffectiveValue(descendant, FrameworkElement.FontSizeProperty, 20f, DependencyPropertyValueSource.Inherited);
            AssertCachedEffectiveValue(descendant, FrameworkElement.FontWeightProperty, "Bold", DependencyPropertyValueSource.Inherited);
            AssertCachedEffectiveValue(descendant, FrameworkElement.FontStyleProperty, "Italic", DependencyPropertyValueSource.Inherited);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static Dictionary<object, object> SnapshotApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void ClearApplicationResources()
    {
        UiApplication.Current.Resources.ReplaceContents(Array.Empty<KeyValuePair<object, object>>(), notifyChanged: false);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static void AssertCachedEffectiveValue(
        DependencyObject target,
        DependencyProperty dependencyProperty,
        object expectedValue,
        DependencyPropertyValueSource expectedSource)
    {
        var valuesField = typeof(DependencyObject).GetField("_values", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(valuesField);

        var values = Assert.IsAssignableFrom<IDictionary>(valuesField!.GetValue(target));
        Assert.True(values.Contains(dependencyProperty));

        var entry = values[dependencyProperty];
        Assert.NotNull(entry);

        var entryType = entry!.GetType();
        var hasCachedEffectiveValue = Assert.IsType<bool>(entryType.GetField("HasCachedEffectiveValue")!.GetValue(entry));
        var effectiveValue = entryType.GetField("EffectiveValue")!.GetValue(entry);
        var effectiveSource = Assert.IsType<DependencyPropertyValueSource>(entryType.GetField("EffectiveSource")!.GetValue(entry));

        Assert.True(hasCachedEffectiveValue);
        Assert.Equal(expectedValue, effectiveValue);
        Assert.Equal(expectedSource, effectiveSource);
    }
}
