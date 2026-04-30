using System;
using System.Runtime.InteropServices;
using System.Text;

namespace InkkSlinger.Designer;

internal static class NativeFolderBrowserHelper
{
    public static string? BrowseForFolder(string description)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var info = new BROWSEINFOW
        {
            hwndOwner = IntPtr.Zero,
            pidlRoot = IntPtr.Zero,
            lpszTitle = description,
            ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE | BIF_NONEWFOLDERBUTTON,
            lpfn = IntPtr.Zero,
            lParam = IntPtr.Zero,
            iImage = 0
        };

        var pidl = SHBrowseForFolderW(ref info);
        if (pidl == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var path = new StringBuilder(260);
            if (SHGetPathFromIDListW(pidl, path))
            {
                return path.ToString();
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }

        return null;
    }

    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_NEWDIALOGSTYLE = 0x0040;
    private const uint BIF_NONEWFOLDERBUTTON = 0x0200;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BROWSEINFOW
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr SHBrowseForFolderW(ref BROWSEINFOW browseInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);
}
