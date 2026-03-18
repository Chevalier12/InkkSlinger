using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class BorderView : UserControl
{
    private Border? _clipProbeBorder;
    private CheckBox? _clipToBoundsCheckBox;
    private TextBlock? _clipStateText;

    public BorderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = BuildDemoSurface();
        }

        ApplyClipMode();
    }

    private UIElement BuildDemoSurface()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var introCard = new Border
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(63, 90, 118),
            Background = new Color(18, 26, 37),
            CornerRadius = new CornerRadius(10f),
            Child = new TextBlock
            {
                Text = "Toggle ClipToBounds to compare Border overflow against rectangular clipping. The orange badge is translated beyond the right and top edges so the change is visible immediately.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Color(220, 231, 245)
            }
        };
        Grid.SetRow(introCard, 0);
        root.AddChild(introCard);

        var controlsPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(controlsPanel, 1);
        root.AddChild(controlsPanel);

        _clipToBoundsCheckBox = new CheckBox
        {
            Content = "ClipToBounds = True",
            IsChecked = false,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _clipToBoundsCheckBox.Checked += HandleClipToggleChanged;
        _clipToBoundsCheckBox.Unchecked += HandleClipToggleChanged;
        controlsPanel.AddChild(_clipToBoundsCheckBox);

        _clipStateText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(197, 212, 228)
        };
        controlsPanel.AddChild(_clipStateText);

        var previewCard = new Border
        {
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(52, 73, 97),
            Background = new Color(14, 18, 24),
            CornerRadius = new CornerRadius(12f)
        };
        Grid.SetRow(previewCard, 2);
        root.AddChild(previewCard);

        previewCard.Child = BuildPreviewSurface();
        return root;
    }

    private UIElement BuildPreviewSurface()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var caption = new TextBlock
        {
            Text = "Interactive probe",
            Foreground = new Color(239, 244, 252),
            FontSize = 16f,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(caption, 0);
        root.AddChild(caption);

        var previewHost = new Border
        {
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(44, 64, 89),
            Background = new Color(12, 16, 21),
            CornerRadius = new CornerRadius(14f)
        };
        Grid.SetRow(previewHost, 1);
        root.AddChild(previewHost);

        var stage = new Grid
        {
            MaxWidth = 320f,
            MinHeight = 210f,
            Background = new Color(14, 20, 28)
        };
        stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewHost.Child = stage;

        var targetLabel = new TextBlock
        {
            Text = "Target Border",
            Foreground = new Color(155, 180, 210),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(targetLabel, 0);
        stage.AddChild(targetLabel);

        _clipProbeBorder = new Border
        {
            Width = 228f,
            Height = 132f,
            Padding = new Thickness(16),
            BorderThickness = new Thickness(2),
            BorderBrush = new Color(126, 168, 212),
            Background = new Color(27, 39, 53),
            CornerRadius = new CornerRadius(18f),
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(_clipProbeBorder, 1);
        stage.AddChild(_clipProbeBorder);

        _clipProbeBorder.Child = BuildProbeContent();

        var footer = new TextBlock
        {
            Text = "The badge is intentionally translated beyond the frame. ClipToBounds crops it to the Border layout slot, and that clip is rectangular rather than rounded.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(175, 191, 209)
        };
        Grid.SetRow(footer, 2);
        stage.AddChild(footer);

        return root;
    }

    private static UIElement BuildProbeContent()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "Session summary",
            Foreground = new Color(237, 243, 252),
            FontSize = 17f,
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetRow(title, 0);
        root.AddChild(title);

        var summary = new TextBlock
        {
            Text = "Retained draw list warmed. Dirty regions active. Overflow badge should protrude past the frame when clipping is off.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(200, 214, 231),
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(summary, 1);
        root.AddChild(summary);

        var progressShell = new Border
        {
            Width = 126f,
            Height = 12f,
            Background = new Color(12, 16, 21),
            CornerRadius = new CornerRadius(6f),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(54, 73, 97),
            Margin = new Thickness(0, 0, 0, 0)
        };
        Grid.SetRow(progressShell, 2);
        root.AddChild(progressShell);
        progressShell.Child = new Border
        {
            Width = 82f,
            Height = 10f,
            Background = new Color(85, 179, 220),
            CornerRadius = new CornerRadius(5f)
        };

        var badge = new Border
        {
            Width = 130f,
            Padding = new Thickness(12, 7, 12, 7),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(255, 214, 133),
            Background = new Color(235, 142, 53),
            CornerRadius = new CornerRadius(16f),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = new TranslateTransform { X = 54f, Y = -12f }
        };
        Panel.SetZIndex(badge, 4);
        badge.Child = new TextBlock
        {
            Text = "Overflow badge",
            Foreground = new Color(34, 19, 8)
        };
        root.AddChild(badge);

        return root;
    }

    private void HandleClipToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        ApplyClipMode();
    }

    private void ApplyClipMode()
    {
        var isEnabled = _clipToBoundsCheckBox?.IsChecked == true;
        if (_clipProbeBorder != null)
        {
            _clipProbeBorder.ClipToBounds = isEnabled;
        }

        if (_clipStateText != null)
        {
            _clipStateText.Text = isEnabled
                ? "ClipToBounds is true. The orange badge is clipped to the Border layout slot, and hit testing follows that same clipped region."
                : "ClipToBounds is false. The orange badge can render and receive hits beyond the Border frame when no ancestor clip blocks it.";
        }
    }
}




