using System;

namespace DailyRoutines.Common.Manager.Attributes;

/// <remarks>
/// <para>0:  最早加载</para>
/// <para>1:  优先早加载</para>
/// <para>2:  早加载</para>
/// <para>4:  模块加载</para>
/// <para>8:  晚加载</para>
/// <para>16: 最晚加载</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ManagerInitOrderAttribute(int order) : Attribute
{
    public int Order { get; } = order;
}
