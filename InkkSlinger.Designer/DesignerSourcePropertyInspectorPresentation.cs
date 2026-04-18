using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using InkkSlinger;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

internal sealed class DesignerSourceInspectorPropertyItem : INotifyPropertyChanged
{
    private string _descriptionText = string.Empty;
    private string _editorText = string.Empty;
    private string? _selectedChoice;
    private Color _selectedColor = Color.Transparent;
    private string _colorDisplayText = string.Empty;
    private Visibility _rowVisibility = Visibility.Visible;

    public DesignerSourceInspectorPropertyItem(DesignerSourceInspectableProperty property)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        ChoiceValues = property.ChoiceValues;
    }

    private DesignerSourceInspectableProperty Property { get; }

    public string Name => Property.Name;

    public DesignerSourcePropertyEditorKind EditorKind => Property.EditorKind;

    public IReadOnlyList<string> ChoiceValues { get; }

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

public sealed class DesignerSourceInspectorEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public DataTemplate? ChoiceTemplate { get; set; }

    public DataTemplate? ColorTemplate { get; set; }

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
            DesignerSourcePropertyEditorKind.Choice => ChoiceTemplate,
            _ => TextTemplate
        };
    }
}
