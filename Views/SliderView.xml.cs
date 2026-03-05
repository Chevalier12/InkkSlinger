using System;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class SliderView : UserControl
{
    public SliderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Slider");
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


