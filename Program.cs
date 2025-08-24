using System;
using System.Collections.Generic;
using System.Linq;
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
                "-DSDL_EXAMPLES=OFF"
            ]).ToArray()
        });

        context.CMakeBuild(new CMakeBuildSettings
        {
            BinaryPath = buildPath
        });

        var libName = context.Environment.Platform.Family switch
        {
            PlatformFamily.Windows => "SDL3.dll",
            PlatformFamily.OSX => "libSDL3.dylib",
            PlatformFamily.Linux => "libSDL3.so",
            var p => throw new PlatformNotSupportedException($"Platform {p} is not supported")
        };
        
        context.ProducedArtifacts.Add(buildPath.CombineWithFilePath(libName));
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
[IsDependentOn(typeof(CopyArtifacts))]
public class DefaultTask : FrostingTask { }
