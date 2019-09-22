using System;
using System.Collections.Generic;
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

		public static readonly DateTime RunStart = new DateTime(2019, 08, 10, 21, 00, 00);

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
			string json = JsonConvert.SerializeObject(Ruleset.Attempts);
			File.WriteAllText("memory.json", json);
		}

		private static async Task MainAsync()
		{
			using (FileStream stream = new FileStream("Settings.json", FileMode.Open))
			{
				using (StreamReader reader = new StreamReader(stream))
				{
					string json = await reader.ReadToEndAsync();
					Settings = JsonConvert.DeserializeObject<Settings>(json);
				}
			}

			if (!Directory.Exists("logs"))
				Directory.CreateDirectory("logs");

			LogStream =
				new FileStream(Path.Combine("logs", Settings.RunName + logdate.ToString("yyyy-MM-dd") + ".txt"), FileMode.Append);

			LogWriter = new StreamWriter(LogStream);

			Dictionary<int, int> attempts = new Dictionary<int, int>();

			if (File.Exists("memory.json"))
			{
				using (FileStream stream = new FileStream("memory.json", FileMode.Open))
				{
					using (StreamReader reader = new StreamReader(stream))
					{
						string json = await reader.ReadToEndAsync();
						attempts = JsonConvert.DeserializeObject<Dictionary<int, int>>(json);
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
			});

			Client.MessageCreated += DmHandler;

			foreach (ulong id in Settings.Channels)
			{
				DiscordChannel channel = await Client.GetChannelAsync(id);
				Console.WriteLine($"Connecting to channel {id}.");
				Channels.Add(channel);
			}

			Ruleset = new TriHardEmeraldRuleset(attempts, Settings);

			await MainLoop();

			await Task.Delay(-1);
		}

		private static async Task DmHandler(MessageCreateEventArgs e)
		{
			if (e.Guild == null)
			{
				if (Settings.SuperUsers.Contains(e.Author.Id) && e.Message.Content.ToLowerInvariant().Trim() == "stop")
				{
					await e.Message.RespondAsync("stopping <:RaccAttack:468748603632910336>");
					Console.WriteLine($"Stopping by request of {e.Author.Username}");
					cancel = true;
				}
				else if (e.Author != Client.CurrentUser)
				{
					await e.Message.RespondAsync("<:RaccAttack:468748603632910336>");
				}
			}
		}

		private static async Task MainLoop()
		{
			while (!cancel)
			{
				await Api.UpdateStatus();
				try
				{
					string message = Ruleset.CalculateDeltas(Api.Status, Api.OldStatus);
					if (message != null)
					{
						TimeSpan time = DateTime.UtcNow - RunStart;
						await Utils.SendMessage($"{time.Days}d {time.Hours}h {time.Minutes}m " + message.Trim());
					}
				}
				catch (Exception e)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"ERROR: Failed to resolve deltas: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");
					await LogWriter.WriteLineAsync(
						$"ERROR: Failed to resolve deltas: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");
					await LogWriter.FlushAsync();
					Console.ForegroundColor = ConsoleColor.White;
				}

				if (logdate != DateTime.UtcNow.Date)
				{
					LogWriter.Dispose();
					LogStream.Dispose();
					logdate = DateTime.UtcNow.Date;
					LogStream = new FileStream(Path.Combine("logs", Settings.RunName + logdate.ToString("yyyy-MM-dd") + ".txt"), FileMode.Append);
					LogWriter = new StreamWriter(LogStream);
				}

				await Task.Delay(RefreshInterval * 1000);
			}
			Disconnect(null, null);
		}
	}
}
