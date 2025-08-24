using System;
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

    public bool UseVendoredShadercrossDeps { get; set; } = context.IsRunningOnMacOs();

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

        context.ProducedArtifacts.Add(buildPath.CombineWithFilePath(libName));
    }
}

[TaskName("Download shadercross DirectXShaderCompiler binaries")]
public sealed class DownloadDirectXShaderCompiler : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.UseVendoredShadercrossDeps)
        {
            context.Log.Information("Skip direct-x shader compiler download");
            return;
        }

        var cmakePath = context.Tools.Resolve(["cmake", "cmake.exe"]);
        var workingDir = new DirectoryPath("Sources/SDL_shadercross/");

        context.StartProcess(
            cmakePath,
            new ProcessSettings
            {
                WorkingDirectory = workingDir,
                Arguments = ProcessArgumentBuilder.FromStrings([
                    "-P", "build-scripts/download-prebuilt-DirectXShaderCompiler.cmake"
                ])
            }
        );

        Environment.SetEnvironmentVariable(
            "DirectXShaderCompiler_ROOT",
            workingDir.Combine("external/DirectXShaderCompiler-binaries").MakeAbsolute(context.Environment).ToString()
        );
    }
}

[TaskName("Build SpirV-cross")]
public sealed class BuildSpirVCross : FrostingTask<BuildContext>
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
            [
                "-DCMAKE_BUILD_TYPE=Release",
                "-DSPIRV_CROSS_SHARED=ON"
            ]
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
        context.ProducedArtifacts.Add(buildPath.CombineWithFilePath(libraryName));
        context.ProducedArtifacts.Add(buildPath.CombineWithFilePath(binaryName));
    }
}

[TaskName("Build SDL_shadercross")]
[IsDependentOn(typeof(BuildSdl))]
[IsDependentOn(typeof(BuildSpirVCross))]
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

        var useVendoredArg = context.UseVendoredShadercrossDeps
            ? "-DSDLSHADERCROSS_VENDORED=ON"
            : "-DSDLSHADERCROSS_VENDORED=OFF";

        context.CMake(new CMakeSettings
        {
            OutputPath = buildPath,
            SourcePath = ShadercrossPath,
            Generator = context.CmakeGenerator,
            Options =
            [
                "-DCMAKE_BUILD_TYPE=Release",
                "-DSDLSHADERCROSS_DXC=ON",
                "-DSDLSHADERCROSS_SHARED=ON",
                "-DSDLSHADERCROSS_STATIC=OFF",
                "-DSDLSHADERCROSS_CLI=ON",
                useVendoredArg,
                $"-DSDL3_DIR={BuildSdl.GetBuildPath(context)}",
                $"-Dspirv_cross_c_shared_DIR={BuildSpirVCross.GetBuildPath(context)}"
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

        var binaryName = Utils.BinaryName(context.Environment.Platform.Family, "shadercross");
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
