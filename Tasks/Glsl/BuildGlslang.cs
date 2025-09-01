using Cake.CMake;
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
            Options =
            [
                "-DCMAKE_BUILD_TYPE=Release",
                "-DBUILD_SHARED_LIBS=1",
                $"-DCMAKE_INSTALL_PREFIX={buildPath.Combine("install")}"
            ]
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath,
            Configuration = "Release",
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
}
