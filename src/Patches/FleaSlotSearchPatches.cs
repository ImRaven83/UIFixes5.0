using SPTushonka.Reflection.Il2Cpp;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Utils;
using EFT;
using EFT.HandBook;
using EFT.UI.Ragfair;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SPTushonka.Reflection.Patching;

namespace UIFixes;

public static class FleaSlotSearchPatches
{
    public static void Enable()
    {
        new HandbookWorkaroundPatch().Enable();
        new LinkedSlotSearchPatch().Enable();
        new MyOffersPatch().Enable();
    }

    // Ragfair search strings are round-tripped through the handbook, so keeping the added suffix requires these shenanigans
    public class HandbookWorkaroundPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(RagFair), nameof(RagFair.SetSearches));
        }

        [PatchPrefix]
        public static void Prefix(RagFair __instance, Il2CppReferenceArray<RagfairSearch> searches, ref string __state)
        {
            if (!Settings.EnableSlotSearch.Value)
            {
                return;
            }

            var search = searches.FirstOrDefault(s => s.Type == EFilterType.LinkedSearch && s.StringValue.Contains(":"));
            if (search != null)
            {
                __state = search.StringValue.Split(':')[0];
                HandbookNode node = __instance._handbook[__state];
                if (node != null)
                {
                    // If the id is in the handbook (any mod slots on actual items), 
                    // fake out the id so the filter is generated with the slot suffix
                    node.Data.Id = search.StringValue;
                }
                else
                {
                    // If the id is not, like equipment slots, inject a dummy node
                    HandbookData dummyData = new()
                    {
                        Id = search.StringValue
                    };
                    HandbookNode dummyNode = new()
                    {
                        Data = dummyData,
                        IsDummy = true
                    };
                    __instance._handbook.StructuredItems.AddVirtual(__state, dummyNode);
                }
                searches[System.Array.IndexOf(searches.ToArray(), search)] = new(EFilterType.LinkedSearch, __state, search.Add);
            }
        }

        [PatchPostfix]
        public static void Postfix(RagFair __instance, ref string __state)
        {
            if (!Settings.EnableSlotSearch.Value)
            {
                return;
            }

            if (__state != null)
            {
                HandbookNode node = __instance._handbook[__state];
                if (node.IsDummy)
                {
                    // We injected this dummy node, remove it
                    __instance._handbook.StructuredItems.RemoveVirtual(__state);
                }
                else
                {
                    // Restore the Id back to its normal value
                    node.Data.Id = __state;
                }
            }
        }
    }

    // I'm not even trying to make this pretty. Just copied from session implementation
    public class LinkedSlotSearchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(EftClientBackendSession), nameof(EftClientBackendSession.GetOffers));
        }

        [PatchPrefix]
        public static bool Prefix(EftClientBackendSession __instance, ref Il2CppSystem.Threading.Tasks.Task<Result<OffersList>> __result, int page, int limit, int sortType, bool direction, int currency, int priceFrom, int priceTo, int quantityFrom, int quantityTo, int conditionFrom, int conditionTo, bool oneHourExpiration, bool removeBartering, int offerOwnerType, bool onlyFunctional, string handbookId, string linkedSearchId, string neededSearchId, Il2CppSystem.Collections.Generic.Dictionary<string, int> buildItems, int buildCount, bool updateOfferCount)
        {
            if (string.IsNullOrEmpty(linkedSearchId) || !linkedSearchId.Contains(":"))
            {
                return true;
            }

            var request = new SendRequest
            {
                Url = __instance._backendUrls.RagFair + "/uifixes/ragfair/find",
                ParseInBackground = true,
                Params = new __f__AnonymousType59<int, int, int, int, int, int, int, int, int, int, int, bool, bool, int, bool, bool, string, string, string, Il2CppSystem.Collections.Generic.Dictionary<string, int>, int, int, int>(page, limit, sortType, direction ? 1 : 0, currency, priceFrom, priceTo, quantityFrom, quantityTo, conditionFrom, conditionTo, oneHourExpiration, removeBartering, offerOwnerType, onlyFunctional, updateOfferCount, handbookId, linkedSearchId, neededSearchId, buildItems, buildCount, 18, 11),
                Retries = new Il2CppSystem.Nullable<byte>(1)
            };

            __result = __instance.Send<OffersList>(request, (System.Action<Result<OffersList>>)(_ => { }));

            return false;
        }
    }

    // In normal flows, displaying my offers will clear filters
    // After adding an offer, this method gets called without doing that, which would be harmless except for my linked search id
    // But can't just force the filters to clear since you might be on the normal ragfair page
    // So just don't do this at all unless you're on the my offers page
    public class MyOffersPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(RagFair), nameof(RagFair.FilterMyOffers));
        }

        [PatchPrefix]
        public static bool Prefix(RagFair __instance)
        {
            return __instance.FilterRule.ViewListType == EViewListType.MyOffers;
        }
    }
}