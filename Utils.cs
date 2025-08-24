using System;
using Cake.Core;

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
}
