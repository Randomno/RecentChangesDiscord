using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RecentChangesDiscord
{
	public class WikiResponse
	{
		public WikiQuery? Query { get; set; }
	}
	public class WikiQuery
	{
		public List<WikiChange>? RecentChanges { get; set; }
	}
	public class WikiChange
	{
		public string? Type { get; set; }
		public string? Title { get; set; }
		public long Rcid { get; set; }
		public string? User { get; set; }
		public string? Comment { get; set; }
		public string? LogType { get; set; }
		public string? LogAction { get; set; }
		public LogParams? LogParams { get; set; }
	}

	public class LogParams
	{
		[JsonPropertyName("img_sha1")]
		public string? ImgSha1 { get; set; }
	}
}
