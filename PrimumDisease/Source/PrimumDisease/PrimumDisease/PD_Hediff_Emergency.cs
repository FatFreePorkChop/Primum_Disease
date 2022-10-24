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
	public class PD_Hediff_Emergency : HediffWithComps
	{
		public override void PostMake()
		{
			base.PostMake();
			this.intervalFactor = Rand.Range(0.1f, 2f);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<float>(ref this.intervalFactor, "intervalFactor", 0f, false);
		}

		public override void Tick()
		{
			base.Tick();
			PD_EmergencyExtension extension = this.def.GetModExtension<PD_EmergencyExtension>();
			if (extension != null)
            {
				if (this.pawn.IsHashIntervalTick((int)(extension.severityChangeInterval * this.intervalFactor)))
				{
					this.Severity += Rand.Range(extension.severityChangeMin, extension.severityChangeMax);
				}
			}
            else
			{
				if (this.pawn.IsHashIntervalTick((int)(SeverityChangeInterval * this.intervalFactor)))
				{
					this.Severity += Rand.Range(-0.4f, 0.6f);
				}
			}
		}

		public override void Tended(float quality, float maxQuality, int batchPosition = 0)
		{
			base.Tended(quality, maxQuality, 0);
			PD_EmergencyExtension extension = this.def.GetModExtension<PD_EmergencyExtension>();
			float num = (extension==null?TendSuccessChanceFactor:extension.tendSuccessChanceFactor) * quality;

			if (Rand.Value < num)
			{
				if (batchPosition == 0 && this.pawn.Spawned)
				{
					MoteMaker.ThrowText(this.pawn.DrawPos, this.pawn.Map, "TextMote_TreatSuccess".Translate(num.ToStringPercent()), num/quality);
				}
				this.Severity -= (extension==null?TendSeverityReduction:extension.tendSeverityReduction);
				return;
			}
			if (batchPosition == 0 && this.pawn.Spawned)
			{
				MoteMaker.ThrowText(this.pawn.DrawPos, this.pawn.Map, "TextMote_TreatFailed".Translate(num.ToStringPercent()), num/quality);
			}
		}

		private float intervalFactor;
		private const int SeverityChangeInterval = 5000;
		private const float TendSuccessChanceFactor = 0.65f;
		private const float TendSeverityReduction = 0.3f;
	}
}
