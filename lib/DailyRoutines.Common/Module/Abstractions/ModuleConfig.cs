using Newtonsoft.Json;

namespace DailyRoutines.Common.Module.Abstractions;

public abstract class ModuleConfig
{
    [JsonIgnore]
    public virtual string? PreviousModuleName => null;
}
