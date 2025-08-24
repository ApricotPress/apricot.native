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
    public string Platform { get; set; } = context.Environment.Platform.Family.ToString();

    public string CmakeGenerator { get; set; } = context.Argument("CmakeGenerator", "Ninja");

    public List<string> SdlExtraFlags { get; set; } = [];
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
    public const string SdlPath = "Sources/SDL";

    public override void Run(BuildContext context)
    {
        context.Log.Information("Preparing to build SDL for current OS");
        var commit = context.GitLog(SdlPath, 1).First();
        context.Log.Information("SDL repository is at {0} - {1}", commit.Sha, commit.Message);

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
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(BuildSdl))]
public class DefaultTask : FrostingTask { }
