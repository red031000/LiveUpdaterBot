using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
				Console.ForegroundColor = ConsoleColor.White;
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
		}
	}
}
