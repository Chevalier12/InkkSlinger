using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class MediaElementTests
{
    [Fact]
    public void Play_WithNullSource_RaisesMediaFailed()
    {
        var media = new MediaElement();
        MediaFailedEventArgs? failed = null;
        media.MediaFailed += (_, args) => failed = args;

        media.Play();

        Assert.NotNull(failed);
        Assert.Contains("Source is null", failed!.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MediaElementState.Closed, media.CurrentState);
    }

    [Fact]
    public void SourceAndPlayback_StateTransitions_AreDeterministic()
    {
        var media = new MediaElement
        {
            Source = new Uri("https://example.com/video.mp4")
        };

        Assert.Equal(MediaElementState.Opening, media.CurrentState);

        media.Play();
        Assert.Equal(MediaElementState.Playing, media.CurrentState);

        media.Pause();
        Assert.Equal(MediaElementState.Paused, media.CurrentState);

        media.Stop();
        Assert.Equal(MediaElementState.Stopped, media.CurrentState);
        Assert.Equal(TimeSpan.Zero, media.Position);

        media.Close();
        Assert.Equal(MediaElementState.Closed, media.CurrentState);
    }

    [Fact]
    public void XamlLoader_CanInstantiate_MediaElement()
    {
        const string xaml = """
                            <MediaElement xmlns=\"urn:inkkslinger-ui\"
                                          Source=\"https://example.com/demo.mp4\"
                                          LoadedBehavior=\"Manual\" />
                            """;

        var root = (MediaElement)XamlLoader.LoadFromString(xaml);

        Assert.NotNull(root);
        Assert.Equal(MediaState.Manual, root.LoadedBehavior);
        Assert.Equal(new Uri("https://example.com/demo.mp4"), root.Source);
    }
}
