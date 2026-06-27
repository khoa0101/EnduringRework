using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Controllers;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using System.Collections.Generic;

namespace EnduringRework;

internal class EnduringSpellsRework
{
    internal static void UpdateEnduringSpellsMythicAbility()
    {
        var enduringSpells = Utils.GetBlueprint<BlueprintFeature>("2f206e6d292bdfb4d981e99dcf08153f");
        enduringSpells.SetComponents(
            new PrerequisiteFeature()
            {
                m_Feature = Utils.GetBlueprintReference<BlueprintFeatureReference>("f180e72e4a9cbaa4da8be9bc958132ef")
            },
            new EnduringSpellsRedone()
            {
                m_Greater = Utils.GetBlueprintReference<BlueprintUnitFactReference>("13f9269b3b48ae94c896f0371ce5e23c")
            }
        );
        enduringSpells.m_Description = Utils.CreateLocalizedString("AlterAsc.EnduringRework.EnduringSpellsDescription"
            , "You've learned a way to prolong the effects of your beneficial spells and abilities.\r\n" +
            "Benefit: Effects granted to your allies that should last shorter than 1 hour last 1 hour.\r\n" +
            "Effects granted to your allies that should last at least an hour but shorter than 24 hours now last 24 hours.");

        var greaterEnduringSpells = Utils.GetBlueprint<BlueprintFeature>("13f9269b3b48ae94c896f0371ce5e23c");
        greaterEnduringSpells.m_Description = Utils.CreateLocalizedString("AlterAsc.EnduringRework.GreaterEnduringSpellsDescription"
            , "You've mastered a way to prolong your beneficial spells and abilities.\r\n" +
            "Benefit: Effects granted to your allies now last at least 24 hours.");
    }
}

/// <summary>
/// New ES/GES component
/// </summary>
[AllowMultipleComponents]
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("07ab83e0eb884687869c091caa1c292e")]
public class EnduringSpellsRedone :
      UnitFactComponentDelegate,
      IUnitBuffHandler,
      IGlobalSubscriber,
      ISubscriber
{
    internal BlueprintUnitFactReference m_Greater;

    public BlueprintUnitFact Greater => this.m_Greater?.Get();

    public void HandleBuffDidAdded(Buff buff)
    {
        AbilityData ability = buff.Context.SourceAbilityContext?.Ability;
        if (ability == null || !(buff.MaybeContext?.MaybeCaster == (UnitDescriptor)this.Owner) 
            || buff.TimeLeft >= 24.Hours())
        {
            return;
        }

        if (buff.TimeLeft < 59.54.Minutes() && !this.Owner.HasFact(Greater)) 
        // 1.Hours or 60.Minutes doesn't work for some reason if the duration is exactly 1 hour
        // 59.54.Minutes is 1 round less.
        {
            buff.SetEndTime(1.Hours() + buff.AttachTime);
        }
        else
        {
            buff.SetEndTime(24.Hours() + buff.AttachTime);
        }
    }

    public void HandleBuffDidRemoved(Buff buff)
    {
    }
}

/// <summary>
/// Applies effect of ES/GES on weapon enchantments
/// This is a non-skipping prefix, same as TTT-Reworks, which is why it's loaded after
/// </summary>
[HarmonyPatch(typeof(ItemEntity), nameof(ItemEntity.AddEnchantment))]
[HarmonyAfter("TabletopTweaks-Reworks")]
static class ItemEntity_AddEnchantment_EnduringSpells_Patch
{
    private static readonly BlueprintFeature EnduringSpells = Utils.GetBlueprint<BlueprintFeature>("2f206e6d292bdfb4d981e99dcf08153f");
    private static readonly BlueprintFeature EnduringSpellsGreater = Utils.GetBlueprint<BlueprintFeature>("13f9269b3b48ae94c896f0371ce5e23c");

    private static Rounds One_Hour = DurationRate.Hours.ToRounds();
    private static Rounds Ten_Minutes = DurationRate.Minutes.ToRounds() * 10;
    private static Rounds One_Minute = DurationRate.Minutes.ToRounds();

    private static Rounds One_Day = DurationRate.Hours.ToRounds() * 24;

    [HarmonyPrefix]
    static bool Prefix(MechanicsContext parentContext, ref Rounds? duration)
    {
        if (parentContext != null && parentContext.MaybeOwner != null && duration != null)
        {
            var abilityData = parentContext.SourceAbilityContext?.Ability;
            if (abilityData == null || abilityData.SourceItem != null)
            {
                return true;
            }
            var owner = parentContext.MaybeOwner;
            if (duration > One_Hour)
            {
                if (owner.Descriptor.HasFact(EnduringSpellsGreater))
                {
                    duration = One_Day;
                }
            }
            else
            {
                if (owner.Descriptor.HasFact(EnduringSpells))
                {
                    if (duration > One_Hour)
                    {
                        duration = One_Day;
                    }
                    else
                    if (duration > Ten_Minutes)
                    {
                        duration = One_Hour;
                    }
                    else if (duration > One_Minute)
                    {
                        duration = Ten_Minutes;
                    }
                }
            }
        }
        return true;
    }
}
