using System;
using System.Collections.Generic;

namespace StreamFeedBot.Rulesets
{
	public abstract class Ruleset
	{
		protected Random Random;

		public Dictionary<int, int> Attempts { get; protected set; }

		protected Settings Settings;

		protected Ruleset(Dictionary<int, int> attempts, Settings settings)
		{
			Random = new Random();
			Attempts = attempts;
			Settings = settings;
		}

		public abstract string CalculateDeltas(RunStatus status, RunStatus oldStatus, out string announcement);
	}
}
