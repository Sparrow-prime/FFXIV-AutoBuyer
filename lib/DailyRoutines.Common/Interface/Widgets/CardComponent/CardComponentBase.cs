using System.Numerics;
using Dalamud.Utility.Numerics;

namespace DailyRoutines.Common.Interface.Widgets;

public abstract class CardComponentBase
{
    private const int BACKGROUND_CHANNEL = 0;
    private const int DECORATION_CHANNEL = 1;
    private const int CONTENT_CHANNEL    = 2;

    private const float HOVER_STIFFNESS = 240f;
    private const float HOVER_DAMPING   = 20f;

    private const float PRESS_STIFFNESS = 320f;
    private const float PRESS_DAMPING   = 22f;

    protected Vector2 CurrentSize => isFirstDraw && currentSize == Vector2.Zero ?
                                         InitialSize :
                                         currentSize;

    protected virtual float Rounding => 8f * GlobalUIScale;

    protected virtual Vector2 InitialSize => Vector2.Zero;

    protected virtual bool DisableContent       => false;
    protected virtual bool DrawDisabledMask     => false;
    protected virtual bool EnablePressAnimation => false;
    protected virtual bool EnableHoverAnimation => true;
    protected virtual bool EnableTopHighlight   => true;
    protected virtual bool EnableGlow           => true;
    protected virtual bool WrapInContentTable   => true;
    protected virtual bool IsSelected           => false;

    protected virtual CardCursorAdvance CursorAdvance => CardCursorAdvance.Bottom;

    protected virtual float HoverFloatOffset => -2.5f * GlobalUIScale;
    protected virtual float PressOffset      => 1.5f  * GlobalUIScale;

    protected virtual Vector4  RestingBorder         => KnownColor.White.ToVector4().WithW(0.06f);
    protected virtual Vector4  HoveredBorder         => KnownColor.DodgerBlue.ToVector4().WithW(0.8f);
    protected virtual Vector4? SelectedBorder        => null;
    protected virtual Vector4? CustomBackgroundColor => null;

    private Vector2 currentSize;
    private Vector2 sizeVelocity;
    private bool    isFirstDraw = true;

    private float hoverProgress;
    private float hoverVelocity;

    private float pressProgress;
    private float pressVelocity;

    private float restingHeight;

    public void Draw()
    {
        var context   = CreateContext();
        var frameMin  = context.FrameMin;
        var drawList  = ImGui.GetWindowDrawList();
        var frameSize = CurrentSize;
        var isHovered = frameSize is { X: > 0f, Y: > 0f } &&
                        ImGui.IsMouseHoveringRect(frameMin, frameMin + frameSize);

        UpdateStates(isHovered);

        var hoverOffset = (EnableHoverAnimation ?
                               HoverFloatOffset :
                               0f) *
                          hoverProgress;
        var pressOffset = (EnablePressAnimation ?
                               PressOffset :
                               0f) *
                          pressProgress;
        var visualOffset = new Vector2(0f, hoverOffset + pressOffset);

        var clipMinY = drawList.GetClipRectMin().Y;
        if (frameMin.Y                + pressOffset >= clipMinY && frameMin.Y + visualOffset.Y < clipMinY)
            visualOffset.Y = clipMinY - frameMin.Y;

        var visualMin = frameMin + visualOffset;

        drawList.ChannelsSplit(3);
        drawList.ChannelsSetCurrent(CONTENT_CHANNEL);

        ImGui.SetCursorScreenPos(context.ContentStartPos + visualOffset);

        using (ImRaii.Disabled(DisableContent))
        using (ImRaii.Group())
        {
            if (WrapInContentTable && context.FrameWidth > 0f)
            {
                ImGui.Dummy(context.Padding with { X = context.FrameWidth });

                using (ImRaii.PushIndent(context.Padding.X))
                {
                    var       contentWidth = context.FrameWidth - (context.Padding.X * 2);
                    using var table        = ImRaii.Table("CardContentTable", 1, ImGuiTableFlags.None, new Vector2(contentWidth, 0));

                    if (table)
                    {
                        ImGui.TableSetupColumn("ContentColumn", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        DrawContent(context, isHovered);
                    }
                }

                ImGui.Dummy(context.Padding with { X = context.FrameWidth });
            }
            else
                DrawContent(context, isHovered);
        }

        var contentRectSize = ImGui.GetItemRectSize();
        var targetSize      = GetTargetSize(context, contentRectSize);

        if (isFirstDraw)
            restingHeight = targetSize.Y;

        if ((hoverProgress == 0f && pressProgress == 0f) ||
            Math.Abs(targetSize.Y - restingHeight) > 5.0f)
            restingHeight = targetSize.Y;

        var stableTargetSize = targetSize with { Y = restingHeight };

        UpdateCurrentSize(stableTargetSize);

        var visualMax = visualMin + currentSize;

        drawList.ChannelsSetCurrent(BACKGROUND_CHANNEL);

        Vector4 restingBg;
        Vector4 hoveredBg;

        if (CustomBackgroundColor.HasValue)
        {
            var customBase = CustomBackgroundColor.Value;
            restingBg = customBase;
            hoveredBg = customBase.WithW(MathF.Min(1f, customBase.W * 1.3f));
        }
        else
        {
            var baseColor = ImGui.GetColorU32(ImGuiCol.ChildBg).ToVector4();
            restingBg = baseColor.WithW(MathF.Max(0f, baseColor.W - 0.1f));
            hoveredBg = baseColor.WithW(MathF.Max(0f, baseColor.W - 0.05f));
        }

        var bgColor = Vector4.Lerp(restingBg, hoveredBg, hoverProgress);

        var activeBorder = IsSelected && SelectedBorder.HasValue ?
                               SelectedBorder.Value :
                               Vector4.Lerp(RestingBorder, HoveredBorder, hoverProgress);

        if (EnableGlow && hoverProgress > 0.01f)
        {
            var shadowColor = KnownColor.Black.ToVector4().WithW(0.18f * hoverProgress);
            var shadowSize  = 12f * GlobalUIScale * hoverProgress;
            ImGuiOm.AddGlowRect(drawList, visualMin, visualMax, shadowColor.ToUInt(), Rounding, shadowSize, 10, 0.20f);

            var glowColor = IsSelected && SelectedBorder.HasValue ?
                                SelectedBorder.Value :
                                HoveredBorder;
            glowColor.W = 0.16f * hoverProgress;
            var glowSize = 6f * GlobalUIScale * hoverProgress;
            ImGuiOm.AddGlowRect(drawList, visualMin, visualMax, glowColor.ToUInt(), Rounding, glowSize, 10);
        }

        drawList.AddRectFilled(visualMin, visualMax, bgColor.ToUInt(), Rounding);
        drawList.AddRect
        (
            visualMin,
            visualMax,
            activeBorder.ToUInt(),
            Rounding,
            ImDrawFlags.None,
            IsSelected ?
                1.5f * GlobalUIScale :
                1f
        );

        if (EnableTopHighlight)
        {
            var topHighlightColor = KnownColor.White.ToVector4().WithW(0.08f + (0.12f * hoverProgress));
            drawList.AddLine
            (
                new Vector2(visualMin.X                 + Rounding, visualMin.Y + 0.5f),
                new Vector2(visualMin.X + currentSize.X - Rounding, visualMin.Y + 0.5f),
                topHighlightColor.ToUInt(),
                1f
            );
        }

        drawList.ChannelsMerge();

        DrawAfterFrame(context, visualMax, isHovered);

        switch (CursorAdvance)
        {
            case CardCursorAdvance.Bottom:
                ImGui.SetCursorScreenPos(frameMin + new Vector2(0f, currentSize.Y + ImGui.GetStyle().ItemSpacing.Y));
                break;
            case CardCursorAdvance.Right:
                ImGui.SetCursorScreenPos(frameMin + new Vector2(currentSize.X + ImGui.GetStyle().ItemSpacing.X, 0f));
                break;
            case CardCursorAdvance.None:
            default:
                break;
        }

        if (DrawDisabledMask)
            drawList.AddRectFilled(visualMin, visualMax, KnownColor.Black.ToVector4().WithW(0.2f).ToUInt(), Rounding);
    }

    protected static int GetDecorationChannel() =>
        DECORATION_CHANNEL;

    protected static int GetContentChannel() =>
        CONTENT_CHANNEL;

    protected virtual void DrawAfterFrame
    (
        CardDrawContext context,
        Vector2         frameMax,
        bool            isHovered
    )
    {
    }

    protected abstract CardDrawContext CreateContext();

    protected abstract void DrawContent
    (
        CardDrawContext context,
        bool            isHovered
    );

    protected abstract Vector2 GetTargetSize
    (
        CardDrawContext context,
        Vector2         contentRectSize
    );

    private void UpdateCurrentSize
    (
        Vector2 targetSize
    )
    {
        var dt = Math.Min(ImGui.GetIO().DeltaTime, 0.05f);

        if (isFirstDraw)
        {
            currentSize  = targetSize;
            sizeVelocity = Vector2.Zero;
            isFirstDraw  = false;
            return;
        }

        const float STIFFNESS = 180f;
        const float DAMPING   = 28f;

        var displacement = currentSize - targetSize;

        if (displacement.Length() < 2.0f && sizeVelocity.Length() < 0.1f)
        {
            currentSize  = targetSize;
            sizeVelocity = Vector2.Zero;
            return;
        }

        var force = (-STIFFNESS * displacement) - (DAMPING * sizeVelocity);

        sizeVelocity += force        * dt;
        currentSize  += sizeVelocity * dt;

        if (displacement.Length() < 0.1f && sizeVelocity.Length() < 0.1f)
        {
            currentSize  = targetSize;
            sizeVelocity = Vector2.Zero;
        }
    }

    private void UpdateStates
    (
        bool isHovered
    )
    {
        var dt = Math.Min(ImGui.GetIO().DeltaTime, 0.05f);

        var targetHover = isHovered ?
                              1f :
                              0f;
        var hoverDisplacement = hoverProgress                          - targetHover;
        var hoverForce        = (-HOVER_STIFFNESS * hoverDisplacement) - (HOVER_DAMPING * hoverVelocity);
        hoverVelocity += hoverForce    * dt;
        hoverProgress += hoverVelocity * dt;

        if (Math.Abs(hoverDisplacement) < 0.001f && Math.Abs(hoverVelocity) < 0.001f)
        {
            hoverProgress = targetHover;
            hoverVelocity = 0f;
        }

        hoverProgress = Math.Clamp(hoverProgress, 0f, 1f);

        var isPressed = EnablePressAnimation && isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var targetPress = isPressed ?
                              1f :
                              0f;
        var pressDisplacement = pressProgress                          - targetPress;
        var pressForce        = (-PRESS_STIFFNESS * pressDisplacement) - (PRESS_DAMPING * pressVelocity);
        pressVelocity += pressForce    * dt;
        pressProgress += pressVelocity * dt;

        if (Math.Abs(pressDisplacement) < 0.001f && Math.Abs(pressVelocity) < 0.001f)
        {
            pressProgress = targetPress;
            pressVelocity = 0f;
        }

        pressProgress = Math.Clamp(pressProgress, 0f, 1f);
    }

    protected readonly record struct CardDrawContext
    (
        Vector2 FrameMin,
        Vector2 ContentStartPos,
        Vector2 Padding,
        float   FrameWidth = 0f
    );
}
