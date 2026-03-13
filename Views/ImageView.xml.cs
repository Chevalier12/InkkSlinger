using System;

namespace InkkSlinger;

public partial class ImageView : UserControl
{
    public ImageView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Image");
        }
    }
}




