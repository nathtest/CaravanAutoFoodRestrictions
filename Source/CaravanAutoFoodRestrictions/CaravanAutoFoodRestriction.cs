using System.Collections.Generic;
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
                
                var caravanAutoFoodRestrictionsData = Find.World.GetComponent<CaravanAutoFoodRestrictionsData>();
                
                if (caravanAutoFoodRestrictionsData.RetainedCaravanData ==  null)
                {
                    caravanAutoFoodRestrictionsData.RetainedCaravanData = new Dictionary<string, string>();
                    Log.Message("CaravanAutoFoodRestrictions loaded setting RetainedCaravanData empty");
                }
                
                var pawnFoodRestrictionLabel = caravanAutoFoodRestrictionsData.PawnFoodRestrictionLabel;
                if (pawnFoodRestrictionLabel == null)
                {
                    Log.Warning("No food restriction selected in setting. Exiting...");
                    return;
                }

                var pawnFoodRestriction = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrFallback(restriction => restriction.label == pawnFoodRestrictionLabel, Current.Game.foodRestrictionDatabase.DefaultFoodRestriction());
                Log.Message("CaravanAutoFoodRestrictions loaded setting pawnFoodRestrictionLabel " + pawnFoodRestrictionLabel);
                
                foreach (var pawn in __result.pawns)
                {
                    if (!pawn.RaceProps.Humanlike) continue;
                    caravanAutoFoodRestrictionsData.RetainedCaravanData[__result.GetUniqueLoadID()+"_"+pawn.GetUniqueLoadID()] = pawn.foodRestriction.CurrentFoodRestriction.label;
                    
                    Log.Message("CaravanAutoFoodRestrictions pawn " + pawn.Name + " set food restriction from " + pawn.foodRestriction.CurrentFoodRestriction.label + " to " +  pawnFoodRestriction.label);
                    pawn.foodRestriction.CurrentFoodRestriction = pawnFoodRestriction;
                }
            }
        }
        
        [HarmonyPatch(typeof(CaravanArrivalAction_Enter))]
        [HarmonyPatch(nameof(CaravanArrivalAction_Enter.Arrived))]
        static class CaravanArrivalAction_Enter_Patch
        {
            static void Prefix(Caravan caravan, ref MapParent ___mapParent) 
            {
                Log.Message("CaravanAutoFoodRestrictions Harmony CaravanArrivalAction_Enter Arrived");
                
                Map map = ___mapParent.Map;
                if (map == null)
                    return;
                
                var caravanAutoFoodRestrictionsData = Find.World.GetComponent<CaravanAutoFoodRestrictionsData>();
                if(caravanAutoFoodRestrictionsData == null) return;

                if (map.IsPlayerHome)
                {
                    Log.Message("CaravanAutoFoodRestrictions Harmony CaravanArrivalAction_Enter caravan home");

                    foreach (var pawn in caravan.pawns)
                    {
                        if(caravanAutoFoodRestrictionsData.RetainedCaravanData.TryGetValue(caravan.GetUniqueLoadID()+"_"+pawn.GetUniqueLoadID(), out var pawnFoodRestrictionLabelSaved))
                        {
                            Log.Message("CaravanAutoFoodRestrictions Harmony CaravanArrivalAction_Enter value found " + pawnFoodRestrictionLabelSaved);
                            pawn.foodRestriction.CurrentFoodRestriction = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrFallback(restriction => restriction.label == pawnFoodRestrictionLabelSaved, Current.Game.foodRestrictionDatabase.DefaultFoodRestriction());
                            caravanAutoFoodRestrictionsData.RetainedCaravanData.Remove(caravan.GetUniqueLoadID()+"_"+pawn.GetUniqueLoadID());
                        }
                        else
                        {
                            Log.Message("CaravanAutoFoodRestrictions Harmony CaravanArrivalAction_Enter value not found " + caravan.GetUniqueLoadID()+"_"+pawn.GetUniqueLoadID());
                        }
                    }
                }
                else
                {
                    Log.Message("CaravanAutoFoodRestrictions Harmony CaravanArrivalAction_Enter caravan is not home");
                }
            }
        }
        
    }
    
    public class CaravanAutoFoodRestrictionsData : WorldComponent {
        public string PawnFoodRestrictionLabel;
        public Dictionary<string,  string> RetainedCaravanData;
        public List<string> PawnCaravanId;  // string concat of pawn id and caravan id
        public List<string> FoodRestrictionLabel;

        public CaravanAutoFoodRestrictionsData(World world) : base(world) {
        }
        public override void ExposeData() {
            Scribe_Values.Look(ref PawnFoodRestrictionLabel, "PawnFoodRestrictionLabel");
            // https://spdskatr.github.io/RWModdingResources/saving-guide
            Scribe_Collections.Look(ref RetainedCaravanData, "RetainedCaravanData", LookMode.Value, LookMode.Value, ref PawnCaravanId, ref FoodRestrictionLabel);
        }
    }
    
    public class CaravanAutoFoodRestrictionsMod : Mod
    {

        public CaravanAutoFoodRestrictionsMod(ModContentPack content) : base(content)
        {

        }
        
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            if (Current.Game != null)
            {
                var labelFoodRestrictions = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.Select(foodRestriction => foodRestriction.label).ToArray();
                listingStandard.AddLabeledRadioList("Pawn Food Restriction", labelFoodRestrictions,ref Find.World.GetComponent<CaravanAutoFoodRestrictionsData>().PawnFoodRestrictionLabel);
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