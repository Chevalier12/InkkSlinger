using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using InkkSlinger;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

public sealed record DesignerPreviewPlaceholderModel(
    string Title,
    string Body,
    Color Accent,
    Color BodyColor);

public sealed record DesignerInspectorSectionViewModel(
    string Title,
    IReadOnlyList<DesignerInspectorProperty> Properties);

public sealed class DesignerSourceLineNumberViewModel : INotifyPropertyChanged
{
    private string _numberText;

    public DesignerSourceLineNumberViewModel(string numberText)
    {
        _numberText = numberText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string NumberText
    {
        get => _numberText;
        set
        {
            if (string.Equals(_numberText, value, StringComparison.Ordinal))
            {
                return;
            }

            _numberText = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class DesignerVisualTreeNodeViewModel : INotifyPropertyChanged
{
    private static readonly Color DefaultForeground = new(216, 227, 238);
    private static readonly Color SelectedForeground = new(231, 237, 245);
    private static readonly Color SelectedBackground = new(16, 34, 54);
    private static readonly Color SelectedBorderBrush = new(30, 74, 120);

    private bool _isExpanded;
    private bool _isSelected;

    public DesignerVisualTreeNodeViewModel(
        string id,
        string label,
        IReadOnlyList<DesignerVisualTreeNodeViewModel> children,
        int depth,
        bool isExpanded)
    {
        Id = id;
        Label = label;
        Children = children;
        Depth = depth;
        _isExpanded = isExpanded;
        IndentMargin = new Thickness(depth * 14f, 0f, 0f, 0f);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Label { get; }

    public int Depth { get; }

    public IReadOnlyList<DesignerVisualTreeNodeViewModel> Children { get; }

    public Thickness IndentMargin { get; }

    public bool HasChildren => Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExpandGlyph));
            OnPropertyChanged(nameof(ChildrenVisibility));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RowBackground));
            OnPropertyChanged(nameof(RowBorderBrush));
            OnPropertyChanged(nameof(RowForeground));
        }
    }

    public string ExpandGlyph => IsExpanded ? "-" : "+";

    public Visibility ExpanderVisibility => HasChildren ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ChildrenVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

    public Color RowBackground => IsSelected ? SelectedBackground : Color.Transparent;

    public Color RowBorderBrush => IsSelected ? SelectedBorderBrush : Color.Transparent;

    public Color RowForeground => IsSelected ? SelectedForeground : DefaultForeground;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}