using System;

namespace InkkSlinger;

public partial class InkPresenterView : UserControl
{
    public InkPresenterView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("InkPresenter");
        }
    }
}




