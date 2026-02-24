using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class GridSplitterView : UserControl
{
    public GridSplitterView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "GridSplitterView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("GridSplitter");
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

