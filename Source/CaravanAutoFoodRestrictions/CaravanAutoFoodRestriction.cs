using System;
using System.Collections.Generic;
using CaravanAutoFoodRestrictions;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using UnityEngine;
using Verse.Sound;

namespace CaravanAutoFoodRestrictions
{
    [StaticConstructorOnStartup]
    public static class CaravanAutoFoodRestrictions
    {
        static CaravanAutoFoodRestrictions()
        {
            Log.Message("CaravanAutoFoodRestrictions StartUp");
            var harmony = new Harmony("com.caravan.auto.food.restrictions.patch");
            harmony.PatchAll(); // todo patch only created methods
        }

        [HarmonyPatch(typeof(CaravanMaker))]
        [HarmonyPatch(nameof(CaravanMaker.MakeCaravan))]
        static class CaravanMaker_MakeCaravan_Patch
        {
            static void Postfix(ref Caravan __result)
            {
                
                var caravanAutoFoodRestrictionsData = Find.World.GetComponent<CaravanAutoFoodRestrictionsData>();
                
                foreach (var pawn in __result.pawns)
                {
                    if (!pawn.RaceProps.Humanlike) continue;
                    caravanAutoFoodRestrictionsData.RetainedHomeData[pawn.GetUniqueLoadID()] = pawn.foodRestriction.CurrentFoodRestriction.label;

                    if (!caravanAutoFoodRestrictionsData.RetainedCaravanData.ContainsKey(pawn.GetUniqueLoadID()))caravanAutoFoodRestrictionsData.RetainedCaravanData[pawn.GetUniqueLoadID()] = Current.Game.foodRestrictionDatabase.DefaultFoodRestriction().label;
                    pawn.foodRestriction.CurrentFoodRestriction = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrFallback(restriction => restriction.label == caravanAutoFoodRestrictionsData.RetainedCaravanData[pawn.GetUniqueLoadID()], Current.Game.foodRestrictionDatabase.DefaultFoodRestriction());
                }
            }
        }

        [HarmonyPatch(typeof(CaravanArrivalAction_Enter))]
        [HarmonyPatch(nameof(CaravanArrivalAction_Enter.Arrived))]
        static class CaravanArrivalAction_Enter_Patch
        {
            static void Prefix(Caravan caravan, ref MapParent ___mapParent)
            {

                var caravanAutoFoodRestrictionsData = Find.World.GetComponent<CaravanAutoFoodRestrictionsData>();
                if (caravanAutoFoodRestrictionsData?.RetainedHomeData == null) return;

                Map map = ___mapParent.Map;
                if (map == null)
                    return;

                if (map.IsPlayerHome)
                {

                    foreach (var pawn in caravan.pawns)
                    {
                        if (caravanAutoFoodRestrictionsData.RetainedHomeData.TryGetValue(pawn.GetUniqueLoadID(), out var pawnFoodRestrictionLabelSaved))
                        {
                            pawn.foodRestriction.CurrentFoodRestriction = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrFallback(restriction => restriction.label == pawnFoodRestrictionLabelSaved, Current.Game.foodRestrictionDatabase.DefaultFoodRestriction());
                        }

                    }
                }

            }
        }
    }

    public class CaravanAutoFoodRestrictionsData : WorldComponent
    {
        public Dictionary<string, string> RetainedCaravanData = new Dictionary<string, string>();
        public Dictionary<string, string> RetainedHomeData = new Dictionary<string, string>();
        public List<string> CaravanPawnId;
        public List<string> CaravanFoodRestrictionLabel;
        public List<string> HomePawnId;
        public List<string> HomeFoodRestrictionLabel;

        public CaravanAutoFoodRestrictionsData(World world) : base(world)
        {

        }

        public override void ExposeData()
        {
            // https://spdskatr.github.io/RWModdingResources/saving-guide
            Scribe_Collections.Look(ref RetainedCaravanData, "RetainedCaravanData", LookMode.Value, LookMode.Value, ref CaravanPawnId, ref CaravanFoodRestrictionLabel);
            Scribe_Collections.Look(ref RetainedHomeData, "RetainedHomeData", LookMode.Value, LookMode.Value, ref HomePawnId, ref HomeFoodRestrictionLabel);
        }
    }
    
}

namespace RimWorld
{
    public class PawnColumnWorker_CaravanFoodRestriction : PawnColumnWorker
    {
        private const int TopAreaHeight = 65;
        public const int ManageFoodRestrictionsButtonHeight = 32;

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            MouseoverSounds.DoRegion(rect);
            if (!Widgets.ButtonText(new Rect(rect.x, rect.y + (rect.height - 65f), Mathf.Min(rect.width, 360f), 32f), (string) "Manage caravan food Restrictions"))
                return;
            Find.WindowStack.Add((Window) new Dialog_ManageFoodRestrictions((FoodRestriction) null));
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (pawn.foodRestriction == null)
                return;
            this.DoAssignFoodRestrictionButtons(rect, pawn);
        }

        private IEnumerable<Widgets.DropdownMenuElement<FoodRestriction>> Button_GenerateMenu(
            Pawn pawn)
        {
            foreach (FoodRestriction allFoodRestriction in Current.Game.foodRestrictionDatabase.AllFoodRestrictions)
            {
                FoodRestriction foodRestriction = allFoodRestriction;
                yield return new Widgets.DropdownMenuElement<FoodRestriction>()
                {
                    option = new FloatMenuOption(foodRestriction.label, (Action) (() => SetCaravanPawnFoodRestriction(pawn, foodRestriction))),
                    payload = foodRestriction
                };
            }
        }

        public override int GetMinWidth(PawnTable table) => Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(194f));

        public override int GetOptimalWidth(PawnTable table) => Mathf.Clamp(Mathf.CeilToInt(251f), this.GetMinWidth(table), this.GetMaxWidth(table));

        public override int GetMinHeaderHeight(PawnTable table) => Mathf.Max(base.GetMinHeaderHeight(table), 65);

        public override int Compare(Pawn a, Pawn b) => this.GetValueToCompare(a).CompareTo(this.GetValueToCompare(b));

        private int GetValueToCompare(Pawn pawn) => pawn.foodRestriction != null && pawn.foodRestriction.CurrentFoodRestriction != null ? GetCaravanPawnFoodRestriction(pawn).id : int.MinValue;

        private void DoAssignFoodRestrictionButtons(Rect rect, Pawn pawn)
        {
            int width1 = Mathf.FloorToInt((float) (((double) rect.width - 4.0) * 0.714285731315613));
            int width2 = Mathf.FloorToInt((float) (((double) rect.width - 4.0) * 0.28571429848671));
            float x1 = rect.x;
            Rect rect1 = new Rect(x1, rect.y + 2f, (float) width1, rect.height - 4f);

            Widgets.Dropdown<Pawn, FoodRestriction>(rect1, pawn, (Func<Pawn, FoodRestriction>) (GetCaravanPawnFoodRestriction), new Func<Pawn, IEnumerable<Widgets.DropdownMenuElement<FoodRestriction>>>(this.Button_GenerateMenu), GetCaravanPawnFoodRestriction(pawn).label.Truncate(rect1.width), dragLabel: GetCaravanPawnFoodRestriction(pawn).label, paintable: true);
            float x2 = x1 + (float) width1 + 4f;
            if (Widgets.ButtonText(new Rect(x2, rect.y + 2f, (float) width2, rect.height - 4f), (string) "AssignTabEdit".Translate()))
                Find.WindowStack.Add((Window) new Dialog_ManageFoodRestrictions(GetCaravanPawnFoodRestriction(pawn)));
            float num = x2 + (float) width2;
        }

        private FoodRestriction GetCaravanPawnFoodRestriction(Pawn pawn)
        {
            var caravanAutoFoodRestrictionsData = Find.World.GetComponent<CaravanAutoFoodRestrictionsData>();
            if (caravanAutoFoodRestrictionsData?.RetainedCaravanData == null) return Current.Game.foodRestrictionDatabase.DefaultFoodRestriction();

            caravanAutoFoodRestrictionsData.RetainedCaravanData.TryGetValue(pawn.GetUniqueLoadID(), out var pawnFoodRestrictionLabelSaved);
            var savedFood = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrFallback(restriction => restriction.label == pawnFoodRestrictionLabelSaved, Current.Game.foodRestrictionDatabase.DefaultFoodRestriction());

            return savedFood;
        }

        private void SetCaravanPawnFoodRestriction(Pawn pawn, FoodRestriction foodRestriction)
        {
            var caravanAutoFoodRestrictionsData = Find.World.GetComponent<CaravanAutoFoodRestrictionsData>();
            
            caravanAutoFoodRestrictionsData.RetainedCaravanData[pawn.GetUniqueLoadID()] = foodRestriction.label;
        }
    }
}