namespace DailyRoutines.Common.Module.Enums;

// 一等分类较为笼统, 二等分类较为具体
// 原则上, 如果一个模块既能被分入一等分类又能被分入二等分类, 则归属于二等分类
public enum ModuleCategory
{
    None,
    
    // 一等分类
    General,
    System,
    Interface,
    Combat,
    Notification,
    Script,
    Assist,
    
    // 二等分类
    Duty,
    Recruitment,
    Action,
    CraftGather,
    GoldSaucer,
}
