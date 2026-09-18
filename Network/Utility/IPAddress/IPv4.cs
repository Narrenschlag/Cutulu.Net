namespace Cutulu.Network;

using System.Threading.Tasks;
using System.Net.Sockets;
using System.Linq;
using System.Net;

public static class IPv4
{
    /// <summary>
    /// Standard loopback address pointing to your own machine.
    /// </summary>
    public const string LocalAddress = "127.0.0.1";

    /// <summary>
    /// The network address of the local computer in your network. Also known as LAN address
    /// </summary>
    public static IPAddress NetworkAddress
    {
        get
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.FirstOrDefault(ip =>
                ip.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(ip)
            );
        }
    }

    /// <summary>
    /// Opens a web request. If connected to the internet it will return your global IPAddress. Will freeze the process until complete. Use async version for ui applications.
    /// </summary>
    public static string InternetAddress
    {
        get
        {
            var http = new Http("https://ipinfo.io/ip");
            return http.Success ? http.Body : "<error>";
        }
    }

    /// <summary>
    /// Opens a web request. If connected to the internet it will return your global IPAddress
    /// </summary>
    public static void GetInternetAddressAsync(Godot.Node node, HttpAsync.Result result)
    {
        _ = new HttpAsync("https://ipinfo.io/ip", result);
    }

    /// <summary>
    /// Opens a web request. If connected to the internet it will return your global IPAddress
    /// </summary>
    public static async Task<string> GetInternetAddressAsync(Godot.Node node)
    {
        var result = await HttpAsync.SendAsync("https://ipinfo.io/ip");
        return result.success ? result.body : "<error>";
    }
}