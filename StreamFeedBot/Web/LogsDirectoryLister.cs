#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO.Files;
using EmbedIO.Utilities;

namespace StreamFeedBot.Web
{
	public class LogsDirectoryLister : IDirectoryLister
	{
		public string ContentType { get; } = "text/html; encoding=" + Encoding.UTF8.WebName;

		public async Task ListDirectoryAsync(
		  MappedResourceInfo info,
		  string absoluteUrlPath,
		  IEnumerable<MappedResourceInfo> entries,
		  Stream stream,
		  CancellationToken cancellationToken)
		{
			if (info?.IsDirectory != true)
				throw new ArgumentException("HtmlDirectoryLister.ListDirectoryAsync invoked with a file, not a directory.");
			string str = WebUtility.HtmlEncode(absoluteUrlPath);
			await using StreamWriter text = new StreamWriter(stream, Encoding.UTF8);
			text.Write("<html><head><title>Index of ");
			text.Write(str);
			text.Write("</title></head><body><h1>Index of ");
			text.Write(str);
			text.Write("</h1><hr/><pre>");
			if (str?.Length > 1)
				text.Write("<a href='../'>../</a>\n");
			entries = entries.ToArray();
			foreach (MappedResourceInfo mappedResourceInfo in entries.Where(m => m.IsDirectory).OrderBy(e => e.Name))
			{
				text.Write(
					$"<a href=\"{(!string.IsNullOrEmpty(str)? str + $"{Path.DirectorySeparatorChar}" : "")}{Uri.EscapeDataString(mappedResourceInfo.Name)}{Path.DirectorySeparatorChar}\">{WebUtility.HtmlEncode(mappedResourceInfo.Name)}</a>");
				text.Write(new string(' ', Math.Max(1, 50 - mappedResourceInfo.Name.Length + 1)));
				text.Write(HttpDate.Format(mappedResourceInfo.LastModifiedUtc));
				text.Write('\n');
				await Task.Yield();
			}
			foreach (MappedResourceInfo mappedResourceInfo in entries.Where(m => m.IsFile).OrderBy(e => e.Name))
			{
				text.Write(
					$"<a href=\"{(!string.IsNullOrEmpty(str) ? str + $"{Path.DirectorySeparatorChar}" : "")}{Uri.EscapeDataString(mappedResourceInfo.Name)}{Path.DirectorySeparatorChar}\">{WebUtility.HtmlEncode(mappedResourceInfo.Name)}</a>");
				text.Write(new string(' ', Math.Max(1, 50 - mappedResourceInfo.Name.Length + 1)));
				text.Write(HttpDate.Format(mappedResourceInfo.LastModifiedUtc));
				text.Write(" {0,-20}\n", mappedResourceInfo.Length.ToString("#,###", CultureInfo.InvariantCulture));
				await Task.Yield();
			}
			text.Write("</pre><hr/></body></html>");
		}

		public Task ListDirectoryAsync(MappedResourceInfo info, Uri absoluteUrlPath, IEnumerable<MappedResourceInfo> entries, Stream stream, CancellationToken cancellationToken)
		{
			if (absoluteUrlPath == null)
				throw new ArgumentException("URI cannot be null", nameof(absoluteUrlPath));
			return ListDirectoryAsync(info, absoluteUrlPath.ToString(), entries, stream, cancellationToken);
		}
	}
}
