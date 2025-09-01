using Cake.CMake;
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
                "-DBUILD_SHARED_LIBS=1"
            ]
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath
        });

        var platform = context.Environment.Platform.Family;
        var glslangLibName = Utils.PlatformLibName(platform, "glslang");
        var glslangResourcesLibName = Utils.PlatformLibName(platform, "glslang-default-resource-limits");

        context.AddArtifact(buildPath.CombineWithFilePath($"glslang/{glslangLibName}"), "glslang");
        context.AddArtifact(buildPath.CombineWithFilePath($"glslang/{glslangResourcesLibName}"), "glslang");
    }
}
