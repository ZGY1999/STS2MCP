using System.Collections.Generic;
using System.Reflection;
using STS2_MCP;
using Xunit;

namespace STS2_MCP.Tests;

public class PetOverlayViewModelTests
{
    [Fact]
    public void ToggleMenu_Flips_MenuState_WithoutChangingPetMessage()
    {
        var store = new PetStateStore();
        store.SetMessage(new PetMessagePayload(
            PetMode.Advise,
            PetVisualState.Talking,
            "Companion",
            new List<string> { "Line 1", "Line 2" }));

        store.ToggleMenu();
        var opened = store.Snapshot();

        Assert.True(opened.MenuOpen);
        Assert.Equal(PetMode.Advise, opened.Mode);
        Assert.Equal(PetVisualState.Talking, opened.State);
        Assert.Equal("Companion", opened.Title);
        Assert.Equal(new[] { "Line 1", "Line 2" }, opened.Lines);

        store.ToggleMenu();

        Assert.False(store.Snapshot().MenuOpen);
    }

    [Fact]
    public void FromSnapshot_Builds_BadgeBubbleAndMenuState()
    {
        var viewModel = PetOverlayViewModel.FromSnapshot(new PetStateSnapshot(
            PetMode.Auto,
            PetVisualState.Thinking,
            "Pet",
            new[] { "Plan first", "Then act" },
            true));

        Assert.Equal("AUTO", viewModel.ModeBadgeText);
        Assert.Equal("Pet", viewModel.BubbleTitle);
        Assert.Equal("Plan first\nThen act", viewModel.BubbleText);
        Assert.True(viewModel.ShowBubble);
        Assert.True(viewModel.ShowMenu);
        Assert.Equal(3, viewModel.MenuItems.Count);
        Assert.Equal("Pause", viewModel.MenuItems[0].Label);
        Assert.False(viewModel.MenuItems[0].IsSelected);
        Assert.True(viewModel.MenuItems[2].IsSelected);
    }

    [Fact]
    public void HasEquivalentMenu_IgnoresBubbleAndVisualStateChanges()
    {
        var first = PetOverlayViewModel.FromSnapshot(new PetStateSnapshot(
            PetMode.Advise,
            PetVisualState.Thinking,
            "Thinking",
            new[] { "Line 1" },
            true));
        var second = PetOverlayViewModel.FromSnapshot(new PetStateSnapshot(
            PetMode.Advise,
            PetVisualState.Talking,
            "Answer",
            new[] { "Line 2", "Line 3" },
            true));
        var differentMode = PetOverlayViewModel.FromSnapshot(new PetStateSnapshot(
            PetMode.Auto,
            PetVisualState.Talking,
            "Answer",
            new[] { "Line 2" },
            true));

        Assert.True(first.HasEquivalentMenu(second));
        Assert.False(first.HasEquivalentMenu(differentMode));
    }

    [Theory]
    [InlineData(PetVisualState.Idle, "Wide", "Rest", false, false, false, false)]
    [InlineData(PetVisualState.Thinking, "Focused", "Gesture", false, false, false, false)]
    [InlineData(PetVisualState.Talking, "Wide", "Talk", true, true, false, false)]
    [InlineData(PetVisualState.AutoRunning, "Focused", "Dash", false, false, true, false)]
    [InlineData(PetVisualState.Paused, "Closed", "Rest", false, false, false, false)]
    [InlineData(PetVisualState.Error, "Crossed", "Rest", false, false, false, true)]
    public void OwlVisualSpec_FromState_UsesReadablePoseAndExpressionMapping(
        PetVisualState state,
        string expectedEyeState,
        string expectedWingPose,
        bool expectedBeakOpen,
        bool expectedTalkWaves,
        bool expectedSpeedLines,
        bool expectedRuffled)
    {
        var spec = CreateOwlSpec(state);

        Assert.Equal(expectedEyeState, ReadEnumName(spec, "EyeState"));
        Assert.Equal(expectedWingPose, ReadEnumName(spec, "WingPose"));
        Assert.Equal(expectedBeakOpen, ReadBoolean(spec, "BeakOpen"));
        Assert.Equal(expectedTalkWaves, ReadBoolean(spec, "ShowTalkWaves"));
        Assert.Equal(expectedSpeedLines, ReadBoolean(spec, "ShowSpeedLines"));
        Assert.Equal(expectedRuffled, ReadBoolean(spec, "IsRuffled"));
        Assert.True(ReadBoolean(spec, "ShowChestRune"));
    }

    [Fact]
    public void OwlVisualSpec_FromState_GivesThinkingAndAutoDistinctSilhouettes()
    {
        var thinking = CreateOwlSpec(PetVisualState.Thinking);
        var autoRunning = CreateOwlSpec(PetVisualState.AutoRunning);

        Assert.NotEqual(ReadEnumName(thinking, "WingPose"), ReadEnumName(autoRunning, "WingPose"));
        Assert.True(ReadSingle(thinking, "HeadTilt") < 0f);
        Assert.True(ReadSingle(autoRunning, "BodyLean") > 0f);
    }

    private static object CreateOwlSpec(PetVisualState state)
    {
        var type = typeof(PetOverlayViewModel).Assembly.GetType("STS2_MCP.PetOwlVisualSpec");
        Assert.NotNull(type);

        var factory = type!.GetMethod("FromState", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(factory);

        var result = factory!.Invoke(null, new object[] { state });
        Assert.NotNull(result);
        return result!;
    }

    private static string ReadEnumName(object instance, string propertyName)
    {
        return ReadProperty(instance, propertyName)?.ToString() ?? string.Empty;
    }

    private static bool ReadBoolean(object instance, string propertyName)
    {
        return Assert.IsType<bool>(ReadProperty(instance, propertyName));
    }

    private static float ReadSingle(object instance, string propertyName)
    {
        return Assert.IsType<float>(ReadProperty(instance, propertyName));
    }

    private static object? ReadProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return property!.GetValue(instance);
    }
}
