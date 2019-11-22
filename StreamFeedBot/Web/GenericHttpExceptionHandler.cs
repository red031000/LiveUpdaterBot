#nullable enable

using System.Net;
using System.Threading.Tasks;
using EmbedIO;

namespace StreamFeedBot.Web
{
	public static class GenericHttpExceptionHandler
	{
		public static Task Handler(IHttpContext context, IHttpException? httpexception)
		{
			if (httpexception == null)
				httpexception = new HttpException(HttpStatusCode.InternalServerError);
			return context.SendStandardHtmlAsync(httpexception.StatusCode,
				async writer =>
				{
					await writer.WriteAsync($"<img src=\"https://http.cat/{httpexception.StatusCode}\">")
						.ConfigureAwait(false);
				});
		}
	}
}
