#nullable enable

using System.Collections.Generic;

namespace StreamFeedBot.Rulesets
{
	public class Memory
	{
		public Dictionary<int, int> Attempts = new Dictionary<int, int>();

		public List<uint> AnnouncedBadges = new List<uint>();

		public uint E4AttemptNum = 1;

		public bool Urned;

		public uint E4RematchNum = 1;
	}
}
