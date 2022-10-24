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
	public class PD_SurgeryOutcomeComp_SoftClampToRange : SurgeryOutcomeComp
	{
		public override void AffectQuality(RecipeDef recipe, Pawn surgeon, Pawn patient, List<Thing> ingredients, BodyPartRecord part, Bill bill, ref float quality)
		{
			quality = PD_Utility.LogClamp(quality, this.range, this.logFactor);
		}

		public FloatRange range;
		public float logFactor;
	}
	public class PD_SurgeryOutcomeComp_RecipeSkillDifficulty : SurgeryOutcomeComp
	{
		public override void AffectQuality(RecipeDef recipe, Pawn surgeon, Pawn patient, List<Thing> ingredients, BodyPartRecord part, Bill bill, ref float quality)
		{
			float relativeLevel = Mathf.Max(0, surgeon.skills.GetSkill(SkillDefOf.Medicine).Level - recipe.skillRequirements[1].minLevel);
			quality *= PD_Utility.LogClamp(relativeLevel, this.range, this.logFactor)*(1-this.minimum) + this.minimum;
		}

		public FloatRange range;
		public float logFactor;
		public float minimum;
	}
}
