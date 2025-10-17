using ABI.CCK.Components;
using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Core.UI.UIRework.Managers;
using ABI_RC.Helpers;
using ABI_RC.Systems.IK;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.InputManagement.InputModules;
using ABI_RC.Systems.InputManagement.XR;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MirrorDimension;
using MirrorDimension.Compat;
using UnityEngine;
using UnityEngine.XR.Hands;
using Valve.VR;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

using Object = UnityEngine.Object;

[assembly: MelonInfo(
    typeof(MirrorDimensionMod),
    nameof(MirrorDimension),
    ModInfo.MOD_VERSION,
    ModInfo.MOD_AUTHOR,
    ModInfo.MOD_URL
)]
[assembly: MelonGame("ChilloutVR", "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(255, 250, 202, 222)]
[assembly: MelonAuthorColor(255, 34, 221, 136)]
[assembly: MelonOptionalDependencies("BTKUILib")]

namespace MirrorDimension;

public partial class MirrorDimensionMod : MelonMod
{
    public static bool isFlipped = false;
    public static event Action? OnFlip;
    public static GameObject? flipObject;
    internal static HarmonyLib.Harmony harmony = null!;

    private static Type cameraIndicatorType = null!;

    public override void OnInitializeMelon()
    {
        // Nightly vs Stable
        cameraIndicatorType = typeof(PlayerSetup).Assembly.GetType("ABI_RC.Core.Player.CameraIndicatorPlate");
        if (cameraIndicatorType == null) cameraIndicatorType = typeof(CameraIndicator);

        harmony = HarmonyInstance;
        Config.Initialize();

        MelonCoroutines.Start(WaitForLocalPlayer());

        // TODO: add networked indicator to players who are using the mod

        // TODO: Known issues (I probably won't fix these because they're not big enough of a problem)
        // world download progress text / icon gets mirrored, but should remain normal
        // Compat - GestureIndicator: because of how I swap gestures the mod displays it flipped
        // Compat - PortableMirrorMod: on desktop the ui is flipped but can be used as if it was in it's usual location
        // Compat - PickupArmMovement: q and e buttons to extend hands should be flipped
        // both hands get glued to each other after mirror flip in one handed mode, this also persists outside of mirror mode
        // ^ I think this is because SteamVR TrackedObject tracks the last valid device it was given, even if u set it to an invalid one

        InitializeModCompat<Compat.BTKUILib>(true);
        InitializeModCompat<BetterFingersTracking>();
        InitializeModCompat<ChatBox>();

        harmony.Patch(AccessTools.Method(cameraIndicatorType, "Start"), 
            postfix: new(AccessTools.Method(
                typeof(FixCameraIndicatorPlate),
                nameof(FixCameraIndicatorPlate.Postfix)
            )
        ));
    }

    public void InitializeModCompat<T>(bool warnOnMissing = false) where T : IModCompat, new()
    {
        var compat = new T();
        var name = compat.ModName;
        if (RegisteredMelons.Any(m => m.Info.Name == name))
        {
            try
            {
#if DEBUG
                MelonLogger.Msg($"Initializing {name} Compat");
#endif
                compat.Initialize();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to initialize {name} Compat: " + e);
            }
        }
        else if(warnOnMissing)
        {
            MelonLogger.Warning($"Mod {name} not found. You should consider installing it for a better experience.");
        }
    }


    public override void OnUpdate()
    {
        if (Config.AllowKeybind.Value && Input.GetKeyDown(KeyCode.L) && !KeyboardManager.Instance.IsViewShown)
        {
            DoAFlip();
        }
    }

    IEnumerator WaitForLocalPlayer()
    {
        while (PlayerSetup.Instance == null)
            yield return null;

        // Shader which flips your screen horizontally
        var shader = typeof(MirrorDimensionMod).Assembly.GetManifestResourceStream("shaderbundle");
        var flipPrefab = AssetBundle.LoadFromStream(shader).LoadAsset<GameObject>("Flip.prefab");

        flipObject = Object.Instantiate(flipPrefab, PlayerSetup.Instance.transform);
        flipObject.name = "[MirrorDimension] Flip Shader";
        flipObject.SetLayerRecursive(CVRLayers.UIInternal);
        var renderer = flipObject.GetComponentInChildren<Renderer>();
        renderer.localBounds = new Bounds(Vector3.zero, new Vector3(999, 999, 999));
        flipObject.SetActive(isFlipped);

        // Put listeners on their own GameObjects so they can be flipped independently from the camera
        var listeners = GetAllObjects<AudioListener>();
        for (int i = listeners.Length - 1; i >= 0; i--)
        {
            var l = listeners[i];
            var newGo = new GameObject("[MirrorDimension] AudioListener");
            newGo.transform.SetParent(l.transform, false);

            var newL = newGo.AddComponent<AudioListener>();
            newL.enabled = l.enabled;
            newL.velocityUpdateMode = l.velocityUpdateMode;
            Object.Destroy(l);
        }
    }

    public static void DoAFlip()
    {
        var origPos = PlayerSetup.Instance.GetPlayerPosition();
        var origRot = PlayerSetup.Instance.GetPlayerRotation();
        isFlipped = !isFlipped;

        var left = IKSystem.Instance.leftController;
        var right = IKSystem.Instance.rightController;
        FlipScale(left.transform);
        FlipScale(right.transform);

        var leftSteamVr = left.GetComponent<SteamVR_TrackedControllerFix>();
        var rightSteamVr = right.GetComponent<SteamVR_TrackedControllerFix>();
        if (leftSteamVr != null && rightSteamVr != null)
        {
            leftSteamVr.inputSource = isFlipped ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            rightSteamVr.inputSource = isFlipped ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
        }
        var leftXr = left.GetComponent<TrackedPoseDriver>();
        var rightXr = right.GetComponent<TrackedPoseDriver>();
        if (leftXr != null && rightXr != null)
        {
            Swap(ref leftXr.m_PositionInput, ref rightXr.m_PositionInput);
            Swap(ref leftXr.m_RotationInput, ref rightXr.m_RotationInput);
            Swap(ref leftXr.m_TrackingStateInput, ref rightXr.m_TrackingStateInput);
        }
        var leftXr2 = left.GetComponentInChildren<XRHandTrackingEvents>();
        var rightXr2 = right.GetComponentInChildren<XRHandTrackingEvents>();
        if (leftXr2 != null && rightXr2 != null)
        {
            Swap(ref leftXr2.m_Handedness, ref rightXr2.m_Handedness);
        }

        // TODO: when you vr hotswitch with mirror mode, this will not reapply
        if (MetaPort.Instance.isUsingVr)
        {
            foreach (var obj in GetAllObjects<CohtmlControlledView>())
            {
                // Skip CCK debugger, since it's already parented under quickmenu which gets flipped
                if (obj.gameObject.name.Contains("[CCK.Debugger]")) continue;

                var p = obj.transform.localPosition;
                p.x *= -1;
                obj.transform.localPosition = p;
                FlipScale(obj.transform);
            }
        }

        FlipVideoPlayers();

        foreach (var obj in Object.FindObjectsByType(cameraIndicatorType, FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            FixCameraIndicatorPlate.Postfix(obj);
        }

        foreach (var obj in GetAllObjects<AudioListener>())
        {
            FlipScale(obj.transform);
        }

        if (flipObject != null)
        {
            flipObject.GetComponentInChildren<Renderer>().sortingOrder = MetaPort.Instance.isUsingVr ? 32 : 0;
            flipObject.SetActive(isFlipped);
        }

        FlipScale(PlayerSetup.Instance.vrCameraRig.transform);
        FlipScale(PlayerSetup.Instance.vrCamera.transform);

        ApplyGlobalTransform(PlayerSetup.Instance.transform, PlayerSetup.Instance.GetPlayerPosition(), PlayerSetup.Instance.GetPlayerRotation(), origPos, origRot);

        // TODO: right now if one event handler throws, it prevents all handlers after it too
        OnFlip?.Invoke();
    }

    public static void FlipVideoPlayers()
    {
        var newScale = isFlipped && Config.FlipVideos.Value ? new Vector4(-1, 1, 0, 0) : new Vector4(1, 1, 0, 0);
        foreach (var obj in GetAllObjects<CVRVideoPlayer>())
        {
            obj?._blitMaterial.SetVector("_Scale", newScale);
        }
    }

    // Applies a desired global transform to a child object by modifying the parent's global transform
    static void ApplyGlobalTransform(
        Transform parent,
        Vector3 currentChildGlobalPos,
        Quaternion currentChildGlobalRot,
        Vector3 desiredChildGlobalPos,
        Quaternion desiredChildGlobalRot)
    {
        Vector3 localPos = parent.InverseTransformPoint(currentChildGlobalPos);
        Quaternion localRot = Quaternion.Inverse(parent.rotation) * currentChildGlobalRot;

        Quaternion newParentRot = desiredChildGlobalRot * Quaternion.Inverse(localRot);
        Vector3 newParentPos = desiredChildGlobalPos - (newParentRot * localPos);

        parent.rotation = newParentRot;
        parent.position = newParentPos;
    }

    // TODO: use this less, and use the 'safe' version more
    public static void FlipScale(Transform t)
    {
        var ls = t.localScale;
        ls.x *= -1;
        t.localScale = ls;
    }
    public static void FlipScaleSafe(Transform t)
    {
        var objFlipped = t.localScale.x < 0;
        if (isFlipped != objFlipped)
            FlipScale(t);
    }
    public static T[] GetAllObjects<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    #region Flip controls
    [HarmonyPatch(typeof(CVRInputManager), nameof(CVRInputManager.Update))]
    public class MirrorFlipInput
    {
        public static void Postfix(CVRInputManager __instance)
        {
            if (!isFlipped) return;

            __instance.movementVector.x *= -1;
            __instance.objectRotationValue.x *= -1;
            __instance.rawLookVector.x *= -1;
            __instance.lookVector.x *= -1;

            // Desktop users are inherently right handed or something
            if (MetaPort.Instance.isUsingVr)
            {
                Swap(ref __instance.interactLeftDown, ref __instance.interactRightDown);
                Swap(ref __instance.interactLeftUp, ref __instance.interactRightUp);
                // TODO: mirror InteractLeftDouble, it doesn't have a right coutnerpart, but I think it's also used nowhere
                Swap(ref __instance.interactLeftValue, ref __instance.interactRightValue);
                Swap(ref __instance.gripLeftDown, ref __instance.gripRightDown);
                Swap(ref __instance.gripLeftUp, ref __instance.gripRightUp);
            }
            Swap(ref __instance.gripLeftValue, ref __instance.gripRightValue);
            Swap(ref __instance.gestureLeftRaw, ref __instance.gestureRightRaw);
            // TODO: should I swap these? not sure what they do
            //Swap(ref __instance.leftHandTracking, ref __instance.rightHandTracking);
            //Swap(ref __instance.spaceAdjustmentLeft, ref __instance.spaceAdjustmentRight);
            Swap(ref __instance.fingerFullCurlNormalizedLeftThumb, ref __instance.fingerFullCurlNormalizedRightThumb);
            Swap(ref __instance.fingerFullCurlNormalizedLeftIndex, ref __instance.fingerFullCurlNormalizedRightIndex);
            Swap(ref __instance.fingerFullCurlNormalizedLeftMiddle, ref __instance.fingerFullCurlNormalizedRightMiddle);
            Swap(ref __instance.fingerFullCurlNormalizedLeftRing, ref __instance.fingerFullCurlNormalizedRightRing);
            Swap(ref __instance.fingerFullCurlNormalizedLeftPinky, ref __instance.fingerFullCurlNormalizedRightPinky);
            Swap(ref __instance.finger1StretchedLeftThumb, ref __instance.finger1StretchedRightThumb);
            Swap(ref __instance.finger2StretchedLeftThumb, ref __instance.finger2StretchedRightThumb);
            Swap(ref __instance.finger3StretchedLeftThumb, ref __instance.finger3StretchedRightThumb);
            Swap(ref __instance.finger1StretchedLeftIndex, ref __instance.finger1StretchedRightIndex);
            Swap(ref __instance.finger2StretchedLeftIndex, ref __instance.finger2StretchedRightIndex);
            Swap(ref __instance.finger3StretchedLeftIndex, ref __instance.finger3StretchedRightIndex);
            Swap(ref __instance.finger1StretchedLeftMiddle, ref __instance.finger1StretchedRightMiddle);
            Swap(ref __instance.finger2StretchedLeftMiddle, ref __instance.finger2StretchedRightMiddle);
            Swap(ref __instance.finger3StretchedLeftMiddle, ref __instance.finger3StretchedRightMiddle);
            Swap(ref __instance.finger1StretchedLeftRing, ref __instance.finger1StretchedRightRing);
            Swap(ref __instance.finger2StretchedLeftRing, ref __instance.finger2StretchedRightRing);
            Swap(ref __instance.finger3StretchedLeftRing, ref __instance.finger3StretchedRightRing);
            Swap(ref __instance.finger1StretchedLeftPinky, ref __instance.finger1StretchedRightPinky);
            Swap(ref __instance.finger2StretchedLeftPinky, ref __instance.finger2StretchedRightPinky);
            Swap(ref __instance.finger3StretchedLeftPinky, ref __instance.finger3StretchedRightPinky);
            Swap(ref __instance.fingerSpreadLeftThumb, ref __instance.fingerSpreadRightThumb);
            Swap(ref __instance.fingerSpreadLeftIndex, ref __instance.fingerSpreadRightIndex);
            Swap(ref __instance.fingerSpreadLeftMiddle, ref __instance.fingerSpreadRightMiddle);
            Swap(ref __instance.fingerSpreadLeftRing, ref __instance.fingerSpreadRightRing);
            Swap(ref __instance.fingerSpreadLeftPinky, ref __instance.fingerSpreadRightPinky);
        }
    }
    public static void Swap<T>(ref T a, ref T b)
    {
        T temp = a;
        a = b;
        b = temp;
    }
    public static void Swap2<T>(ref T left, ref T right)
    {
        T temp = left;
        if (CVRInputManager.Instance._leftController != eXRControllerType.None)
        {
            left = right;
        }
        else if (CVRInputManager.Instance._rightController != eXRControllerType.None)
        {
            right = temp;
        }
    }
    #endregion

    #region Fix Scrolling
    [HarmonyPatch(typeof(CVRInputModule_XR), nameof(CVRInputModule_XR.Update_Interaction))]
    public class MirrorFlipScrolling
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var found = false;
            var lookFor1 = AccessTools.Method(typeof(CVRInputModule_XR), nameof(CVRInputModule_XR.GetModuleForLookHand));
            var lookFor2 = AccessTools.Method(typeof(CVRInputModule_XR), nameof(CVRInputModule_XR.GetModuleForMovementHand));
            foreach (var code in codes)
            {
                var isLook = code.Calls(lookFor1);
                if (isLook || code.Calls(lookFor2))
                {
                    found = true;
                    yield return new(isLook ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                    yield return new(OpCodes.Call, AccessTools.Method(typeof(MirrorFlipScrolling), nameof(MirrorFlipScrolling.GetModule)));
                }
                else
                {
                    yield return code;
                }
            }
            if (!found)
            {
                MelonLogger.Error("Transpiler patch failed: MirrorFlipScrolling!");
            }
        }
        public static CVRXRModule GetModule(CVRInputModule_XR instance, bool isLook)
        {
            return isFlipped ^ isLook ? instance.GetModuleForLookHand() : instance.GetModuleForMovementHand();
        }
    }
    [HarmonyPatch(typeof(CVRInputManager), nameof(CVRInputManager.IsControllerPointedAtMenu))]
    public class MirrorFlipMenuPointer
    {
        public static void Prefix(ref bool isLeftController)
        {
            if (isFlipped)
            {
                isLeftController = !isLeftController;
            }
        }
    }
    #endregion

    #region Fix Vibrations
    [HarmonyPatch(typeof(CVRInputManager), nameof(CVRInputManager.Vibrate))]
    public class MirrorFlipVibrate
    {
        public static void Prefix(ref CVRHand hand)
        {
            if (!isFlipped) return;
            hand = (CVRHand) (1 - (int) hand);
        }
    }
    #endregion

    #region Fix swapping avatars or changing player height ruining the mirror effect
    [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.SetPlaySpaceScale))]
    public static class FixPlayerSetup
    {
        public static void Postfix(PlayerSetup __instance)
        {
            if (isFlipped) FlipScale(__instance.vrCameraRig.transform);
        }
    }
    #endregion

    #region Fix the quickmenu being positioned on the wrong hand
    [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.SelectedQuickMenuHand), MethodType.Getter)]
    public static class FixQuickMenuHand
    {
        // TODO: due to the way this works, if you offset your menu, it will be at a different offset after flipping
        public static bool Prefix(ref CVRHand __result)
        {
            if (!isFlipped) return true;
            __result = __result == CVRHand.Right ? CVRHand.Left : CVRHand.Right;
            return false;
        }
    }
    #endregion

    #region Fix Nameplates and Camera Nameplates
    [HarmonyPatch(typeof(PlayerNameplate), nameof(PlayerNameplate.Update))]
    public static class FixNameplates
    {
        public static void Postfix(PlayerNameplate __instance)
        {
            foreach (Transform r in __instance.s_Nameplate.transform)
            {
                FlipScaleSafe(r);
            }
        }
    }

    public static class FixCameraIndicatorPlate
    {
        public static void Postfix(object __instance)
        {
            try
            {
                var img = (Image) Traverse.Create(__instance).Field("nameplateBackground").GetValue();
                FlipScaleSafe(img.transform.parent);
            }
            catch
            { }
        }
    }
    #endregion

    #region Swap Gestures
    [HarmonyPatch]
    public class MirrorFlipGestures
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayerSetup), nameof(PlayerSetup.AnimateCoreParameters));
            yield return AccessTools.Method(typeof(GestureIndicator), nameof(GestureIndicator.GetLeftGestureIndex));
            yield return AccessTools.Method(typeof(GestureIndicator), nameof(GestureIndicator.GetRightGestureIndex));
        }

        private static FieldInfo leftHand = AccessTools.Field(typeof(CVRInputManager), nameof(CVRInputManager.gestureLeft));
        private static FieldInfo rightHand = AccessTools.Field(typeof(CVRInputManager), nameof(CVRInputManager.gestureRight));
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var found = false;
            foreach (var code in codes)
            {
                yield return code;
                var isLeft = code.Is(OpCodes.Ldfld, leftHand);
                if (isLeft || code.Is(OpCodes.Ldfld, rightHand))
                {
                    found = true;
                    yield return new(isLeft ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                    yield return new(OpCodes.Call, AccessTools.Method(typeof(MirrorFlipGestures), nameof(MirrorFlipGestures.GetGesture)));
                }
            }
            if (!found)
            {
                MelonLogger.Error("Transpiler patch failed: MirrorFlipGestures!");
            }
        }

        public static float GetGesture(float orig, bool isLeft)
        {
            if (!isFlipped) return orig;
            return isLeft ? CVRInputManager.Instance.gestureRight : CVRInputManager.Instance.gestureLeft;
        }
    }
    #endregion

    #region Swap Controller Tracking State
    [HarmonyPatch]
    public class MirrorFlipControllers
    {

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CVRInputModule_XR), nameof(CVRInputModule_XR.UpdateInput));
        }

        private static FieldInfo leftHand = AccessTools.Field(typeof(CVRInputManager), nameof(CVRInputManager._leftController));
        private static FieldInfo rightHand = AccessTools.Field(typeof(CVRInputManager), nameof(CVRInputManager._rightController));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var found = false;
            foreach (var code in codes)
            {
                var isLeft = code.Is(OpCodes.Stfld, leftHand);
                if (isLeft || code.Is(OpCodes.Stfld, rightHand))
                {
                    found = true;
                    yield return new(isLeft ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                    yield return new(OpCodes.Call, AccessTools.Method(typeof(MirrorFlipControllers), nameof(MirrorFlipControllers.SetController)));
                }
                else
                {
                    yield return code;
                }
            }
            if (!found)
            {
                MelonLogger.Error("Transpiler patch failed: MirrorFlipControllers!");
            }
        }

        public static void SetController(CVRInputManager instance, eXRControllerType val, bool isLeft)
        {
            // this is the first time I've used xor for boolean logic, neat
            if (isFlipped ^ isLeft)
                instance._leftController = val;
            else
                instance._rightController = val;
        }
    }
    #endregion

    #region Swap Full Body Tracker Roles
    [HarmonyPatch(typeof(TrackingSystem), nameof(TrackingSystem.Update))]
    public class MirrorFlipFbt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var found = false;
            var lookFor = AccessTools.PropertyGetter(typeof(TrackingSystem), nameof(TrackingSystem.AllTrackingPoints));
            foreach (var code in codes)
            {
                yield return code;
                if (code.Calls(lookFor))
                {
                    found = true;
                    yield return new(OpCodes.Dup);
                    yield return new(OpCodes.Call, AccessTools.Method(typeof(MirrorFlipFbt), nameof(FlipAllTrackingPoints)));
                }
            }

            if (!found)
            {
                MelonLogger.Error("Transpiler patch failed: MirrorFlipFbt!");
            }
        }

        public static void FlipAllTrackingPoints(List<TrackingPoint> points)
        {
            foreach (var point in points)
            {
                if (point?.displayObject == null) continue;
                var pointFlipped = point.displayObject.name.Contains("[Flip]");
                if (isFlipped != pointFlipped)
                {
                    point.assignedRole = FlipRole(point.assignedRole);
                    point.suggestedRole = FlipRole(point.suggestedRole);
                    if (isFlipped)
                    {
                        point.displayObject.name += " [Flip]";
                    }
                    else
                    {
                        point.displayObject.name = point.displayObject.name.Replace(" [Flip]", "");
                    }
                }
            }
        }
    }
    public static TrackingPoint.TrackingRole FlipRole(TrackingPoint.TrackingRole role)
    {
        switch (role)
        {
            case TrackingPoint.TrackingRole.LeftFoot:
                return TrackingPoint.TrackingRole.RightFoot;
            case TrackingPoint.TrackingRole.RightFoot:
                return TrackingPoint.TrackingRole.LeftFoot;
            case TrackingPoint.TrackingRole.LeftKnee:
                return TrackingPoint.TrackingRole.RightKnee;
            case TrackingPoint.TrackingRole.RightKnee:
                return TrackingPoint.TrackingRole.LeftKnee;
            case TrackingPoint.TrackingRole.LeftElbow:
                return TrackingPoint.TrackingRole.RightElbow;
            case TrackingPoint.TrackingRole.RightElbow:
                return TrackingPoint.TrackingRole.LeftElbow;
            case TrackingPoint.TrackingRole.LeftHand:
                return TrackingPoint.TrackingRole.RightHand;
            case TrackingPoint.TrackingRole.RightHand:
                return TrackingPoint.TrackingRole.LeftHand;
            case TrackingPoint.TrackingRole.Invalid:
            case TrackingPoint.TrackingRole.Generic:
            case TrackingPoint.TrackingRole.Hips:
            case TrackingPoint.TrackingRole.Chest:
            case TrackingPoint.TrackingRole.Head:
            default:
                break;
        }
        return role;
    }
    #endregion
}