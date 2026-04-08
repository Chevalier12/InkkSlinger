using System;

namespace InkkSlinger;

public partial class ItemsControlView : UserControl
{
    public ItemsControlView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ItemsControl");
        }
    }
}




