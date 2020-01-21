#nullable enable

using StreamFeedBot.Web;

namespace StreamFeedBot
{
	public class Settings
	{
		public string? Token;
		public ulong[]? Channels;
		public string? OAuth;
		public string? RunName;
		public ulong ReportId;
		public ulong ReportServer;
		public ulong[]? SuperUsers;
		public bool WebOnly;
		public AnnounceSettings[]? AnnounceSettings;
		public WebSettings? WebSettings;
	}
}
