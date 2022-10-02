using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;

namespace PrimumDisease
{
    public class PD_FoodPathogen
    {
        public HediffDef def;
        public float penetration = 1; //How much of the stack harbors the disease

        public bool canGiveHediff = true;
        public bool canChangeSeverity = false;

        public float severityMin = -1f;
        public float severityMax = -1f;
        public float severityOffset = 0f;
    }
    public class PD_ResistanceCapacity
    {
        public PawnCapacityDef capacity;
        public float factor = 1;
    }
    public class PD_Endemic
    {
        public HediffDef def;
        public int duration = 3*60000; //3 days by default
        public int tickCounter = 0;
        public float transmissivity
        {
            get
            {
                return this.def.GetModExtension<PD_TransmissionExtension>().transmissivity;
            }
        }
        public List<PD_TransmissionDef> transmissionMethods
        {
            get
            {
                return this.def.GetModExtension<PD_TransmissionExtension>().transmissionMethods;
            }
        }
    }
    public class PD_TransmissionDef : Def
    {
        public bool isVectorBorne
        {
            get
            {
                return (this == Internal_DefOf.PD_UndergroundVectorBorne || this == Internal_DefOf.PD_IndoorsVectorBorne || this == Internal_DefOf.PD_OutdoorsVectorBorne);
            }
        }
    }
    public class PD_ImplantTypeDef : Def
    {
    }


    public class PD_TransmissionExtension : DefModExtension //Appliied to diseases that can be transmitted; gives information about how
    {
        public List<PD_TransmissionDef> transmissionMethods;
        public float transmissivity=0;
        public float transmissionsPerDay=0;
        public float survivalOnCooked = 0.25f;
        public bool affectsHumanlikes = true;
        public bool affectsAnimals = false;
        public bool affectsInsects = false;
        public bool affectsMechanoids = false;
        public List<PD_ResistanceCapacity> resistanceCapacities;

        public bool canAffectPawn(Pawn pawn)
        {
            if (pawn.RaceProps.IsMechanoid)
            {
                return this.affectsMechanoids;
            } else if (pawn.RaceProps.Insect)
            {
                return this.affectsInsects;
            } else if (pawn.RaceProps.Humanlike)
            {
                return this.affectsHumanlikes;
            }
            return this.affectsAnimals;
        }
    }
    public class PD_AcuteDiseaseExtension : DefModExtension //Applied to the heartattack_def, along with a couple harmony patches. Default settings are what heartattack_def is vanilla set to
    {
        public int severityChangeInterval = 5000; //2 in-game hours
        public float severityChangeMin = -0.4f;
        public float severityChangeMax = 0.6f;
        public float tendSuccessChanceFactor = 0.65f;
        public float tendSeverityReduction = 0.3f;
    }
}
