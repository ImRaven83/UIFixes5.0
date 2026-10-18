using System.Reflection;
using EFT.Trading;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPTushonka.Reflection.Patching;

namespace UIFixes;

public static class TradingHighlightPatches
{
    private static readonly System.Collections.Generic.Dictionary<System.IntPtr, System.Action> Handlers = [];

    public static void Enable()
    {
        new RequisiteChangePatch().SafeEnable();
        new ClosePatch().SafeEnable();
    }

    public class RequisiteChangePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(TradingGridView), nameof(TradingGridView.LateInit));
        }

        [PatchPostfix]
        public static void Postfix(TradingGridView __instance)
        {
            if (__instance._traderAssortment != null)
            {
                // Keep the exact delegate so it can be removed later; il2cpp wrappers for __instance aren't guaranteed to be the same object
                System.Action handler = __instance.RequisiteChangedHandler;
                Handlers[__instance.Pointer] = handler;
                __instance._traderAssortment.RequisiteChanged += handler;
            }
        }
    }

    public class ClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(TradingGridView), nameof(TradingGridView.Close));
        }

        [PatchPrefix]
        public static void Prefix(TradingGridView __instance)
        {
            if (__instance._traderAssortment != null && Handlers.Remove(__instance.Pointer, out var handler))
            {
                __instance._traderAssortment.RequisiteChanged -= handler;
            }
        }
    }
}