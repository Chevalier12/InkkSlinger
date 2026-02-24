using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class PageView : UserControl
{
    public PageView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "PageView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Page");
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

