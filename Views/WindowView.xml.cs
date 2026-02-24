using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class WindowView : UserControl
{
    public WindowView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "WindowView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Window");
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
}

