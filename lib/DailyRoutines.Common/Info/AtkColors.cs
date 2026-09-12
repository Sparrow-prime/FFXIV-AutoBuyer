using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace DailyRoutines.Common.Info;

public static class AtkColors
{
    /// <summary>列表列头：在绝大多数主题下为浅灰色文字与白色描边</summary>
    public static AtkColor ListHeader { get; } = new(21, 7);
    
    /// <summary>列表行文本：在绝大多数主题下为浅灰色文字与白色描边</summary>
    public static AtkColor ListRow { get; } = new(8, 7);
    
    /// <summary>列表页脚：在绝大多数主题下为浅灰色文字与白色描边</summary>
    public static AtkColor ListFooter { get; } = new(3, 7);
    
    /// <summary>标签：在绝大多数主题下为棕色文字与白色描边</summary>
    public static AtkColor Label { get; } = new(8, 7);
    
    /// <summary>文本：在绝大多数主题下为深灰色文字与白色描边</summary>
    public static AtkColor Text { get; } = new(2, 7);
    
    /// <summary>提示：在绝大多数主题下为浅灰色文字与白色描边</summary>
    public static AtkColor Hint { get; } = new(3, 7);
    
    /// <summary>小标签：在绝大多数主题下为棕黄色文字与白色描边</summary>
    public static AtkColor LabelLight { get; } = new(3, 7);
    
    /// <summary>值：在绝大多数主题下为白色文字与橙色描边</summary>
    public static AtkColor Value { get; } = new(50, 32);
    
    /// <summary>强调值：在绝大多数主题下为深棕色文字与浅粉色描边</summary>
    public static AtkColor ValueEmphasize { get; } = new(64, 65);
    
    public readonly unsafe struct AtkColor
    (
        uint textColor,
        uint edgeColor
    )
    {
        public readonly uint TextColor = textColor;
        public readonly uint EdgeColor = edgeColor;

        public void ApplyTo(ref TextNode node)
        {
            node.TextColor        = GetTextColor();
            node.TextOutlineColor = GetEdgeColor();
        }
        
        public void ApplyTo(ref LabelTextNode node)
        {
            node.TextColor        = GetTextColor();
            node.TextOutlineColor = GetEdgeColor();
        }

        public Vector4 GetTextColor() =>
            AtkStage.Instance()->AtkUIColorHolder->GetColor(true, TextColor).ToVector4();
        
        public Vector4 GetEdgeColor() =>
            AtkStage.Instance()->AtkUIColorHolder->GetColor(true, EdgeColor).ToVector4();
    }
}
