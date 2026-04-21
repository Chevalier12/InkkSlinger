using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using InkkSlinger;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

internal sealed class DesignerSourceInspectorCompositeComponentItem : INotifyPropertyChanged
{
    private string _text = string.Empty;

    public DesignerSourceInspectorCompositeComponentItem(DesignerSourceInspectorPropertyItem owner, int index, string label)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Index = index;
        Label = label ?? throw new ArgumentNullException(nameof(label));
    }

    internal DesignerSourceInspectorPropertyItem Owner { get; }

    public int Index { get; }

    public string Label { get; }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}

internal sealed class DesignerSourceInspectorPropertyItem : INotifyPropertyChanged
{
    private string _descriptionText = string.Empty;
    private string _editorText = string.Empty;
    private string? _selectedChoice;
    private Color _selectedColor = Color.Transparent;
    private string _colorDisplayText = string.Empty;
    private Visibility _rowVisibility = Visibility.Visible;
    private readonly string[] _compositeFallbackValues = [string.Empty, string.Empty, string.Empty, string.Empty];

    public DesignerSourceInspectorPropertyItem(DesignerSourceInspectableProperty property)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        ChoiceValues = property.ChoiceValues;

        if (property.CompositeValueKind != DesignerSourceCompositeValueKind.None)
        {
            var labels = GetCompositeLabels(property.CompositeValueKind);
            CompositeComponent1 = new DesignerSourceInspectorCompositeComponentItem(this, 0, labels[0]);
            CompositeComponent2 = new DesignerSourceInspectorCompositeComponentItem(this, 1, labels[1]);
            CompositeComponent3 = new DesignerSourceInspectorCompositeComponentItem(this, 2, labels[2]);
            CompositeComponent4 = new DesignerSourceInspectorCompositeComponentItem(this, 3, labels[3]);
        }
    }

    private DesignerSourceInspectableProperty Property { get; }

    public string Name => Property.Name;

    public DesignerSourcePropertyEditorKind EditorKind => Property.EditorKind;

    public DesignerSourceCompositeValueKind CompositeValueKind => Property.CompositeValueKind;

    public IReadOnlyList<string> ChoiceValues { get; }

    public DesignerSourceInspectorCompositeComponentItem? CompositeComponent1 { get; }

    public DesignerSourceInspectorCompositeComponentItem? CompositeComponent2 { get; }

    public DesignerSourceInspectorCompositeComponentItem? CompositeComponent3 { get; }

    public DesignerSourceInspectorCompositeComponentItem? CompositeComponent4 { get; }

    public string DescriptionText
    {
        get => _descriptionText;
        set => SetField(ref _descriptionText, value);
    }

    public string EditorText
    {
        get => _editorText;
        set => SetField(ref _editorText, value);
    }

    public string? SelectedChoice
    {
        get => _selectedChoice;
        set => SetField(ref _selectedChoice, value);
    }

    public Color SelectedColor
    {
        get => _selectedColor;
        set => SetField(ref _selectedColor, value);
    }

    public string ColorDisplayText
    {
        get => _colorDisplayText;
        set => SetField(ref _colorDisplayText, value);
    }

    public Visibility RowVisibility
    {
        get => _rowVisibility;
        set => SetField(ref _rowVisibility, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetCompositeEditorValues(IReadOnlyList<string> values, bool preserveVisibleValues)
    {
        if (CompositeValueKind == DesignerSourceCompositeValueKind.None)
        {
            return;
        }

        for (var index = 0; index < _compositeFallbackValues.Length; index++)
        {
            _compositeFallbackValues[index] = index < values.Count ? values[index] : string.Empty;
        }

        if (preserveVisibleValues)
        {
            return;
        }

        CompositeComponent1!.Text = _compositeFallbackValues[0];
        CompositeComponent2!.Text = _compositeFallbackValues[1];
        CompositeComponent3!.Text = _compositeFallbackValues[2];
        CompositeComponent4!.Text = _compositeFallbackValues[3];
    }

    public string? BuildCompositeEditorText()
    {
        if (CompositeValueKind == DesignerSourceCompositeValueKind.None)
        {
            return EditorText;
        }

        var componentValues = new[]
        {
            CompositeComponent1?.Text ?? string.Empty,
            CompositeComponent2?.Text ?? string.Empty,
            CompositeComponent3?.Text ?? string.Empty,
            CompositeComponent4?.Text ?? string.Empty
        };

        if (componentValues.All(static value => string.IsNullOrWhiteSpace(value)))
        {
            return null;
        }

        for (var index = 0; index < componentValues.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(componentValues[index]))
            {
                componentValues[index] = componentValues[index].Trim();
                continue;
            }

            componentValues[index] = _compositeFallbackValues[index];
        }

        return string.Join(",", componentValues);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private static IReadOnlyList<string> GetCompositeLabels(DesignerSourceCompositeValueKind compositeValueKind)
    {
        return compositeValueKind switch
        {
            DesignerSourceCompositeValueKind.CornerRadius => ["TL", "TR", "BR", "BL"],
            DesignerSourceCompositeValueKind.Thickness => ["L", "T", "R", "B"],
            _ => [string.Empty, string.Empty, string.Empty, string.Empty]
        };
    }
}

public sealed class DesignerSourceInspectorEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public DataTemplate? TextChoiceTemplate { get; set; }

    public DataTemplate? ChoiceTemplate { get; set; }

    public DataTemplate? ColorTemplate { get; set; }

    public DataTemplate? CompositeTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        _ = container;
        if (item is not DesignerSourceInspectorPropertyItem propertyItem)
        {
            return base.SelectTemplate(item, container);
        }

        return propertyItem.EditorKind switch
        {
            DesignerSourcePropertyEditorKind.Color => ColorTemplate,
            DesignerSourcePropertyEditorKind.TextChoice => TextChoiceTemplate,
            DesignerSourcePropertyEditorKind.Choice => ChoiceTemplate,
            DesignerSourcePropertyEditorKind.Composite => CompositeTemplate,
            _ => TextTemplate
        };
    }
}
