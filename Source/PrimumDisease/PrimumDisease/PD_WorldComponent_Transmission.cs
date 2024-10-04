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
    public class PD_WorldComponent_Transmission : WorldComponent
    {
        public int tickCounter;
        public int tickThreshold = 10000; //Attempt to transmit 6 times per day: 60 000 ticks per day


        public PD_WorldComponent_Transmission(World world) : base(world)
        {
        }

        public void TransmitDiseases()
        {
            foreach(Pawn sourcePawn in PD_Utility.AllMaps_Alive_OfPlayerFaction_NoCryptosleep()) //Pawns belonging to other factions can't transmit for speed purposes
            {

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
                TransmitDiseases();
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
