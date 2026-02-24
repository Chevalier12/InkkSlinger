using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ToolBarOverflowPanelView : UserControl
{
    public ToolBarOverflowPanelView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ToolBarOverflowPanelView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ToolBarOverflowPanel");
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

