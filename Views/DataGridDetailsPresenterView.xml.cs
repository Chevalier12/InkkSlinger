using System;

namespace InkkSlinger;

public partial class DataGridDetailsPresenterView : UserControl
{
    public DataGridDetailsPresenterView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DataGridDetailsPresenter");
        }
    }
}




