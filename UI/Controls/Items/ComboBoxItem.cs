namespace InkkSlinger;

public class ComboBoxItem : ListBoxItem
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ComboBoxItem),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ComboBoxItem comboBoxItem)
                    {
                        comboBoxItem.SyncGeneratedTextContent();
                    }
                }));

    private Label? _generatedLabel;

    public string Text
    {
        get => GetValue<string>(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == ContentControl.ContentProperty)
        {
            if (!ReferenceEquals(Content, _generatedLabel))
            {
                _generatedLabel = null;
            }
            else
            {
                SyncGeneratedLabelStyling();
            }

            return;
        }

        if (args.Property == ForegroundProperty)
        {
            SyncGeneratedLabelStyling();
        }
    }

    private void SyncGeneratedTextContent()
    {
        if (_generatedLabel == null)
        {
            if (Content != null)
            {
                return;
            }

            _generatedLabel = new Label();
            Content = _generatedLabel;
        }

        _generatedLabel.Text = Text;
        SyncGeneratedLabelStyling();
    }

    private void SyncGeneratedLabelStyling()
    {
        if (_generatedLabel == null)
        {
            return;
        }

        _generatedLabel.Foreground = Foreground;
        _generatedLabel.Font = Font;
    }
}
