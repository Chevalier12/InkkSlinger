using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using InkkSlinger;

namespace InkkSlinger.Designer;

public enum DesignerPreviewState
{
    Idle,
    Success,
    Error
}

public enum DesignerDiagnosticLevel
{
    Warning,
    Error
}

public sealed record DesignerDiagnosticEntry(
    DesignerDiagnosticLevel Level,
    XamlDiagnosticCode Code,
    string Message,
    string? ElementName,
    string? PropertyName,
    int? Line,
    int? Position,
    string? Hint)
{
    public string TargetDescription => string.IsNullOrWhiteSpace(ElementName)
        ? (string.IsNullOrWhiteSpace(PropertyName) ? "Document" : PropertyName!)
        : (string.IsNullOrWhiteSpace(PropertyName)
            ? ElementName!
            : string.Create(CultureInfo.InvariantCulture, $"{ElementName}.{PropertyName}"));

    public string LocationText => Line.HasValue
        ? string.Create(CultureInfo.InvariantCulture, $"Line {Line}, Col {Position ?? 1}")
        : "No location";
}

public sealed record DesignerVisualNode(
    string Id,
    string Label,
    string TypeName,
    string? ElementName,
    int VisualChildCount,
    IReadOnlyList<DesignerVisualNode> Children);

public sealed record DesignerInspectorProperty(string Name, string Value);

public sealed record DesignerInspectorModel(string Header, IReadOnlyList<DesignerInspectorProperty> Properties)
{
    public static readonly DesignerInspectorModel Empty = new(
        "Nothing selected",
        Array.Empty<DesignerInspectorProperty>());
}

public sealed class DesignerController
{
    private static readonly string[] PreferredInspectorPropertyNames =
    [
        nameof(FrameworkElement.Width),
        nameof(FrameworkElement.Height),
        nameof(FrameworkElement.MinWidth),
        nameof(FrameworkElement.MinHeight),
        nameof(FrameworkElement.MaxWidth),
        nameof(FrameworkElement.MaxHeight),
        nameof(FrameworkElement.Margin),
        nameof(FrameworkElement.HorizontalAlignment),
        nameof(FrameworkElement.VerticalAlignment),
        nameof(UIElement.Visibility),
        nameof(UIElement.IsEnabled),
        nameof(UIElement.Opacity),
        nameof(Control.Background),
        nameof(Control.Foreground),
        nameof(Control.BorderBrush),
        nameof(Control.BorderThickness),
        nameof(Control.Padding),
        nameof(ContentControl.Content),
        nameof(TextBlock.Text),
        nameof(TextBlock.TextWrapping),
        nameof(TextBlock.LineHeight),
        nameof(StackPanel.Orientation),
        nameof(Border.CornerRadius)
    ];

    private static readonly HashSet<string> PreferredInspectorPropertyNameSet = new(PreferredInspectorPropertyNames, StringComparer.Ordinal);

    private readonly Dictionary<string, UIElement> _elementsByNodeId = new(StringComparer.Ordinal);

    public string SourceText { get; private set; } = string.Empty;

    public DesignerPreviewState PreviewState { get; private set; } = DesignerPreviewState.Idle;

    public bool LastRefreshSucceeded => PreviewState == DesignerPreviewState.Success;

    public UserControl? PreviewRoot { get; private set; }

    public string? PreviewFailureMessage { get; private set; }

    public IReadOnlyList<DesignerDiagnosticEntry> Diagnostics { get; private set; } = Array.Empty<DesignerDiagnosticEntry>();

    public DesignerVisualNode? VisualTreeRoot { get; private set; }

    public string? SelectedNodeId { get; private set; }

    public DesignerInspectorModel Inspector { get; private set; } = DesignerInspectorModel.Empty;

    public bool Refresh(string rawXml)
    {
        SourceText = rawXml ?? string.Empty;

        var capturedDiagnostics = new List<XamlDiagnostic>();
        try
        {
            using var sink = XamlLoader.PushDiagnosticSink(capturedDiagnostics.Add);
            var loadedRoot = XamlLoader.LoadFromString(SourceText);
            if (loadedRoot is not UserControl previewRoot)
            {
                throw new InvalidOperationException("Designer preview requires a UserControl root element.");
            }

            _elementsByNodeId.Clear();
            PreviewRoot = previewRoot;
            PreviewFailureMessage = null;
            PreviewState = DesignerPreviewState.Success;
            Diagnostics = MapDiagnostics(capturedDiagnostics);
            VisualTreeRoot = BuildVisualTree(previewRoot, "0");

            if (VisualTreeRoot != null)
            {
                SelectVisualNode(VisualTreeRoot.Id);
            }
            else
            {
                SelectedNodeId = null;
                Inspector = DesignerInspectorModel.Empty;
            }

            return true;
        }
        catch (Exception ex)
        {
            PreviewRoot = null;
            PreviewFailureMessage = ex.Message;
            PreviewState = DesignerPreviewState.Error;
            VisualTreeRoot = null;
            SelectedNodeId = null;
            Inspector = DesignerInspectorModel.Empty;
            _elementsByNodeId.Clear();

            var mappedDiagnostics = new List<DesignerDiagnosticEntry>(MapDiagnostics(capturedDiagnostics));
            if (mappedDiagnostics.Count == 0)
            {
                mappedDiagnostics.Add(
                    new DesignerDiagnosticEntry(
                        DesignerDiagnosticLevel.Error,
                        XamlDiagnosticCode.GeneralFailure,
                        ex.Message,
                        null,
                        null,
                        null,
                        null,
                        null));
            }

            Diagnostics = mappedDiagnostics;
            return false;
        }
    }

    public bool SelectVisualNode(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !_elementsByNodeId.TryGetValue(nodeId, out var element))
        {
            SelectedNodeId = null;
            Inspector = DesignerInspectorModel.Empty;
            return false;
        }

        SelectedNodeId = nodeId;
        Inspector = BuildInspector(nodeId, element);
        return true;
    }

    private DesignerVisualNode BuildVisualTree(UIElement element, string nodeId)
    {
        _elementsByNodeId[nodeId] = element;

        var children = element
            .GetVisualChildren()
            .Select((child, index) => BuildVisualTree(child, string.Create(CultureInfo.InvariantCulture, $"{nodeId}.{index}")))
            .ToArray();

        var typeName = element.GetType().Name;
        var elementName = element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name)
            ? frameworkElement.Name
            : null;

        return new DesignerVisualNode(
            nodeId,
            BuildNodeLabel(typeName, elementName),
            typeName,
            elementName,
            children.Length,
            children);
    }

    private static DesignerInspectorModel BuildInspector(string nodeId, UIElement element)
    {
        var typeName = element.GetType().Name;
        var elementName = element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name)
            ? frameworkElement.Name
            : "(unnamed)";
        var properties = new List<DesignerInspectorProperty>
        {
            new("Node", nodeId),
            new("Type", typeName),
            new("Name", elementName),
            new("Visual Children", element.GetVisualChildren().Count().ToString(CultureInfo.InvariantCulture)),
            new("Is Enabled", element.IsEnabled ? "True" : "False")
        };

        if (element is FrameworkElement framework)
        {
            properties.Add(new("Actual Size", FormatSize(framework.ActualWidth, framework.ActualHeight)));
            properties.Add(new("Desired Size", FormatVector(framework.DesiredSize)));
        }

        properties.AddRange(BuildDependencyPropertyInspectorRows(element));

        return new DesignerInspectorModel(BuildNodeLabel(typeName, elementName == "(unnamed)" ? null : elementName), properties);
    }

    private static IReadOnlyList<DesignerDiagnosticEntry> MapDiagnostics(IEnumerable<XamlDiagnostic> diagnostics)
    {
        return diagnostics
            .Select(
                diagnostic => new DesignerDiagnosticEntry(
                    MapDiagnosticLevel(diagnostic),
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.ElementName,
                    diagnostic.PropertyName,
                    diagnostic.Line,
                    diagnostic.Position,
                    diagnostic.Hint))
                    .Distinct()
                    .OrderBy(static diagnostic => diagnostic.Line ?? int.MaxValue)
                    .ThenBy(static diagnostic => diagnostic.Position ?? int.MaxValue)
                    .ThenBy(static diagnostic => diagnostic.Code)
                    .ThenBy(static diagnostic => diagnostic.ElementName ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(static diagnostic => diagnostic.PropertyName ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static DesignerDiagnosticLevel MapDiagnosticLevel(XamlDiagnostic diagnostic)
    {
        return diagnostic.Code == XamlDiagnosticCode.UnsupportedConstruct
            ? DesignerDiagnosticLevel.Warning
            : DesignerDiagnosticLevel.Error;
    }

    private static string BuildNodeLabel(string typeName, string? elementName)
    {
        return string.IsNullOrWhiteSpace(elementName)
            ? typeName
            : string.Create(CultureInfo.InvariantCulture, $"{elementName} : {typeName}");
    }

    private static IReadOnlyList<DesignerInspectorProperty> BuildDependencyPropertyInspectorRows(UIElement element)
    {
        return ResolveInspectableDependencyProperties(element)
            .Where(property => ShouldShowInspectorProperty(element, property))
            .OrderBy(property => GetPreferredInspectorPropertyIndex(property.Name))
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .Select(
                property =>
                {
                    var value = element.GetValue(property);
                    var valueSource = element.GetValueSource(property);
                    var formattedValue = FormatInspectorValue(value);
                    if (valueSource != DependencyPropertyValueSource.Default)
                    {
                        formattedValue = string.Create(CultureInfo.InvariantCulture, $"{formattedValue} ({valueSource})");
                    }

                    return new DesignerInspectorProperty(property.Name, formattedValue);
                })
            .ToArray();
    }

    private static IReadOnlyList<DependencyProperty> ResolveInspectableDependencyProperties(UIElement element)
    {
        return DependencyProperty
            .GetRegisteredProperties()
            .Where(property => !property.IsAttached && property.IsApplicableTo(element))
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Select(
                group => group
                    .OrderBy(property => GetOwnerTypeDistance(element.GetType(), property.OwnerType))
                    .ThenBy(property => property.OwnerType.Name, StringComparer.Ordinal)
                    .First())
            .ToArray();
    }

    private static bool ShouldShowInspectorProperty(UIElement element, DependencyProperty property)
    {
        if (property.Name is nameof(FrameworkElement.Name) or nameof(FrameworkElement.Tag))
        {
            return false;
        }

        var valueSource = element.GetValueSource(property);
        if (valueSource != DependencyPropertyValueSource.Default)
        {
            return true;
        }

        return PreferredInspectorPropertyNameSet.Contains(property.Name);
    }

    private static int GetPreferredInspectorPropertyIndex(string propertyName)
    {
        for (var index = 0; index < PreferredInspectorPropertyNames.Length; index++)
        {
            if (string.Equals(PreferredInspectorPropertyNames[index], propertyName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static int GetOwnerTypeDistance(Type currentType, Type ownerType)
    {
        var distance = 0;
        for (var type = currentType; type != null; type = type.BaseType)
        {
            if (type == ownerType)
            {
                return distance;
            }

            distance++;
        }

        return int.MaxValue;
    }

    private static string FormatInspectorValue(object? value)
    {
        return value switch
        {
            null => "(null)",
            string text => string.IsNullOrEmpty(text) ? "\"\"" : text,
            bool boolean => boolean ? "True" : "False",
            float number => FormatFloat(number),
            double doubleNumber => doubleNumber.ToString("0.##", CultureInfo.InvariantCulture),
            Thickness thickness => FormatThickness(thickness),
            CornerRadius radius => FormatCornerRadius(radius),
            Color color => FormatColor(color),
            SolidColorBrush solidColorBrush => FormatColor(solidColorBrush.Color),
            Brush brush => FormatColor(brush.ToColor()),
            UIElement element => BuildNodeLabel(element.GetType().Name, element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name) ? frameworkElement.Name : null),
            Enum enumValue => enumValue.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty
        };
    }

    private static string FormatSize(float width, float height)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{FormatFloat(width)} x {FormatFloat(height)}");
    }

    private static string FormatVector(Vector2 value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{FormatFloat(value.X)} x {FormatFloat(value.Y)}");
    }

    private static string FormatFloat(float value)
    {
        if (float.IsNaN(value))
        {
            return "NaN";
        }

        if (float.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (float.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatThickness(Thickness value)
    {
        if (value.Left == value.Top && value.Left == value.Right && value.Left == value.Bottom)
        {
            return FormatFloat(value.Left);
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatFloat(value.Left)}, {FormatFloat(value.Top)}, {FormatFloat(value.Right)}, {FormatFloat(value.Bottom)}");
    }

    private static string FormatCornerRadius(CornerRadius value)
    {
        if (value.TopLeft == value.TopRight && value.TopLeft == value.BottomRight && value.TopLeft == value.BottomLeft)
        {
            return FormatFloat(value.TopLeft);
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatFloat(value.TopLeft)}, {FormatFloat(value.TopRight)}, {FormatFloat(value.BottomRight)}, {FormatFloat(value.BottomLeft)}");
    }

    private static string FormatColor(Color value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}");
    }
}