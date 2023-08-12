using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace RushGet.Function
{
    public class TurboHub
    {
        private readonly ILogger _logger;

        public TurboHub(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TurboHub>();
        }

        /// <summary>
        /// check uri should be release or zip code
        /// template:
        /// https://github.com/${org}/${project}/archive/refs/heads/${branch}.zip
        /// https://github.com/${org}/${project}/releases/download/${tag}/${file}
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<Regex> UriCheckRegexes()
        {
            yield return new Regex(
                @"^https://github.com/.+/.+/archive/refs/heads/.+.zip$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
            yield return new Regex(
                @"^https://github.com/.+/.+/releases/download/.+/.+$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
        }

        [Function("TurboHub")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
            HttpRequestData req)
        {
            // get uri parameters from query string
            // send get request to uri and return the response to the client
            var uri = req.Query["uri"];
            if (!string.IsNullOrWhiteSpace(uri))
            {
                _logger.LogInformation("uri: {Uri}", uri);
                bool matched = false;
                foreach (var regex in UriCheckRegexes())
                {
                    if (regex.IsMatch(uri))
                    {
                        matched = true;
                        _logger.LogInformation("uri matched rule: {Rule}", regex.ToString());
                    }
                }

                if (!matched)
                {
                    _logger.LogInformation("uri not matched any rule");
                    var rep = req.CreateResponse(HttpStatusCode.BadRequest);
                    await rep.WriteStringAsync("uri not matched any rule");
                    return rep;
                }

                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Success to get response from uri");
                    var rep = req.CreateResponse(HttpStatusCode.OK);
                    rep.Headers.Add("Content-Type", "application/octet-stream");
                    // get last part of uri as file name
                    var fileName = uri.Split('/').Last();
                    rep.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");
                    await using var steam = await response.Content.ReadAsStreamAsync();
                    await steam.CopyToAsync(rep.Body);
                    return rep;
                }
                else
                {
                    _logger.LogInformation("Failed to get response from uri");
                    var rep = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await rep.WriteStringAsync("Failed to get response from uri");
                    return rep;
                }
            }
            else
            {
                _logger.LogInformation("uri is null or empty");
                var response = req.CreateResponse(HttpStatusCode.NotFound);
                await response.WriteStringAsync("Please pass a name on the query string");
                return response;
            }
        }
    }
}