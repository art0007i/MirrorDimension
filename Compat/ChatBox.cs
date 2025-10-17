using HarmonyLib;
using Kafe.ChatBox;

namespace MirrorDimension.Compat;

public class ChatBox : IModCompat
{
    public void Initialize()
    {
        var patch = AccessTools.Method(typeof(ChatBoxBehavior), nameof(ChatBoxBehavior.OnMessage));
        var prefix = AccessTools.Method(typeof(ChatBox), nameof(ChatBox.Prefix));
        MirrorDimensionMod.harmony.Patch(patch, new HarmonyMethod(prefix));

        MirrorDimensionMod.OnFlip += () =>
        {
            MirrorDimensionMod.GetAllObjects<ChatBoxBehavior>().Do(x =>
            {
                MirrorDimensionMod.FlipScaleSafe(x._textBubbleGo.transform);
            });
        };
    }

    public static void Prefix(ChatBoxBehavior __instance)
    {
        MirrorDimensionMod.FlipScaleSafe(__instance._textBubbleGo.transform);
    }
}
