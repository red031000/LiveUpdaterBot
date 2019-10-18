#nullable enable

using System.Collections.Generic;

namespace StreamFeedBot.Rulesets
{
	public class Memory
	{
		public Dictionary<int, int> Attempts = new Dictionary<int, int>();

		public List<uint> AnnouncedCrystals = new List<uint>(); //TODO find a more permanent solution

		public uint E4AttemptNum = 1;

		public bool Urned;

		public uint E4RematchNum = 1;
	}
}
