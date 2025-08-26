using System;
using System.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Apricot.Native.Build;

public static class Utils
{
    public static string PlatformLibName(PlatformFamily platformFamily, string lib) =>
        platformFamily switch
        {
            PlatformFamily.Windows => $"{lib}.dll",
            PlatformFamily.OSX => $"lib{lib}.dylib",
            PlatformFamily.Linux => $"lib{lib}.so",
            var p => throw new PlatformNotSupportedException($"Platform {p} is not supported")
        };
    
    public static string BinaryName(PlatformFamily platformFamily, string binary) =>
        platformFamily switch
        {
            PlatformFamily.Windows => $"{binary}.exe",
            PlatformFamily.OSX or PlatformFamily.Linux => binary,
            var p => throw new PlatformNotSupportedException($"Platform {p} is not supported")
        };

    public static bool TryGetSymLink(FilePath path, out FilePath target)
    {
        if (File.ResolveLinkTarget(path.ToString(), false) is { } link)
        {
            target = link.FullName;
            return true;
        }
        else
        {
            target = null;
            return false;
        }
    }
}
