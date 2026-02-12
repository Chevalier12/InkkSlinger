using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class WindowDemoView : UserControl
{
    public event EventHandler? CloseRequested;

    public WindowDemoView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "WindowDemoView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        if (WindowTitleLabel != null)
        {
            WindowTitleLabel.Font = font;
        }

        if (WindowBodyLabel != null)
        {
            WindowBodyLabel.Font = font;
        }

        if (CloseWindowButton != null)
        {
            CloseWindowButton.Font = font;
        }
    }

    private void OnCloseWindowClick(object? sender, RoutedSimpleEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
