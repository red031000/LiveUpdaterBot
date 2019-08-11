using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;

namespace LiveUpdaterBot
{
	public static class Utils
	{
		public static async Task<List<DiscordMessage>> SendMessage(DiscordClient client, string content, DiscordEmbed embed = null)
		{
			List<DiscordMessage> messages = new List<DiscordMessage>();
			foreach (DiscordChannel channel in Program.Channels)
			{
				DiscordMessage message = await channel.SendMessageAsync(content, embed: embed);
				messages.Add(message);
			}
			Console.WriteLine($"Sent message: {content}{(embed != null ? $", With embed: {embed.Description}" : "" )}");
			await Program.LogWriter.WriteLineAsync($"Sent message: {content}{(embed != null ? $", With embed: {embed.Description}" : "")}");
			await Program.LogWriter.FlushAsync();
			return messages;
		}
	}
}
