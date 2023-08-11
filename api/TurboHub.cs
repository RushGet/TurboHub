using System.Net;
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

        [Function("TurboHub")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            // get uri parameters from query string
            // send get request to uri and return the response to the client
            var uri = req.Query["uri"];
            if (!string.IsNullOrWhiteSpace(uri))
            {
                _logger.LogInformation("uri: {Uri}", uri);
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