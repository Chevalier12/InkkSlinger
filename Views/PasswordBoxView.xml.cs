using System;

namespace InkkSlinger;

public partial class PasswordBoxView : UserControl
{
    public PasswordBoxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("PasswordBox");
        }
    }
}




