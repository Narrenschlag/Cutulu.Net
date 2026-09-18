namespace Cutulu.Network;

using System.Net.Sockets;
using System.Net;

public static class IPvX
{
    /// <summary>
    /// Returns address
    /// </summary>
    public static IPAddress GetAddress(this TcpClient client) => ((IPEndPoint)client.Client.RemoteEndPoint).Address;

    /// <summary>
    /// Returns port
    /// </summary>
    public static int GetPort(this TcpClient client) => ((IPEndPoint)client.Client.RemoteEndPoint).Port;

    /// <summary>
    /// Returns full address:port string and each of them as out variable
    /// </summary>
    public static string GetAddressPort(this TcpClient client, out string address, out int port)
    {
        var full = GetAddressPort(client, out IPAddress _address, out port);
        address = _address.ToString();
        return full;
    }

    /// <summary>
    /// Returns full address:port string
    /// </summary>
    public static string GetAddressPort(this TcpClient client) => GetAddressPort(client, out IPAddress _, out _);

    /// <summary>
    /// Returns full address:port string and each of them as out variable
    /// </summary>
    public static string GetAddressPort(this TcpClient client, out IPAddress address, out int port)
    {
        var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;

        address = endpoint.Address;
        port = endpoint.Port;

        return $"{address}:{port}";
    }

    /// <summary>
    /// Returns ip address bytes
    /// </summary>
    public static byte[] IpAddressToByteArray(this string ipAddress)
    {
        // Try to parse the input as an IP address
        if (!IPAddress.TryParse(ipAddress, out IPAddress address))
        {
            throw new System.ArgumentException("Invalid IP address format", nameof(ipAddress));
        }

        // GetAddressBytes returns the address as a byte array
        // IPv4 will return 4 bytes, IPv6 will return 16 bytes
        return address.GetAddressBytes();
    }
}