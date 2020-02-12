#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Newtonsoft.Json;
using StreamFeedBot.Rulesets;

namespace StreamFeedBot.Web
{
	public class ApiController : WebApiController
	{
		public Memory Memory;
		public Dictionary<Pokemon, int> ReleasedDictionary;
		public Api Api;

		public ApiController(Memory memory, Dictionary<Pokemon, int> releasedDictionary, Api api)
		{
			Memory = memory;
			ReleasedDictionary = releasedDictionary;
			Api = api;
		}

		[Route(HttpVerbs.Get, "/status")]
		public async Task GetStatus()
		{
			BotStatus status = new BotStatus
			{
				Memory = Memory,
				Released = ReleasedDictionary.Keys.ToList(),
				RunStatus = Api.Status
			};
			await HttpContext.SendStringAsync(JsonConvert.SerializeObject(status, Formatting.Indented), "application/json", Encoding.UTF8).ConfigureAwait(false);
		}
	}
}
