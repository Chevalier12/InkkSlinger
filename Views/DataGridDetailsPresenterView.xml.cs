using System;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class DataGridDetailsPresenterView : UserControl
{
    public DataGridDetailsPresenterView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DataGridDetailsPresenter");
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


