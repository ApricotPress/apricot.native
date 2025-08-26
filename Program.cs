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

        var binariesPath = workingDir
            .Combine("external/DirectXShaderCompiler-binaries")
            .MakeAbsolute(context.Environment);

        Environment.SetEnvironmentVariable(
            "DirectXShaderCompiler_ROOT",
            binariesPath.ToString()
        );

        if (context.Environment.Platform.Family == PlatformFamily.Linux)
        {
            context.ProducedArtifacts.Add(binariesPath.CombineWithFilePath($"linux/lib/libdxcompiler.so"));
        }
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
        context.ProducedArtifacts.Add(buildPath.CombineWithFilePath(libraryName));
        context.ProducedArtifacts.Add(buildPath.CombineWithFilePath(binaryName));
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
                    $"-Dspirv_cross_c_shared_DIR={BuildSpirVCross.GetBuildPath(context)}"
                ]).ToArray()
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

        if (context.UseVendoredShadercrossDeps)
        {
            var dxcompilerLibName = Utils.PlatformLibName(context.Environment.Platform.Family, "dxcompiler");
            context.ProducedArtifacts.Add(
                buildPath.CombineWithFilePath($"external/DirectXShaderCompiler/lib/{dxcompilerLibName}")
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


[TaskName("Copy artifacts")]
public sealed class CopyArtifacts : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var targetDirPath = new DirectoryPath($"Artifacts/{context.Platform}");

        context.EnsureDirectoryDoesNotExist(targetDirPath);
        context.EnsureDirectoryExists(targetDirPath);

        foreach (var artifactPath in context.ProducedArtifacts)
        {
            context.Log.Information($"Copying {artifactPath} to artifacts");
            CopyArtifact(context, artifactPath, targetDirPath);
        }
    }

    private FilePath CopyArtifact(BuildContext context, FilePath artifactPath, DirectoryPath targetDirPath)
    {
        var resultPath = targetDirPath.CombineWithFilePath(artifactPath.GetFilename());

        if (Utils.TryGetSymLink(artifactPath, out FilePath original))
        {
            var copied = CopyArtifact(context, original, targetDirPath);

            System.IO.File.CreateSymbolicLink(resultPath.ToString(), copied.ToString());
        }
        else
        {
            context.CopyFileToDirectory(artifactPath, targetDirPath);
        }

        return artifactPath.GetFilename();
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(BuildSdl))]
[IsDependentOn(typeof(BuildSdlShadercross))]
[IsDependentOn(typeof(CopyArtifacts))]
public class DefaultTask : FrostingTask { }
