using System.Collections.Generic;
using System.Linq;
using Apricot.Native.Build;
using Cake.CMake;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;

return new CakeHost()
    .UseContext<BuildContext>()
    .Run(args);

public class BuildContext(ICakeContext context) : FrostingContext(context)
{
    public string Platform { get; set; } = context.Argument("Platform", context.Environment.Platform.Family.ToString());

    public string CmakeGenerator { get; set; } = context.Argument("CmakeGenerator", "Ninja");

    public List<string> SdlExtraFlags { get; set; } = [];

    public List<FilePath> ProducedArtifacts { get; set; } = [];

    public DirectoryPath InstallPrefixPath => new DirectoryPath($"InstallPrefix/{Platform}").MakeAbsolute(Environment);
}

[TaskName("Prepare SDL")]
public sealed class PrepareSdlBuild : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.IsRunningOnMacOs())
        {
            context.SdlExtraFlags.AddRange([
                "-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64",
                "-DCMAKE_OSX_DEPLOYMENT_TARGET=10.13"
            ]);
        }
    }
}

[TaskName("SDL")]
[IsDependentOn(typeof(PrepareSdlBuild))]
public sealed class BuildSdl : FrostingTask<BuildContext>
{
    private const string SdlPath = "Sources/SDL";

    public override void Run(BuildContext context)
    {
        context.Log.Information("Preparing to build SDL for current OS");
        var commit = context.GitLog(SdlPath, 1).First();
        context.Log.Information("SDL repository is at {0} - {1}", commit.Sha, commit.MessageShort);

        var buildPath = new DirectoryPath($"Builds/{context.Platform}/SDL");

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
                "-DSDL_EXAMPLES=OFF",
                $"-DCMAKE_INSTALL_PREFIX={context.InstallPrefixPath}"
            ]).ToArray()
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath,
            Targets = ["install"]
        });

        var libName = Utils.PlatformLibName(context.Environment.Platform.Family, "SDL3");

        context.ProducedArtifacts.Add(buildPath.CombineWithFilePath(libName));
    }
}

[TaskName("Build SDL_shadercross")]
[IsDependentOn(typeof(BuildSdl))]
public sealed class BuildSdlShadercross : FrostingTask<BuildContext>
{
    private const string ShadercrossPath = "Sources/SDL_shadercross/";

    public override void Run(BuildContext context)
    {
        context.Log.Information("Preparing to build SDL_shadercross");
        var commit = context.GitLog(ShadercrossPath, 1).First();
        context.Log.Information("SDL_shadercross repository is at {0} - {1}", commit.Sha, commit.MessageShort);

        var buildPath = new DirectoryPath($"Builds/{context.Platform}/SDL_shadercross");

        context.CMake(new CMakeSettings
        {
            OutputPath = buildPath,
            SourcePath = ShadercrossPath,
            Generator = context.CmakeGenerator,
            Options =
            [
                "-DCMAKE_BUILD_TYPE=Release",
                "-DSDLSHADERCROSS_DXC=ON",
                "-DSDLSHADERCROSS_VENDORED=ON",
                "-DSDLSHADERCROSS_SHARED=ON",
                "-DSDLSHADERCROSS_STATIC=OFF",
                "-DSDLSHADERCROSS_CLI=ON",
                $"-DCMAKE_INSTALL_PREFIX={context.InstallPrefixPath}"
            ]
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath
        });

        var libName = Utils.PlatformLibName(context.Environment.Platform.Family, "SDL3_shadercross");
        context.ProducedArtifacts.Add(
            buildPath.CombineWithFilePath(libName)
        );

        var binaryName = context.IsRunningOnWindows() ? "shadercross.exe" : "shadercross";
        context.ProducedArtifacts.Add(
            buildPath.CombineWithFilePath(binaryName)
        );
    }
}


[TaskName("Copy artifacts")]
public sealed class CopyArtifacts : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var targetDirPath = new DirectoryPath($"Artifacts/{context.Platform}");

        context.EnsureDirectoryExists(targetDirPath);

        context.CopyFiles(context.ProducedArtifacts, targetDirPath);
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(BuildSdl))]
[IsDependentOn(typeof(BuildSdlShadercross))]
[IsDependentOn(typeof(CopyArtifacts))]
public class DefaultTask : FrostingTask { }
