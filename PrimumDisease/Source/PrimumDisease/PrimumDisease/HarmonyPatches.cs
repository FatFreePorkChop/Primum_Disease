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

        [HarmonyPatch(typeof(MemoryThoughtHandler), "TryGainMemory")] //BADBADBAD using memories has not worked previously, and is unlikely to work now.
        public static class PrimumDisease_MemoryThoughtHandler_TryGainMemory_Patch
        {
            public static void Postfix(MemoryThoughtHandler __instance, ThoughtDef def, Pawn otherPawn)
            {
                if(def == ThoughtDefOf.GotSomeLovin && Rand.Chance(Internal_DefOf.TransmissionMultiplierChance_Interaction))
                {
                    PD_Utility.TransmitBetweenPawns(__instance.pawn, otherPawn, Internal_DefOf.PD_Venereal);
                }
            }
        }


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






        [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")] //BADBADBAD, dunno if I can modify a protected function
        public static class PrimumDisease_Recipe_Surgery_CheckSurgeryFail_Patch
        {
            public static bool Prefix(ref bool __result, Recipe_Surgery __instance, Pawn surgeon, Pawn patient, List<Thing> ingredients, BodyPartRecord part, Bill bill)
            {
                if (bill.recipe.surgerySuccessChanceFactor >= 99999f)
                {
                    return false;
                }


                float BC = (!__instance.recipe.surgeryIgnoreEnvironment && patient.InBed()) ? patient.CurrentBed().GetStatValue(StatDefOf.SurgerySuccessChanceFactor, true) : 1;
                float D = !patient.RaceProps.IsMechanoid ? surgeon.GetStatValue(StatDefOf.MedicalSurgerySuccessChance, true) : 1;
                D *= __instance.recipe.surgerySuccessChanceFactor; //BAD?
                if (surgeon.InspirationDef == InspirationDefOf.Inspired_Surgery && !patient.RaceProps.IsMechanoid)
                {
                    D *= 2f;
                    surgeon.mindState.inspirationHandler.EndInspiration(InspirationDefOf.Inspired_Surgery);
                }
                float M = MedicineMedicalPotencyToSurgeryChanceFactor.Evaluate(GetAverageMedicalPotency(ingredients, bill));
                float E = Mathf.Min(1, 0.4f + 0.6f*(surgeon.skills.GetSkill(SkillDefOf.Medicine).levelInt -     (__instance.recipe.skillRequirements.Count>=1 ? __instance.recipe.skillRequirements.First<SkillRequirement>().minLevel:0)    )/5);

                float num = Mathf.Pow(BC*M * D * D * E * E, 1 / 2);
                num = Mathf.Min(num, 0.95f + (num - 0.95f) / 5);


                if (!Rand.Chance(num))
                {
                    if (Rand.Chance(__instance.recipe.deathOnFailedSurgeryChance))
                    {
                        HealthUtility.GiveInjuriesOperationFailureCatastrophic(patient, part);
                        if (!patient.Dead)
                        {
                            patient.Kill(null, null);
                        }
                        Find.LetterStack.ReceiveLetter("LetterLabelSurgeryFailed".Translate(patient.Named("PATIENT")), "MessageMedicalOperationFailureFatal".Translate(surgeon.LabelShort, patient.LabelShort, __instance.recipe.LabelCap, surgeon.Named("SURGEON"), patient.Named("PATIENT")), LetterDefOf.NegativeEvent, patient, null, null, null, null);
                    }
                    else if (Rand.Chance(0.5f))
                    {
                        if (Rand.Chance(0.1f))
                        {
                            Find.LetterStack.ReceiveLetter("LetterLabelSurgeryFailed".Translate(patient.Named("PATIENT")), "MessageMedicalOperationFailureRidiculous".Translate(surgeon.LabelShort, patient.LabelShort, surgeon.Named("SURGEON"), patient.Named("PATIENT"), __instance.recipe.Named("RECIPE")), LetterDefOf.NegativeEvent, patient, null, null, null, null);
                            HealthUtility.GiveInjuriesOperationFailureRidiculous(patient);
                        }
                        else
                        {
                            Find.LetterStack.ReceiveLetter("LetterLabelSurgeryFailed".Translate(patient.Named("PATIENT")), "MessageMedicalOperationFailureCatastrophic".Translate(surgeon.LabelShort, patient.LabelShort, surgeon.Named("SURGEON"), patient.Named("PATIENT"), __instance.recipe.Named("RECIPE")), LetterDefOf.NegativeEvent, patient, null, null, null, null);
                            HealthUtility.GiveInjuriesOperationFailureCatastrophic(patient, part);
                        }
                    }
                    else
                    {
                        Find.LetterStack.ReceiveLetter("LetterLabelSurgeryFailed".Translate(patient.Named("PATIENT")), "MessageMedicalOperationFailureMinor".Translate(surgeon.LabelShort, patient.LabelShort, surgeon.Named("SURGEON"), patient.Named("PATIENT"), __instance.recipe.Named("RECIPE")), LetterDefOf.NegativeEvent, patient, null, null, null, null);
                        HealthUtility.GiveInjuriesOperationFailureMinor(patient, part);
                    }
                    if (!patient.Dead)
                    {
                        TryGainBotchedSurgeryThought(patient, surgeon);
                    }
                    __result = true;
                }
                __result = false;


                return false;
            }
            private static void TryGainBotchedSurgeryThought(Pawn patient, Pawn surgeon)
            {
                if (!patient.RaceProps.Humanlike || patient.needs.mood == null)
                {
                    return;
                }
                patient.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.BotchedMySurgery, surgeon, null);
            }

            private static float GetAverageMedicalPotency(List<Thing> ingredients, Bill bill)
            {
                Bill_Medical bill_Medical = bill as Bill_Medical;
                ThingDef thingDef;
                if (bill_Medical != null)
                {
                    thingDef = bill_Medical.consumedInitialMedicineDef;
                }
                else
                {
                    thingDef = null;
                }
                int num = 0;
                float num2 = 0f;
                if (thingDef != null)
                {
                    num++;
                    num2 += thingDef.GetStatValueAbstract(StatDefOf.MedicalPotency, null);
                }
                for (int i = 0; i < ingredients.Count; i++)
                {
                    Medicine medicine = ingredients[i] as Medicine;
                    if (medicine != null)
                    {
                        num += medicine.stackCount;
                        num2 += medicine.GetStatValue(StatDefOf.MedicalPotency, true) * (float)medicine.stackCount;
                    }
                }
                if (num == 0)
                {
                    return 1f;
                }
                return num2 / (float)num;
            }

            private static readonly SimpleCurve MedicineMedicalPotencyToSurgeryChanceFactor = new SimpleCurve
        {
            {
                new CurvePoint(0f, 0.7f),
                true
            },
            {
                new CurvePoint(1f, 1f),
                true
            },
            {
                new CurvePoint(2f, 1.3f),
                true
            }
        };
        }
    }
}
