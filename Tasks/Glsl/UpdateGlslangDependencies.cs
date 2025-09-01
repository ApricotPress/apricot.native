using Apricot.Native.Build.Python;
using Cake.Core.IO;
using Cake.Frosting;

namespace Apricot.Native.Build.Tasks.Glsl;

[TaskName("Update glslang dependencies")]
public class UpdateGlslangDependencies : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var updateScript = new FilePath("./Sources/glslang/update_glslang_sources.py");
        
        context.Python(updateScript, new PythonSettings()
        {
            WorkingDirectory = "./Sources/glslang/"
        });
    }
}
