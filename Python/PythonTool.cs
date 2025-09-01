using System.Collections.Generic;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Apricot.Native.Build.Python;

public class PythonTool(
    IFileSystem fileSystem,
    ICakeEnvironment environment,
    IProcessRunner processRunner,
    IToolLocator tools
) : Tool<PythonSettings>(fileSystem, environment, processRunner, tools)
{
    private readonly ICakeEnvironment _environment = environment;

    protected override string GetToolName() => "Python";

    protected override IEnumerable<string> GetToolExecutableNames() =>
    [
        "python3", "python", "python3.exe", "python.exe", "py.exe"
    ];

    public void RunScript(FilePath scriptPath, PythonSettings? settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.AppendQuoted(scriptPath.MakeAbsolute(_environment).FullPath);

        settings ??= new PythonSettings();

        foreach (var a in settings.Arguments)
        {
            builder.Append(a);
        }

        Run(settings, builder);
    }

    public void RunModule(string moduleName, PythonSettings? settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("-m");
        builder.AppendQuoted(moduleName);

        settings ??= new PythonSettings();

        foreach (var a in settings.Arguments)
        {
            builder.Append(a);
        }

        Run(settings, builder);
    }
}
