using System;

namespace InkkSlinger;

public partial class HeaderedContentControlView : UserControl
{
    public HeaderedContentControlView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("HeaderedContentControl");
        }
    }
}




