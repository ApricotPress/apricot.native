using System.Collections.Generic;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Apricot.Native.Build;

public record struct ArtifactInfo(FilePath Path, DirectoryPath TargetPath);

public class BuildContext(ICakeContext context) : FrostingContext(context)
{
    public string Platform { get; set; } = context.Argument("Platform", context.Environment.Platform.Family.ToString());

    public string CmakeGenerator { get; set; } = context.Argument("CmakeGenerator", "Ninja");

    public bool UseVendoredShadercrossDeps { get; set; } = context.IsRunningOnMacOs();

    public List<string> SdlExtraFlags { get; set; } = [];

    public List<ArtifactInfo> ProducedArtifacts { get; set; } = [];

    public void AddArtifact(FilePath path, DirectoryPath target) =>
        ProducedArtifacts.Add(new ArtifactInfo(path, target));
    
    public DirectoryPath GetBuildPath(string module) =>
        new DirectoryPath($"Builds/{Platform}/{module}/").MakeAbsolute(context.Environment);
}
