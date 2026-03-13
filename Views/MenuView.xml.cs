using System;

namespace InkkSlinger;

public partial class MenuView : UserControl
{
    public MenuView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Menu");
        }
    }
}




