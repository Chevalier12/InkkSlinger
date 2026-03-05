using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationControlCoverageSmokeTests
{
    [Fact]
    public void PublicInstantiableUiElements_GetPeersWithStableControlType()
    {
        var candidates = typeof(UIElement).Assembly
            .GetTypes()
            .Where(static type =>
                type.IsPublic &&
                !type.IsAbstract &&
                typeof(UIElement).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(static type => type.Name)
            .ToList();

        var host = new Canvas();
        var uiRoot = new UiRoot(host);
        var covered = new List<Type>();

        foreach (var type in candidates)
        {
            UIElement? element = null;
            try
            {
                element = Activator.CreateInstance(type) as UIElement;
            }
            catch
            {
                continue;
            }

            if (element == null)
            {
                continue;
            }

            host.AddChild(element);
            var peer = uiRoot.Automation.GetPeer(element);
            if (peer == null)
            {
                continue;
            }

            var first = peer.GetControlType();
            var second = peer.GetControlType();
            Assert.Equal(first, second);
            covered.Add(type);
        }

        Assert.True(covered.Count >= 60);

        uiRoot.Shutdown();
    }
}
