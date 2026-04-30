using InkkSlinger.Designer;
using Xunit;

namespace InkkSlinger.Tests;

public class DesignerProjectFileStoreTests
{
    [Fact]
    public void Delete_RecyclesFileWhenRecycleBinAcceptsIt()
    {
        var root = CreateTempRoot();
        try
        {
            var path = Path.Combine(root, "Scratch.xml");
            File.WriteAllText(path, "<UserControl />");
            var recycleBin = new RecordingRecycleBin(recycleFiles: true, recycleDirectories: false);
            var store = new PhysicalDesignerProjectFileStore(recycleBin);

            store.Delete(path);

            Assert.Equal(path, Assert.Single(recycleBin.FilePaths));
            Assert.True(File.Exists(path));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Delete_PermanentlyDeletesFileWhenRecycleBinFails()
    {
        var root = CreateTempRoot();
        try
        {
            var path = Path.Combine(root, "Scratch.xml");
            File.WriteAllText(path, "<UserControl />");
            var store = new PhysicalDesignerProjectFileStore(new RecordingRecycleBin(recycleFiles: false, recycleDirectories: false));

            store.Delete(path);

            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Delete_RecyclesDirectoryWhenRecycleBinAcceptsIt()
    {
        var root = CreateTempRoot();
        try
        {
            var path = Path.Combine(root, "Views");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Main.xml"), "<UserControl />");
            var recycleBin = new RecordingRecycleBin(recycleFiles: false, recycleDirectories: true);
            var store = new PhysicalDesignerProjectFileStore(recycleBin);

            store.Delete(path);

            Assert.Equal(path, Assert.Single(recycleBin.DirectoryPaths));
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Delete_PermanentlyDeletesDirectoryWhenRecycleBinFails()
    {
        var root = CreateTempRoot();
        try
        {
            var path = Path.Combine(root, "Views");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Main.xml"), "<UserControl />");
            var store = new PhysicalDesignerProjectFileStore(new RecordingRecycleBin(recycleFiles: false, recycleDirectories: false));

            store.Delete(path);

            Assert.False(Directory.Exists(path));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "InkkSlingerDesignerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingRecycleBin(bool recycleFiles, bool recycleDirectories) : IDesignerProjectRecycleBin
    {
        public List<string> FilePaths { get; } = new();

        public List<string> DirectoryPaths { get; } = new();

        public bool TryRecycleFile(string path)
        {
            FilePaths.Add(path);
            return recycleFiles;
        }

        public bool TryRecycleDirectory(string path)
        {
            DirectoryPaths.Add(path);
            return recycleDirectories;
        }
    }
}