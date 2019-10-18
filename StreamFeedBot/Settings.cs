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
		public string[]? BadgeNames;
		public ulong ReportId;
		public ulong ReportServer;
		public ulong[]? SuperUsers;
		public AnnounceSettings[]? AnnounceSettings;
		public WebSettings? WebSettings;
	}
}
