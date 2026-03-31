using ABI_RC.Core.Savior;
using HarmonyLib;
using ml_bft;
using System;
using UnityEngine;
using Valve.VR;

namespace MirrorDimension.Compat;

public class BetterFingersTracking : IModCompat
{
    // TODO: switching avatars while mirror flipped calibrates your fingers poorly,
    // but switching in normal mode and mirroring after works perfectly. why?
    public void Initialize()
    {
        var patch2 = AccessTools.Method(typeof(HandHandlerVR), nameof(HandHandlerVR.Update));
        var prefix2 = AccessTools.Method(typeof(BetterFingersTracking), nameof(BetterFingersTracking.HandHandlerPrefix));
        MirrorDimensionMod.harmony.Patch(patch2, prefix: new(prefix2));

        MirrorDimensionMod.OnFlip += () =>
        {
            var i = InputHandler.Instance;

            var left = i.m_leftHandHandler;
            var right = i.m_rightHandHandler;
            if(left != null && right != null)
            {
                MirrorDimensionMod.Swap(ref left.m_skeletonAction, ref right.m_skeletonAction);
            }
        };
    }

    // SteamVR sends tracking data for these bones, but it's asymmetrical.
    // The rest of the finger data is the exact same on both hands interestingly enough.
    // Maybe there's a way to flip it but I can't be bothered and this is good enough for me
    public static bool ShouldSkip(int i)
    {
        if (i == (int) SteamVR_Skeleton_JointIndexEnum.wrist) return true;
        if (i == (int) SteamVR_Skeleton_JointIndexEnum.root) return true;
        if (i == (int) SteamVR_Skeleton_JointIndexEnum.thumbMetacarpal) return true;
        if (i == (int) SteamVR_Skeleton_JointIndexEnum.indexMetacarpal) return true;
        if (i == (int) SteamVR_Skeleton_JointIndexEnum.middleMetacarpal) return true;
        if (i == (int) SteamVR_Skeleton_JointIndexEnum.ringMetacarpal) return true;
        if (i == (int) SteamVR_Skeleton_JointIndexEnum.pinkyMetacarpal) return true;
        return false;
    }

    public static bool HandHandlerPrefix(HandHandlerVR __instance)
    {
        if (!MirrorDimensionMod.IsFlipped) return true;

        if (__instance.m_skeletonAction != null)
        {
            var l_rotations = __instance.m_skeletonAction.GetBoneRotations();
            var l_positions = __instance.m_skeletonAction.GetBonePositions();
            for (int i = 0; i < HandHandlerVR.c_fingerBonesCount; i++)
            {
                if (ShouldSkip(i)) continue;
                if (__instance.m_bones[i] != null)
                {
                    var r = l_rotations[i];
                    var p = l_positions[i];
                    var flippedPos = new Vector3(-p.x, p.y, p.z);
                    __instance.m_bones[i].localRotation = r;
                    __instance.m_bones[i].localPosition = flippedPos;
                }
            }
        }

        return false;
    }
}
