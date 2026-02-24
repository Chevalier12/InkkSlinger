using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class VirtualizingStackPanelView : UserControl
{
    public VirtualizingStackPanelView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "VirtualizingStackPanelView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("VirtualizingStackPanel");
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

