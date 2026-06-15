using System.Net.Http.Json;
using System.Text.Json;

namespace RecentChangesDiscord
{

	internal static class JsonConfig
	{
		public static JsonSerializerOptions Options = new()
		{
			PropertyNameCaseInsensitive = true,
		};
	}
	public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _http = new();
		private long _latestRcid = 0;
		private bool firstRun = true;

		private readonly string webhookUrl = File.ReadAllText("webhook.txt").Trim();
		private readonly string wikiUrl = File.ReadAllText("wiki.txt").Trim();
		private readonly string wikiRoot;

		public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
			_http.DefaultRequestHeaders.UserAgent.ParseAdd("RecentChangesDiscord/1.0");
			wikiRoot = new Uri(wikiUrl).GetLeftPart(UriPartial.Authority);
		}

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
				WikiResponse? wikiResponse;

				try
				{
					wikiResponse = await _http.GetFromJsonAsync<WikiResponse>(
						wikiUrl + "/api.php?action=query&format=json&list=recentchanges&rclimit=50&rcprop=title|timestamp|user|comment|loginfo|ids",
						JsonConfig.Options, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex.ToString());
					continue;
				}

				if (wikiResponse?.Query?.RecentChanges is null)
				{
					continue;
				}
				var recentChanges = wikiResponse.Query.RecentChanges;
				recentChanges.Reverse();

				if (firstRun)
				{
					_latestRcid = recentChanges[^1].Rcid;
					firstRun = false;
					await Task.Delay(5000, stoppingToken);
					continue;
				}

				foreach (var rc in recentChanges)
				{
					if (rc.Rcid <= _latestRcid)
						continue;

					_latestRcid = rc.Rcid;
					string escTitle = Uri.EscapeDataString(rc.Title!);


					string message = "";
					switch (rc.Type, rc.LogType, rc.LogAction)
					{
						case ("edit", _, _):
							message += $"{rc.User} edited page [{rc.Title}]({wikiUrl}/index.php?diff={rc.RevId})";
							break;
						case ("new", _, _):
							message += $"{rc.User} created page [{rc.Title}]({wikiRoot}/wiki/{escTitle})";
							break;
						case ("log", "upload", "upload"):
							message += $":new: {rc.User} uploaded [{rc.Title}]({wikiRoot}/wiki/{escTitle})";
							break;
						case ("log", "upload", "overwrite"):
							message += $"{rc.User} uploaded a new version of [{rc.Title}]({wikiRoot}/wiki/{escTitle})";
							break;
						case ("log", "upload", "revert"):
							message += $"{rc.User} reverted [{rc.Title}]({wikiRoot}/wiki/{escTitle}) to an old version";
							break;
						case ("log", "delete", _):
							message += $"{rc.User} deleted page {rc.Title}";
							break;
						case ("categorize", _, _):
							continue;
						default:
							_logger.LogInformation("unknown log type: {type}, {logtype}, {logaction}", rc.Type, rc.LogType, rc.LogAction);
							message += $"{rc.User} edited page [{rc.Title}]({wikiRoot}/wiki/{escTitle})";
							break;
					}

					if (!string.IsNullOrEmpty(rc.LogParams?.ImgSha1))
					{
						message += $"\n-# {rc.LogParams.ImgSha1}";
					}

					if (!string.IsNullOrEmpty(rc.Comment))
					{
						message += $"\n`{rc.Comment}`";
					}


					// message += $" -- {rc.Rcid}";

					// _logger.LogTrace("{message}\n", message);

					// flag 4 = suppress embeds
					var webhookResponse = await _http.PostAsJsonAsync(webhookUrl, new { content = message, flags = 4 }, stoppingToken);
					if (!webhookResponse.IsSuccessStatusCode) 
						_logger.LogWarning("Webhook POST failed: {status}", webhookResponse.StatusCode);

					await Task.Delay(500, stoppingToken);

					/*
					if (_logger.IsEnabled(LogLevel.Information))
					{
						_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
					}
					*/
				}

				
				await Task.Delay(5000, stoppingToken);
			}
        }
    }
}
