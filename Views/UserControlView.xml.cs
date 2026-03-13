using System;

namespace InkkSlinger;

public partial class UserControlView : UserControl
{
    public UserControlView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("UserControl");
        }
    }
}




