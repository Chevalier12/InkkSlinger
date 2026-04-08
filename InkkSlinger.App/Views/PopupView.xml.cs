using System;

namespace InkkSlinger;

public partial class PopupView : UserControl
{
    public PopupView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Popup");
        }
    }
}




