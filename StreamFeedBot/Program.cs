#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using EmbedIO;
using EmbedIO.Files;
using Newtonsoft.Json;
using StreamFeedBot.Rulesets;
using StreamFeedBot.Web;
using Swan.Logging;
using LogLevel = DSharpPlus.LogLevel;

namespace StreamFeedBot
{
	internal class Program
	{
		public static DiscordClient? Client;
		public static Settings? Settings;
		public static List<DiscordChannel> Channels = new List<DiscordChannel>();
		private static DateTime logdate = DateTime.UtcNow.Date;

		public static List<string>? Badges => Ruleset?.Badges;

		private static FileStream? LogStream;
		public static StreamWriter? LogWriter;

		private static FileStream? PrivateStream;
		public static StreamWriter? PrivateWriter;

		private static bool cancel;
		private static ManualResetEvent? mre;

		private static Ruleset? Ruleset;
		private static WebServer? Server;

		private static Api? Api;

		private static readonly DateTime RunStart = new DateTime(2020, 06, 13, 21, 00, 00, DateTimeKind.Utc);

		private const int RefreshInterval = 15;

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

				try
				{
					if (Client != null)
						Utils.ReportError(e, Client, true).Wait();
				}
				// ReSharper disable once EmptyGeneralCatchClause
				catch //literally fuck all we can do about it
				{ }

				Disconnect(null, null);
			}
		}

		private static void Disconnect(object? sender, ConsoleCancelEventArgs? e)
		{
			try
			{
				Client?.DisconnectAsync().Wait();
			}
			// ReSharper disable once EmptyGeneralCatchClause
			catch //again, fuck all we can do about it
			{ }
			mre?.Set();
			cancel = true;
			if (e != null)
				e.Cancel = true;
			DumpMemory();
			Server?.Dispose();
			Console.WriteLine("Press Enter to continue...");
			Console.ReadLine();
			Environment.Exit(0);
		}


		private static void DumpMemory()
		{
			if (Ruleset?.Memory == null) return;
			string json = JsonConvert.SerializeObject(Ruleset.Memory, Formatting.Indented);
			File.WriteAllText("memory.json", json);
		}

		private static async Task MainAsync()
		{
			await using (FileStream stream = new FileStream("Settings.json", FileMode.Open))
			{
				using StreamReader reader = new StreamReader(stream);
				string json = await reader.ReadToEndAsync().ConfigureAwait(true);
				Settings = JsonConvert.DeserializeObject<Settings>(json);
			}

			if (!Directory.Exists("logs"))
				Directory.CreateDirectory("logs");

			if (!Directory.Exists(Path.Combine("logs", Settings.RunName!)))
				Directory.CreateDirectory(Path.Combine("logs", Settings.RunName!));

			if (!Settings.WebOnly)
			{
				LogStream =
					new FileStream(
						Path.Combine("logs", Settings.RunName!,
							Settings.RunName + logdate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt"),
						FileMode.Append);

				LogWriter = new StreamWriter(LogStream);
			}

			if (!Directory.Exists("privatelogs"))
				Directory.CreateDirectory("privatelogs");

			PrivateStream =
				new FileStream(Path.Combine("privatelogs", Settings.RunName + logdate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt"), FileMode.Append);

			PrivateWriter = new StreamWriter(PrivateStream);

			Memory memory = new Memory();

			if (File.Exists("memory.json"))
			{
				await using FileStream stream = new FileStream("memory.json", FileMode.Open);
				using StreamReader reader = new StreamReader(stream);
				string json = await reader.ReadToEndAsync().ConfigureAwait(true);
				memory = JsonConvert.DeserializeObject<Memory>(json);
			}

			Api = new Api();

			if (!Settings.WebOnly)
			{
				Client = new DiscordClient(new DiscordConfiguration
				{
					Token = Settings.Token,
					TokenType = TokenType.Bot,
					LogLevel = LogLevel.Debug
				});

				Client.DebugLogger.LogMessageReceived += DebugLoggerOnLogMessageReceived;

				await Client.ConnectAsync(new DiscordActivity("TwitchPlaysPokemon", ActivityType.Streaming)
				{
					StreamUrl = "https://www.twitch.tv/twitchplayspokemon"
				}).ConfigureAwait(false);

				Client.MessageCreated += DmHandler;

				foreach (ulong id in Settings.Channels!)
				{
					DiscordChannel channel = await Client.GetChannelAsync(id).ConfigureAwait(false);
					Console.WriteLine($"Connecting to channel {id}.");
					Channels.Add(channel);
				}

				Ruleset = new SiriusRuleset(memory, Settings); //TODO change as needed

				if (DateTime.UtcNow < RunStart)
				{
					TimeSpan span = RunStart - DateTime.UtcNow;
					await Utils.SendMessage(
							$"Connected, {span.Days}d {span.Hours}h {span.Minutes}m {span.Seconds}s until {Settings.RunName}!")
						.ConfigureAwait(false);
				}
			}

			Logger.RegisterLogger<WebLogger>();

			Server = new WebServer(opt => opt
					.WithUrlPrefix($"http://*:{Settings.WebSettings!.Port}/")
					.WithMode(HttpListenerMode.EmbedIO))
				.WithLocalSessionManager()
				.HandleHttpException(GenericHttpExceptionHandler.Handler);

			if (!Settings.WebOnly)
			{
				Server.WithWebApi("/api", m => m
					.RegisterController(() => new ApiController(Ruleset!.Memory, Ruleset!.ReleasedDictionary, Api)));
			}

			if (Settings.WebSettings?.LogsDir != null)
			{
				Server.WithStaticFolder("/logs", Settings.WebSettings.LogsDir, true,
					m => m.WithDirectoryLister(new LogsDirectoryLister()).WithContentCaching(false));
			}

			if (Settings.WebSettings?.SnapshotsDir != null)
			{
				Server.WithStaticFolder("/snapshots", Settings.WebSettings.SnapshotsDir, true,
					m => m.WithDirectoryLister(new LogsDirectoryLister()).WithContentCaching(false));
			}

			if (Settings.WebSettings?.ResDir != null)
			{
				Server.WithStaticFolder("/", Settings.WebSettings.ResDir, true, m => m.WithContentCaching(true));
			}

			//don't fucking touch, no matter how tempting
			// ReSharper disable once AssignmentIsFullyDiscarded
			_ = Server.RunAsync();

			if (!Settings.WebOnly)
			{
				await MainLoop().ConfigureAwait(false);

				await Task.Delay(-1).ConfigureAwait(false);
			}
			else
			{
				mre = new ManualResetEvent(false);
				mre.WaitOne();
			}
		}

		private static void DebugLoggerOnLogMessageReceived(object? sender, DebugLogMessageEventArgs? e)
		{
			if (e == null || e.Level == LogLevel.Debug &&
				e.Message.Contains("heartbeat", StringComparison.InvariantCultureIgnoreCase)) return;
			if (PrivateWriter != null)
			{
				PrivateWriter
					.WriteLine(
						$"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)} [{e.Application}] {Enum.GetName(typeof(LogLevel), e.Level)}: {e.Message}");
				if (e.Exception != null)
				{
					PrivateWriter.WriteLine(
						$"{e.Exception} {e.Exception.Message}{Environment.NewLine}{e.Exception.StackTrace}");
					if (e.Exception.InnerException != null)
					{
						PrivateWriter.WriteLine("Inner Exception:");
						PrivateWriter.WriteLine(
							$"{e.Exception.InnerException} {e.Exception.InnerException.Message}{Environment.NewLine}{e.Exception.InnerException.StackTrace}");
					}
				}

				PrivateWriter.Flush();
			}

			Console.ForegroundColor = e.Level switch
			{
				LogLevel.Warning => ConsoleColor.Yellow,
				LogLevel.Critical => ConsoleColor.Red,
				LogLevel.Error => ConsoleColor.Red,
				_ => Console.ForegroundColor
			};
			Console.WriteLine(
				$"[{e.Application}] {Enum.GetName(typeof(LogLevel), e.Level)}: {e.Message}");
			if (e.Exception != null)
			{
				Console.WriteLine(
					$"{e.Exception} {e.Exception.Message}{Environment.NewLine}{e.Exception.StackTrace}");
				if (e.Exception.InnerException != null)
				{
					Console.WriteLine("Inner Exception:");
					Console.WriteLine(
						$"{e.Exception.InnerException} {e.Exception.InnerException.Message}{Environment.NewLine}{e.Exception.InnerException.StackTrace}");
				}
			}

			Console.ResetColor();
		}

		private static async Task DmHandler(MessageCreateEventArgs e)
		{
			if (e.Guild == null)
			{
				if (e.Author != Client!.CurrentUser)
				{
					Console.WriteLine($"Received DM from {e.Author.Username}#{e.Author.Discriminator}: {e.Message.Content}");
				}
				if (Settings!.SuperUsers.Contains(e.Author.Id) && e.Message.Content.ToUpperInvariant().Trim() == "STOP")
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
				else if (Settings.SuperUsers.Contains(e.Author.Id) &&
				         (e.Message.Content.ToUpperInvariant().Trim() == "RELOADMEM"
				         || e.Message.Content.ToUpperInvariant().Trim() == "RELOADMEMORY"
				         || e.Message.Content.ToUpperInvariant().Trim() == "RELOAD MEMORY"))
				{
					await e.Message.RespondAsync("reloading memory from ~/publish/memory.json <:RaccAttack:468748603632910336>").ConfigureAwait(true);
					if (File.Exists("memory.json"))
					{
						await using FileStream stream = new FileStream("memory.json", FileMode.Open);
						using StreamReader reader = new StreamReader(stream);
						string json = await reader.ReadToEndAsync().ConfigureAwait(true);
						Ruleset!.Memory = JsonConvert.DeserializeObject<Memory>(json);
					}
					Console.WriteLine($"Reloading memory by request of {e.Author.Username}#{e.Author.Discriminator}");
				}
				else if (Settings.SuperUsers.Contains(e.Author.Id) &&
				         (e.Message.Content.ToUpperInvariant().Trim() == "SAVESNAPSHOT"
				          || e.Message.Content.ToUpperInvariant().Trim() == "SAVE SNAPSHOT"
				          || e.Message.Content.ToUpperInvariant().Trim() == "SNAPSHOT"))
				{
					string? link = Api?.PostSnapshot();
					await e.Message.RespondAsync($"snapshot saved to {link ?? "ERROR: API is null!!"} <:RaccAttack:468748603632910336>").ConfigureAwait(true);
					Console.WriteLine($"Saved snapshot by request of {e.Author.Username}#{e.Author.Discriminator}");
				}
				else if (e.Author != Client.CurrentUser &&
				         new[] {"UWU", "OWO"}.Contains(e.Message.Content.Trim().ToUpperInvariant()))
				{
					await e.Message.RespondAsync("<:RaccAttack:468748603632910336>w<:RaccAttack:468748603632910336>")
						.ConfigureAwait(false);
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
				bool result = await Api!.UpdateStatus().ConfigureAwait(false);
				if (result)
				{
					try
					{
						string? message = Ruleset!.CalculateDeltas(Api.Status, Api.OldStatus, out string? announcement, out bool ping);
						if (message != null)
						{
							TimeSpan time = DateTime.UtcNow - RunStart;
							await Utils.SendMessage($"{time.Days}d {time.Hours}h {time.Minutes}m " + message.Trim())
								.ConfigureAwait(true);
						}

						if (announcement != null)
							await Utils.AnnounceMessage(announcement, Client, ping).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine(
							$"ERROR: Failed to resolve deltas: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");
						PrivateWriter!.WriteLine(
							$"ERROR: Failed to resolve deltas: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");
						PrivateWriter.Flush();
						await Utils.ReportError(e, Client).ConfigureAwait(true);
						Console.ResetColor();
					}
				}

				if (logdate != DateTime.UtcNow.Date)
				{
					if (!Settings!.WebOnly)
					{
						LogWriter?.Dispose();
						LogStream?.Dispose();
						logdate = DateTime.UtcNow.Date;
						LogStream = new FileStream(
							Path.Combine("logs", Settings.RunName!,
								Settings!.RunName + logdate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
								".txt"), FileMode.Append);
						LogWriter = new StreamWriter(LogStream);
					}

					PrivateWriter?.Dispose();
					PrivateStream?.Dispose();
					PrivateStream =
						new FileStream(Path.Combine("privatelogs", Settings.RunName + logdate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt"), FileMode.Append);

					PrivateWriter = new StreamWriter(PrivateStream);
				}

				await Task.Delay(RefreshInterval * 1000).ConfigureAwait(false);
			}
			Disconnect(null, null);
		}
	}
}
