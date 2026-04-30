using Xunit;

namespace InkkSlinger.Tests;

public class DesignerRecentProjectStoreTests
{
    [Fact]
    public void AddOrUpdate_DedupesByNormalizedPathAndKeepsNewestFirst()
    {
        var persistence = new FakeRecentProjectPersistenceStore();
        var store = new InkkSlinger.Designer.DesignerRecentProjectStore(persistence);

        store.AddOrUpdate("C:/projects/Alpha", new DateTimeOffset(2026, 4, 27, 9, 0, 0, TimeSpan.Zero));
        store.AddOrUpdate("C:/projects/Beta", new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero));
        store.AddOrUpdate("C:\\projects\\Alpha\\", new DateTimeOffset(2026, 4, 27, 11, 0, 0, TimeSpan.Zero));

        var recents = store.Load();

        Assert.Equal(2, recents.Count);
        Assert.Equal(new[] { "C:/projects/Alpha", "C:/projects/Beta" }, recents.Select(recent => recent.Path).ToArray());
        Assert.Equal("Alpha", recents[0].DisplayName);
        Assert.Equal(new DateTimeOffset(2026, 4, 27, 11, 0, 0, TimeSpan.Zero), recents[0].LastOpenedAt);
    }

    [Fact]
    public void Load_DerivesDisplayNameFromProjectFolder()
    {
        var store = new InkkSlinger.Designer.DesignerRecentProjectStore(new FakeRecentProjectPersistenceStore());

        store.AddOrUpdate("C:/projects/Nested/MyProject", new DateTimeOffset(2026, 4, 27, 9, 0, 0, TimeSpan.Zero));

        var recent = Assert.Single(store.Load());
        Assert.Equal("MyProject", recent.DisplayName);
    }

    [Fact]
    public void RecentsPersistThroughInjectedStore()
    {
        var persistence = new FakeRecentProjectPersistenceStore();
        var firstStore = new InkkSlinger.Designer.DesignerRecentProjectStore(persistence);
        firstStore.AddOrUpdate("C:/projects/Persisted", new DateTimeOffset(2026, 4, 27, 9, 0, 0, TimeSpan.Zero));

        var secondStore = new InkkSlinger.Designer.DesignerRecentProjectStore(persistence);

        var recent = Assert.Single(secondStore.Load());
        Assert.Equal("C:/projects/Persisted", recent.Path);
        Assert.Equal("Persisted", recent.DisplayName);
    }

    [Fact]
    public void Remove_DeletesMatchingRecentProjectByNormalizedPath()
    {
        var persistence = new FakeRecentProjectPersistenceStore();
        var store = new InkkSlinger.Designer.DesignerRecentProjectStore(persistence);
        store.AddOrUpdate("C:/projects/Alpha", new DateTimeOffset(2026, 4, 27, 9, 0, 0, TimeSpan.Zero));
        store.AddOrUpdate("C:/projects/Beta", new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero));

        store.Remove(@"C:\projects\Alpha\");

        var recent = Assert.Single(store.Load());
        Assert.Equal("C:/projects/Beta", recent.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ this is not valid json")]
    public void Load_ToleratesEmptyOrCorruptPersistenceData(string? persistedText)
    {
        var persistence = new FakeRecentProjectPersistenceStore { PersistedText = persistedText };
        var store = new InkkSlinger.Designer.DesignerRecentProjectStore(persistence);

        var recents = store.Load();

        Assert.Empty(recents);
    }

    private sealed class FakeRecentProjectPersistenceStore : InkkSlinger.Designer.IDesignerRecentProjectPersistenceStore
    {
        public string? PersistedText { get; set; }

        public string? ReadAllText()
        {
            return PersistedText;
        }

        public void WriteAllText(string text)
        {
            PersistedText = text;
        }
    }
}