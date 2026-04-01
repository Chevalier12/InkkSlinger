using System.Reflection;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CheckBoxTelemetryTests
{
    [Fact]
    public void CheckBox_RuntimeTelemetry_CapturesMeasureStyle_AndDrawTextNoOpPaths()
    {
        _ = CheckBox.GetTelemetryAndReset();

        var checkBox = new CheckBox
        {
            Content = "Telemetry",
            Padding = new Thickness(2f),
            FontSize = 14f,
            IsChecked = true
        };

        checkBox.Measure(new Vector2(120f, 40f));
        checkBox.Arrange(new LayoutRect(0f, 0f, 120f, 40f));

        Assert.NotNull(InvokeGetFallbackStyle(checkBox));
        Assert.NotNull(InvokeGetFallbackStyle(checkBox));

        InvokeDrawText(checkBox, new LayoutRect(0f, 0f, 0f, 0f), 14f);

        checkBox.Content = string.Empty;
        checkBox.InvalidateMeasure();
        checkBox.Measure(new Vector2(120f, 40f));
        checkBox.Arrange(new LayoutRect(0f, 0f, 120f, 40f));
        InvokeDrawText(checkBox, new LayoutRect(0f, 0f, 0f, 0f), 14f);

        var snapshot = checkBox.GetCheckBoxSnapshotForDiagnostics();

        Assert.False(snapshot.HasTemplateRoot);
        Assert.True(snapshot.IsEnabled);
        Assert.True(snapshot.IsChecked);
        Assert.False(snapshot.IsThreeState);
        Assert.Equal(nameof(String), snapshot.ContentType);
        Assert.Equal(string.Empty, snapshot.DisplayText);
        Assert.True(snapshot.MeasureOverrideCallCount >= 2);
        Assert.True(snapshot.MeasureOverrideSelfLayoutPathCount >= 2);
        Assert.True(snapshot.GetGlyphSizeCallCount > 0);
        Assert.True(snapshot.GetGlyphSpacingCallCount > 0);
        Assert.Equal(2, snapshot.GetFallbackStyleCallCount);
        Assert.Equal(2, snapshot.GetFallbackStyleCacheHitCount + snapshot.GetFallbackStyleCacheMissCount);
        Assert.True(snapshot.MeasureTextCallCount >= 2);
        Assert.True(snapshot.MeasureTextEmptyTextCount > 0);
        Assert.True(snapshot.MeasureTextLayoutCallCount > 0);
        Assert.Equal(2, snapshot.DrawTextCallCount);
        Assert.Equal(1, snapshot.DrawTextEmptyTextCount);
        Assert.Equal(1, snapshot.DrawTextNoSpaceCount);
    }

    [Fact]
    public void CheckBox_AggregateTelemetry_CapturesActivity_AndResets()
    {
        _ = CheckBox.GetTelemetryAndReset();

        var first = new CheckBox
        {
            Content = "Telemetry",
            Padding = new Thickness(2f)
        };
        first.Measure(new Vector2(120f, 40f));
        Assert.NotNull(InvokeGetFallbackStyle(first));
        InvokeDrawText(first, new LayoutRect(0f, 0f, 0f, 0f), 14f);

        var second = new CheckBox
        {
            Content = string.Empty,
            Padding = new Thickness(2f)
        };
        second.Measure(new Vector2(120f, 40f));
        Assert.NotNull(InvokeGetFallbackStyle(second));
        InvokeDrawText(second, new LayoutRect(0f, 0f, 0f, 0f), 14f);

        var diagnostics = CheckBox.GetAggregateTelemetrySnapshotForDiagnostics();

        Assert.Equal(2, diagnostics.ConstructorCallCount);
        Assert.True(diagnostics.MeasureOverrideCallCount >= 2);
        Assert.True(diagnostics.MeasureOverrideSelfLayoutPathCount >= 2);
        Assert.Equal(2, diagnostics.GetFallbackStyleCallCount);
        Assert.Equal(2, diagnostics.GetFallbackStyleCacheHitCount + diagnostics.GetFallbackStyleCacheMissCount);
        Assert.True(diagnostics.GetGlyphSizeCallCount > 0);
        Assert.True(diagnostics.GetGlyphSpacingCallCount > 0);
        Assert.True(diagnostics.MeasureTextCallCount >= 2);
        Assert.True(diagnostics.MeasureTextEmptyTextCount > 0);
        Assert.True(diagnostics.MeasureTextLayoutCallCount > 0);
        Assert.Equal(2, diagnostics.DrawTextCallCount);
        Assert.Equal(1, diagnostics.DrawTextEmptyTextCount);
        Assert.Equal(1, diagnostics.DrawTextNoSpaceCount);

        var aggregate = CheckBox.GetTelemetryAndReset();

        Assert.Equal(2, aggregate.ConstructorCallCount);
        Assert.True(aggregate.MeasureOverrideCallCount >= 2);
        Assert.True(aggregate.GetFallbackStyleCallCount >= 2);
        Assert.True(aggregate.MeasureTextLayoutCallCount > 0);
        Assert.True(aggregate.MeasureTextEmptyTextCount > 0);
        Assert.Equal(2, aggregate.DrawTextCallCount);
        Assert.Equal(1, aggregate.DrawTextEmptyTextCount);
        Assert.Equal(1, aggregate.DrawTextNoSpaceCount);

        var cleared = CheckBox.GetTelemetryAndReset();

        Assert.Equal(0, cleared.ConstructorCallCount);
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.GetFallbackStyleCallCount);
        Assert.Equal(0, cleared.MeasureTextCallCount);
        Assert.Equal(0, cleared.DrawTextCallCount);
    }

    private static Style? InvokeGetFallbackStyle(CheckBox checkBox)
    {
        var method = typeof(CheckBox).GetMethod("GetFallbackStyle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (Style?)method!.Invoke(checkBox, null);
    }

    private static void InvokeDrawText(CheckBox checkBox, LayoutRect slot, float glyphSize)
    {
        var method = typeof(CheckBox).GetMethod("DrawText", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(checkBox, [null, slot, glyphSize]);
    }
}