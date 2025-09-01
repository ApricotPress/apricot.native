using System;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;

namespace Apricot.Native.Build.Python;

[CakeAliasCategory("Python")]
public static class PythonAliases
{
    [CakeMethodAlias]
    public static void Python(
        this ICakeContext context,
        FilePath scriptPath,
        PythonSettings? settings = null
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        new PythonTool(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools)
            .RunScript(scriptPath, settings);
    }
    [CakeMethodAlias]
    public static void PythonModule(
        this ICakeContext context,
        string module,
        PythonSettings? settings = null
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        new PythonTool(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools)
            .RunModule(module, settings);
    }
}
