using System;

namespace InkkSlinger;

public partial class MediaElementView : UserControl
{
    public MediaElementView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("MediaElement");
        }
    }
}




