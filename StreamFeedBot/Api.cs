#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamFeedBot
{
	public class Api : IDisposable
	{
		public RunStatus? Status;
		public RunStatus? OldStatus;
		private string? message;

		public int Hour;

		public HttpClient Client = new HttpClient();

		public Api()
		{
			Client.DefaultRequestHeaders.Add("Accept", "application/json");
			Client.DefaultRequestHeaders.Add("OAuth-Token", Program.Settings!.OAuth);
			Hour = DateTime.UtcNow.Hour;
		}

		private void PostSnapshot()
		{
			if (!Directory.Exists("Snapshots"))
				Directory.CreateDirectory("Snapshots");
			if (!Directory.Exists(Path.Combine("Snapshots", Program.Settings?.RunName ?? "UntitledRun")))
				Directory.CreateDirectory(Path.Combine("Snapshots", Program.Settings?.RunName ?? "UntitledRun"));
			string date = DateTime.UtcNow.ToString("o", CultureInfo.CurrentCulture);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) //windows is weird
				date = date.Replace(":", "-", StringComparison.InvariantCultureIgnoreCase);
            File.WriteAllText($"Snapshots/{Program.Settings?.RunName ?? "UntitledRun"}/ApiSnapshot{date}.txt", message);
		}

		public async Task<bool> UpdateStatus()
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
					return false;
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
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Exception has occured: {e.Message}{Environment.NewLine}{e.StackTrace}");
				Console.ResetColor();
				if (replaced)
					Status = OldStatus;
				result?.Dispose();
				return false;
			}

			if (Status.BattleKind == BattleKind.Wild && Status.EnemyParty != null && Status.EnemyParty.Count >= 1 && Status.EnemyParty[0].Species?.Name == "???")
			{
				Status = OldStatus;
				result.Dispose();
				await Task.Delay(1000).ConfigureAwait(true);
				await UpdateStatus().ConfigureAwait(true);
			}

			if (Program.Badges != null)
			{
				for (int j = 0; j < Program.Badges.Count; j++)
				{
					Status!.BadgesFlags.Add((Status.Badges & (int) Math.Pow(2, j)) != 0);
				}
			}

			if (Status!.EnemyTrainers != null)
			{
				foreach (Trainer t in Status.EnemyTrainers)
				{
					if (t?.ClassName != null)
						t.ClassName = t.ClassName.Replace("πµ", "PkMn", StringComparison.InvariantCultureIgnoreCase);
				}
			}

			ProcessShedinja();

			if (DateTime.UtcNow.Hour != Hour)
			{
				PostSnapshot();
				Hour = DateTime.UtcNow.Hour;
			}

			result.Dispose();

			return true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool managed)
		{
			Client.Dispose();
		}

		private void ProcessShedinja()
		{
			if (Status!.Party != null)
			{
				foreach (Pokemon mon in Status.Party)
				{
					if (mon.Species!.NationalDex == 292) mon.PersonalityValue++;
				}
			}

			if (Status!.Daycare != null)
			{
				foreach (Pokemon mon in Status.Daycare)
				{
					if (mon.Species!.NationalDex == 292) mon.PersonalityValue++;
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
							if (mon.Species!.NationalDex == 292) mon.PersonalityValue++;
						}
					}
				}
			}
		}
	}
}
