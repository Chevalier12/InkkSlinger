using System;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ItemsControlView : UserControl
{
    public ItemsControlView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ItemsControl");
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


