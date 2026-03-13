using System;

namespace InkkSlinger;

public partial class ContentPresenterView : UserControl
{
    public ContentPresenterView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ContentPresenter");
        }
    }
}




