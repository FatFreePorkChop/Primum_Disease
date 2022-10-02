using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;
using Unity;
using UnityEngine;

namespace PrimumDisease
{
    [StaticConstructorOnStartup]
    public class PD_CompFoodPathogens : ThingComp
    {
        public List<PD_FoodPathogen> foodPathogens = new List<PD_FoodPathogen>();
        public PD_CompProperties_FoodPathogens PropsFoodPathogens
        {
            get
            {
                return (PD_CompProperties_FoodPathogens)this.props;
            }
        }
        public void PropogatePathogensFromCookedIngredients(List<Thing> ingredients)
        {
            foreach(Thing ingredient in ingredients)
            {
                PD_CompFoodPathogens other = ingredient.TryGetComp<PD_CompFoodPathogens>();
                if(other != null)
                {
                    foreach(PD_FoodPathogen otherPathogen in other.foodPathogens)
                    {
                        PD_TransmissionExtension extension = otherPathogen.def.HasModExtension<PD_TransmissionExtension>() ? otherPathogen.def.GetModExtension<PD_TransmissionExtension>() : null;
                        if (extension != null && Rand.Chance(extension.survivalOnCooked))
                        {
                            this.TryAddPathogen(otherPathogen);
                        }
                    }
                }
            }
        }
        public void AddPathogensFromCorpse(Corpse corpse)
        {
            Pawn pawn = corpse.InnerPawn;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                PD_TransmissionExtension extension = hediff.def.HasModExtension<PD_TransmissionExtension>() ? hediff.def.GetModExtension<PD_TransmissionExtension>() : null;
                if (extension != null && extension.transmissionMethods.Contains(Internal_DefOf.PD_FoodBorne) && Rand.Chance(extension.transmissivity))
                {
                    PD_FoodPathogen newFoodPathogen = new PD_FoodPathogen()
                    {
                        def = hediff.def
                    };
                    this.TryAddPathogen(newFoodPathogen);
                }
            }
        }
        public void AddPathogensFromWorker(Pawn worker)
        {
            foreach (Hediff hediff in worker.health.hediffSet.hediffs)
            {
                PD_TransmissionExtension extension = hediff.def.HasModExtension<PD_TransmissionExtension>() ? hediff.def.GetModExtension<PD_TransmissionExtension>() : null;
                if (extension != null && extension.transmissionMethods.Contains(Internal_DefOf.PD_Contact) && Rand.Chance(extension.transmissivity * PD_Utility.ContactFoodPoisoningChance(worker)))
                {
                    PD_FoodPathogen newFoodPathogen = new PD_FoodPathogen()
                    {
                        def = hediff.def
                    };
                    this.TryAddPathogen(newFoodPathogen);
                }
            }
        }

        public void TryAddPathogen(PD_FoodPathogen otherPathogen, float otherPenetrationWeight = -1)
        {
            bool hasPathogen = false;
            foreach(PD_FoodPathogen selfPathogen in this.foodPathogens)
            {
                if(otherPathogen.def == selfPathogen.def)
                {
                    hasPathogen = true;
                    if (otherPenetrationWeight < 0)
                    {
                        selfPathogen.penetration = Mathf.Max(selfPathogen.penetration, otherPathogen.penetration);
                    }
                    else
                    {
                        selfPathogen.penetration = selfPathogen.penetration * (1 - otherPenetrationWeight) + otherPathogen.penetration * otherPenetrationWeight;
                    }
                }
            }
            if (!hasPathogen)
            {
                if (otherPenetrationWeight >= 0)
                {
                    otherPathogen.penetration = 0 * (1 - otherPenetrationWeight) + otherPathogen.penetration * otherPenetrationWeight;
                }
                this.foodPathogens.Add(otherPathogen);
            }
        }   

        public override void PostSplitOff(Thing piece)
        {
            ((ThingWithComps)piece).GetComp<PD_CompFoodPathogens>().foodPathogens = this.foodPathogens.ListFullCopy();
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            base.PreAbsorbStack(otherStack, count);
            PD_CompFoodPathogens compFoodPathogens = otherStack.TryGetComp<PD_CompFoodPathogens>();
            if (compFoodPathogens != null)
            {
                foreach (PD_FoodPathogen otherPathogen in compFoodPathogens.foodPathogens)
                {
                    otherPathogen.penetration = GenMath.WeightedAverage(0, (float)this.parent.stackCount, otherPathogen.penetration, (float)count);
                    this.TryAddPathogen(otherPathogen, ((float)count) / ((float)this.parent.stackCount + count)); //BADBADBAD
                }
            }
        }
        public override void PrePostIngested(Pawn ingester)
        {
            foreach (PD_FoodPathogen foodPathogen in this.foodPathogens)
            {
                if (Rand.Chance(foodPathogen.penetration * FoodUtility.GetFoodPoisonChanceFactor(ingester)))
                {
                    Hediff hediff = ingester.health.hediffSet.GetFirstHediffOfDef(foodPathogen.def);
                    if (hediff == null && foodPathogen.canGiveHediff)
                    {
                        hediff = PD_Utility.TryGivePawnDisease(ingester, foodPathogen.def);
                    }
                    if (hediff != null && foodPathogen.canChangeSeverity)
                    {
                        hediff.Severity = Mathf.Clamp(hediff.Severity + foodPathogen.severityOffset, Mathf.Max(0, foodPathogen.severityMin), Mathf.Min(1, foodPathogen.severityMax));
                    }
                }
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref foodPathogens, "foodPathogens", LookMode.Deep);
        }
    }
    public class PD_CompProperties_FoodPathogens : CompProperties
    {
        public PD_CompProperties_FoodPathogens()
        {
            this.compClass = typeof(PD_CompFoodPathogens);
        }
    }
}
