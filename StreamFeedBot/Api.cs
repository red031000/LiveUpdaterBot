#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamFeedBot
{
	public class Api : IDisposable
	{
		public RunStatus? Status;
		public RunStatus? OldStatus;
		private string? message;

		private readonly Timer Timer;

		public HttpClient Client = new HttpClient();

		public Api()
		{
			Client.DefaultRequestHeaders.Add("Accept", "application/json");
			Client.DefaultRequestHeaders.Add("OAuth-Token", Program.Settings!.OAuth);
			Timer = new Timer
			{
				AutoReset = true,
				Interval = 3.6e+6
			};
			Timer.Elapsed += TimerOnElapsed;
			Timer.Enabled = true;
		}

		private void TimerOnElapsed(object sender, ElapsedEventArgs e)
		{
			if (!Directory.Exists("Snapshots"))
				Directory.CreateDirectory("Snapshots");
			File.WriteAllText("Snapshots/ApiSnapshot" + DateTime.UtcNow.ToString("o", CultureInfo.CurrentCulture) + ".txt", message);
		}

		public void StopTimer()
		{
			Timer.Elapsed -= TimerOnElapsed;
			Timer.Enabled = false;
		}

		public async Task UpdateStatus()
		{
			HttpResponseMessage? result = null;
			bool replaced = false;
			try
			{
				result = await Client.GetAsync(new Uri("https://twitchplayspokemon.tv/api/run_status")).ConfigureAwait(true);
				string content = await result.Content.ReadAsStringAsync().ConfigureAwait(true);
				message = JToken.Parse(content).ToString(Formatting.Indented);
				if (!result.IsSuccessStatusCode)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"ERROR: Failed to update run_status: {result.StatusCode}: {content}");
					Console.ResetColor();
					result.Dispose();
					return;
				}

				if (Status != null)
				{
					OldStatus = Status;
					replaced = true;
				}

				Status = JsonConvert.DeserializeObject<RunStatus>(content);
			}
			catch (Exception e)
			{
				await Utils.ReportError(e, Program.Client).ConfigureAwait(false);
				if (replaced)
					Status = OldStatus;
				result?.Dispose();
				return;
			}

			if (Status.BattleKind == BattleKind.Wild && Status.EnemyParty != null && Status.EnemyParty.Count >= 1 && Status.EnemyParty[0].Species?.Name == "???")
			{
				Status = OldStatus;
				result.Dispose();
				await Task.Delay(1000).ConfigureAwait(true);
				await UpdateStatus().ConfigureAwait(true);
			}

			/*for (int j = 0; j < Program.Settings.BadgeNames.Length; j++)
			{
				Status.BadgesFlags.Add((Status.Badges & (int)Math.Pow(2, j)) != 0);
			}*/

			if (Status!.EnemyTrainers != null)
			{
				foreach (Trainer t in Status.EnemyTrainers)
				{
					if (t?.ClassName != null)
						t.ClassName = t.ClassName.Replace("πµ", "PkMn", StringComparison.InvariantCultureIgnoreCase);
				}
			}

			ProcessShedinja();

			result.Dispose();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool managed)
		{
			Timer.Dispose();
			Client.Dispose();
		}

		private void ProcessShedinja()
		{
			if (Status!.Party != null)
			{
				foreach (Pokemon mon in Status.Party)
				{
					if (mon.Species!.Id == 292) mon.PersonalityValue++;
				}
			}

			if (Status!.Daycare != null)
			{
				foreach (Pokemon mon in Status.Daycare)
				{
					if (mon.Species!.Id == 292) mon.PersonalityValue++;
				}
			}

			if (Status.PC?.Boxes != null)
			{
				foreach (List<Pokemon>? mons in Status.PC.Boxes.Select(x => x.BoxContents))
				{
					if (mons != null)
					{
						foreach (Pokemon mon in mons)
						{
							if (mon.Species!.Id == 292) mon.PersonalityValue++;
						}
					}
				}
			}
		}
	}
}
