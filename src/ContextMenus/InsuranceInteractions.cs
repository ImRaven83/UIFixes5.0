using System;
using SPTushonka.Reflection.Il2Cpp;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Diz.Utils;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Insurance;
using Il2CppInterop.Runtime.Injection;

namespace UIFixes;

// Injected into il2cpp (see Plugin.Load). The game's ContextInteractions<T> is generic and can't be subclassed from here,
// so this derives from BaseEmptyContextInteractions and only uses dynamic interactions.
public class InsuranceInteractions : BaseEmptyContextInteractions
{
    private readonly InsuranceCompany _insurance;
    private readonly List<Item> _items;
    private readonly int playerRubles;
    private List<InsuredItem> _insurableItems;
    private readonly Dictionary<string, int> _prices = [];

    public InsuranceInteractions(IntPtr pointer) : base(pointer) { }

    public InsuranceInteractions(Item item, ItemUiContext uiContext, int playerRubles) : this([item], uiContext, playerRubles) { }

    public InsuranceInteractions(IEnumerable<Item> items, ItemUiContext uiContext, int playerRubles)
        : base(ClassInjector.DerivedConstructorPointer<InsuranceInteractions>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, uiContext);

        _insurance = uiContext.Session.InsuranceCompany;
        _items = [.. items];
        this.playerRubles = playerRubles;
    }

    public void LoadAsync(Action callback)
    {
        IEnumerable<InsuredItem> insuranceItems = _items.Select(InsuredItem.FindOrCreate).ToList();
        _insurableItems = insuranceItems.SelectMany(i => _insurance.GetItemChildren(i))
            .FlattenManaged(i => _insurance.GetItemChildren(i))
            .Concat(insuranceItems)
            .Where(i => _insurance.ItemTypeAvailableForInsurance(i) && !_insurance.InsuredItems.Contains(i))
            .ToList();

        _insurance.GetInsurePriceAsync(_insurableItems.ToIl2CppList(), (Action<Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<InsuredItem>>)(_ =>
        {
            foreach (var insurer in _insurance.Insurers)
            {
                int price = _insurableItems.Select(i => _insurance.InsureSummary[insurer.Id][i]).Where(s => s.Loaded).Sum(s => s.Amount);
                _prices[insurer.Id] = price;

                string priceColor = price > playerRubles ? "#FF0000" : "#ADB8BC";

                string text = string.Format("<b><color=#C6C4B2>{0}</color> <color={1}>({2} ₽)</color></b>", insurer.LocalizedName, priceColor, price);

                AddDynamicInteraction(MakeInteractionId(insurer.Id), text, (Action)(() => Insure(insurer.Id)));
            }

            if (!_insurance.Insurers.Any())
            {
                AddDynamicInteraction(NoneInteractionId, "None", null, null, null, "No insurers?");
            }

            callback();
        }));
    }

    private void Insure(string insurerId)
    {
        _insurance.SelectedInsurerId = insurerId;
        _insurance.InsureItems(_insurableItems.ToIl2CppList(), (Action<IResult>)(result => { }));
    }

    public IResult GetButtonInteraction(string interactionId)
    {
        string traderId = interactionId.Split(':')[1];
        if (_prices[traderId] > playerRubles)
        {
            return new FailedResult("ragfair/Not enough money", 0);
        }

        return SuccessfulResult.New;
    }

    public override bool HasIcons
    {
        get { return false; }
    }

    private const string NoneInteractionId = "UIFixesInsurerNone";

    private static string MakeInteractionId(string traderId)
    {
        return "UIFixesInsurerId:" + traderId;
    }

    public static bool IsInsuranceInteractionId(string id)
    {
        return id.StartsWith("UIFixesInsurerId:");
    }
}

public static class InsuranceExtensions
{
    public static bool IsInsuranceInteraction(this DynamicContextInteraction interaction)
    {
        return interaction.Id.StartsWith("UIFixesInsurerId:");
    }
}