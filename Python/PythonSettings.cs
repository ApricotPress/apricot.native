using System.Collections.Generic;
using Cake.Core.Tooling;

namespace Apricot.Native.Build.Python;

public class PythonSettings : ToolSettings
{
    public ICollection<string> Arguments { get; set; } = [];
}
