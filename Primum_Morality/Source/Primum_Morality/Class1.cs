using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Unity;
using UnityEngine;
//using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

//using VFEAncients;

/*
This system is designed to let other factions judge the player for their heinous misdeeds. This is done via karma.
Every action that is morally relevent (so butchering a human but not making a t-shirt) should have a modextension
Morally relevant memes should also have a mod extension. Morally relevent memes have one file devoted to them. Weight is generally proportional to impact
Moral categories are defined separately

Included moral categories: (A = finished, B = partially finished)
A Cannibalism: eating or butchering humans, as well as #human leather objects and buildings (proportional to building size)
A Treachery: Most negative actions, reduced by rescuing members
A Environmentalism: -cutting trees, mining, killing animals, as well as #bonded animal
A Transhumanism: bionic surgery, -organic surgery, -#naturalprosthetics, #nonnatural prosthetics
A? Mysticism: as well as ?presence of various mystic objects, #psycast level, #Gauranlen trees
A? Hedonism: lovin'-nonspouse, eating gourmet or lavish food or desserts, social drug use, hard drug use as well as ?high bedroom quality   
A Peace: raiding, killing innocents, peace talks participation
HumanRights: charity, releasing prisoners or slaves, -organ harvesting, -organ selling -killing prisoners, -enslavement, slave trading, prisoner trading as well as -#slaves
*/

namespace Primum_Morality
{

    [StaticConstructorOnStartup]
    [DefOf]
    public static class Primum_DefOf
    {
        public static int maxKarmaCPY = 35; //Takes ~three years to go from minimum to maximum karma
        public static int minKarmaCPY = -100; //It's much easier to lose a reputation than to gain one
        public static int weighingsPerYear = 3;

        
        public static MoralCategoryDef Cannibalism;
        public static MoralCategoryDef Treachery;
        public static MoralCategoryDef Environmentalism;
        public static MoralCategoryDef Transhumanism;
        public static MoralCategoryDef Mysticism;
        public static MoralCategoryDef Hedonism;
        public static MoralCategoryDef Peace;
        public static MoralCategoryDef HumanRights;


        public static MoralEventDef HumanLeatherApparel;
        public static MoralEventDef HumanLeatherBuildings;

        public static MoralEventDef BondedAnimalCount;

        public static MoralEventDef NonnaturalProstheticCount;
        public static MoralEventDef NaturalProstheticCount;
        public static MoralEventDef LuciferiumAddictionCount;

        public static MoralEventDef PsylinkLevelCount;
        public static MoralEventDef PawnsWithPsylinkCount;
        public static MoralEventDef GauranlenTreeCount;

        public static MoralEventDef SlaveCount;
    }

    //Karma gives a goodwill cap, and changes naturalgoodwill
    public class GoodwillSituationWorker_Karma : GoodwillSituationWorker
    {
        private int GetKarma(Faction faction)
        {
            return Find.World.GetComponent<WorldComponent_WeighMorality>().GetKarma(faction);
        }
        public override string GetPostProcessedLabel(Faction other)
        {
            return this.def.label.Formatted(Faction.OfPlayer.ideos.PrimaryIdeo);
        }
        public override int GetNaturalGoodwillOffset(Faction other)
        {
            return GetKarma(other)*2 - 100;
        }
        public override int GetMaxGoodwill(Faction other)
        {
            return (int)(3.2 * GetKarma(other) - 80);
        }
    }


    public class MoralCategoryDef : Def
    {
        public string negativeLabel = null;
        public float positiveMagnitude;
        public float negativeMagnitude;
        public int relevance = 0;
        public string defaultState = "Negative";
    }
    public class MoralEventDef : Def //In order to assign weights to things like the number of human leather apparel or buildings in the colony
    {
        public MoralCategoryDef category;
        public float weight;
        public bool scaleWeightWithColonists = true; //Should be true for things that apply to one colonist at a time--eg. wearing a human-leather shirt, false for things that are global--like memes
        public bool scaleWeightWithTime = false; //Should be true for things that are temporary, eg. butchering a human; false for things that are persistent, like human-leather armchairs
    }

    public class HistoryEventDef_ModExtension : DefModExtension
    {
        public MoralCategoryDef category;
        public float weight;
        public bool scaleWeightWithColonists = true;
        public bool scaleWeightWithTime = true;
    }
    public class ThoughtDef_ModExtension : DefModExtension
    {
        public MoralCategoryDef category;
        public float weight;
        public bool scaleWeightWithColonists = true;
        public bool scaleWeightWithTime = true;
    }
    public class MemeDef_ModExtension : DefModExtension
    {
        public MoralCategoryDef category;
        public float weight = 1;
        public bool scaleWeightWithColonists = false;
        public bool scaleWeightWithTime = false; 
        public string overrideState = null; //Positive, Negative, Neutral, or Silent. Null defaults to by-weight calculation
    }

    public class WorldComponent_WeighMorality : WorldComponent
    {
        public int tickCounter;
        public int ticksToNextWeighing = 60000 * 60 / Primum_DefOf.weighingsPerYear;



        public Dictionary<Faction, int> karmaByFaction = new Dictionary<Faction, int>();
        public List<Faction> karmaByFaction_FactionScribe;
        public List<int> karmaByFaction_intScribe;
        public Dictionary<MoralCategoryDef, float> weightDictionary = new Dictionary<MoralCategoryDef, float>();
        public List<MoralCategoryDef> weightDictionary_MoralCategoryDefScribe;
        public List<float> weightDictionary_floatScribe;

        public string HediffProstheticClass(HediffDef hediffDef) //Natural, Nonnatural, archotech
        {
            string hediffDefSublabel = hediffDef.label.Length >= 5 ? hediffDef.label.Substring(0, 5).ToLower() : hediffDef.label;
            if (hediffDef.countsAsAddedPartOrImplant)
            {
                if (hediffDefSublabel == "archo")
                {
                    return "Archotech";
                }
                //HediffSublabel != "woode" && HediffSublabel != "simpl" && HediffSublabel != "cochl" && HediffSublabel != "peg l" && HediffSublabel != "prost"
                if (hediffDefSublabel == "woode" || hediffDefSublabel == "peg l" ||
                    hediffDef.spawnThingOnRemoved?.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("AA_ImplantCategory")) == true ||
                    hediffDef.spawnThingOnRemoved?.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("VFEI_BodyPartsInsect")) == true ||
                    hediffDef.spawnThingOnRemoved?.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("GR_ImplantCategory")) == true)
                {
                    return "Natural";
                }
                return "Nonnatural";
            }
            return null;
        }
        public int GetKarma(Faction faction)
        {
            return karmaByFaction.ContainsKey(faction) ? karmaByFaction[faction] : 50;
        }
        public float AddWeight(MoralCategoryDef category, float baseWeight, bool scaleWeightWithColonists = false, bool scaleWeightWithTime = true)
        {
            float weight = baseWeight;
            if (scaleWeightWithColonists)
            {
                weight /= PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction_NoCryptosleep.Count;
            }
            if (scaleWeightWithTime)
            {
                weight *= Primum_DefOf.weighingsPerYear;
            }
            weightDictionary[category] = weight + (weightDictionary.ContainsKey(category) ? weightDictionary[category] : 0);
            return weight;
        }

        public WorldComponent_WeighMorality(World world) : base(world)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            tickCounter++;
            if (tickCounter >= ticksToNextWeighing)
            {
                tickCounter = 0;
                Dictionary<Faction, Dictionary<MoralCategoryDef, float>> goalKarmaOffsetDictionary = new Dictionary<Faction, Dictionary<MoralCategoryDef, float>> { };


                //What to do:
                //1. Assign weights to weightDictionary according to player's memes and currrent state of the world (eg. number of human leather items worn by colonists)
                Log.Message("Moral weighing: AssignWeights");
                AssignWeights();
                //2. Convert weightDictionary to categoryKarmaPairsDictionary according to each factions' ideology
                Log.Message("Moral weighing: ConvertWeightsToKarma");
                ConvertWeightsToKarma();
                //3. Adjust each factions karma with the player according to categoryKarmaPairsDictionary
                Log.Message("Moral weighing: AdjustFactionKarma");
                AdjustFactionKarma();
                //BADBADBAD 4. Inform the player of any changes via letters or similar
                weightDictionary.Clear(); //Reset everything


                void AssignWeights_Memes()
                {
                    Faction playerFaction = Find.FactionManager.OfPlayer;
                    foreach (MemeDef meme in playerFaction.ideos.PrimaryIdeo.memes)
                    //Ideos.PrimaryIdeo.memes)
                    {
                        if (meme.HasModExtension<MemeDef_ModExtension>())
                        {
                            MemeDef_ModExtension extension = meme.GetModExtension<MemeDef_ModExtension>();
                            AddWeight(extension.category, extension.weight * (Mathf.Max(((float)meme.impact), 1f) / 3f), extension.scaleWeightWithColonists,extension.scaleWeightWithTime);
                        }
                    }
                }
                void AssignWeights_Cannibalism()
                {
                    Faction playerFaction = Find.FactionManager.OfPlayer;
                    List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction;
                    MoralEventDef moralEvent;

                    int count = 0;
                    foreach (Pawn pawn in pawns)
                    {
                        if (pawn.apparel != null)
                        {
                            foreach (Apparel wornItem in (pawn.apparel?.WornApparel))
                            {
                                count += wornItem.Stuff == ThingDefOf.Human.race.leatherDef ? 1 : 0;
                            }
                        }
                    }
                    moralEvent = Primum_DefOf.HumanLeatherApparel;
                    AddWeight(moralEvent.category, count*moralEvent.weight,moralEvent.scaleWeightWithColonists,moralEvent.scaleWeightWithTime);

                    count = 0;
                    Map map = Find.AnyPlayerHomeMap;
                    foreach (Building building in map.listerBuildings.allBuildingsColonist)
                    {
                        if (building.Stuff == ThingDefOf.Human.race.leatherDef)
                        {
                            count += building.def.size.Area;
                        }
                    }
                    moralEvent = Primum_DefOf.HumanLeatherBuildings;
                    AddWeight(moralEvent.category, count * moralEvent.weight, moralEvent.scaleWeightWithColonists,moralEvent.scaleWeightWithTime);
                }
                void AssignWeights_Environmentalism()
                {
                    Faction playerFaction = Find.FactionManager.OfPlayer;
                    List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction;
                    MoralEventDef moralEvent;


                    int countBondedAnimals = 0;
                    foreach (Pawn pet in pawns)
                    {
                        countBondedAnimals += TrainableUtility.GetAllColonistBondsFor(pet).Count() > 0 ? 1 : 0;
                    }
                    moralEvent = Primum_DefOf.BondedAnimalCount;
                    AddWeight(moralEvent.category, countBondedAnimals * moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);
                }
                void AssignWeights_Transhumanism()
                {
                    Faction playerFaction = Find.FactionManager.OfPlayer;
                    List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction;
                    MoralEventDef moralEvent;


                    int countNonnaturalProsthetics = 0;
                    int countNaturalProsthetics = 0;
                    int countLuciferiumAddictions = 0;
                    foreach (Pawn pawn in pawns)
                    {
                        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                        {
                            if (hediff.def.countsAsAddedPartOrImplant)
                            {
                                string implantType = HediffProstheticClass(hediff.def);
                                countNonnaturalProsthetics+= implantType=="Nonnatural" ? 1 : 0;
                                countNaturalProsthetics += implantType == "Natural" ? 1 : 0;
                            }
                            countLuciferiumAddictions += hediff.def.defName == "LuciferiumAddiction" ? 1 : 0;
                        }
                    }
                    moralEvent = Primum_DefOf.NaturalProstheticCount;
                    AddWeight(moralEvent.category, countNaturalProsthetics * moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);
                    moralEvent = Primum_DefOf.NonnaturalProstheticCount;
                    AddWeight(moralEvent.category, countNonnaturalProsthetics * moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);
                    moralEvent = Primum_DefOf.LuciferiumAddictionCount;
                    AddWeight(moralEvent.category, countLuciferiumAddictions * moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);
                }

                void AssignWeights_Mysticism()
                {
                    Faction playerFaction = Find.FactionManager.OfPlayer;
                    List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction;
                    MoralEventDef moralEvent;

                    int countPawnsWithPsylink = 0; //To be addded
                    float countPsylinkLevels = 0;
                    foreach (Pawn pawn in pawns)
                    {
                        if (pawn.HasPsylink)
                        {
                            countPawnsWithPsylink++;
                            countPsylinkLevels += pawn.GetPsylinkLevel();
                        }
                    }
                    moralEvent = Primum_DefOf.PsylinkLevelCount;
                    AddWeight(moralEvent.category, countPsylinkLevels * moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);
                    moralEvent = Primum_DefOf.PawnsWithPsylinkCount;
                    AddWeight(moralEvent.category, countPawnsWithPsylink* moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);

                    int gauranlenTreeCount = 0;
                    Map map = Find.AnyPlayerHomeMap;
                    foreach (Thing thing in map.listerThings.AllThings)
                    {
                        if (thing.def == DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeGauranlen"))
                        {
                            gauranlenTreeCount++;
                        }
                    }
                    moralEvent = Primum_DefOf.GauranlenTreeCount;
                    AddWeight(moralEvent.category, gauranlenTreeCount * moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);
                }

                void AssignWeights_HumanRights()
                {
                    Faction playerFaction = Find.FactionManager.OfPlayer;
                    List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction;
                    MoralEventDef moralEvent;

                    int slaveCount = 0;
                    foreach(Pawn pawn in pawns)
                    {
                        slaveCount+=(pawn.IsSlaveOfColony ? 1 : 0);
                    }
                    moralEvent = Primum_DefOf.SlaveCount;
                    AddWeight(moralEvent.category, slaveCount * moralEvent.weight, moralEvent.scaleWeightWithColonists, moralEvent.scaleWeightWithTime);
                }


                void AssignWeights()
                {
                    AssignWeights_Memes();
                    AssignWeights_Cannibalism();
                    AssignWeights_Environmentalism();
                    AssignWeights_Transhumanism();
                    AssignWeights_Mysticism();
                        
                    AssignWeights_HumanRights();
                }
                void ConvertWeightsToKarma()
                {
                    foreach (Faction faction in Find.FactionManager.AllFactions)
                    {
                        if (faction != Find.FactionManager.OfPlayer && !faction.def.permanentEnemy && faction.HasGoodwill)
                        {
                            goalKarmaOffsetDictionary[faction] = new Dictionary<MoralCategoryDef, float> { };
                            foreach (KeyValuePair<MoralCategoryDef, float> pair in weightDictionary)
                            {
                                MoralCategoryDef category = pair.Key;
                                float weight = pair.Value;

                                string state = category.defaultState;
                                int overrideImpact = -1;
                                foreach (MemeDef meme in faction.ideos.PrimaryIdeo.memes)
                                {
                                    if (meme.HasModExtension<MemeDef_ModExtension>())
                                    {
                                        MemeDef_ModExtension extension = meme.GetModExtension<MemeDef_ModExtension>();
                                        if(extension.category == category)
                                        {
                                            string overrideState = 
                                                extension.overrideState == null ? 
                                                (extension.weight <= 0 ? "Negative" : "Positive") :
                                                extension.overrideState;
                                            if (meme.impact > overrideImpact && overrideState != null && overrideState != "Silent")
                                            {
                                                state = overrideState;
                                                overrideImpact = meme.impact;
                                            }
                                        }
                                    }
                                }
                                weight *= state == "Positive" ? category.positiveMagnitude : (state == "Negative" ? -category.negativeMagnitude : 0);
                                goalKarmaOffsetDictionary[faction][category] = weight;
                            }
                        }
                    }
                }
                void AdjustFactionKarma()
                {
                    foreach (Faction faction in Find.FactionManager.AllFactions)
                    {
                        if (faction != Find.FactionManager.OfPlayer && !faction.def.permanentEnemy && faction.HasGoodwill)
                        {
                            karmaByFaction[faction] = karmaByFaction.ContainsKey(faction) ? karmaByFaction[faction] : 50;
                        }
                    }

                    (Faction, MoralCategoryDef, float, int) largestKarmicChange = (null, null, 0, 0);
                    foreach (KeyValuePair<Faction, Dictionary<MoralCategoryDef, float>> pair in goalKarmaOffsetDictionary)
                    {
                        //(MoralCategoryDef, float) maxLocalChange = (new MoralCategoryDef(), 0);
                        (Faction, MoralCategoryDef, float) localLargestKarmicChange = (largestKarmicChange.Item1, largestKarmicChange.Item2, largestKarmicChange.Item3);
                        float goalKarma = 50;
                        foreach (KeyValuePair<MoralCategoryDef, float> pair2 in pair.Value)
                        {
                            if (Mathf.Abs(pair2.Value) > Mathf.Abs(localLargestKarmicChange.Item3))
                            {
                                localLargestKarmicChange = (pair.Key, pair2.Key, pair2.Value);
                            }
                            goalKarma += pair2.Value;
                        }
                        int startKarma = karmaByFaction[pair.Key];
                        karmaByFaction[pair.Key] = (int)Mathf.Clamp(startKarma + Mathf.Clamp(goalKarma-startKarma, Primum_DefOf.minKarmaCPY / Primum_DefOf.weighingsPerYear, Primum_DefOf.maxKarmaCPY / Primum_DefOf.weighingsPerYear), 0, 100);
                        if (Mathf.Abs(karmaByFaction[pair.Key] - startKarma) >= 12f/Primum_DefOf.weighingsPerYear)
                        {
                            largestKarmicChange = (localLargestKarmicChange.Item1, localLargestKarmicChange.Item2, localLargestKarmicChange.Item3, karmaByFaction[pair.Key] - startKarma);
                        }
                    }

                    TaggedString text;
                    if (Mathf.Abs(largestKarmicChange.Item3) > 0) 
                    {
                        text = "Your karma has been adjusted... your karma with " + largestKarmicChange.Item1.Name + " has ";
                        if(largestKarmicChange.Item4 == 0)
                        {
                            text += "not changed, despite your";
                        } 
                        else
                        {
                            text += (largestKarmicChange.Item4 > 0 ? "increased " : "decreased") + " by " + (int)Mathf.Abs(largestKarmicChange.Item3);
                            text += (largestKarmicChange.Item3 * largestKarmicChange.Item4 > 0 ? " due to your " : " despite your ");
                        }
                        //text += (weightDictionary[largestKarmicChange.Item2] > 0 ? "pursuit of " : "rejection of ");
                        if (weightDictionary[largestKarmicChange.Item2] > 0)
                        {
                            text += largestKarmicChange.Item2.label;
                        }
                        else
                        {
                            text += largestKarmicChange.Item2.negativeLabel != null ? largestKarmicChange.Item2.negativeLabel : ("rejection of " + largestKarmicChange.Item2.label);
                        }
                    } 
                    else
                    {
                        text = "Your karma has been adjusted... nothing changed much!";
                    }
                    Find.LetterStack.ReceiveLetter("Moral weighing", text, LetterDefOf.NeutralEvent, null, null, null, null, null);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.tickCounter, nameof(this.tickCounter));
            Scribe_Values.Look(ref this.ticksToNextWeighing, nameof(this.ticksToNextWeighing));
            Scribe_Collections.Look(ref this.karmaByFaction, nameof(this.karmaByFaction), LookMode.Reference,LookMode.Value, ref karmaByFaction_FactionScribe, ref karmaByFaction_intScribe);
            Scribe_Collections.Look(ref this.weightDictionary, nameof(this.weightDictionary), LookMode.Reference, LookMode.Value, ref weightDictionary_MoralCategoryDefScribe, ref weightDictionary_floatScribe);
        }
    }


    public static class HarmonyPatches
    {

        public static int GetKarma(Faction faction)
        {
            return Find.World.GetComponent<WorldComponent_WeighMorality>().GetKarma(faction);
        }
        static HarmonyPatches()
        {
            new Harmony("Primum_Morality.Mod").PatchAll();
        }

        
        //HistoryEvents with the appropriate mod extension affect moral weights
        [HarmonyPatch(typeof(IdeoUtility), "Notify_HistoryEvent")]
        public static class Notify_HistoryEvent_Patch
        {
            public static void Postfix(HistoryEvent ev, bool canApplySelfTookThoughts = true)
            {
                if (ev.def.HasModExtension<HistoryEventDef_ModExtension>())
                {
                    HistoryEventDef_ModExtension extension = ev.def.GetModExtension<HistoryEventDef_ModExtension>();
                    Faction faction;

                    if (extension.category == Primum_DefOf.Treachery && ev.args.TryGetArg<Faction>(HistoryEventArgsNames.AffectedFaction, out faction)) 
                    {
                        if (faction.AllyOrNeutralTo(Faction.OfPlayer)) //Can't betray an enemy
                        {
                            float weight = extension.weight;
                            if(faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Neutral)  //Betrayinig allies is far worse than betraying those with whom you're neutral
                            {
                                weight /= 3;
                                Find.World.GetComponent<WorldComponent_WeighMorality>().AddWeight(extension.category, weight, extension.scaleWeightWithColonists, extension.scaleWeightWithTime);
                            }
                        }
                    }
                    else
                    {
                        Find.World.GetComponent<WorldComponent_WeighMorality>().AddWeight(extension.category, extension.weight, extension.scaleWeightWithColonists, extension.scaleWeightWithTime);
                    }
                }
            }
        }
        
        /*//ThoughtDefs with the appropriate mod extension affect moral weights
        [HarmonyPatch(typeof(MemoryThoughtHandler), "TryGainMemory")]
        public static class TryGainMemory_Patch
        {
            public static void Prefix(ThoughtDef thoughtDef)
            {
                if (thoughtDef.HasModExtension<ThoughtDef_ModExtension>())
                {
                    ThoughtDef_ModExtension extension = thoughtDef.GetModExtension<ThoughtDef_ModExtension>();
                    Find.World.GetComponent<WorldComponent_WeighMorality>()?.AddWeight(extension.category, extension.weight, extension.scaleWeightWithColonists, extension.scaleWeightWithTime);
                }  
            }
        }*/
        


        //Karma shouuld be displayed on the factions HUD just below natural goodwill.
        [HarmonyPatch(typeof(FactionUIUtility), "DrawFactionRow")]
        public static class DrawFactionRow_Patch
        {
            public static void Prefix(Faction faction, float rowY, Rect fillRect)
            {
                Log.Message("Yo, what.");
                float num = fillRect.width - 250f - 40f - 70f - 54f - 16f - 120f;
                Faction[] array = (from f in Find.FactionManager.AllFactionsInViewOrder
                                   where f != faction && f.HostileTo(faction) && ((!f.IsPlayer && !f.Hidden) || false) //FactionUIUtility.showAll)
                                   select f).ToArray<Faction>();
                Rect rect = new Rect(90f, rowY, 250f, 80f);
                Rect r = new Rect(24f, rowY + 4f, 42f, 42f);
                float num2 = 62f;
                Rect rect2 = new Rect(rect.xMax, rowY, 40f, 80f);
                Rect rect3 = new Rect(rect2.xMax, rowY, 60f, 80f);
                if (ModsConfig.IdeologyActive && !Find.IdeoManager.classicMode)
                {
                    if (faction.ideos != null)
                    {
                        float num3 = rect3.x;
                        float num4 = rect3.y;
                        if (faction.ideos.PrimaryIdeo != null)
                        {
                            if (num3 + 40f > rect3.xMax)
                            {
                                num3 = rect3.x;
                                num4 += 45f;
                            }
                            Rect rect4 = new Rect(num3, num4, 40f, 40f);
                            num3 += rect4.width + 5f;
                            num3 = rect3.x;
                            num4 += 45f;
                        }
                        int k;
                        int i;
                        for (i = 0; i < 1; i = k + 1)
					{
                            if (num3 + 22f > rect3.xMax)
                            {
                                num3 = rect3.x;
                                num4 += 27f;
                            }
                            if (num4 + 22f > rect3.yMax)
                            {
                                break;
                            }
                            Rect rect5 = new Rect(num3, num4, 22f, 22f);
                            num3 += rect5.width + 5f;
                            k = i;
                        }
                    }
                }
                else
                {
                    rect3.width = 0f;
                }
                Rect rect6 = new Rect(rect3.xMax, rowY, 70f, 80f);
                Rect rect7 = new Rect(rect6.xMax, rowY, 54f, 80f);
                if (!faction.IsPlayer && faction.HasGoodwill && !faction.def.permanentEnemy)
                {
                    Log.Message("Yo, what. B");
                    FactionRelationKind relationKindForKarma = FactionUIUtility_GetRelationKindForKarma(GetKarma(faction));
                    GUI.color = relationKindForKarma.GetColor();
                    Rect rect7a = new Rect(rect6.xMax, rowY + 25, 54f, 80f);
                    Rect rect9 = rect7a.ContractedBy(7f);
                    rect9.height = 20f;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.DrawRectFast(rect9, Color.black, null);
                    Widgets.Label(rect9, GetKarma(faction).ToStringWithSign());
                    GenUI.ResetLabelAlign();
                    GUI.color = Color.white;
                    if (Mouse.IsOver(rect7))
                    {
                        TaggedString taggedString2 = "Karma".Colorize(ColoredText.TipSectionTitleColor) + ": " + GetKarma(faction).ToString().Colorize(relationKindForKarma.GetColor());
                        int karmaMin = Mathf.Clamp(GetKarma(faction) + Primum_DefOf.minKarmaCPY, 0, 100);
                        int karmaMax = Mathf.Clamp(GetKarma(faction) + Primum_DefOf.maxKarmaCPY, 0, 100);
                        taggedString2 += string.Concat(new string[]
                        {
                        "\n",
                        "Karma One-Year Range".Colorize(ColoredText.TipSectionTitleColor),
                        ": ",
                        karmaMin.ToString().Colorize(FactionUIUtility_GetRelationKindForKarma(karmaMin).GetColor()),
                        " "
                        }) + "Range To" + " " + karmaMax.ToString().Colorize(FactionUIUtility_GetRelationKindForKarma(karmaMax).GetColor());
                        /*TaggedString naturalGoodwillExplanation = FactionUIUtility_GetNaturalGoodwillExplanation(faction);
                        if (!naturalGoodwillExplanation.NullOrEmpty())
                        {
                            taggedString2 += "\n\n" + "Affected By".Colorize(ColoredText.TipSectionTitleColor) + "\n" + naturalGoodwillExplanation;
                        }*/
                        taggedString2 += "\n\n" + "Karma is based on how compatibility between your actions and other factions' beliefs. Karma affects maximum good will, and natural goodwill.".Colorize(ColoredText.SubtleGrayColor);
                        TooltipHandler.TipRegion(rect7, taggedString2);
                    }
                }
            }
        }
        public static FactionRelationKind FactionUIUtility_GetRelationKindForGoodwill(int goodwill)
        {
            if (goodwill <= -75)
            {
                return FactionRelationKind.Hostile;
            }
            if (goodwill >= 75)
            {
                return FactionRelationKind.Ally;
            }
            return FactionRelationKind.Neutral;
        }
        public static FactionRelationKind FactionUIUtility_GetRelationKindForKarma(int karma)
        {
            if (karma <= 30)
            {
                return FactionRelationKind.Hostile;
            }
            if (karma >= 70)
            {
                return FactionRelationKind.Ally;
            }
            return FactionRelationKind.Neutral;
        }
    }
}