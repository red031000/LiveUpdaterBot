using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;

namespace StreamFeedBot
{
	public class Api
	{
		public RunStatus Status;
		public RunStatus OldStatus;
		private string message;

		private readonly Timer timer;

		public HttpClient client = new HttpClient();

		public Api()
		{
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.DefaultRequestHeaders.Add("OAuth-Token", Program.Settings.OAuth);
			timer = new Timer
			{
				AutoReset = true,
				Interval = 3.6e+6
			};
			timer.Elapsed += TimerOnElapsed;
			timer.Enabled = true;
		}

		private void TimerOnElapsed(object sender, ElapsedEventArgs e)
		{
			if (!Directory.Exists("Snapshots"))
				Directory.CreateDirectory("Snapshots");
			File.WriteAllText("Snapshots/ApiSnapshot" + DateTime.UtcNow.ToString("o") + ".txt", message);
		}

		public void StopTimer()
		{
			timer.Elapsed -= TimerOnElapsed;
			timer.Enabled = false;
		}

		public async Task UpdateStatus()
		{
			HttpResponseMessage result = null;
			bool replaced = false;
			try
			{
				result = await client.GetAsync("https://twitchplayspokemon.tv/api/run_status");
				string content = await result.Content.ReadAsStringAsync();
				message = content;
				if (!result.IsSuccessStatusCode)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"ERROR: Failed to update run_status: {result.StatusCode}: {content}");
					Console.ForegroundColor = ConsoleColor.White;
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
				await Utils.ReportError(e, Program.Client);
				if (replaced)
					Status = OldStatus;
				result?.Dispose();
				return;
			}

			/*if (Status.Seen == 0) //TODO temp fix
			{
				Status = OldStatus;
				result.Dispose();
				return; //Game hasn't loaded yet
			}*/

			if (Status.BattleKind == BattleKind.Wild && Status.EnemyParty != null && Status.EnemyParty.Count >= 1 && Status.EnemyParty[0].Species.Name == "???")
			{
				Status = OldStatus;
				result.Dispose();
				await Task.Delay(1000);
				await UpdateStatus();
			}

			for (int j = 0; j < Program.Settings.BadgeNames.Length; j++)
			{
				Status.BadgesFlags.Add((Status.Badges & (int)Math.Pow(2, j)) != 0);
			}

			if (Status.EnemyTrainers != null)
			{
				foreach (Trainer t in Status.EnemyTrainers)
				{
					if (t?.ClassName != null)
						t.ClassName = t.ClassName.Replace("πµ", "PkMn");
				}
			}

			result.Dispose();
		}
	}
}
