using System.Reflection;
using EFT.UI.WeaponModding;
using HarmonyLib;
using SPTushonka.Reflection.Patching;

namespace UIFixes;

public static class WeaponPreviewPatches
{
    public static void Enable()
    {
        new WeaponPreviewCameraNearClipPatch().SafeEnable();
    }

    public class WeaponPreviewCameraNearClipPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // this is called when WeaponPreview is opened and fully initialized,
            // WeaponPreview is used both by weapon modding screen, edit build screen, and item overview
            return AccessTools.Method(typeof(WeaponPreview.__c__DisplayClass23_0), nameof(WeaponPreview.__c__DisplayClass23_0.Method_Internal_Void_Token_IEasyBundle_0));
        }

        [PatchPostfix]
        public static void Postfix(WeaponPreview.__c__DisplayClass23_0 __instance)
        {
            __instance.__4__this.WeaponPreviewCamera.nearClipPlane = 0.01f;
        }
    }
}
