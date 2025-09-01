using System;
using System.Linq;
using Cake.CMake;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Apricot.Native.Build.Tasks.Sdl;

[TaskName("Build SpirV-cross")]
public sealed class BuildSdlSpirVCross : FrostingTask<BuildContext>
{
    private const string SpirVCrossPath = "Sources/SDL_shadercross/external/SPIRV-Cross";

    public static DirectoryPath GetBuildPath(BuildContext context) =>
        new DirectoryPath($"Builds/{context.Platform}/spirv-cross-c-shared/").MakeAbsolute(context.Environment);

    public override void Run(BuildContext context)
    {
        var buildPath = GetBuildPath(context);

        context.EnsureDirectoryExists(buildPath);


        context.CMake(new CMakeSettings
        {
            SourcePath = SpirVCrossPath,
            OutputPath = buildPath,
            Generator = context.CmakeGenerator,
            Options =
                GetPlatformSpecificOptions(context.Environment.Platform.Family).Concat(
                [
                    "-DCMAKE_BUILD_TYPE=Release",
                    "-DSPIRV_CROSS_SHARED=ON"
                ]).ToArray()
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath
        });

        var platform = context.Environment.Platform.Family;
        var libraryName = platform == PlatformFamily.Windows
            ? "libspirv-cross-c-shared.dll" // spirv cross on windows adds lib in the beginning for some reason...
            : Utils.PlatformLibName(platform, "spirv-cross-c-shared");
        var binaryName = Utils.BinaryName(platform, "spirv-cross");
        context.AddArtifact(buildPath.CombineWithFilePath(libraryName), "SDL_shadercross");
        context.AddArtifact(buildPath.CombineWithFilePath(binaryName), "SDL_shadercross");
    }

    public string[] GetPlatformSpecificOptions(PlatformFamily family) => family switch
    {
        PlatformFamily.OSX =>
        [
            "-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64",
            "-DCMAKE_OSX_DEPLOYMENT_TARGET=10.13",
            "-DCMAKE_INSTALL_NAME_DIR=@rpath",
            "-DCMAKE_BUILD_WITH_INSTALL_RPATH=ON",
            "-DCMAKE_INSTALL_RPATH=@loader_path",
            "-DCMAKE_MACOSX_RPATH=ON"
        ],
        PlatformFamily.Windows =>
        [
            "-DCMAKE_SHARED_LIBRARY_PREFIX="
        ],
        PlatformFamily.Linux =>
        [
            "-DCMAKE_BUILD_WITH_INSTALL_RPATH=ON",
            "-DCMAKE_SKIP_BUILD_RPATH=OFF",
            "-DCMAKE_INSTALL_RPATH=$ORIGIN",
            "-DCMAKE_INSTALL_RPATH_USE_LINK_PATH=ON"
        ],
        _ => throw new PlatformNotSupportedException()
    };
}
