using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace LiveUpdaterBot
{
	class Program
	{
		public static DiscordClient Client;
		public static Settings Settings;
		public static List<DiscordChannel> Channels = new List<DiscordChannel>();
		private static bool cancel;

		private static Api Api;

		public static readonly DateTime RunStart = new DateTime(2019, 08, 10, 21, 00, 00);

		private static Random Random = new Random();

		public const int RefreshInterval = 15;

		private static void Main(string[] args)
		{
			try
			{
				Console.CancelKeyPress += Disconnect;
				MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
			}
			catch (Exception e)
			{
				using (FileStream stream = new FileStream("crash.log", FileMode.OpenOrCreate, FileAccess.ReadWrite))
				{
					using (StreamWriter writer = new StreamWriter(stream))
					{
						writer.WriteLine($"{e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");
						writer.Flush();
					}
				}
				Disconnect(null, null);
			}
		}

		private static async void Disconnect(object sender, ConsoleCancelEventArgs e)
		{
			if (Client != null)
			{
				Utils.SendMessage(Client, "**Disconnecting**").Wait();
				await Client.DisconnectAsync();
			}

			cancel = true;
			Console.ReadLine();
			Environment.Exit(0);
		}

		private static async Task MainAsync(string[] args)
		{
			using (FileStream stream = new FileStream("Settings.json", FileMode.Open))
			{
				using (StreamReader reader = new StreamReader(stream))
				{
					string json = await reader.ReadToEndAsync();
					Settings = JsonConvert.DeserializeObject<Settings>(json);
				}
			}

			Client = new DiscordClient(new DiscordConfiguration
			{
				Token = Settings.Token,
				TokenType = TokenType.Bot
			});

			Api = new Api();

			await Client.ConnectAsync();

			foreach (ulong id in Settings.Channels)
			{
				DiscordChannel channel = await Client.GetChannelAsync(id);
				await channel.SendMessageAsync("**Connected**");
				Console.WriteLine($"Connecting to channel {id}.");
				Channels.Add(channel);
			}

			await MainLoop();

			await Task.Delay(-1);
		}

		private static async Task MainLoop()
		{
			while (!cancel)
			{
				await Api.UpdateStatus();
				string message = await CalculateDeltas(Api.Status, Api.OldStatus);
				if (message != null)
				{
					TimeSpan time = DateTime.UtcNow - RunStart;
					await Utils.SendMessage(Client, $"{time.Days}d {time.Hours}h {time.Minutes}m " + message.Trim());
				}

				//await Utils.SendMessage(Client, $"AreaName: {Api.Status.AreaName}, Blackouts: {Api.Status.GameStats.Blackouts}, Saves: {Api.Status.GameStats.Saves}, MapName: {Api.Status.MapName}");
				await Task.Delay(RefreshInterval * 1000);
			}
		}

		private static async Task<string> CalculateDeltas(RunStatus status, RunStatus oldStatus)
		{
			StringBuilder builder = new StringBuilder();
			if (oldStatus == null)
				return null; //calculate deltas between two statuses, not just one

			if (status.Badges != oldStatus.Badges)
			{
				List<bool> gains = new List<bool>();
				List<bool> losses = new List<bool>();
				int j = 0;
				foreach (bool flag in status.BadgesFlags)
				{
					if (flag != oldStatus.BadgesFlags[j] && flag)
					{
						gains.Add(true);
						losses.Add(false);
					}
					else if (flag != oldStatus.BadgesFlags[j] && !flag)
					{
						gains.Add(false);
						losses.Add(true);
					}
					else
					{
						gains.Add(false);
						losses.Add(false);
					}
					j++;
				}

				for (int i = 0; i < losses.Count; i++)
				{
					if (losses[i])
					{
						string[] choices = { $"**Lost the {Settings.BadgeNames[i]} Badge!** " };
						builder.Append(choices[Random.Next(choices.Length - 1)]);
					}
				}

				for (int i = 0; i < gains.Count; i++)
				{
					if (gains[i])
					{
						string[] choices =
						{
							$"**Got the {Settings.BadgeNames[i]} Badge!** ",
							$"**Received the {Settings.BadgeNames[i]} Badge!** "
						};
						builder.Append(choices[Random.Next(choices.Length - 1)]);
					}
				}
			}

			if (status.BattleKind != null && oldStatus.BattleKind == null)
			{
				switch (status.BattleKind) //TODO double trainer
				{
					case BattleKind.Wild:
						string[] rand1 =
							{"come across", "run into", "step on", "stumble upon", "encounter", "bump into", "run across"};
						string[] rand2 = { "Facing off against", "Battling", "Grappling", "Affronted by", "Wrestling" };
						string[] rand3 =
						{
							"picks a fight with", "engages", "thinks it can take", "crashes into", "smacks into",
							"collides with", "jumps", "ambushes", "attacks", "assaults"
						};
						string[] choice =
						{
							$"We {rand1[Random.Next(rand1.Length - 1)]} a wild {status.EnemyParty[0].Species.Name}. ",
							$"{rand2[Random.Next(rand2.Length - 1)]} a wild {status.EnemyParty[0].Species.Name}. ",
							$"A wild {status.EnemyParty[0].Species.Name} {rand3[Random.Next(rand3.Length - 1)]} us. "
						};
						string message = choice[Random.Next(choice.Length - 1)];
						builder.Append(message);
						break;
					case BattleKind.Trainer:
						if (status.EnemyTrainers.Count == 1)
						{
							Trainer trainer = status.EnemyTrainers[0];
							if (trainer.ClassName == "Leader")
							{
								builder.Append($"**VS {trainer.ClassName} {trainer.Name}!** ");
								break;
							}
							string[] c1 = {"fight", "battle", "face off against"};
							string[] c2 = {"cheeky", "rogue", "roving", "wandering"};
							string[] c3 = {" wandering", " n eager"};
							string[] choices =
							{
								$"We {c1[Random.Next(c1.Length - 1)]} a {c2[Random.Next(c2.Length - 1)]} {trainer.ClassName}, named {trainer.Name}{ (status.EnemyParty.Count(x => (bool)x.Active) != 0 ? $", and their {string.Join(", ", status.EnemyParty.Where(x => (bool)x.Active).Select(x => x.Species.Name))}" : "")}. ",
								$"We get spotted by a{c3[Random.Next(c3.Length - 1)]} {trainer.ClassName} named {trainer.Name}, and begin a battle{ (status.EnemyParty.Count(x => (bool)x.Active) != 0 ? $" against their {string.Join(", ", status.EnemyParty.Where(x => (bool)x.Active).Select(x => x.Species.Name))}" : "")}. ",
								$"{trainer.ClassName} {trainer.Name} picks a fight with us{ (status.EnemyParty.Count(x => (bool)x.Active) != 0 ? $", using their {string.Join(", ", status.EnemyParty.Where(x => (bool)x.Active).Select(x => x.Species.Name))}" : "")}. "
							};
							builder.Append(choices[Random.Next(choices.Length - 1)]);
							break;
						}
						else
						{
							break;
						}
				}
			}

			for (int i = 0; i < status.Party.Count; i++)
			{
				Pokemon oldMon = oldStatus.Party[i];
				if (oldMon == null) continue;
				uint pv = oldMon.PersonalityValue;
				if (oldMon.Species.Id == 292)
					pv++;
				Pokemon mon = status.Party.FirstOrDefault(x =>
					x.Species.Id == 292 ? x.PersonalityValue + 1 == pv : x.PersonalityValue == pv);
				if (mon == null)
				{
					builder.Append($"**We lose {oldMon.Name} ({oldMon.Species.Name})!** ");
					continue;
				}
				if (mon.Level != oldMon.Level)
				{
					string[] choices =
					{
						$"**{oldMon.Name} ({oldMon.Species.Name}) has grown to level {mon.Level}!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) is now level {mon.Level}!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) has leveled up to {mon.Level}!** "
					};
					string message = choices[Random.Next(choices.Length - 1)];
					builder.Append(message);

					if (mon.Species.Id != oldMon.Species.Id)
					{
						choices = new[]
						{
							$"**{oldMon.Name} ({oldMon.Species.Name}) has evolved into a {mon.Species.Name}! **",
							$"**{oldMon.Name} ({oldMon.Species.Name}) evolves into a {mon.Species.Name}! **"
						};
						message = choices[Random.Next(choices.Length - 1)];
						builder.Append(message);
					}
				}
			}

			if (status.GameStats.Blackouts != oldStatus.GameStats.Blackouts)
			{
				string[] options = { "**BLACKED OUT!** ", "**We BLACK OUT!** ", "**BLACK OUT...** " };
				string message = options[Random.Next(options.Length - 1)];
				builder.Append(message);
			}

			if (status.GameStats.Saves != oldStatus.GameStats.Saves) builder.Append("**We save!**");

			if (status.MapName != oldStatus.MapName)
			{
				string[] move = { "head", "go", "step", "move", "travel", "walk", "stroll", "stride" };
				string choice = move[Random.Next(move.Length - 1)];
				List<string> options = new List<string>
				{
					$"{status.MapName ?? status.AreaName}. ", $"In {status.MapName ?? status.AreaName}. ",
					$"Now in {status.MapName ?? status.AreaName}. ",
					$"We {choice} into {status.MapName ?? status.AreaName}. ",
					$"Arrived at {status.MapName ?? status.AreaName}. "
				};
				string message = options[Random.Next(options.Count - 1)];
				builder.Append(message);
			}

			return builder.ToString().Length == 0 ? null : builder.ToString();
		}
	}
}
