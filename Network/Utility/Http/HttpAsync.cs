namespace Cutulu.Network;

using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System;

/// <summary>
/// HTTP request based web request system for fast and easy usage
/// </summary>
public class HttpAsync
{
    public delegate void Result(bool success, string result, object[] given);

    private static readonly HttpClient Client = new();

    public static int TimeoutMilliseconds
    {
        get => (int)Client.Timeout.TotalMilliseconds;
        set => Client.Timeout = TimeSpan.FromMilliseconds(value);
    }

    private readonly Result Receive;
    private readonly object[] Given;

    /// <summary>
    /// Request data from an url.
    /// </summary>
    public HttpAsync(string url, Result result = null, params object[] given)
        : this(result, given)
    {
        _ = _SendAsync(url, null, null);
    }

    /// <summary>
    /// Request data from an url by headers.
    /// </summary>
    public HttpAsync(string url, string[] headers, Result result, params object[] given)
        : this(url, "", headers, result, given) { }

    /// <summary>
    /// Request data from an url by json.
    /// </summary>
    public HttpAsync(string url, string json, Result result, params object[] given)
        : this(url, json, null, result, given) { }

    /// <summary>
    /// Request data from an url by headers and json.
    /// </summary>
    public HttpAsync(string url, string json, string[] headers, Result result = null, params object[] given)
        : this(result, given)
    {
        _ = _SendAsync(url, json, headers);
    }

    /// <summary>
    /// Base constructor for web requests
    /// </summary>
    private HttpAsync(Result result, params object[] given)
    {
        Receive = result;
        Given = given;
    }

    /// <summary>
    /// Performs the request and waits until completion
    /// </summary>
    public static async Task<(bool success, string body)> SendAsync(string url, string json = null, string[] headers = null, params object[] given)
    {
        bool complete = false;
        bool success = false;
        string body = null;

        Result result = (bool _success, string _body, object[] _given) =>
        {
            complete = true;
            success = _success;
            body = _body;
        };

        var http = new HttpAsync(result, given);

        await http._SendAsync(url, json, headers);

        while (complete == false)
            await Task.Delay(10);

        return (success, body);
    }

    /// <summary>
    /// Performs the request and invokes the callback function on completion
    /// </summary>
    private async Task _SendAsync(string url, string json, string[] headers)
    {
        bool success;
        string body;

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

            success = response.IsSuccessStatusCode;
            body = Encoding.UTF8.GetString(bytes).Trim();
        }
        catch (Exception ex)
        {
            success = false;
            body = ex.Message;
        }

        Receive?.Invoke(success, body, Given);
    }
}