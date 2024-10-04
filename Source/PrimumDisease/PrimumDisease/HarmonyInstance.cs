using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;
using HarmonyLib;

using System.Reflection;
using System.Reflection.Emit;
using Verse.AI;
using RimWorld.Planet;

namespace PrimumDisease
{
    public class HarmonyInstanceMod : Mod
    {
        public HarmonyInstanceMod(ModContentPack content) : base(content)
        {
            var h = new Harmony("com.primumdisease");
            h.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
