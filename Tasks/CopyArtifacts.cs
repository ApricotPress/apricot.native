using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Apricot.Native.Build.Tasks;

[TaskName("Copy artifacts")]
public sealed class CopyArtifacts : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var targetDirPath = new DirectoryPath($"Artifacts/{context.Platform}");

        context.EnsureDirectoryDoesNotExist(targetDirPath);
        context.EnsureDirectoryExists(targetDirPath);

        foreach (var artifact in context.ProducedArtifacts)
        {
            context.Log.Information($"Copying {artifact} to artifacts");
            CopyArtifact(context, artifact.Path, targetDirPath.Combine(artifact.TargetPath));
        }
    }

    private FilePath CopyArtifact(BuildContext context, FilePath artifactPath, DirectoryPath targetDirPath)
    {
        context.EnsureDirectoryExists(targetDirPath);

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
