#nullable enable

using System;
using System.Globalization;
using EmbedIO;
using Swan;
using Swan.Logging;

namespace StreamFeedBot.Web
{
	public class WebLogger : ILogger
	{
		public LogLevel LogLevel => LogLevel.Info;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool managed)
		{
		}

		public async void Log(LogMessageReceivedEventArgs logEvent)
		{
			if (logEvent != null)
			{
				if (Program.PrivateWriter != null)
				{
					Program.PrivateWriter
						.WriteLine(
							$"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)} {Enum.GetName(typeof(LogLevel), logEvent.MessageType)}: {logEvent.Message}");
					Program.PrivateWriter.Flush();
				}

				if ((logEvent.MessageType == LogLevel.Error || logEvent.MessageType == LogLevel.Fatal) && !(logEvent.Exception is HttpException))
					await Utils.ReportError(logEvent.Exception, Program.Client).ConfigureAwait(false);
			}
		}
	}
}
