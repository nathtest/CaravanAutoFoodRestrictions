using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using UnityEngine;
using SettingsHelper;


namespace CaravanAutoFoodRestrictions
{
    [StaticConstructorOnStartup]
    public static class CaravanAutoFoodRestrictions
    {
        static CaravanAutoFoodRestrictions()
        {
            Log.Message("CaravanAutoFoodRestrictions StartUp");
            var harmony = new Harmony("com.caravan.auto.food.restrictions.patch");
            Log.Message("CaravanAutoFoodRestrictions Harmony instance loaded");
            harmony.PatchAll();  // todo patch only created methods
            Log.Message("CaravanAutoFoodRestrictions Harmony PatchAll called");
        }
        
        [HarmonyPatch(typeof(CaravanMaker))]
        [HarmonyPatch(nameof(CaravanMaker.MakeCaravan))]
        static class CaravanMaker_MakeCaravan_Patch
        {
            static void Postfix(ref Caravan __result) 
            {
                Log.Message("CaravanAutoFoodRestrictions Harmony CaravanMaker MakeCaravan");
                Log.Message("CaravanAutoFoodRestrictions Number of pawns in caravan " + __result.pawns.Count);
                
                var pawnFoodRestrictionLabel = LoadedModManager.GetMod<CaravanAutoFoodRestrictionsMod>().GetSettings<CaravanAutoFoodRestrictionsSettings>().PawnFoodRestrictionLabel;
                if (pawnFoodRestrictionLabel == null)
                {
                    Log.Warning("No food restriction selected in setting. Exiting...");
                    return;
                }
                var defaultFoodRestriction = Current.Game.foodRestrictionDatabase.DefaultFoodRestriction();
                var pawnFoodRestriction = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrFallback(restriction => restriction.label == pawnFoodRestrictionLabel, defaultFoodRestriction);
                
                Log.Message("CaravanAutoFoodRestrictions loaded setting pawnFoodRestrictionLabel " + pawnFoodRestrictionLabel);

                foreach (var pawn in __result.pawns)
                {
                    if (!pawn.RaceProps.Humanlike) continue;
                    Log.Message("CaravanAutoFoodRestrictions pawn " + pawn.Name + " set food restriction from " + pawn.foodRestriction.CurrentFoodRestriction.label + " to " +  pawnFoodRestriction.label);
                    pawn.foodRestriction.CurrentFoodRestriction = pawnFoodRestriction;
                    Log.Message("CaravanAutoFoodRestrictions debug " + pawn.IsWorldPawn() + " " + (Find.WorldPawns.GetSituation(pawn) == WorldPawnSituation.Free) + " " + (Find.WorldPawns.GetSituation(pawn) == WorldPawnSituation.CaravanMember) );
                }
            }
        }
        
        [HarmonyPatch(typeof(CaravanArrivalAction_Enter))]
        [HarmonyPatch(nameof(CaravanArrivalAction_Enter.Arrived))]
        static class CaravanArrivalAction_Enter_Patch
        {
            static void Prefix(Caravan caravan, ref CaravanArrivalAction_Enter __instance) 
            {
                Log.Message("CaravanAutoFoodRestrictions Harmony CaravanArrivalAction_Enter Arrived");
                
                MapParent mapParent = Traverse.Create(__instance).Field("mapParent").GetValue() as MapParent;
                
                Map map = mapParent.Map;
                if (map == null)
                    return;

                if (map.IsPlayerHome)
                {
                    Log.Message("CaravanAutoFoodRestrictions Harmony CaravanArrivalAction_Enter caravan home");
                }
            }
        }
        
    }
    
    
    public class CaravanAutoFoodRestrictionsSettings : ModSettings
    {
        public string PawnFoodRestrictionLabel;
        
        public override void ExposeData()
        {
            Scribe_Values.Look(ref PawnFoodRestrictionLabel, "PawnFoodRestrictionLabel");
            Log.Message("CaravanAutoFoodRestrictions ModSettings " + PawnFoodRestrictionLabel);
            base.ExposeData();
        }
    }

    public class CaravanAutoFoodRestrictionsMod : Mod
    {
        CaravanAutoFoodRestrictionsSettings settings;

        public CaravanAutoFoodRestrictionsMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<CaravanAutoFoodRestrictionsSettings>();
        }
        
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            if (Current.Game != null)
            {
                var labelFoodRestrictions = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.Select(foodRestriction => foodRestriction.label).ToArray();
                listingStandard.AddLabeledRadioList("Pawn Food Restriction", labelFoodRestrictions,ref settings.PawnFoodRestrictionLabel);
            }
            else
            {
                listingStandard.Label("A save game must be loaded.");
            }
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
        
        public override string SettingsCategory()
        {
            return "Caravan Auto Food Restrictions".Translate();
        }
    }
}