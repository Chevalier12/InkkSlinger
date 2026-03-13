using System;

namespace InkkSlinger;

public partial class MenuItemView : UserControl
{
    public MenuItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("MenuItem");
        }
    }
}




