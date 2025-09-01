using System;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Apricot.Native.Build.Tasks.Sdl;

[TaskName("Download shadercross DirectXShaderCompiler binaries")]
public sealed class DownloadDirectXShaderCompiler : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => !context.UseVendoredShadercrossDeps;

    public override void Run(BuildContext context)
    {
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
            context.AddArtifact(binariesPath.CombineWithFilePath($"linux/lib/libdxcompiler.so"), "SDL_shadercross");
        }
    }
}
