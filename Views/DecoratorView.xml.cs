using System;

namespace InkkSlinger;

public partial class DecoratorView : UserControl
{
    public DecoratorView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Decorator");
        }
    }
}




