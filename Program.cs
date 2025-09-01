using Apricot.Native.Build;
using Apricot.Native.Build.Tasks;
using Apricot.Native.Build.Tasks.Sdl;
using Cake.Frosting;

return new CakeHost()
    .UseContext<BuildContext>()
    .Run(args);

[TaskName("Default")]
[IsDependentOn(typeof(BuildSdl))]
[IsDependentOn(typeof(BuildSdlShadercross))]
[IsDependentOn(typeof(CopyArtifacts))]
public class DefaultTask : FrostingTask { }
