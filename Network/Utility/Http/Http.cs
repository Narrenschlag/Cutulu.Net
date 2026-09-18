namespace Cutulu.Network;

using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System;

/// <summary>
/// HTTP request based web request system for fast and easy usage
/// </summary>
public class Http
{
    private static readonly HttpClient Client = new();

    public bool Success { get; private set; }
    public string Body { get; private set; }

    /// <summary>
    /// Blocking GET/POST request. Returns once complete.
    /// </summary>
    public Http(string url, string json = null, string[] headers = null)
    {
        var (success, body) = Task.Run(() => SendAsync(url, json, headers))
                                   .GetAwaiter()
                                   .GetResult();

        Success = success;
        Body = body;
    }

    private static async Task<(bool Success, string Body)> SendAsync(string url, string json, string[] headers)
    {
        try
        {
            using var message = new HttpRequestMessage(
                string.IsNullOrEmpty(json) ? HttpMethod.Get : HttpMethod.Post,
                url);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    var split = header.Split(':', 2);
                    if (split.Length == 2)
                    {
                        message.Headers.TryAddWithoutValidation(split[0].Trim(), split[1].Trim());
                    }
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                message.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var response = await Client.SendAsync(message).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            return (response.IsSuccessStatusCode, Encoding.UTF8.GetString(bytes).Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Performs the request and waits until completion
    /// </summary>
    public static async Task<(bool success, string body)> SendAsync(string url, string json = null, string[] headers = null, params object[] given)
    => await HttpAsync.SendAsync(url, json, headers, given);
}