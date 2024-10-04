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
    public class PD_Utility
    {
        public static float LogClamp(float q, FloatRange range, float logFactor)  //Math to clamp a number to a range, and apply a logarithmic scale--with continuous slope--outside that range.
        {
            if (q > range.max)
            {
                q = range.max + Mathf.Log((q - range.max) * logFactor + 1) / logFactor;
            }
            if (q < range.min)
            {
                q = range.max - Mathf.Log((-q + range.min) * logFactor + 1) / logFactor;
            }
            return q;
        }
        public static int CountImplants(Pawn pawn, PD_ImplantTypeDef type) //Type can be Natural, Nonnatural, or Archotech
        {
            int count = 0;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (type == null)
                {
                    count += hediff.def.countsAsAddedPartOrImplant ? 1 : 0;
                }
                else
                {
                    count += GetProstheticClass(hediff.def) == type ? 1 : 0;
                }
            }
            return count;
        }

        public static void TransmitBetweenPawns(Pawn sourcePawn, Pawn targetPawn, PD_TransmissionDef transmissionMethod1 = null, PD_TransmissionDef transmissionMethod2 = null, PD_TransmissionDef transmissionMethod3 = null)
        {
            foreach (Hediff disease in PD_Utility.GetTransmissableDiseases(sourcePawn, transmissionMethod1, transmissionMethod2, transmissionMethod3))
            {
                PD_Utility.TryGivePawnDisease(targetPawn, disease.def);
            }
        }
        public static Hediff TryGivePawnDisease(Pawn pawn, HediffDef diseaseDef)
        {
            PD_TransmissionExtension extension = diseaseDef.HasModExtension<PD_TransmissionExtension>() ? diseaseDef.GetModExtension<PD_TransmissionExtension>() : null;
            float transmissionChance = 1;
            if(extension != null)
            {
                float resistance = 1;
                foreach(PD_ResistanceCapacity resistanceCapacity in extension.resistanceCapacities)
                {
                    resistance += (pawn.health.capacities.GetLevel(resistanceCapacity.capacity) - 1) * resistanceCapacity.factor;
                }
                transmissionChance = extension.transmissivity / Mathf.Clamp(resistance, 0.01f, 100f);
                if (!extension.canAffectPawn(pawn))
                    transmissionChance = 0;
            }
            if(Rand.Value <= transmissionChance)
            {
                Hediff hediff = HediffMaker.MakeHediff(diseaseDef, pawn);
                pawn.health.AddHediff(hediff);
                return hediff;
            }
            return null;
        }

        public static List<Hediff> GetTransmissableDiseases(Pawn pawn, PD_TransmissionDef transmissionMethod1=null, PD_TransmissionDef transmissionMethod2=null, PD_TransmissionDef transmissionMethod3 = null)
        {
            List<Hediff> hediffList = new List<Hediff>();
            foreach(Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def.HasModExtension<PD_TransmissionExtension>())
                {
                    if(transmissionMethod1==null || hediff.def.GetModExtension<PD_TransmissionExtension>().transmissionMethods.Contains(transmissionMethod1) || hediff.def.GetModExtension<PD_TransmissionExtension>().transmissionMethods.Contains(transmissionMethod2) || hediff.def.GetModExtension<PD_TransmissionExtension>().transmissionMethods.Contains(transmissionMethod3))
                        hediffList.Add(hediff);
                }
            }
            return hediffList;
        }

        public static RoofDef RoofFromPawn(Pawn pawn)
        {
            return pawn.Map.roofGrid.RoofAt(pawn.Position);
        }

        public static List<Pawn> GetClosePawns(Pawn sourcePawn, float distance) //Only gets player-responsible pawns
        {
            List<Pawn> closePawns = new List<Pawn>();
            List<Pawn> otherPawnList = sourcePawn.Map.mapPawns.FreeColonistsAndPrisoners;
            foreach (Pawn otherPawn in otherPawnList)
            {
                if(otherPawn!=sourcePawn && sourcePawn.Position.InHorDistOf(otherPawn.Position, distance))
                    closePawns.Add(otherPawn);
            }
            return closePawns;
        }
        public static List<Pawn> AllMaps_Alive_OfPlayerFaction_NoCryptosleep() //Used extensively for disease transmission and updating?
        {
            List<Pawn> pawnList = new List<Pawn>();
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction_NoCryptosleep)
            {
                if (pawn.Map != null)
                    pawnList.Add(pawn);
            }
            return pawnList;
        }

        public static List<Room> AdjoiningRooms(Room room, List<Room> excluding, bool doorsOnly = true)
        {
            List<Room> roomList = new List<Room>();
            Map map = room.Map;
            foreach(IntVec3 borderPosition in room.BorderCells)
            {
                Room testRoom = RegionAndRoomQuery.RoomAt(borderPosition, map);
                if (testRoom != null && (!doorsOnly || testRoom.IsDoorway))
                {
                    List<Room> adjacentRooms = new List<Room>();
                    for (int i = 0; i < 8; i++)
                    {
                        testRoom = RegionAndRoomQuery.RoomAt(borderPosition + GenAdj.AdjacentCells[i], map);
                        if (testRoom != null && testRoom != room && !testRoom.IsDoorway && !roomList.Contains(testRoom) && (excluding == null || !excluding.Contains(testRoom)))
                        {
                            roomList.Add(testRoom);
                        }
                    }
                }
            }
            return roomList;
        }
        public static List<Room> AllJoinedRooms(Room room, int remainingDistance, float openAirFactor = 3, int iterationCap = 3, List<Room> roomList = null)
        {
            remainingDistance -= (int) (room.CellCount + (openAirFactor-1) * room.OpenRoofCount);
            if (roomList == null)
                roomList=new List<Room>() {};
            if(remainingDistance < 0 && iterationCap>0)
            {
                if(roomList.Count==0)
                    roomList.Add(room);
                List<Room> adjoiningRooms = AdjoiningRooms(room, roomList);
                foreach (Room adjoiningRoom in adjoiningRooms)
                {
                    if (!roomList.Contains(adjoiningRoom))
                    {
                        roomList.Add(adjoiningRoom);
                        roomList = AllJoinedRooms(adjoiningRoom, remainingDistance, openAirFactor, iterationCap - 1, roomList);
                    }
                }
            }
            return roomList;
        }
        



        public static float ContactFoodPoisoningChance(Pawn worker)
        {
            return 0.1f*(20-(float)worker.skills.GetSkill(SkillDefOf.Cooking).Level)/20 + worker.GetStatValue(StatDefOf.FoodPoisonChance) * 4; //worker.GetStatValue(StatDefOf.FoodPoisonChance)
        }



        public static PD_ImplantTypeDef GetProstheticClass(HediffDef hediffDef) //Natural, Nonnatural, Archotech BADBADBAD
        {
            string hediffDefSublabel = hediffDef.label.Length >= 5 ? hediffDef.label.Substring(0, 5).ToLower() : hediffDef.label;
            if (hediffDef.countsAsAddedPartOrImplant)
            {
                if (hediffDefSublabel == "archo")
                {
                    return Internal_DefOf.PD_Archotech;
                }
                //HediffSublabel != "woode" && HediffSublabel != "simpl" && HediffSublabel != "cochl" && HediffSublabel != "peg l" && HediffSublabel != "prost"
                if (hediffDefSublabel == "woode" || hediffDefSublabel == "peg l" ||
                    hediffDef.spawnThingOnRemoved?.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("AA_ImplantCategory")) == true ||
                    hediffDef.spawnThingOnRemoved?.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("VFEI_BodyPartsInsect")) == true ||
                    hediffDef.spawnThingOnRemoved?.thingCategories?.Contains(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("GR_ImplantCategory")) == true)
                {
                    return Internal_DefOf.PD_Natural;
                }
                return Internal_DefOf.PD_Nonnatural;
            }
            return null;
        }
    }
}
