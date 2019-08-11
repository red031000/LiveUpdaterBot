using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;

namespace LiveUpdaterBot
{
	public static class Utils
	{
		public static async Task<List<DiscordMessage>> SendMessage(DiscordClient client, string content)
		{
			List<DiscordMessage> messages = new List<DiscordMessage>();
			foreach (DiscordChannel channel in Program.Channels)
			{
				DiscordMessage message = await channel.SendMessageAsync(content);
				messages.Add(message);
			}
			Console.WriteLine($"Sent message: {content}");
			return messages;
		}
	}
}
