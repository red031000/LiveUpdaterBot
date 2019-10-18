#nullable enable

using System;
using System.Collections.Generic;

namespace StreamFeedBot.Rulesets
{
	public abstract class Ruleset
	{
		protected Random Random;

		public Memory Memory;

		public Dictionary<int, int> Attempts => Memory.Attempts;

		public Dictionary<Pokemon, int> ReleasedDictionary = new Dictionary<Pokemon, int>();

		protected Settings Settings;

		protected Ruleset(Memory memory, Settings settings)
		{
			Random = new Random();
			Memory = memory;
			Settings = settings;
		}

		public abstract string? CalculateDeltas(RunStatus? status, RunStatus? oldStatus, out string? announcement);
	}
}
