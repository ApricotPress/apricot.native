using System.Linq;
using Cake.CMake;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;

namespace Apricot.Native.Build.Tasks.Sdl;

[TaskName("SDL")]
[IsDependentOn(typeof(PrepareSdlBuild))]
public sealed class BuildSdl : FrostingTask<BuildContext>
{
    private const string SdlPath = "Sources/SDL";

    public static DirectoryPath GetBuildPath(BuildContext context) =>
        new DirectoryPath($"Builds/{context.Platform}/SDL/").MakeAbsolute(context.Environment);

    public override void Run(BuildContext context)
    {
        context.Log.Information("Preparing to build SDL for current OS");
        var commit = context.GitLog(SdlPath, 1).First();
        context.Log.Information("SDL repository is at {0} - {1}", commit.Sha, commit.MessageShort);

        var buildPath = GetBuildPath(context);

        context.EnsureDirectoryExists(buildPath);

        context.CMake(new CMakeSettings
        {
            OutputPath = buildPath,
            SourcePath = SdlPath,
            Generator = context.CmakeGenerator,
            Options = context.SdlExtraFlags.Concat(
            [
                "-DCMAKE_BUILD_TYPE=Release",
                "-DSDL_SHARED=ON",
                "-DSDL_TESTS=OFF",
                "-DSDL_EXAMPLES=OFF"
            ]).ToArray()
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath
        });

        var libName = Utils.PlatformLibName(context.Environment.Platform.Family, "SDL3");

        context.AddArtifact(buildPath.CombineWithFilePath(libName), "SDL");
    }
}
