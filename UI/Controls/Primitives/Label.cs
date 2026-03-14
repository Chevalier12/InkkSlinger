using System;

namespace InkkSlinger;

public sealed class Label : ContentControl
{
    private static readonly Lazy<Style> DefaultLabelStyle = new(BuildDefaultLabelStyle);

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(
            nameof(Target),
            typeof(UIElement),
            typeof(Label),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Label label)
                    {
                        label.OnTargetChanged(args.OldValue as UIElement, args.NewValue as UIElement);
                    }
                }));

    public Label()
    {
        Focusable = false;
    }

    public UIElement? Target
    {
        get => GetValue<UIElement>(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    internal UIElement? ResolveAccessKeyTarget()
    {
        return Target ?? this;
    }

    internal string GetAutomationContentText()
    {
        return ExtractAutomationText(Content);
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultLabelStyle.Value;
    }

    private void OnTargetChanged(UIElement? oldTarget, UIElement? newTarget)
    {
        if (oldTarget != null &&
            ReferenceEquals(AutomationProperties.GetLabeledBy(oldTarget), this))
        {
            AutomationProperties.SetLabeledBy(oldTarget, null);
        }

        if (newTarget != null)
        {
            AutomationProperties.SetLabeledBy(newTarget, this);
        }
    }

    private static Style BuildDefaultLabelStyle()
    {
        var style = new Style(typeof(Label));
        style.Setters.Add(new Setter(TemplateProperty, BuildDefaultLabelTemplate()));
        return style;
    }

    private static ControlTemplate BuildDefaultLabelTemplate()
    {
        var template = new ControlTemplate(static _ =>
        {
            var border = new Border
            {
                Name = "PART_Border"
            };

            border.Child = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                ContentSource = nameof(Content),
                RecognizesAccessKey = true
            };

            return border;
        })
        {
            TargetType = typeof(Label)
        };

        template.BindTemplate("PART_Border", Border.BackgroundProperty, BackgroundProperty);
        template.BindTemplate("PART_Border", Border.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_Border", Border.BorderThicknessProperty, BorderThicknessProperty);
        template.BindTemplate("PART_Border", Border.PaddingProperty, PaddingProperty);
        template.BindTemplate("PART_ContentPresenter", ContentPresenter.HorizontalContentAlignmentProperty, HorizontalContentAlignmentProperty);
        template.BindTemplate("PART_ContentPresenter", ContentPresenter.VerticalContentAlignmentProperty, VerticalContentAlignmentProperty);

        return template;
    }

    internal static string ExtractAutomationText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => MenuAccessText.StripAccessMarkers(text),
            AccessText accessText => accessText.DisplayText,
            Label label => label.GetAutomationContentText(),
            TextBlock textBlock => textBlock.Text,
            ContentControl contentControl => ExtractAutomationText(contentControl.Content),
            _ => value.ToString() ?? string.Empty
        };
    }
}
