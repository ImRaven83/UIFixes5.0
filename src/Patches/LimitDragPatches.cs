using System;
using System.Linq;
using System.Reflection;
using Diz.Utils;
using EFT.UI;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UIFixes;

public static class LimitDragPatches
{
    public static void Enable()
    {
        new OnDragEventPatch(typeof(DragTrigger), nameof(DragTrigger.OnDrag)).SafeEnable();
        new OnDragEventPatch(typeof(DragTrigger), nameof(DragTrigger.OnBeginDrag)).SafeEnable();
        new OnDragEventPatch(typeof(DragTrigger), nameof(DragTrigger.OnEndDrag)).SafeEnable();

        // Delightfully, these have to be fully qualified because they are declared with the interface name
        new OnDragEventPatch(typeof(UIDragComponent), "UnityEngine.EventSystems.IDragHandler.OnDrag").SafeEnable();
        new OnDragEventPatch(typeof(UIDragComponent), "UnityEngine.EventSystems.IBeginDragHandler.OnBeginDrag").SafeEnable();
    }

    // Prevent drag events with right mouse, or when shift is down (to avoid multiselect conflicts)
    public class OnDragEventPatch(Type type, string methodName) : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(type, methodName);
        }

        [PatchPrefix]
        public static bool Prefix(PointerEventData eventData)
        {
            if (!Settings.LimitNonstandardDrags.Value)
            {
                return true;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                return !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift);
            }

            // allow middle button to pan
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                return true;
            }

            // right mouse (and anything else) disabled
            return false;
        }
    }
}