using System.Collections.Generic;

namespace InkkSlinger.Tests;

internal static class TestApplicationResources
{
    public static void Restore(IEnumerable<KeyValuePair<object, object>> entries)
    {
        UiApplication.Current.Resources.ReplaceContents(entries, notifyChanged: false);
    }

    public static void Restore(
        IEnumerable<KeyValuePair<object, object>> entries,
        IEnumerable<ResourceDictionary> mergedDictionaries)
    {
        UiApplication.Current.Resources.ReplaceContents(entries, mergedDictionaries, notifyChanged: false);
    }
}