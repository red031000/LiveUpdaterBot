using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;

namespace StreamFeedBot
{
	public static class Utils
	{
		public static async Task<List<DiscordMessage>> SendMessage(string content, DiscordEmbed embed = null)
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

		public static async Task ReportError(Exception e, DiscordClient client, bool crashLog = false)
		{
			if (Program.Settings != null)
			{
				DiscordGuild RPS = await client.GetGuildAsync(Program.Settings.ReportServer);
				DiscordMember red = await RPS.GetMemberAsync(Program.Settings.ReportId);
				string message =
					$"Exception has occured: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}Inner exception: {e.InnerException?.Message}{Environment.NewLine}{e.InnerException?.StackTrace}";
				string message2 =
					$"Exception has occured: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}Inner exception too long.";
				await red.SendMessageAsync(message.Length >= 2000
					? message2.Length >= 2000
						?
						$"Exception has occured: {e.Message}{Environment.NewLine}Stack Trace too long."
						: message2
					: message);
				if (crashLog)
				{
					using (FileStream stream = new FileStream("crash.log", FileMode.Open))
					{
						await red.SendFileAsync(stream);
					}
				}
			}
		}
	}
}
