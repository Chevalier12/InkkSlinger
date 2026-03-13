using System;

namespace InkkSlinger;

public partial class BorderView : UserControl
{
    public BorderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Border");
        }
    }
}




