#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;

namespace StreamFeedBot
{
	public static class Utils
	{
		public static async Task<List<DiscordMessage>> SendMessage(string content, DiscordEmbed? embed = null)
		{
			List<DiscordMessage> messages = new List<DiscordMessage>();
			if (content == null)
				throw new ArgumentException("Content is null", nameof(content));

			if (content.Length <= 2000)
			{
				foreach (DiscordChannel channel in Program.Channels)
				{
					DiscordMessage message = await channel.SendMessageAsync(content, embed: embed).ConfigureAwait(true);
					messages.Add(message);
				}

				Console.WriteLine(
					$"Sent message: {content}{(embed != null ? $", With embed: {embed.Description}" : "")}");
				if (Program.LogWriter != null)
				{
					await Program.LogWriter
						.WriteLineAsync(
							$"Sent message: {content}{(embed != null ? $", With embed: {embed.Description}" : "")}")
						.ConfigureAwait(true);
					await Program.LogWriter.FlushAsync().ConfigureAwait(false);
				}
			}
			else
			{
				string[] parts = content.Split(" ", StringSplitOptions.RemoveEmptyEntries);
				string message = "";
				foreach (string part in parts)
				{
					if (part.Length + message.Length <= 2000)
						message += part + " ";
					else
					{
						foreach (DiscordChannel channel in Program.Channels)
						{
							DiscordMessage msg = await channel.SendMessageAsync(message.Trim(), embed: embed).ConfigureAwait(true);
							messages.Add(msg);
						}

						Console.WriteLine(
							$"Sent message: {message.Trim()}{(embed != null ? $", With embed: {embed.Description}" : "")}");
						if (Program.LogWriter != null)
						{
							await Program.LogWriter
								.WriteLineAsync(
									$"Sent message: {message.Trim()}{(embed != null ? $", With embed: {embed.Description}" : "")}")
								.ConfigureAwait(true);
							await Program.LogWriter.FlushAsync().ConfigureAwait(false);
						}

						message = part + " ";
					}
				}

				if (!string.IsNullOrEmpty(message))
				{
					foreach (DiscordChannel channel in Program.Channels)
					{
						DiscordMessage msg = await channel.SendMessageAsync(message.Trim(), embed: embed).ConfigureAwait(true);
						messages.Add(msg);
					}

					Console.WriteLine(
						$"Sent message: {message.Trim()}{(embed != null ? $", With embed: {embed.Description}" : "")}");
					if (Program.LogWriter != null)
					{
						await Program.LogWriter
							.WriteLineAsync(
								$"Sent message: {message.Trim()}{(embed != null ? $", With embed: {embed.Description}" : "")}")
							.ConfigureAwait(true);
						await Program.LogWriter.FlushAsync().ConfigureAwait(false);
					}
				}
			}

			return messages;
		}

		public static async Task ReportError(Exception? e, DiscordClient? client, bool crashLog = false)
		{
			if (Program.Settings != null && client != null && e != null)
			{
				DiscordGuild rps = await client.GetGuildAsync(Program.Settings.ReportServer).ConfigureAwait(true);
				DiscordMember red = await rps.GetMemberAsync(Program.Settings.ReportId).ConfigureAwait(true);
				string message =
					$"Exception has occured: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}Inner exception: {e.InnerException?.Message}{Environment.NewLine}{e.InnerException?.StackTrace}";
				string message2 =
					$"Exception has occured: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}Inner exception too long.";
				await red.SendMessageAsync(message.Length >= 2000
					? message2.Length >= 2000
						? $"Exception has occured: {e.Message}{Environment.NewLine}Stack Trace too long."
						: message2
					: message).ConfigureAwait(true);
				if (crashLog)
				{
					await using FileStream stream = new FileStream("crash.log", FileMode.Open);
					await red.SendFileAsync("crash.log", stream).ConfigureAwait(false);
				}
			}
		}

		public static async Task<List<DiscordMessage>?> AnnounceMessage(string? message, DiscordClient? client, bool ping = false)
		{
			if (client != null && message != null && Program.Settings != null)
			{
				List<DiscordMessage> messages = new List<DiscordMessage>();
				foreach (AnnounceSettings setting in Program.Settings.AnnounceSettings!)
				{
					DiscordRole? role = null;
					if (ping)
					{
						DiscordGuild guild = await client.GetGuildAsync(setting.AnnounceServer).ConfigureAwait(true);
						role = guild.GetRole(setting.AnnounceRole);
						role?.ModifyAsync(mentionable: true)?.Wait();
					}

					DiscordChannel channel = await client.GetChannelAsync(setting.AnnounceChannel).ConfigureAwait(true);
					DiscordMessage sent = await channel.SendMessageAsync($"{(ping ? "<@&{setting.AnnounceRole}> " : "")}" + message)
						.ConfigureAwait(true);
					Console.WriteLine($"Announced message: {(ping ? "<@&{setting.AnnounceRole}> " : "")}{message}");
					if (Program.LogWriter != null)
					{
						await Program.LogWriter
							.WriteLineAsync($"Announced message: {(ping ? "<@&{setting.AnnounceRole}> " : "")}{message}")
							.ConfigureAwait(true);
						await Program.LogWriter.FlushAsync().ConfigureAwait(true);
					}

					messages.Add(sent);
					if (role != null && ping)
						role?.ModifyAsync(mentionable: false)?.Wait();
				}

				return messages;
			}

			return null;
		}

		public static string IndefiniteArticle(string? item)
		{
			if (item == null) return "a";
			return new char?[] {'A', 'E', 'I', 'O', 'U'}.Contains(item.Trim().ToUpperInvariant().FirstOrDefault()) ? "an" : "a";
		}
	}
}
