using System.Reflection;
using System.Runtime.Loader;

namespace DailyRoutines.Common.Module.Compilers;

public class ModuleLoadContext(AssemblyLoadContext parentLoadContext, string name) : AssemblyLoadContext(name, true)
{
    protected override Assembly Load(AssemblyName assemblyName) =>
        parentLoadContext.LoadFromAssemblyName(assemblyName);

    protected override nint LoadUnmanagedDll(string unmanagedDLLName) =>
        nint.Zero;
}
