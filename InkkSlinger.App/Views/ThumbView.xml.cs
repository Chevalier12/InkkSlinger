using System;

namespace InkkSlinger;

public partial class ThumbView : UserControl
{
    public ThumbView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Thumb");
        }
    }
}




