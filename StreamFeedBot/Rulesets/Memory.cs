using System;
using System.Collections.Generic;
using System.Text;

namespace StreamFeedBot.Rulesets
{
	[Serializable]
	public class Memory
	{
		public Dictionary<int, int> Attempts = new Dictionary<int, int>();

		public List<uint> AnnouncedCrystals = new List<uint>(); //TODO find a more permanent solution
	}
}
