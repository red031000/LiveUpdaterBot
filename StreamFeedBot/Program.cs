using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;

namespace StreamFeedBot
{
	class Program //TODO re-write to use events
	{
		public static DiscordClient Client;
		public static Settings Settings;
		public static List<DiscordChannel> Channels = new List<DiscordChannel>();
		public static DateTime logdate = DateTime.UtcNow.Date;

		public static FileStream LogStream =
			new FileStream("TriHardEmerald" + logdate.ToString("yyyy-MM-dd") + ".txt", FileMode.Append);

		public static StreamWriter LogWriter = new StreamWriter(LogStream);
		private static bool cancel, reset;

		private static Dictionary<int, int> attempts = new Dictionary<int, int>();

		private static uint expected;
		private static readonly List<Pokemon> lost = new List<Pokemon>();

		private static Api Api;

		public static readonly DateTime RunStart = new DateTime(2019, 08, 10, 21, 00, 00);

		private static readonly Random Random = new Random();

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
			Console.WriteLine("Press Enter to continue...");
			Console.ReadLine();
			Environment.Exit(0);
		}


		private static void DumpMemory()
		{
			string json = JsonConvert.SerializeObject(attempts);
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
					string message = CalculateDeltas(Api.Status, Api.OldStatus);
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
					LogStream = new FileStream("TriHardEmerald" + logdate.ToString("yyyy-MM-dd") + ".txt", FileMode.Append);
					LogWriter = new StreamWriter(LogStream);
				}

				await Task.Delay(RefreshInterval * 1000);
			}

			Disconnect(null, null);
		}

		private static string CalculateDeltas(RunStatus status, RunStatus oldStatus)
		{
			bool reset2 = reset;

			StringBuilder builder = new StringBuilder();
			if (oldStatus == null)
				return null; //calculate deltas between two statuses, not just one

			if (status.BattleKind != null && status.GameStats.BattlesFought != oldStatus.GameStats.BattlesFought)
			{
				switch (status.BattleKind)
				{
					case BattleKind.Wild:
						string[] rand1 =
						{
							"come across", "run into", "step on", "stumble upon", "encounter", "bump into", "run across"
						};
						string[] rand2 = {"Facing off against", "Battling", "Grappling", "Confronted by", "Wrestling"};
						string[] rand3 =
						{
							"picks a fight with", "engages", "thinks it can take", "crashes into", "smacks into",
							"collides with", "jumps", "ambushes", "attacks", "assaults"
						};
						string[] choice =
						{
							$"We {rand1[Random.Next(rand1.Length)]} a wild {status.EnemyParty[0].Species.Name}. ",
							$"{rand2[Random.Next(rand2.Length)]} a wild {status.EnemyParty[0].Species.Name}. ",
							$"A wild {status.EnemyParty[0].Species.Name} {rand3[Random.Next(rand3.Length)]} us. "
						};
						string message = choice[Random.Next(choice.Length)];
						builder.Append(message);
						break;
					case BattleKind.Trainer:
						if (status.EnemyTrainers.Count == 1)
						{
							Trainer trainer = status.EnemyTrainers[0];
							if (trainer.ClassName == "Magma Admin" || trainer.ClassName == "Magma Leader" ||
							    trainer.ClassName == "Aqua Leader" || trainer.ClassName == "Aqua Admin" ||
							    trainer.ClassName == "Leader" || trainer.ClassName == "Elite Four" ||
							    trainer.ClassName == "Champion" ||
							    trainer.ClassId == 50 /* brenden, may, steven, few others */)
							{
								builder.Append($"**VS {trainer.ClassName} {trainer.Name}!** ");
								if (attempts.TryGetValue(trainer.Id, out int val))
								{
									builder.Append($"Attempt #{val + 1}! ");
									attempts.Remove(trainer.Id);
									attempts.Add(trainer.Id, val + 1);
								}
								else
								{
									attempts.Add(trainer.Id, 1);
								}

								break;
							}

							string[] c1 = {"fight", "battle", "face off against"};
							string[] c2 = {"cheeky", "rogue", "roving", "wandering"};
							string[] c3 = {" wandering", "n eager"};
							string[] choices =
							{
								$"We {c1[Random.Next(c1.Length)]} a {c2[Random.Next(c2.Length)]} {trainer.ClassName}, named {trainer.Name}{(status.EnemyParty.Count(x => (bool) x.Active) != 0 ? $", and their {string.Join(", ", status.EnemyParty.Where(x => (bool) x.Active).Select(x => x.Species.Name))}" : "")}. ",
								$"We get spotted by a{c3[Random.Next(c3.Length)]} {trainer.ClassName} named {trainer.Name}, and begin a battle{(status.EnemyParty.Count(x => (bool) x.Active) != 0 ? $" against their {string.Join(", ", status.EnemyParty.Where(x => (bool) x.Active).Select(x => x.Species.Name))}" : "")}. ",
								$"{trainer.ClassName} {trainer.Name} picks a fight with us{(status.EnemyParty.Count(x => (bool) x.Active) != 0 ? $", using their {string.Join(", ", status.EnemyParty.Where(x => (bool) x.Active).Select(x => x.Species.Name))}" : "")}. "
							};
							builder.Append(choices[Random.Next(choices.Length)]);
						}
						else if (status.EnemyTrainers.Count == 2)
						{
							Trainer trainer0 = status.EnemyTrainers[0];
							Trainer trainer1 = status.EnemyTrainers[1];

							if (trainer0.ClassName == "Magma Admin" || trainer0.ClassName == "Magma Leader" ||
							    trainer0.ClassName == "Aqua Leader" || trainer0.ClassName == "Aqua Admin" ||
							    trainer0.ClassName == "Leader" || trainer0.ClassName == "Elite Four" ||
							    trainer0.ClassName == "Champion" ||
							    trainer0.ClassId == 50 /* brenden, may, steven, few others */)
							{
								builder.Append($"**VS {trainer0.ClassName}s {trainer0.Name}!** ");
								if (attempts.TryGetValue(trainer0.Id, out int val))
								{
									builder.Append($"Attempt #{val + 1}! ");
									attempts.Remove(trainer0.Id);
									attempts.Add(trainer0.Id, val + 1);
								}
								else
								{
									attempts.Add(trainer0.Id, 1);
								}
							}
							else if (trainer1.ClassId != 0)
							{
								string[] choices =
								{
									$"Both {trainer0.ClassName} {trainer0.Name} and {trainer1.ClassName} {trainer1.Name} challenge us to a battle at the same time!",
								};
								builder.Append(choices[Random.Next(choices.Length)]);
							}
							else
							{
								string[] choices =
								{
									$"{trainer0.ClassName} {trainer0.Name} challenge us to a battle at the same time!",
								};
								builder.Append(choices[Random.Next(choices.Length)]);
							}
						}

						break;
				}
			}

			if (status.GameStats.Blackouts != oldStatus.GameStats.Blackouts)
			{
				string[] options = { "**BLACKED OUT!** ", "**We BLACK OUT!** ", "**BLACK OUT...** " };
				string message = options[Random.Next(options.Length)];
				reset = true;
				lost.Clear();
				builder.Append(message);
			}

			if (status.BattleKind == null && oldStatus.BattleKind == BattleKind.Trainer && !reset)
			{
				if (oldStatus.EnemyTrainers.Count == 1)
				{
					Trainer trainer = oldStatus.EnemyTrainers[0];
					if (trainer.ClassName == "Magma Admin" || trainer.ClassName == "Magma Leader" ||
					    trainer.ClassName == "Aqua Leader" || trainer.ClassName == "Aqua Admin" ||
					    trainer.ClassName == "Leader" || trainer.ClassName == "Elite Four" ||
					    trainer.ClassName == "Champion" ||
					    trainer.ClassId == 50 /* brenden, may, steven, few others */)
					{
						builder.Append($"**Defeated {trainer.ClassName} {trainer.Name}!** ");
					}

					if (trainer.ClassName == "Champion")
						builder.Append("**TEH URN!** ");
				}
				else if (oldStatus.EnemyTrainers.Count == 2)
				{
					if (oldStatus.EnemyTrainers[1].Id == 0)
					{
						Trainer trainer = oldStatus.EnemyTrainers[0];
						if (trainer.ClassName == "Magma Admin" || trainer.ClassName == "Magma Leader" ||
						    trainer.ClassName == "Aqua Leader" || trainer.ClassName == "Aqua Admin" ||
						    trainer.ClassName == "Leader" || trainer.ClassName == "Elite Four" ||
						    trainer.ClassName == "Champion" ||
						    trainer.ClassId == 50 /* brenden, may, steven, few others */)
						{
							builder.Append($"**Defeated {trainer.ClassName}s {trainer.Name}!** ");
						}
					}
				}
			}

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
						builder.Append(choices[Random.Next(choices.Length)]);
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
						builder.Append(choices[Random.Next(choices.Length)]);
					}
				}
			}

			List<uint> ids = new List<uint>();

			if (!reset)
			{
				foreach (Item item in status.Items.Balls)
				{
					if (ids.Contains(item.Id)) continue;
					long count = status.Items.Balls.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
					bool res = oldStatus.Items.Balls.FirstOrDefault(x => x.Id == item.Id) != null;
					if (res)
					{
						long? oldCount = oldStatus.Items.Balls.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
						count -= oldCount ?? 1;
					}

					if (count != 0)
					{
						Pokemon[] monsGive = status.Party.Where(x => x.HeldItem != null && x.HeldItem.Id == item.Id)
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								(oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem == null ||
								oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id != x.HeldItem.Id))
							.ToArray();
						Pokemon[] monsTake = status.Party.Where(x =>
								x.HeldItem == null ||
								(oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								 oldStatus.Party.First(y => x.PersonalityValue == y.PersonalityValue).HeldItem.Id !=
								 x.HeldItem.Id))
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem != null
								&& oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id ==
								item.Id)
							.ToArray();
						if (monsGive.Length != 0)
						{
							foreach (Pokemon mon in monsGive)
							{
								builder.Append($"We give {mon.Name} ({mon.Species.Name}) a {item.Name} to hold. ");
								count++;
							}
						}

						if (monsTake.Length != 0)
						{
							foreach (Pokemon mon in monsTake)
							{
								builder.Append($"We take a {item.Name} away from {mon.Name} ({mon.Species.Name}). ");
								count--;
							}
						}

						if (status.BattleKind != null && count < 0)
							builder.Append(
								$"We throw {(count == -1 ? $"a {item.Name}" : $"some {item.Name}s")} at the wild {status.EnemyParty[0].Species.Name}. ");
						else if (count < 0)
							builder.Append(
								$"We toss {(count == -1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
						else if (count > 0)
							builder.Append(
								$"We pick up {(count == 1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
					}

					ids.Add(item.Id);
				}

				foreach (Item item in status.Items.Berries)
				{
					if (ids.Contains(item.Id)) continue;
					long count = status.Items.Berries.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
					bool res = oldStatus.Items.Berries.FirstOrDefault(x => x.Id == item.Id) != null;
					if (res)
					{
						long? oldCount = oldStatus.Items.Berries.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
						count -= oldCount ?? 1;
					}

					if (count != 0)
					{
						Pokemon[] monsGive = status.Party.Where(x => x.HeldItem != null && x.HeldItem.Id == item.Id)
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								(oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem == null ||
								 oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id != x.HeldItem.Id))
							.ToArray();
						Pokemon[] monsTake = status.Party.Where(x =>
								x.HeldItem == null ||
								(oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								 oldStatus.Party.First(y => x.PersonalityValue == y.PersonalityValue).HeldItem.Id !=
								 x.HeldItem.Id))
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem != null
								&& oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id ==
								item.Id)
							.ToArray();
						if (monsGive.Length != 0)
						{
							foreach (Pokemon mon in monsGive)
							{
								builder.Append($"We give {mon.Name} ({mon.Species.Name}) a {item.Name} to hold. ");
								count++;
							}
						}

						if (monsTake.Length != 0)
						{
							foreach (Pokemon mon in monsTake)
							{
								builder.Append($"We take a {item.Name} away from {mon.Name} ({mon.Species.Name}). ");
								count--;
							}
						}

						if (status.BattleKind != null && count < 0)
							builder.Append(
								$"We use {(count == -1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
						else if (count < 0)
							builder.Append(
								$"We use {(count == -1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
						else if (count > 0)
							builder.Append(
								$"We pick up {(count == 1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
					}

					ids.Add(item.Id);
				}

				foreach (Item item in status.Items.Items)
				{
					if (ids.Contains(item.Id)) continue;
					long count = status.Items.Items.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
					bool res = oldStatus.Items.Items.FirstOrDefault(x => x.Id == item.Id) != null;
					if (res)
					{
						long? oldCount = oldStatus.Items.Items.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
						count -= oldCount ?? 1;
					}

					if (count != 0)
					{
						Pokemon[] monsGive = status.Party.Where(x => x.HeldItem != null && x.HeldItem.Id == item.Id)
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								(oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem == null ||
								 oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id != x.HeldItem.Id))
							.ToArray();
						Pokemon[] monsTake = status.Party.Where(x =>
								x.HeldItem == null ||
								(oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								 oldStatus.Party.First(y => x.PersonalityValue == y.PersonalityValue).HeldItem.Id !=
								 x.HeldItem.Id))
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem != null
								&& oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id ==
								item.Id)
							.ToArray();
						if (monsGive.Length != 0)
						{
							foreach (Pokemon mon in monsGive)
							{
								builder.Append($"We give {mon.Name} ({mon.Species.Name}) a {item.Name} to hold. ");
								count++;
							}
						}

						if (monsTake.Length != 0)
						{
							foreach (Pokemon mon in monsTake)
							{
								builder.Append($"We take a {item.Name} away from {mon.Name} ({mon.Species.Name}). ");
								count--;
							}
						}

						if (status.BattleKind != null && count < 0)
							builder.Append(
								$"We use {(count == -1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
						else if (count < 0)
							builder.Append(
								$"We use {(count == -1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
						else if (count > 0)
							builder.Append(
								$"We pick up {(count == 1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
					}

					ids.Add(item.Id);
				}

				foreach (Item item in status.Items.Key)
				{
					if (ids.Contains(item.Id)) continue;
					long count = status.Items.Key.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
					bool res = oldStatus.Items.Key.FirstOrDefault(x => x.Id == item.Id) != null;
					if (res)
					{
						long? oldCount = oldStatus.Items.Key.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
						count -= oldCount ?? 1;
					}

					if (count != 0)
					{
						Pokemon[] monsGive = status.Party.Where(x => x.HeldItem != null && x.HeldItem.Id == item.Id)
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								(oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem == null ||
								 oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id != x.HeldItem.Id))
							.ToArray();
						Pokemon[] monsTake = status.Party.Where(x =>
								x.HeldItem == null ||
								(oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								 oldStatus.Party.First(y => x.PersonalityValue == y.PersonalityValue).HeldItem.Id !=
								 x.HeldItem.Id))
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem != null
								&& oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id ==
								item.Id)
							.ToArray();
						if (monsGive.Length != 0)
						{
							foreach (Pokemon mon in monsGive)
							{
								builder.Append($"We give {mon.Name} ({mon.Species.Name}) a {item.Name} to hold. ");
								count++;
							}
						}

						if (monsTake.Length != 0)
						{
							foreach (Pokemon mon in monsTake)
							{
								builder.Append($"We take a {item.Name} away from {mon.Name} ({mon.Species.Name}). ");
								count--;
							}
						}

						if (count < 0)
							builder.Append(
								$"We use {(count == -1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
						else if (count > 0)
							builder.Append(
								$"We pick up {(count == 1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
					}

					ids.Add(item.Id);
				}

				foreach (Item item in status.Items.TMs)
				{
					if (ids.Contains(item.Id)) continue;
					long count = status.Items.TMs.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
					bool res = oldStatus.Items.TMs.FirstOrDefault(x => x.Id == item.Id) != null;
					if (res)
					{
						long? oldCount = oldStatus.Items.TMs.Where(x => x.Id == item.Id).Sum(x => x.Count ?? 1);
						count -= oldCount ?? 1;
					}

					if (count != 0)
					{
						Pokemon[] monsGive = status.Party.Where(x => x.HeldItem != null && x.HeldItem.Id == item.Id)
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								(oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem == null ||
								 oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id != x.HeldItem.Id))
							.ToArray();
						Pokemon[] monsTake = status.Party.Where(x =>
								x.HeldItem == null ||
								(oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								 oldStatus.Party.First(y => x.PersonalityValue == y.PersonalityValue).HeldItem.Id !=
								 x.HeldItem.Id))
							.Where(x =>
								oldStatus.Party.Any(y => x.PersonalityValue == y.PersonalityValue) &&
								oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem != null
								&& oldStatus.Party.First(y => y.PersonalityValue == x.PersonalityValue).HeldItem.Id ==
								item.Id)
							.ToArray();
						if (monsGive.Length != 0)
						{
							foreach (Pokemon mon in monsGive)
							{
								builder.Append($"We give {mon.Name} ({mon.Species.Name}) a {item.Name} to hold. ");
								count++;
							}
						}

						if (monsTake.Length != 0)
						{
							foreach (Pokemon mon in monsTake)
							{
								builder.Append($"We take a {item.Name} away from {mon.Name} ({mon.Species.Name}). ");
								count--;
							}
						}

						if (count < 0)
							builder.Append(
								$"We use {(count == -1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
						else if (count > 0)
							builder.Append(
								$"We pick up {(count == 1 ? $"a {item.Name}" : $"{Math.Abs(count)} {item.Name}s")}. ");
					}

					ids.Add(item.Id);
				}
			}

			for (int i = 0; i < status.Party.Count; i++)
			{
				Pokemon oldMon = oldStatus.Party[i];
				if (oldMon == null) continue;
				uint pv = oldMon.PersonalityValue;
				if (oldMon.Species.Id == 292)
					pv++;
				Pokemon mon = status.Party.Where(x => x != null).FirstOrDefault(x =>
					x.Species.Id == 292 ? x.PersonalityValue + 1 == pv : x.PersonalityValue == pv);
				if (mon == null)
				{
					continue;
				}
				if (mon.Level != oldMon.Level && !reset)
				{
					string[] choices =
					{
						$"**{oldMon.Name} ({oldMon.Species.Name}) has grown to level {mon.Level}!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) is now level {mon.Level}!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) has leveled up to {mon.Level}!** "
					};
					string message = choices[Random.Next(choices.Length)];
					builder.Append(message);
				}

				if (mon.Species.Id != oldMon.Species.Id && !reset)
				{
					string[] choices =
					{
						$"**{oldMon.Name} ({oldMon.Species.Name}) has evolved into a {mon.Species.Name}! **",
						$"**{oldMon.Name} ({oldMon.Species.Name}) evolves into a {mon.Species.Name}! **"
					};
					string message = choices[Random.Next(choices.Length)];
					builder.Append(message);
				}
			}

			foreach (Pokemon mon in status.Party)
			{
				if (mon == null) continue;
				uint pv = mon.PersonalityValue;
				if (mon.Species.Id == 292)
					pv++;
				Pokemon oldMon = oldStatus.Party.Where(x => x != null).FirstOrDefault(x =>
					x.Species.Id == 292 ? x.PersonalityValue + 1 == pv : x.PersonalityValue == pv);
				if (oldMon == null) continue;
				foreach (Move move in mon.Moves)
				{
					if (move == null) continue;
					if (!oldMon.Moves.Where(x => x != null).Select(x => x.Id).Contains(move.Id))
					{
						if (oldMon.Moves.Count == 4)
						{
							Move oldMove = oldMon.Moves.First(x => !mon.Moves.Contains(x));
							builder.Append(
								$"**{mon.Name} ({mon.Species.Name}) learned {move.Name} over {oldMove.Name}!** ");
						}
						else
						{
							builder.Append($"**{mon.Name} ({mon.Species.Name}) learned {move.Name}!** ");
						}
					}
				}

				if (mon.Health[0] == 0 && oldMon.Health[0] != 0)
				{
					string[] choice =
					{
						$"**We lose {oldMon.Name} ({oldMon.Species.Name})!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) has died!** ",
						$"**Press F48 to pay your respects to {oldMon.Name} ({oldMon.Species.Name}).** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) has fallen!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) is no more!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) has kicked the bucket!** ",
						$"**{oldMon.Name} ({oldMon.Species.Name}) has fainted. Bye bye {oldMon.Name}.** "
					};
					builder.Append(choice[Random.Next(choice.Length)]);
					if (lost.All(x => x.PersonalityValue != mon.PersonalityValue))
						lost.Add(mon);
				}
			}

			foreach (Pokemon mon in status.Party)
			{
				if (mon == null) continue;
				uint pv = mon.PersonalityValue;
				if (mon.Species.Id == 292)
					pv++;
				List<uint> values =
					oldStatus.Party.Where(x => x != null).Select(x => x.Species.Id == 292 ? x.PersonalityValue + 1 : x.PersonalityValue)
						.ToList();
				List<Pokemon> mons = oldStatus.Party.Where(x => x != null).ToList();
				if ((!values.Contains(pv) || mons.First(x => x.Species.Id == 292 ? x.PersonalityValue + 1 == pv : x.PersonalityValue == pv).Health[0] == 0) && reset)
				{
					string[] choice =
					{
						$"**We regain {mon.Name} ({mon.Species.Name})!** ",
						$"**{mon.Name} ({mon.Species.Name}) reappears!** ",
						$"**{mon.Name} ({mon.Species.Name}) returns to us!** ",
						$"**{mon.Name} ({mon.Species.Name}) returns from beyond the grave!** ",
						$"**We find {mon.Name} ({mon.Species.Name}) again!** ",
						$"**{mon.Name} ({mon.Species.Name}) came back!** ",
						$"**We revert to a time before {mon.Name}'s ({mon.Species.Name}) demise.** "
					};
					builder.Append(choice[Random.Next(choice.Length)]);
				}
				else if (!values.Contains(pv))
				{
					builder.Append(
						$"**Caught a {(mon.Gender != null ? Enum.GetName(typeof(Gender), mon.Gender) : "")} Lv. {mon.Level} {mon.Species.Name}!** {(mon.Name == mon.Species.Name ? "No nickname. " : $"Nickname: `{mon.Name}` ")}");
				}
			}

			expected = expected > oldStatus.GameStats.Saves ? expected : oldStatus.GameStats.Saves;

			bool saved = false;

			if (status.GameStats.PokemonCentersUsed > oldStatus.GameStats.PokemonCentersUsed)
			{
				builder.Append("**We heal** at the Poké Center! Progress saved. ");
				expected++;
				saved = true;
			}

			if (status.GameStats.Saves > expected)
			{
				builder.Append("**We save!** ");
				saved = true;
			}

			if (saved)
			{
				foreach (Pokemon mon in lost)
				{
					string[] choice =
					{
						$"**{mon.Name} ({mon.Species.Name}) fades from existence.** ",
						$"**{mon.Name} ({mon.Species.Name}) is enjoying the afterlife.** ",
						$"**{mon.Name} ({mon.Species.Name}) has crossed the boundary of no return!** ",
						$"**May {mon.Name} ({mon.Species.Name}) rest in eternal peace.** ",
						$"**We seal the fate of {mon.Name} ({mon.Species.Name}).** ",
						$"**{mon.Name} ({mon.Species.Name}) is gone** 🦀 ",
						$"**{mon.Name} ({mon.Species.Name}) no longer exists in any timeline!** ",
						$"**Requiescat in pace {mon.Name} ({mon.Species.Name}).** "
					};
					builder.Append(choice[Random.Next(choice.Length)]);
				}
				lost.Clear();
			}

			if (status.MapName != oldStatus.MapName)
			{
				if (!string.IsNullOrWhiteSpace(status.MapName))
				{
					string[] move = {"head", "go", "step", "move", "travel", "walk", "stroll", "stride"};
					string choice = move[Random.Next(move.Length)];
					List<string> options = new List<string>
					{
						$"{status.MapName}. ", $"In {status.MapName}. ",
						$"Now in {status.MapName}. ",
						$"We {choice} into {status.MapName}. ",
						$"Arrived at {status.MapName}. "
					};
					string message = options[Random.Next(options.Count)];
					builder.Append(message);
				}
				else if (status.MapId == 13 || status.MapId == 15)
				{
					builder.Append("Entered into a contest. ");
				}
			}

			if (reset2)
				reset = false;

			return builder.ToString().Length == 0 ? null : builder.ToString();
		}
	}
}
