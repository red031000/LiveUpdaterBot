#nullable enable

using System.Collections.Generic;

namespace StreamFeedBot.Rulesets
{
	public class Memory
	{
		public Dictionary<int, int> Attempts = new Dictionary<int, int>();

		public List<uint> AnnouncedCrystals = new List<uint>(); //TODO find a more permanent solution
	}
}
