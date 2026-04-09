using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RadioButtonViewRegressionTests
{
    [Fact]
    public void RadioButtonView_SiblingGroupingLabel_ShouldTrackPublishReviewAndDraftSelections()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new RadioButtonView();

            var draft = Assert.IsType<RadioButton>(view.FindName("DraftOptionRadio"));
            var review = Assert.IsType<RadioButton>(view.FindName("ReviewOptionRadio"));
            var publish = Assert.IsType<RadioButton>(view.FindName("PublishOptionRadio"));
            var selectionLabel = Assert.IsType<TextBlock>(view.FindName("SiblingSelectionLabel"));

            Assert.True(draft.IsChecked);
            Assert.Equal("Selected: Draft", selectionLabel.Text);

            publish.IsChecked = true;
            Assert.True(publish.IsChecked);
            Assert.False(draft.IsChecked);
            Assert.Equal("Selected: Publish", selectionLabel.Text);

            review.IsChecked = true;
            Assert.True(review.IsChecked);
            Assert.False(publish.IsChecked);
            Assert.Equal("Selected: Review", selectionLabel.Text);

            draft.IsChecked = true;
            Assert.True(draft.IsChecked);
            Assert.False(review.IsChecked);
            Assert.Equal("Selected: Draft", selectionLabel.Text);

            publish.IsChecked = true;
            Assert.True(publish.IsChecked);
            Assert.False(draft.IsChecked);
            Assert.Equal("Selected: Publish", selectionLabel.Text);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void RadioButton_CheckedEvent_ShouldObservePeersAlreadyUnchecked()
    {
        var panel = new StackPanel();
        var draft = new RadioButton { Content = "Draft", IsChecked = true };
        var publish = new RadioButton { Content = "Publish" };
        panel.AddChild(draft);
        panel.AddChild(publish);

        bool? draftStateWhenPublishChecked = null;
        publish.Checked += (_, _) => draftStateWhenPublishChecked = draft.IsChecked;

        publish.IsChecked = true;

        Assert.False(draft.IsChecked);
        Assert.True(publish.IsChecked);
        Assert.False(draftStateWhenPublishChecked);
    }

    private static void LoadRootAppResources()
    {
        TestApplicationResources.LoadDemoAppResources();
    }

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        UiApplication.Current.Resources.Clear();
        foreach (var pair in snapshot)
        {
            UiApplication.Current.Resources[pair.Key] = pair.Value;
        }
    }
}