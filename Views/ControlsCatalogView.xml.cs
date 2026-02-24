using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ControlsCatalogView : UserControl
{
    private StackPanel? _controlButtonsHost;
    private Label? _selectedControlLabel;
    private ContentControl? _previewHost;

    public ControlsCatalogView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ControlsCatalogView.xml");
        XamlLoader.LoadInto(this, markupPath, this);

        _controlButtonsHost = this.FindName("ControlButtonsHost") as StackPanel;
        _selectedControlLabel = this.FindName("SelectedControlLabel") as Label;
        _previewHost = this.FindName("PreviewHost") as ContentControl;

        BuildButtons();
        if (ControlViews.All.Length > 0)
        {
            ShowControl(ControlViews.All[0]);
        }
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ControlDemoSupport.ApplyFontRecursive(this, font);
    }

    private void BuildButtons()
    {
        if (_controlButtonsHost == null)
        {
            return;
        }

        foreach (var name in ControlViews.All)
        {
            var capture = name;
            var button = new Button
            {
                Text = name,
                Margin = new Thickness(0, 0, 0, 4)
            };
            button.Click += (_, _) => ShowControl(capture);
            _controlButtonsHost.AddChild(button);
        }
    }

    private void ShowControl(string controlName)
    {
        if (_selectedControlLabel != null)
        {
            _selectedControlLabel.Text = $"Selected: {controlName}";
        }

        if (_previewHost != null)
        {
            _previewHost.Content = CreateView(controlName);
        }
    }

    private static UserControl CreateView(string controlName)
    {
        var typeName = $"InkkSlinger.{controlName}View";
        var type = typeof(ControlsCatalogView).Assembly.GetType(typeName);
        if (type != null && Activator.CreateInstance(type) is UserControl view)
        {
            return view;
        }

        return new MissingControlView(controlName);
    }
}

public sealed class MissingControlView : UserControl
{
    public MissingControlView(string controlName)
    {
        Content = new Label
        {
            Text = $"Missing generated view for {controlName}",
            Foreground = new Color(232, 245, 255)
        };
    }
}

