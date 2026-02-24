using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class MediaElementView : UserControl
{
    public MediaElementView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "MediaElementView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("MediaElement");
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

