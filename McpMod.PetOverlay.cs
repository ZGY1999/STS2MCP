using System;
using System.Collections.Generic;
using System.Linq;

#if !STS2_MCP_TESTS
using Godot;
#endif

namespace STS2_MCP;

public sealed record PetOverlayMenuItem(PetMode Mode, string Label, bool IsSelected);

public sealed record PetOverlayViewModel(
    PetMode Mode,
    PetVisualState VisualState,
    string ModeBadgeText,
    string BubbleTitle,
    string BubbleText,
    bool ShowBubble,
    bool ShowMenu,
    IReadOnlyList<PetOverlayMenuItem> MenuItems)
{
    public static PetOverlayViewModel FromSnapshot(PetStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var lines = snapshot.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        var bubbleText = string.Join('\n', lines);
        var bubbleTitle = snapshot.Title ?? string.Empty;
        var showBubble = bubbleTitle.Length > 0 || bubbleText.Length > 0;

        return new PetOverlayViewModel(
            snapshot.Mode,
            snapshot.State,
            ToModeBadgeText(snapshot.Mode),
            bubbleTitle,
            bubbleText,
            showBubble,
            snapshot.MenuOpen,
            BuildMenuItems(snapshot.Mode));
    }

    public bool HasEquivalentMenu(PetOverlayViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (MenuItems.Count != other.MenuItems.Count)
            return false;

        for (var index = 0; index < MenuItems.Count; index++)
        {
            var left = MenuItems[index];
            var right = other.MenuItems[index];
            if (left.Mode != right.Mode || left.Label != right.Label || left.IsSelected != right.IsSelected)
                return false;
        }

        return true;
    }

    private static IReadOnlyList<PetOverlayMenuItem> BuildMenuItems(PetMode selectedMode)
    {
        return new[]
        {
            new PetOverlayMenuItem(PetMode.Pause, "Pause", selectedMode == PetMode.Pause),
            new PetOverlayMenuItem(PetMode.Advise, "Advise", selectedMode == PetMode.Advise),
            new PetOverlayMenuItem(PetMode.Auto, "Auto", selectedMode == PetMode.Auto)
        };
    }

    private static string ToModeBadgeText(PetMode mode)
    {
        return mode switch
        {
            PetMode.Pause => "PAUSE",
            PetMode.Advise => "ADVISE",
            PetMode.Auto => "AUTO",
            _ => "PAUSE"
        };
    }
}

public enum PetOwlEyeState
{
    Wide,
    Focused,
    Closed,
    Crossed
}

public enum PetOwlWingPose
{
    Rest,
    Gesture,
    Talk,
    Dash
}

public sealed record PetOwlVisualSpec(
    PetOwlEyeState EyeState,
    PetOwlWingPose WingPose,
    bool BeakOpen,
    bool ShowChestRune,
    bool ShowTalkWaves,
    bool ShowSpeedLines,
    bool ShowErrorMarks,
    bool IsRuffled,
    float HeadTilt,
    float BodyLean)
{
    public static PetOwlVisualSpec FromState(PetVisualState state)
    {
        return state switch
        {
            PetVisualState.Thinking => new PetOwlVisualSpec(
                PetOwlEyeState.Focused,
                PetOwlWingPose.Gesture,
                false,
                true,
                false,
                false,
                false,
                false,
                -0.12f,
                0f),
            PetVisualState.Talking => new PetOwlVisualSpec(
                PetOwlEyeState.Wide,
                PetOwlWingPose.Talk,
                true,
                true,
                true,
                false,
                false,
                false,
                0f,
                0f),
            PetVisualState.AutoRunning => new PetOwlVisualSpec(
                PetOwlEyeState.Focused,
                PetOwlWingPose.Dash,
                false,
                true,
                false,
                true,
                false,
                false,
                0f,
                0.18f),
            PetVisualState.Paused => new PetOwlVisualSpec(
                PetOwlEyeState.Closed,
                PetOwlWingPose.Rest,
                false,
                true,
                false,
                false,
                false,
                false,
                0f,
                0f),
            PetVisualState.Error => new PetOwlVisualSpec(
                PetOwlEyeState.Crossed,
                PetOwlWingPose.Rest,
                false,
                true,
                false,
                false,
                true,
                true,
                0f,
                0f),
            _ => new PetOwlVisualSpec(
                PetOwlEyeState.Wide,
                PetOwlWingPose.Rest,
                false,
                true,
                false,
                false,
                false,
                false,
                0f,
                0f)
        };
    }
}

#if !STS2_MCP_TESTS
internal sealed class PetOverlayController
{
    private const float OverlayWidth = 210f;
    private const float OverlayHeight = 164f;
    private const float OverlayRightMargin = 18f;
    private const float OverlayTopMargin = 86f;

    private readonly PetStateStore _store;
    private CanvasLayer? _layer;
    private Control? _root;
    private Control? _menuDismissOverlay;
    private VBoxContainer? _stack;
    private PanelContainer? _bubblePanel;
    private Label? _bubbleTitle;
    private Label? _bubbleBody;
    private PanelContainer? _menuPanel;
    private VBoxContainer? _menuList;
    private Label? _badgeLabel;
    private PetBodyControl? _petBody;
    private PetOverlayViewModel? _lastViewModel;
    private int _refreshLogCount;

    public PetOverlayController(PetStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void EnsureAttached(SceneTree tree)
    {
        if (GodotObject.IsInstanceValid(_layer))
            return;

        if (!GodotObject.IsInstanceValid(tree.Root))
            return;

        BuildTree();
        tree.Root.AddChild(_layer);
        GD.Print("[STS2 MCP] Pet overlay layer attached.");
        Refresh();
    }

    public void Refresh()
    {
        if (!GodotObject.IsInstanceValid(_root))
            return;

        var viewModel = PetOverlayViewModel.FromSnapshot(_store.Snapshot());

        if (_bubblePanel != null)
            _bubblePanel.Visible = viewModel.ShowBubble;

        if (_bubbleTitle != null)
            _bubbleTitle.Text = viewModel.BubbleTitle;

        if (_bubbleBody != null)
            _bubbleBody.Text = viewModel.BubbleText;

        if (_menuPanel != null)
            _menuPanel.Visible = viewModel.ShowMenu;

        if (_menuDismissOverlay != null)
            _menuDismissOverlay.Visible = viewModel.ShowMenu;

        if (_badgeLabel != null)
            _badgeLabel.Text = viewModel.ModeBadgeText;

        if (_petBody != null)
            _petBody.VisualState = viewModel.VisualState;

        if (_refreshLogCount < 8)
        {
            _refreshLogCount++;
            var petSize = _petBody?.Size.ToString() ?? "<null>";
            var petMin = _petBody?.CustomMinimumSize.ToString() ?? "<null>";
            GD.Print(
                $"[STS2 MCP] Refresh state={viewModel.VisualState} mode={viewModel.Mode} " +
                $"showBubble={viewModel.ShowBubble} petSize={petSize} petMin={petMin}");
        }

        if (_lastViewModel == null || !_lastViewModel.HasEquivalentMenu(viewModel))
            RebuildMenu(viewModel);

        _lastViewModel = viewModel;
    }

    private void BuildTree()
    {
        _layer = new CanvasLayer
        {
            Name = "STS2MCPPetOverlay",
            Layer = 60
        };

        _root = new Control
        {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _menuDismissOverlay = new Control
        {
            Name = "MenuDismissOverlay",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _menuDismissOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menuDismissOverlay.GuiInput += OnDismissOverlayGuiInput;
        _root.AddChild(_menuDismissOverlay);

        _stack = new VBoxContainer
        {
            Name = "CornerStack",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.End
        };
        _stack.AnchorLeft = 1;
        _stack.AnchorTop = 0;
        _stack.AnchorRight = 1;
        _stack.AnchorBottom = 0;
        _stack.OffsetLeft = -(OverlayRightMargin + OverlayWidth);
        _stack.OffsetTop = OverlayTopMargin;
        _stack.OffsetRight = -OverlayRightMargin;
        _stack.OffsetBottom = OverlayTopMargin + OverlayHeight;
        _root.AddChild(_stack);

        _bubblePanel = new PanelContainer
        {
            Name = "Bubble",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(186, 0)
        };
        _bubblePanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.09f, 0.10f, 0.15f, 0.92f), 18));
        _stack.AddChild(_bubblePanel);

        var bubbleContent = new VBoxContainer();
        bubbleContent.AddThemeConstantOverride("separation", 4);
        _bubblePanel.AddChild(bubbleContent);

        _bubbleTitle = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _bubbleTitle.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.80f));
        bubbleContent.AddChild(_bubbleTitle);

        _bubbleBody = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _bubbleBody.AddThemeColorOverride("font_color", Colors.White);
        bubbleContent.AddChild(_bubbleBody);

        _menuPanel = new PanelContainer
        {
            Name = "ModeMenu",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(136, 0)
        };
        _menuPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.08f, 0.09f, 0.12f, 0.95f), 16));
        _stack.AddChild(_menuPanel);

        _menuList = new VBoxContainer();
        _menuList.AddThemeConstantOverride("separation", 6);
        _menuPanel.AddChild(_menuList);

        var footer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        footer.AddThemeConstantOverride("separation", 8);
        _stack.AddChild(footer);

        var badgePanel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        badgePanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.12f, 0.17f, 0.20f, 0.94f), 14));
        footer.AddChild(badgePanel);

        _badgeLabel = new Label();
        _badgeLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.95f, 1.0f));
        badgePanel.AddChild(_badgeLabel);

        var petPanel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(68, 68),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        petPanel.AddThemeStyleboxOverride(
            "panel",
            CreatePanelStyle(
                new Color(0.16f, 0.13f, 0.10f, 0.96f),
                28,
                0,
                0,
                0,
                0));
        petPanel.GuiInput += OnPetGuiInput;
        footer.AddChild(petPanel);

        _petBody = new PetBodyControl
        {
            CustomMinimumSize = new Vector2(68, 68),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        petPanel.AddChild(_petBody);
        GD.Print("[STS2 MCP] PetBodyControl created and added to pet panel.");
    }

    private void RebuildMenu(PetOverlayViewModel viewModel)
    {
        if (_menuList == null)
            return;

        foreach (var child in _menuList.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var item in viewModel.MenuItems)
        {
            var selectedMode = item.Mode;
            var button = new Button
            {
                Text = item.Label,
                Flat = false,
                Alignment = HorizontalAlignment.Left,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                FocusMode = Control.FocusModeEnum.None,
                ActionMode = BaseButton.ActionModeEnum.Press
            };
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.AddThemeStyleboxOverride(
                "normal",
                CreateButtonStyle(item.IsSelected
                    ? new Color(0.27f, 0.38f, 0.31f, 0.98f)
                    : new Color(0.16f, 0.17f, 0.20f, 0.96f)));
            button.AddThemeStyleboxOverride(
                "hover",
                CreateButtonStyle(item.IsSelected
                    ? new Color(0.31f, 0.42f, 0.36f, 0.98f)
                    : new Color(0.22f, 0.24f, 0.28f, 0.98f)));
            button.AddThemeStyleboxOverride(
                "pressed",
                CreateButtonStyle(item.IsSelected
                    ? new Color(0.22f, 0.34f, 0.28f, 0.98f)
                    : new Color(0.14f, 0.22f, 0.31f, 0.98f)));
            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
            button.ButtonDown += () => SelectMode(selectedMode);
            _menuList.AddChild(button);
        }
    }

    private void OnPetGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseButton)
            return;

        if (mouseButton.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
            return;

        _store.ToggleMenu();
        Refresh();
    }

    private void OnDismissOverlayGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseButton)
            return;

        if (mouseButton.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
            return;

        _store.SetMenuOpen(false);
        Refresh();
        _menuDismissOverlay?.AcceptEvent();
    }

    private void SelectMode(PetMode mode)
    {
        _store.SetMode(mode);
        _store.SetMenuOpen(false);
        Refresh();
    }

    private static StyleBoxFlat CreatePanelStyle(
        Color color,
        int cornerRadius,
        int contentMarginLeft = 14,
        int contentMarginTop = 10,
        int contentMarginRight = 14,
        int contentMarginBottom = 10)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            ContentMarginLeft = contentMarginLeft,
            ContentMarginRight = contentMarginRight,
            ContentMarginTop = contentMarginTop,
            ContentMarginBottom = contentMarginBottom,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(1f, 1f, 1f, 0.08f)
        };
    }

    private static StyleBoxFlat CreateButtonStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusBottomLeft = 12,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
    }
}

internal sealed partial class PetBodyControl : Control
{
    private PetVisualState _visualState = PetVisualState.Paused;
    private int _drawLogCount;
    private int _resizeLogCount;
    private int _stateLogCount;

    public PetVisualState VisualState
    {
        get => _visualState;
        set
        {
            if (_visualState == value)
                return;

            if (_stateLogCount < 8)
            {
                _stateLogCount++;
                GD.Print($"[STS2 MCP] PetBodyControl state {_visualState} -> {value}");
            }

            _visualState = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_drawLogCount < 8)
        {
            _drawLogCount++;
            GD.Print($"[STS2 MCP] PetBodyControl _Draw size={Size} position={Position} state={_visualState}");
        }

        var spec = PetOwlVisualSpec.FromState(_visualState);
        var center = Size / 2f;
        var bodyCenter = center + new Vector2(spec.BodyLean * 10f, 8f);
        var headCenter = center + new Vector2(spec.BodyLean * 6f, -11f + spec.HeadTilt * 12f);

        var outline = new Color(0.12f, 0.10f, 0.24f);
        var bodyBase = spec.IsRuffled ? new Color(0.26f, 0.27f, 0.52f) : new Color(0.22f, 0.22f, 0.48f);
        var bodyShade = new Color(0.16f, 0.15f, 0.34f);
        var faceMask = new Color(0.68f, 0.78f, 0.98f);
        var faceShade = new Color(0.44f, 0.54f, 0.84f);
        var glowBlue = new Color(0.38f, 0.88f, 1.0f);
        var gold = new Color(0.97f, 0.76f, 0.28f);
        var footColor = new Color(0.62f, 0.45f, 0.20f);
        var errorRed = new Color(0.88f, 0.22f, 0.24f);

        if (spec.ShowSpeedLines)
            DrawSpeedLines(bodyCenter);

        DrawTail(bodyCenter, bodyShade, outline);
        DrawBody(bodyCenter, bodyBase, bodyShade, outline, spec.IsRuffled);
        DrawWing(bodyCenter, true, spec.WingPose, bodyBase, outline);
        DrawWing(bodyCenter, false, spec.WingPose, bodyBase, outline);
        DrawHead(headCenter, bodyBase, outline);
        DrawFaceMask(headCenter, faceMask, faceShade);
        DrawFeet(bodyCenter, footColor, outline);

        if (spec.ShowChestRune)
            DrawChestRune(bodyCenter, gold);

        DrawBeak(headCenter, gold, outline, spec.BeakOpen);
        DrawEyes(headCenter, spec, glowBlue, outline, errorRed);

        if (spec.ShowTalkWaves)
            DrawTalkWaves(headCenter, glowBlue);

        if (spec.ShowErrorMarks)
            DrawErrorMarks(headCenter, errorRed);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GD.Print($"[STS2 MCP] PetBodyControl _Ready size={Size} min={CustomMinimumSize}");
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            if (_resizeLogCount < 8)
            {
                _resizeLogCount++;
                GD.Print($"[STS2 MCP] PetBodyControl resized size={Size}");
            }
            QueueRedraw();
        }
    }

    private void DrawTail(Vector2 bodyCenter, Color color, Color outline)
    {
        var points = new[]
        {
            bodyCenter + new Vector2(-8f, 20f),
            bodyCenter + new Vector2(0f, 28f),
            bodyCenter + new Vector2(8f, 20f),
            bodyCenter + new Vector2(0f, 18f)
        };
        DrawColoredPolygon(points, color);
        DrawPolyline(points.Append(points[0]).ToArray(), outline, 1.5f);
    }

    private void DrawBody(Vector2 bodyCenter, Color fill, Color shade, Color outline, bool isRuffled)
    {
        DrawEllipse(bodyCenter, new Vector2(20f, 24f), fill);
        DrawEllipse(bodyCenter + new Vector2(0f, 6f), new Vector2(14f, 16f), shade);

        if (isRuffled)
        {
            var tufts = new[]
            {
                bodyCenter + new Vector2(-14f, -8f),
                bodyCenter + new Vector2(-22f, 2f),
                bodyCenter + new Vector2(-12f, 8f),
                bodyCenter + new Vector2(0f, 6f),
                bodyCenter + new Vector2(12f, 8f),
                bodyCenter + new Vector2(22f, 2f),
                bodyCenter + new Vector2(14f, -8f)
            };
            DrawPolyline(tufts, outline, 2f);
        }
    }

    private void DrawWing(Vector2 bodyCenter, bool isLeft, PetOwlWingPose pose, Color fill, Color outline)
    {
        var side = isLeft ? -1f : 1f;
        var offset = pose switch
        {
            PetOwlWingPose.Gesture when !isLeft => new Vector2(8f, -4f),
            PetOwlWingPose.Talk when !isLeft => new Vector2(10f, 0f),
            PetOwlWingPose.Dash => new Vector2(side * 4f, 4f),
            _ => new Vector2(side * 12f, 6f)
        };
        var size = pose == PetOwlWingPose.Dash ? new Vector2(8f, 14f) : new Vector2(9f, 16f);
        var center = bodyCenter + offset;
        var angle = pose switch
        {
            PetOwlWingPose.Gesture when !isLeft => side * 0.7f,
            PetOwlWingPose.Talk when !isLeft => side * 0.45f,
            PetOwlWingPose.Dash => side * 1.2f,
            _ => side * 0.18f
        };

        var points = CreateLeafPolygon(center, size, angle, side);
        DrawColoredPolygon(points, fill);
        DrawPolyline(points.Append(points[0]).ToArray(), outline, 1.5f);
    }

    private void DrawHead(Vector2 headCenter, Color fill, Color outline)
    {
        DrawEllipse(headCenter + new Vector2(0f, 2f), new Vector2(18f, 16f), fill);
        var leftTuft = new[]
        {
            headCenter + new Vector2(-10f, -7f),
            headCenter + new Vector2(-18f, -18f),
            headCenter + new Vector2(-4f, -12f)
        };
        var rightTuft = new[]
        {
            headCenter + new Vector2(10f, -7f),
            headCenter + new Vector2(18f, -18f),
            headCenter + new Vector2(4f, -12f)
        };
        DrawColoredPolygon(leftTuft, fill);
        DrawColoredPolygon(rightTuft, fill);
        DrawPolyline(leftTuft.Append(leftTuft[0]).ToArray(), outline, 1.5f);
        DrawPolyline(rightTuft.Append(rightTuft[0]).ToArray(), outline, 1.5f);
    }

    private void DrawFaceMask(Vector2 headCenter, Color fill, Color shade)
    {
        DrawEllipse(headCenter + new Vector2(0f, 3f), new Vector2(13f, 11f), fill);
        var chestPlate = new[]
        {
            headCenter + new Vector2(0f, 3f),
            headCenter + new Vector2(-8f, 14f),
            headCenter + new Vector2(0f, 20f),
            headCenter + new Vector2(8f, 14f)
        };
        DrawColoredPolygon(chestPlate, shade);
    }

    private void DrawFeet(Vector2 bodyCenter, Color fill, Color outline)
    {
        DrawFoot(bodyCenter + new Vector2(-6f, 25f), fill, outline);
        DrawFoot(bodyCenter + new Vector2(6f, 25f), fill, outline);
    }

    private void DrawFoot(Vector2 center, Color fill, Color outline)
    {
        var points = new[]
        {
            center + new Vector2(-4f, 0f),
            center + new Vector2(-2f, 4f),
            center + new Vector2(0f, 0f),
            center + new Vector2(2f, 4f),
            center + new Vector2(4f, 0f),
            center + new Vector2(0f, -3f)
        };
        DrawColoredPolygon(points, fill);
        DrawPolyline(points.Append(points[0]).ToArray(), outline, 1.2f);
    }

    private void DrawChestRune(Vector2 bodyCenter, Color color)
    {
        var stroke = 2.4f;
        DrawLine(bodyCenter + new Vector2(-1f, -5f), bodyCenter + new Vector2(-1f, 8f), color, stroke);
        DrawLine(bodyCenter + new Vector2(-1f, -5f), bodyCenter + new Vector2(9f, 1f), color, stroke);
        DrawLine(bodyCenter + new Vector2(9f, 1f), bodyCenter + new Vector2(-1f, 8f), color, stroke);
    }

    private void DrawBeak(Vector2 headCenter, Color fill, Color outline, bool beakOpen)
    {
        var top = new[]
        {
            headCenter + new Vector2(0f, 4f),
            headCenter + new Vector2(-4f, 10f),
            headCenter + new Vector2(0f, 14f),
            headCenter + new Vector2(4f, 10f)
        };
        DrawColoredPolygon(top, fill);
        DrawPolyline(top.Append(top[0]).ToArray(), outline, 1.3f);

        if (!beakOpen)
            return;

        var lower = new[]
        {
            headCenter + new Vector2(0f, 14f),
            headCenter + new Vector2(-3f, 17f),
            headCenter + new Vector2(3f, 17f)
        };
        DrawColoredPolygon(lower, new Color(0.88f, 0.56f, 0.20f));
        DrawPolyline(lower.Append(lower[0]).ToArray(), outline, 1.1f);
    }

    private void DrawEyes(Vector2 headCenter, PetOwlVisualSpec spec, Color glowBlue, Color outline, Color errorRed)
    {
        var leftEye = headCenter + new Vector2(-7f, 2f);
        var rightEye = headCenter + new Vector2(7f, 2f);

        switch (spec.EyeState)
        {
            case PetOwlEyeState.Closed:
                DrawArc(leftEye, 4f, MathF.PI * 0.15f, MathF.PI * 0.85f, 12, outline, 2f);
                DrawArc(rightEye, 4f, MathF.PI * 0.15f, MathF.PI * 0.85f, 12, outline, 2f);
                return;
            case PetOwlEyeState.Crossed:
                DrawEyeCross(leftEye, errorRed);
                DrawEyeCross(rightEye, errorRed);
                return;
        }

        var eyeRadius = spec.EyeState == PetOwlEyeState.Wide ? 5f : 4.3f;
        var pupilOffsetY = spec.EyeState == PetOwlEyeState.Focused ? 1.2f : 0.2f;
        DrawCircle(leftEye, eyeRadius, glowBlue);
        DrawCircle(rightEye, eyeRadius, glowBlue);
        DrawCircle(leftEye + new Vector2(0f, pupilOffsetY), 2.1f, outline);
        DrawCircle(rightEye + new Vector2(0f, pupilOffsetY), 2.1f, outline);
        DrawCircle(leftEye + new Vector2(-1.2f, -1.5f), 0.8f, Colors.White);
        DrawCircle(rightEye + new Vector2(-1.2f, -1.5f), 0.8f, Colors.White);
    }

    private void DrawTalkWaves(Vector2 headCenter, Color color)
    {
        DrawArc(headCenter + new Vector2(18f, 4f), 4f, -0.8f, 0.8f, 10, color, 1.6f);
        DrawArc(headCenter + new Vector2(23f, 4f), 6f, -0.8f, 0.8f, 10, color, 1.6f);
    }

    private void DrawErrorMarks(Vector2 headCenter, Color color)
    {
        DrawLine(headCenter + new Vector2(-16f, -11f), headCenter + new Vector2(-10f, -15f), color, 2f);
        DrawLine(headCenter + new Vector2(-16f, -15f), headCenter + new Vector2(-10f, -11f), color, 2f);
        DrawLine(headCenter + new Vector2(10f, -11f), headCenter + new Vector2(16f, -15f), color, 2f);
        DrawLine(headCenter + new Vector2(10f, -15f), headCenter + new Vector2(16f, -11f), color, 2f);
    }

    private void DrawSpeedLines(Vector2 bodyCenter)
    {
        var color = new Color(0.40f, 0.86f, 1f, 0.85f);
        DrawLine(bodyCenter + new Vector2(-26f, -4f), bodyCenter + new Vector2(-12f, -4f), color, 2f);
        DrawLine(bodyCenter + new Vector2(-24f, 2f), bodyCenter + new Vector2(-10f, 2f), color, 2f);
        DrawLine(bodyCenter + new Vector2(-20f, 8f), bodyCenter + new Vector2(-8f, 8f), color, 2f);
    }

    private void DrawEyeCross(Vector2 center, Color color)
    {
        DrawLine(center + new Vector2(-3f, -3f), center + new Vector2(3f, 3f), color, 2f);
        DrawLine(center + new Vector2(-3f, 3f), center + new Vector2(3f, -3f), color, 2f);
    }

    private void DrawEllipse(Vector2 center, Vector2 radius, Color color, int segments = 28)
    {
        var points = new Vector2[segments];
        for (var index = 0; index < segments; index++)
        {
            var angle = MathF.Tau * index / segments;
            points[index] = center + new Vector2(MathF.Cos(angle) * radius.X, MathF.Sin(angle) * radius.Y);
        }

        DrawColoredPolygon(points, color);
    }

    private static Vector2[] CreateLeafPolygon(Vector2 center, Vector2 radius, float angle, float side)
    {
        return new[]
        {
            center + Rotate(new Vector2(0f, -radius.Y), angle),
            center + Rotate(new Vector2(side * radius.X, -radius.Y * 0.25f), angle),
            center + Rotate(new Vector2(side * radius.X * 0.55f, radius.Y), angle),
            center + Rotate(new Vector2(0f, radius.Y * 0.7f), angle),
            center + Rotate(new Vector2(-side * radius.X * 0.2f, radius.Y * 0.15f), angle)
        };
    }

    private static Vector2 Rotate(Vector2 point, float angle)
    {
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new Vector2(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
    }
}
#endif
