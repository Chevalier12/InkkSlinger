using System;

namespace InkkSlinger;

public partial class TabItemView : UserControl
{
    public TabItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("TabItem");
        }
    }
}




