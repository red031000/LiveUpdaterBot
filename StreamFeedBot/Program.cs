using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using StreamFeedBot.Rulesets;

namespace StreamFeedBot
{
	internal class Program
	{
		public static DiscordClient Client;
		public static Settings Settings;
		public static List<DiscordChannel> Channels = new List<DiscordChannel>();
		public static DateTime logdate = DateTime.UtcNow.Date;

		public static FileStream LogStream;

		public static StreamWriter LogWriter;
		private static bool cancel;

		private static Ruleset Ruleset;

		private static Api Api;

		public static readonly DateTime RunStart = new DateTime(2019, 10, 12, 21, 00, 00, DateTimeKind.Utc);

		public const int RefreshInterval = 15;

		private static void Main()
		{
			try
			{
				Console.CancelKeyPress += Disconnect;
				MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
			}
			catch (Exception e)
			{
				File.WriteAllText("crash.log",
					$"{e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}{e.InnerException?.Message}{Environment.NewLine}{e.InnerException?.StackTrace}{Environment.NewLine}");

				if (Client != null)
					Utils.ReportError(e, Client, true).Wait();
				Disconnect(null, null);
			}
		}

		private static void Disconnect(object sender, ConsoleCancelEventArgs e)
		{
			Client?.DisconnectAsync().Wait();

			cancel = true;
			if (e != null)
				e.Cancel = true;
			DumpMemory();
			Api.StopTimer();
			Console.WriteLine("Press Enter to continue...");
			Console.ReadLine();
			Environment.Exit(0);
		}


		private static void DumpMemory()
		{
			string json = JsonConvert.SerializeObject(Ruleset.Memory);
			File.WriteAllText("memory.json", json);
		}

		private static async Task MainAsync()
		{
			using (FileStream stream = new FileStream("Settings.json", FileMode.Open))
			{
				using (StreamReader reader = new StreamReader(stream))
				{
					string json = await reader.ReadToEndAsync().ConfigureAwait(true);
					Settings = JsonConvert.DeserializeObject<Settings>(json);
				}
			}

			if (!Directory.Exists("logs"))
				Directory.CreateDirectory("logs");

			LogStream =
				new FileStream(Path.Combine("logs", Settings.RunName + logdate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt"), FileMode.Append);

			LogWriter = new StreamWriter(LogStream);

			Memory memory = new Memory();

			if (File.Exists("memory.json"))
			{
				using (FileStream stream = new FileStream("memory.json", FileMode.Open))
				{
					using (StreamReader reader = new StreamReader(stream))
					{
						string json = await reader.ReadToEndAsync().ConfigureAwait(true);
						memory = JsonConvert.DeserializeObject<Memory>(json);
					}
				}
			}

			Client = new DiscordClient(new DiscordConfiguration
			{
				Token = Settings.Token,
				TokenType = TokenType.Bot
			});

			Api = new Api();

			await Client.ConnectAsync(new DiscordActivity("TwitchPlaysPokemon", ActivityType.Streaming)
			{
				StreamUrl = "https://www.twitch.tv/twitchplayspokemon"
			}).ConfigureAwait(false);

			Client.MessageCreated += DmHandler;

			foreach (ulong id in Settings.Channels)
			{
				DiscordChannel channel = await Client.GetChannelAsync(id).ConfigureAwait(false);
				Console.WriteLine($"Connecting to channel {id}.");
				Channels.Add(channel);
			}

			Ruleset = new RandomizedUltraMoonRuleset(memory, Settings);

			if (DateTime.UtcNow < RunStart)
			{
				TimeSpan span = RunStart - DateTime.UtcNow;
				await Utils.SendMessage(
					$"Connected, {span.Days}d {span.Hours}h {span.Minutes}m {span.Seconds}s until {Settings.RunName}!").ConfigureAwait(false);
			}

			await MainLoop().ConfigureAwait(false);

			await Task.Delay(-1).ConfigureAwait(false);
		}

		private static async Task DmHandler(MessageCreateEventArgs e)
		{
			if (e.Guild == null)
			{
				if (e.Author != Client.CurrentUser)
				{
					Console.WriteLine($"Recieved DM from {e.Author.Username}#{e.Author.Discriminator}: {e.Message.Content}");
				}
				if (Settings.SuperUsers.Contains(e.Author.Id) && e.Message.Content.ToUpperInvariant().Trim() == "STOP")
				{
					await e.Message.RespondAsync("stopping <:RaccAttack:468748603632910336>").ConfigureAwait(true);
					Console.WriteLine($"Stopping by request of {e.Author.Username}#{e.Author.Discriminator}");
					cancel = true;
				}
				else if (Settings.SuperUsers.Contains(e.Author.Id) &&
				         e.Message.Content.ToUpperInvariant().Trim() == "DUMPMEM"
				         || e.Message.Content.ToUpperInvariant().Trim() == "DUMPMEMORY"
				         || e.Message.Content.ToUpperInvariant().Trim() == "DUMP MEMORY")
				{
					await e.Message.RespondAsync("dumping memory to ~/publish/memory.json <:RaccAttack:468748603632910336>").ConfigureAwait(true);
					DumpMemory();
					Console.WriteLine($"Dumping memory by request of {e.Author.Username}#{e.Author.Discriminator}");
				}
				else if (e.Author != Client.CurrentUser)
				{
					await e.Message.RespondAsync("<:RaccAttack:468748603632910336>").ConfigureAwait(false);
				}
			}
		}

		private static async Task MainLoop()
		{
			while (!cancel)
			{
				await Api.UpdateStatus().ConfigureAwait(false);
				try
				{
					string message = Ruleset.CalculateDeltas(Api.Status, Api.OldStatus, out string announcement);
					if (message != null)
					{
						TimeSpan time = DateTime.UtcNow - RunStart;
						await Utils.SendMessage($"{time.Days}d {time.Hours}h {time.Minutes}m " + message.Trim()).ConfigureAwait(true);
					}

					if (announcement != null) await Utils.AnnounceMessage(announcement, Client).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"ERROR: Failed to resolve deltas: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");
					await LogWriter.WriteLineAsync(
						$"ERROR: Failed to resolve deltas: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}").ConfigureAwait(true);
					await LogWriter.FlushAsync().ConfigureAwait(true);
					await Utils.ReportError(e, Client).ConfigureAwait(true);
					Console.ResetColor();
				}

				if (logdate != DateTime.UtcNow.Date)
				{
					LogWriter.Dispose();
					LogStream.Dispose();
					logdate = DateTime.UtcNow.Date;
					LogStream = new FileStream(Path.Combine("logs", Settings.RunName + logdate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt"), FileMode.Append);
					LogWriter = new StreamWriter(LogStream);
				}

				await Task.Delay(RefreshInterval * 1000).ConfigureAwait(false);
			}
			Disconnect(null, null);
		}
	}
}
