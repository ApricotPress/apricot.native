using System;
using System.Linq;
using Cake.CMake;
using Cake.Common;
using Cake.Core;
using Cake.Frosting;

namespace Apricot.Native.Build.Tasks.Glsl;

[TaskName("Build glslang")]
[IsDependentOn(typeof(UpdateGlslangDependencies))]
public class BuildGlslang : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var buildPath = context.GetBuildPath("glslang");

        context.CMake(new CMakeSettings
        {
            OutputPath = buildPath,
            SourcePath = "Sources/glslang",
            Generator = context.IsRunningOnWindows() ? null : context.CmakeGenerator,
            Options =
                GetPlatformSpecificOptions(context.Environment.Platform.Family).Concat(
                [
                    "-DCMAKE_BUILD_TYPE=Release",
                    "-DBUILD_SHARED_LIBS=1",
                    $"-DCMAKE_INSTALL_PREFIX={buildPath.Combine("install")}"
                ]).ToArray()
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath,
            Configuration = "Release",
            Options = context.IsRunningOnWindows() ? [] : ["-j", "4"],
            Targets = ["install"]
        });

        var platform = context.Environment.Platform.Family;
        var glslangLibName = Utils.PlatformLibName(platform, "glslang");
        var glslangResourcesLibName = Utils.PlatformLibName(platform, "glslang-default-resource-limits");
        var binaryPath = Utils.BinaryName(platform, "glslang");

        var librariesPath = platform == PlatformFamily.Windows
            ? "install/bin/"
            : "install/lib/";

        context.AddArtifact(buildPath.CombineWithFilePath($"{librariesPath}{glslangLibName}"), "glslang");
        context.AddArtifact(buildPath.CombineWithFilePath($"{librariesPath}{glslangResourcesLibName}"), "glslang");
        context.AddArtifact(buildPath.CombineWithFilePath($"install/bin/{binaryPath}"), "glslang");
    }

    public string[] GetPlatformSpecificOptions(PlatformFamily family) => family switch
    {
        PlatformFamily.OSX =>
        [
            "-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64",
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
