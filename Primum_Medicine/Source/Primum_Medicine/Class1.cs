using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Unity;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

//This system is designed to find all implants, possibly excluding basic implants like peg legs, and suppress the immune system accordingly.
//Archotech implants (starting with "Archotech") will not increase this counter, but do increment another hediff, archotech influence, that makes pawns more likely to go mad. Possibly other effects?
//Maybe some drug/disease/condition should prvent rejections?
//Also adjusts capacity calculations to make capacities more meaningful.

namespace Primum_Medicine
{
    [StaticConstructorOnStartup]
    [DefOf]


    public static class Primum_DefOf
    {
        public static HediffDef PM_Immunosuppression;
        public static HediffDef PM_ArchotechInfluence;
        public static HediffDef PM_CreutzfeldtRimDisease;
        public static ThingDef PM_CRD_Meat_Human;
    }

    public class PM_Hediff_ImplantCounter : HediffWithComps
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            TryChangeSeverity();
        }
        public override void PostTick()
        {
            base.PostTick();
            if (Find.TickManager.TicksGame % 600 == 0)
            {
                TryChangeSeverity();
            }
        }
        public void TryChangeSeverity()
        {
            this.severityInt = 0.05f * HarmonyPatches.CountImplantsOfType(pawn, this.Label);
        }
        public override bool ShouldRemove => this.severityInt == 0;// HarmonyPatches.CountImplantsOfType(pawn, this.Label) == 0;
    }

    public class WorldComponent_Test : WorldComponent
    {
        public int tickCounter;


        public WorldComponent_Test(World world) : base(world)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();



        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            //Log.Message("Yo, this do anything at all?");
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }


    public static class HarmonyPatches
    {
        //Predefined functions
        public static int CountImplantsOfType(this Pawn pawn, string implantType) //implantType can be "archotech", "bionic", or nothing
        {
            var ImplantCount = 0;
            if (!pawn.Dead && pawn.health?.hediffSet != null)
            {
                var Hediffs = pawn.health.hediffSet.hediffs;
                for (int i = 0; i < Hediffs.Count; i++)
                {
                    if (Hediffs[i].def.countsAsAddedPartOrImplant)
                    {
                        string HediffLabel = Hediffs[i].Label;
                        string HediffSublabel = HediffLabel.Length >= 5 ? HediffLabel.Substring(0, 5).ToLower() : HediffLabel;
                        if (implantType is null)
                        {
                            ImplantCount++;
                        }
                        else if (implantType == "archotech influence")
                        {
                            if (HediffSublabel == "archo")
                            {
                                ImplantCount++;
                            }
                        }
                        else if (implantType == "immunosuppression")
                        {
                            if (HediffSublabel != "archo" && HediffSublabel != "woode" && HediffSublabel != "simpl" && HediffSublabel != "cochl" && HediffSublabel != "peg l" && HediffSublabel != "prost")
                            {
                                ImplantCount++;
                            }
                        }
                    }
                }
            }
            return ImplantCount;
        }
        public static bool ShouldReceiveArchotechInfluenceHediff(this Pawn pawn)
        {
            return (!pawn.Dead && pawn.health?.hediffSet != null && pawn.health.hediffSet.GetFirstHediffOfDef(Primum_DefOf.PM_ArchotechInfluence) is null && CountImplantsOfType(pawn, "archotech influence") > 0);
        }
        public static bool ShouldReceiveImmunosuppressionHediff(this Pawn pawn)
        {
            return (!pawn.Dead && pawn.health?.hediffSet != null && pawn.health.hediffSet.GetFirstHediffOfDef(Primum_DefOf.PM_Immunosuppression) is null && CountImplantsOfType(pawn, "immunosuppression") > 0);
        }


        static HarmonyPatches()
        {
            new Harmony("Primum_Medicine.Mod").PatchAll();
        }

        /*
        //Immunosuppression/archotech influence
        [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
        public static class Pawn_SpawnSetup_Patch
        {
            public static void Postfix(Pawn __instance)
            {
                Log.Message("Test blah");
                if (__instance.ShouldReceiveImmunosuppressionHediff())
                {
                    __instance.health.AddHediff(HediffMaker.MakeHediff(Primum_DefOf.PM_Immunosuppression, __instance));
                }
                if (__instance.ShouldReceiveArchotechInfluenceHediff())
                {
                    __instance.health.AddHediff(HediffMaker.MakeHediff(Primum_DefOf.PM_ArchotechInfluence, __instance));
                }
            }
        }
        [HarmonyPatch(typeof(Pawn_HealthTracker), "CheckForStateChange")]
        public static class Pawn_CheckForStateChange_Patch
        {
            public static void Postfix(Pawn ___pawn)
            {
                if (___pawn.ShouldReceiveImmunosuppressionHediff())
                {
                    var hediff = HediffMaker.MakeHediff(Primum_DefOf.PM_Immunosuppression, ___pawn);
                    ___pawn.health.AddHediff(hediff);
                }
                if (___pawn.ShouldReceiveArchotechInfluenceHediff())
                {
                    var hediff = HediffMaker.MakeHediff(Primum_DefOf.PM_ArchotechInfluence, ___pawn);
                    ___pawn.health.AddHediff(hediff);
                }
            }
        }
        */


        [HarmonyPatch(typeof(Pawn))]
        [HarmonyPatch("ButcherProducts")]
        //[HarmonyPatch(typeof(Pawn), "ButcherProducts")]
        public static class ButcherProducts_Patch
        {
            /*public static IEnumerable<Thing> Replace<Thing>(IEnumerable<Thing> enumerable, int index, Thing value)
            {
                return enumerable.Select((x, i) => index == i ? value : x);
            }
            public static void Prefix(Pawn __instance, IEnumerable<Thing> __result, Pawn butcher, float efficiency)
            {
                Log.Message("A");
                int index = 0;
                foreach (Thing meat in __result)
                {
                    if (meat.def.IsMeat)
                    {
                        Log.Message("B");
                        ThingDef newMeatDef = Primum_DefOf.PM_CRD_Meat_Human;
                        Log.Message("Is " + __instance.def.defName + " null? " + (newMeatDef == null));
                        if (newMeatDef != null && __instance.def.defName == "Human")//__instance.health.hediffSet.HasHediff(Primum_DefOf.PM_CreutzfeldtRimDisease))
                        {
                            Log.Message("C");
                            Thing newMeat = ThingMaker.MakeThing(newMeatDef, null);
                            newMeat.stackCount = meat.stackCount;
                            Replace<Thing>(__result, index, newMeat);
                        }
                        break;
                    }
                    index++;
                }
            }*/
            [HarmonyPrefix]
            public static void TestThisJunk()
            {
                Log.Message("This thing on?");
            }
        }
    }
}