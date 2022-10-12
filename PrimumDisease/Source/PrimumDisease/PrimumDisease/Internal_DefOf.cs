using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;

namespace PrimumDisease
{
    [DefOf]
    public class Internal_DefOf
    {
        public static HediffDef PD_CreutzfeldtRimDisease;
        public static HediffDef PD_Immunosuppression;
        public static HediffDef PD_ArchotechInfluence;

        public static PD_TransmissionDef PD_Contact;
        public static PD_TransmissionDef PD_FoodBorne;
        public static PD_TransmissionDef PD_OutdoorsVectorBorne;
        public static PD_TransmissionDef PD_IndoorsVectorBorne;
        public static PD_TransmissionDef PD_UndergroundVectorBorne;
        public static PD_TransmissionDef PD_Memetic;
        public static PD_TransmissionDef PD_Proximity;
        public static PD_TransmissionDef PD_Venereal;

        public static float TransmissionMultiplierChance_Combat = 0.1f;
        public static float TransmissionMultiplierChance_Lovin = 1f;
        public static float TransmissionMultiplierChance_Interaction = 0.5f;

        public static PD_ImplantTypeDef PD_Natural;
        public static PD_ImplantTypeDef PD_Nonnatural;
        public static PD_ImplantTypeDef PD_Archotech;


    }
}