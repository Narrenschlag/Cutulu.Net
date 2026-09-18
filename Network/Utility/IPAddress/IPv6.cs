namespace Cutulu.Network;

using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

public static class IPv6
{
    /// <summary>
    /// Standard loopback address pointing to your own machine.
    /// </summary>
    public const string LocalAddress = "::1";

    /// <summary>
    /// The network address of the local computer in your network. Also known as LAN address
    /// </summary>
    public static IPAddress NetworkAddress
    {
        get
        {
            // Get all network interfaces
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface iface in interfaces)
            {
                // Filter out loopback and non-operational interfaces
                if (iface.NetworkInterfaceType != NetworkInterfaceType.Ethernet ||
                    iface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                // Get IPv6 addresses for the selected interface
                foreach (UnicastIPAddressInformation ip in iface.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6 && !ip.Address.IsIPv6LinkLocal && !ip.Address.IsIPv6SiteLocal)
                    {
                        return ip.Address;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Opens a web request. If connected to the internet it will return your global IPAddress. Will freeze the process until complete. Use async version for ui applications.
    /// </summary>
    public static string InternetAddress
    {
        get
        {
            var http = new Http("https://api6.ipify.org/");
            return http.Success ? http.Body : "<error>";
        }
    }

    /// <summary>
    /// Opens a web request. If connected to the internet it will return your global IPAddress
    /// </summary>
    public static void GetInternetAddressAsync(Godot.Node node, HttpAsync.Result result)
    {
        _ = new HttpAsync("https://api6.ipify.org/", result);
    }

    /// <summary>
    /// Opens a web request. If connected to the internet it will return your global IPAddress
    /// </summary>
    public static async Task<string> GetInternetAddressAsync(Godot.Node node)
    {
        var result = await HttpAsync.SendAsync("https://api6.ipify.org/");
        return result.success ? result.body : "<error>";
    }
}