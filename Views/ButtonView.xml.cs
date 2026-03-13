using System;

namespace InkkSlinger;

public partial class ButtonView : UserControl
{
    public ButtonView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Button");
        }
    }
}




