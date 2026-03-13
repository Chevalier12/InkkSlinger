using System;

namespace InkkSlinger;

public partial class StatusBarView : UserControl
{
    public StatusBarView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("StatusBar");
        }
    }
}




