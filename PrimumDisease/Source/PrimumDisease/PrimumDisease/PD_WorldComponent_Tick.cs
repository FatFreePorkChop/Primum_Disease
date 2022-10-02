using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PrimumDisease
{
    public class PD_WorldComponent_Tick : WorldComponent
    {
        public int tickCounter;
        public int tickThreshold = 10000; //Recalculate implant counter severity 4 times a day: 60 000 ticks per day
        public float implantSeverityConstant = 0.05f;
        public List<PD_Endemic> endemicDiseases = new List<PD_Endemic>();


        public PD_WorldComponent_Tick(World world) : base(world)
        {
        }


        public void UpdatePawnImplantCounter(Pawn pawn, HediffDef implantCounterDef, int count)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(implantCounterDef);
            if (count == 0)
            {
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
            else
            {
                if (hediff == null)
                {
                    hediff = HediffMaker.MakeHediff(implantCounterDef, pawn);
                    pawn.health.AddHediff(hediff);
                }
                hediff.Severity = count * implantSeverityConstant;
            }
        }
        public void UpdateImplantCounters()
        {
            List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction; //We don't really care if pawns belonging to other factions are immunosuppressed
            foreach (Pawn pawn in pawns)
            {
                UpdatePawnImplantCounter(pawn, Internal_DefOf.PD_Immunosuppression, PD_Utility.CountImplants(pawn, Internal_DefOf.PD_Nonnatural));
                UpdatePawnImplantCounter(pawn, Internal_DefOf.PD_ArchotechInfluence, PD_Utility.CountImplants(pawn, Internal_DefOf.PD_Archotech));
            }
        }
        
        public void UpdateEndemicDiseases()
        {
            foreach (PD_Endemic endemic in endemicDiseases) //BADBADBAD. I definitely didn't connsider exactly wat remove does, so this could get ugly.
            {
                endemic.tickCounter += tickCounter;
                if (endemic.tickCounter >= endemic.duration)
                {
                    endemicDiseases.Remove(endemic);
                }
            }
        }
        public void TransmitDiseases_Proximity()
        {
            List<Pawn> pawns = PD_Utility.AllMaps_Alive_OfPlayerFaction_NoCryptosleep();
            foreach (Pawn sourcePawn in pawns) //Pawns belonging to other factions can't transmit for speed purposes
            {
                Room sourceRoom = sourcePawn.GetRoom();
                if (!sourceRoom.OutdoorsForWork)
                {
                    List<Hediff> transmissibleDiseases = PD_Utility.GetTransmissableDiseases(sourcePawn, Internal_DefOf.PD_Proximity);
                    foreach(Hediff disease in transmissibleDiseases)
                    {
                        PD_TransmissionExtension extension = disease.def.GetModExtension<PD_TransmissionExtension>();
                        List<Room> rooms = PD_Utility.AllJoinedRooms(sourceRoom, (int)(extension.transmissivity * 100)); //BADBADBAD, should probably a better calculation for distance
                        foreach (Pawn pawn in pawns)
                        {
                            if (pawn != sourcePawn)
                            {
                                Room pawnRoom = pawn.GetRoom();
                                if (pawnRoom != null && rooms.Contains(pawnRoom))
                                {
                                    PD_Utility.TryGivePawnDisease(pawn, disease.def);
                                }
                            }
                        }
                    }
                }
            }
        }
        public void TransmitDiseases_VectorBorne()
        {
            List<Pawn> pawns = PD_Utility.AllMaps_Alive_OfPlayerFaction_NoCryptosleep();
            foreach(Pawn pawn in pawns)
            {
                Room room = pawn.GetRoom();
                RoofDef roof = PD_Utility.RoofFromPawn(pawn);
                PD_TransmissionDef pawnState = (room == null || room.OpenRoofCount > 0) ? Internal_DefOf.PD_OutdoorsVectorBorne : (
                    (roof == RoofDefOf.RoofRockThin || roof == RoofDefOf.RoofRockThin) ? Internal_DefOf.PD_UndergroundVectorBorne : Internal_DefOf.PD_IndoorsVectorBorne
                    );
                foreach (PD_Endemic endemic in endemicDiseases)
                {
                    if (endemic.transmissionMethods.Contains(pawnState))
                    {
                        PD_Utility.TryGivePawnDisease(pawn, endemic.def);
                    }
                }
            }
        }
        public override void FinalizeInit()
        {
            base.FinalizeInit();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            tickCounter++;
            if (tickCounter >= tickThreshold)
            {
                TransmitDiseases_Proximity();
                TransmitDiseases_VectorBorne();
                UpdateEndemicDiseases();
                UpdateImplantCounters();
                tickCounter = 0;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.tickCounter, nameof(this.tickCounter));
        }
    }
}
