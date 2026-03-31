using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using MelonLoader;

namespace MirrorDimension;

public static class QuickMenu
{
    public static void Initialize()
    {
        QuickMenuAPI.OnMenuRegenerate += LoadUI;
    }

    private static void LoadUI(CVR_MenuManager manager)
    {
        QuickMenuAPI.OnMenuRegenerate -= LoadUI;

        var miscPage = QuickMenuAPI.MiscTabPage;
        var category = miscPage.AddCategory("Mirror Dimension");

        var flipToggle = category.AddToggle("Mirror Flip",
            "Enable to enter the mirror dimension.",
            MirrorDimensionMod.IsFlipped);
        flipToggle.OnValueUpdated += b =>
        {
            if (b == MirrorDimensionMod.IsFlipped) return;
            MirrorDimensionMod.DoAFlip();
        };
        MirrorDimensionMod.OnFlip += () =>
        {
            if (MirrorDimensionMod.IsFlipped == flipToggle.ToggleValue) return;
            flipToggle.ToggleValue = MirrorDimensionMod.IsFlipped;
        };

        var allowKeybindToggle = category.AddMelonToggle(Config.AllowKeybind);
        Config.AllowKeybind.OnEntryValueChanged.Subscribe((oldVal, newVal) =>
        {
            if (newVal != allowKeybindToggle.ToggleValue) allowKeybindToggle.ToggleValue = newVal;
        });
        var flipVideos = category.AddMelonToggle(Config.FlipVideos);
        Config.FlipVideos.OnEntryValueChanged.Subscribe((oldVal, newVal) =>
        {
            if (newVal != flipVideos.ToggleValue) flipVideos.ToggleValue = newVal;
        });
    }

    // yoinked from https://github.com/BTK-Development/BTKUILib/blob/c3c49e12bfa8998126412f9b556776cd504bf160/UIObjects/Category.cs#L240
    public static ToggleButton AddMelonToggle(this Category cat, MelonPreferences_Entry<bool> entry)
    {
        ToggleButton toggle = cat.AddToggle(entry.DisplayName, entry.Description, entry.Value);
        toggle.OnValueUpdated += b => entry.Value = b;
        return toggle;
    }
}
