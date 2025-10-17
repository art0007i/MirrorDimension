using MelonLoader;

namespace MirrorDimension;

public static class Config
{
#nullable disable
    private static MelonPreferences_Category _melonCategory;
    public static MelonPreferences_Entry<bool> AllowKeybind;
    public static MelonPreferences_Entry<bool> FlipVideos;
#nullable enable

    public static void Initialize()
    {
        _melonCategory = MelonPreferences.CreateCategory("MirrorDimension", "Mirror Dimension");
        AllowKeybind = _melonCategory.CreateEntry("AllowKeybind", true, "Allow Keybind",
            "If enabled, you can use the `L` key to do a flip.");
        FlipVideos = _melonCategory.CreateEntry("FlipVideos", false, "Flip Videos",
            "If enabled, videos will be flipped to look normal in mirror mode.");

        FlipVideos.OnEntryValueChanged.Subscribe((_,_) =>
        {
            MirrorDimensionMod.FlipVideoPlayers();
        });
    }
}
