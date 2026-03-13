using System;

namespace InkkSlinger;

public partial class WindowView : UserControl
{
    public WindowView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Window");
        }
    }
}




