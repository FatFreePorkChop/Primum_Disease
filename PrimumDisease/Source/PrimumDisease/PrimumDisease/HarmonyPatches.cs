using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;
using HarmonyLib;

using Verse.AI;
using RimWorld.Planet;

using Unity;
using UnityEngine;

namespace PrimumDisease
{
    //Patch all in harmony instance
    public static class HarmonyPatches
    {
        [HarmonyPatch(typeof(ThingDefGenerator_Meat)), HarmonyPatch("ImpliedMeatDefs")]
        public static class PrimumDisease_ThingDefGenerator_Meat_ImpliedMeatDefs_Patch
        {
            public static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> __result)
            {
                foreach (var meatDef in __result)
                {
                    meatDef.comps.Add(new PD_CompProperties_FoodPathogens());
                    yield return meatDef;
                }
            }
        }
        [HarmonyPatch(typeof(ThingDefGenerator_Corpses)), HarmonyPatch("ImpliedCorpseDefs")]
        public static class PrimumDisease_ThingDefGenerator_Corpses_ImpliedCorpseDefs_Patch
        {
            public static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> __result)
            {
                foreach (var corpseDef in __result)
                {
                    corpseDef.comps.Add(new PD_CompProperties_FoodPathogens());
                    yield return corpseDef;
                }
            }
        }

        [HarmonyPatch(typeof(Pawn)), HarmonyPatch("MakeCorpse")]
        public static class PrimumDisease_Pawn_MakeCorpse_Patch
        {
            public static void Postfix(ref Corpse __result)
            {
                PD_CompFoodPathogens comp = __result.TryGetComp<PD_CompFoodPathogens>();
                if(comp != null)
                {
                    comp.AddPathogensFromCorpse(__result);
                }
            }
        }

        [HarmonyPatch(typeof(GenRecipe), "MakeRecipeProducts")]
        public static class PrimumDisease_GenRecipe_MakeRecipeProducts_Patch
        {
            public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient, IBillGiver billGiver, Precept_ThingStyle precept = null)
            {
                foreach(Thing organicProduct in __result)
                {
                    PD_CompFoodPathogens comp = organicProduct.TryGetComp<PD_CompFoodPathogens>();
                    if (comp != null)
                    {
                        comp.PropogatePathogensFromCookedIngredients(ingredients); //foodPathogens = PD_Utility.GetIngestiveHediffDefs(__instance);
                        comp.AddPathogensFromWorker(worker);
                        if (dominantIngredient.def.IsCorpse)
                        {
                            comp.AddPathogensFromCorpse((Corpse)dominantIngredient);
                        }
                    }
                    yield return organicProduct;
                }
            }
        }





        [HarmonyPatch(typeof(Pawn_InteractionsTracker), "AddInteractionThought")]
        public static class PrimumDisease_Pawn_InteractionsTracker_AddInteractionThought_Patch
        {
            public static void Postfix(Pawn pawn, Pawn otherPawn, ThoughtDef thoughtDef)
            {
                if(Rand.Chance(Internal_DefOf.TransmissionMultiplierChance_Interaction))
                    PD_Utility.TransmitBetweenPawns(pawn, otherPawn, Internal_DefOf.PD_Memetic);
            }
        }

        /*[HarmonyPatch(typeof(MemoryThoughtHandler), "TryGainMemory")] //BADBADBAD using memories has not worked previously, and is unlikely to work now.
        public static class PrimumDisease_MemoryThoughtHandler_TryGainMemory_Patch
        {
            public static void Postfix(MemoryThoughtHandler __instance, ThoughtDef def, Pawn otherPawn)
            {
                if(def == ThoughtDefOf.GotSomeLovin && Rand.Chance(Internal_DefOf.TransmissionMultiplierChance_Interaction))
                {
                    PD_Utility.TransmitBetweenPawns(__instance.pawn, otherPawn, Internal_DefOf.PD_Venereal);
                }
            }
        }*/


        [HarmonyPatch(typeof(Pawn_MindState), "Notify_DamageTaken")]
        public static class PrimumDisease_Pawn_MindState_Notify_DamageTaken_Patch
        {
            public static void Prefix(Pawn_MindState __instance, ref DamageInfo dinfo)
            {
                if (dinfo.Def.ExternalViolenceFor(__instance.pawn))
                {
                    if (__instance.pawn.Spawned)
                    {
                        Pawn attackingPawn = dinfo.Instigator as Pawn;
                        if(Rand.Chance(Internal_DefOf.TransmissionMultiplierChance_Combat) && attackingPawn != null && (dinfo.Def == DamageDefOf.Bite || dinfo.Def == DamageDefOf.Scratch || dinfo.Def == DamageDefOf.Blunt)) //Presumably, this translates to punching?
                        {
                            PD_Utility.TransmitBetweenPawns(attackingPawn,__instance.pawn,Internal_DefOf.PD_Contact);
                        }
                    }
                }
            }
        }
    }
}
