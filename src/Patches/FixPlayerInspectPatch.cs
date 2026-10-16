using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.Matchmaker;
using HarmonyLib;
using SPTushonka.Reflection.Patching;

namespace UIFixes;

// If a player is inspected during loading after the SinglePlayerInventoryController is constructed, the controller is 
// overwritten and bugs ensue. Block the eye from doing anything once the controller has been created
public class FixPlayerInspectPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(PartyInfoPanel), nameof(PartyInfoPanel.Method_Private_Void_RaidPlayer_PDM_0)); // OnViewEquipmentClick
    }

    [PatchPrefix]
    public static bool Prefix(RaidPlayer raidPlayer)
    {
        var equipment = raidPlayer.PlayerVisualRepresentation.Equipment;
        return equipment.CurrentAddress.GetOwnerOrNull() is not Player.PlayerOwnerInventoryController;
    }
}