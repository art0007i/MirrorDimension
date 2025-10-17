using ABI_RC.Core.InteractionSystem;
using BTKUILib;

namespace MirrorDimension.Compat;

public class BTKUILib : IModCompat
{
    public void Initialize()
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
            MirrorDimensionMod.isFlipped);
        flipToggle.OnValueUpdated += b =>
        {
            if (b == MirrorDimensionMod.isFlipped) return;
            MirrorDimensionMod.DoAFlip();
        };
        MirrorDimensionMod.OnFlip += () =>
        {
            if (MirrorDimensionMod.isFlipped == flipToggle.ToggleValue) return;
            flipToggle.ToggleValue = MirrorDimensionMod.isFlipped;
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
}
