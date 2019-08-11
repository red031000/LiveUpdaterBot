using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LiveUpdaterBot
{
	public class Api
	{
		public RunStatus Status;
		public RunStatus OldStatus;

		public HttpClient client = new HttpClient();

		public Api()
		{
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.DefaultRequestHeaders.Add("OAuth-Token", Program.Settings.OAuth);
		}

		public async Task UpdateStatus()
		{
			HttpResponseMessage result = await client.GetAsync("https://twitchplayspokemon.tv/api/run_status");
			string content = await result.Content.ReadAsStringAsync();
			if (!result.IsSuccessStatusCode)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"ERROR: Failed to update run_status: {result.StatusCode}: {content}");
				await Program.LogWriter.WriteLineAsync(
					$"ERROR: Failed to update run_status: {result.StatusCode}: {content}");
				await Program.LogWriter.FlushAsync();
				Console.ForegroundColor = ConsoleColor.White;
				result.Dispose();
				return;
			}

			if (Status != null)
				OldStatus = Status;

			Status = JsonConvert.DeserializeObject<RunStatus>(content);

			int j = 0;
			foreach (string _ in Program.Settings.BadgeNames)
			{
				Status.BadgesFlags.Add((Status.Badges & (int)Math.Pow(2, j)) != 0);
				j++;
			}

			if (Status.EnemyTrainers != null)
			{
				foreach (Trainer t in Status.EnemyTrainers)
				{
					t.ClassName = t.ClassName.Replace("πµ", "PkMn");
				}
			}

			result.Dispose();
		}
	}
}
