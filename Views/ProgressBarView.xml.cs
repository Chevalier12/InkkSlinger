using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ProgressBarView : UserControl
{
    public ProgressBarView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ProgressBarView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ProgressBar");
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

