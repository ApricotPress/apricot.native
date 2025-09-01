using System;
using System.Linq;
using Cake.CMake;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;

namespace Apricot.Native.Build.Tasks.Sdl;

[TaskName("Build SDL_shadercross")]
[IsDependentOn(typeof(BuildSdl))]
[IsDependentOn(typeof(BuildSdlSpirVCross))]
[IsDependentOn(typeof(DownloadDirectXShaderCompiler))]
public sealed class BuildSdlShadercross : FrostingTask<BuildContext>
{
    private const string ShadercrossPath = "Sources/SDL_shadercross/";

    public override void Run(BuildContext context)
    {
        context.Log.Information("Preparing to build SDL_shadercross");
        var commit = context.GitLog(ShadercrossPath, 1).First();
        context.Log.Information("SDL_shadercross repository is at {0} - {1}", commit.Sha, commit.MessageShort);

        var buildPath = new DirectoryPath($"Builds/{context.Platform}/SDL_shadercross");

        context.EnsureDirectoryExists(buildPath);

        context.CMake(new CMakeSettings
        {
            OutputPath = buildPath,
            SourcePath = ShadercrossPath,
            Generator = context.CmakeGenerator,
            Options =
                GetPlatformSpecificOptions(context.Environment.Platform.Family).Concat(
                [
                    "-DCMAKE_BUILD_TYPE=Release",
                    "-DSDLSHADERCROSS_DXC=ON",
                    "-DSDLSHADERCROSS_SHARED=ON",
                    "-DSDLSHADERCROSS_STATIC=OFF",
                    "-DSDLSHADERCROSS_CLI=ON",
                    $"-DSDL3_DIR={BuildSdl.GetBuildPath(context)}",
                    $"-Dspirv_cross_c_shared_DIR={BuildSdlSpirVCross.GetBuildPath(context)}"
                ]).ToArray()
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath
        });

        var libName = Utils.PlatformLibName(context.Environment.Platform.Family, "SDL3_shadercross");
        context.AddArtifact(buildPath.CombineWithFilePath(libName), "SDL_shadercross");

        var binaryName = Utils.BinaryName(context.Environment.Platform.Family, "shadercross");
        context.AddArtifact(buildPath.CombineWithFilePath(binaryName), "SDL_shadercross");

        if (context.UseVendoredShadercrossDeps)
        {
            var dxcompilerLibName = Utils.PlatformLibName(context.Environment.Platform.Family, "dxcompiler");
            context.AddArtifact(
                buildPath.CombineWithFilePath($"external/DirectXShaderCompiler/lib/{dxcompilerLibName}"),
                "SDL_shadercross"
            );
        }
    }

    public string[] GetPlatformSpecificOptions(PlatformFamily family) => family switch
    {
        PlatformFamily.OSX =>
        [
            "-DSDLSHADERCROSS_VENDORED=ON",
            "-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64",
            "-DCMAKE_OSX_DEPLOYMENT_TARGET=10.13",
            "-DCMAKE_INSTALL_NAME_DIR=@rpath",
            "-DCMAKE_BUILD_WITH_INSTALL_RPATH=ON",
            "-DCMAKE_INSTALL_RPATH=@loader_path",
            "-DCMAKE_MACOSX_RPATH=ON"
        ],
        PlatformFamily.Windows =>
        [
            "-DSDLSHADERCROSS_VENDORED=OFF",
        ],
        PlatformFamily.Linux =>
        [
            "-DSDLSHADERCROSS_VENDORED=OFF",
            "-DCMAKE_BUILD_WITH_INSTALL_RPATH=ON",
            "-DCMAKE_SKIP_BUILD_RPATH=OFF",
            "-DCMAKE_INSTALL_RPATH=$ORIGIN",
            "-DCMAKE_INSTALL_RPATH_USE_LINK_PATH=ON"
        ],
        _ => throw new PlatformNotSupportedException()
    };
}
