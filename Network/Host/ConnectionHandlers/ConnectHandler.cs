namespace Cutulu.Network;

using System.Threading.Tasks;
using Cutulu.Network.Sockets;
using System.Net;
using Core;

public class ConnectHandler(byte key) : ConnectionHandler(key)
{
    // Receives ClientManager's SEND_CONNECT
    public override async Task<(bool Status, object Data)> Validate(IConnectionWrapper _wrapper, TcpSocket socket)
    {
        if (_wrapper is not IConnectWrapper wrapper) return (false, null);

        var (Success, Buffer) = await socket.Receive(4);

        //Debug.Log($"Received connection type [{Success}].");

        if (Success == false) return (false, null);

        await socket.SendAsync(true.Encode());

        var connection = wrapper.CreateConnection(socket, Buffer);

        // Check if the client is still connected
        try
        {
            await socket.SendAsync(connection.UserId.Encode());
        }
        catch
        {
            Debug.LogError($"Socket closed session remotely. Aboarting onboarding process.");
            return (false, null);
        }

        // -> Moved to wrapper.AssignConnection()
        // Remove already connected connections with same address
        //if (wrapper.Manager.ConnectionsByUdp.TryGetValue(connection.EndPoint, out var existingConnection))
        //    wrapper.InvokeDisconnect(existingConnection.Socket);

        // Stop connection if max client limit has been reached
        if (wrapper.GetMaxClientCount() > 0 && wrapper.GetConnectionCount() >= wrapper.GetMaxClientCount())
        {
            Debug.LogError($"Maximum client capacity has been reached. Cancelling connection.");
            return (false, null);
        }

        wrapper.AssignConnection(connection, socket);

        //Debug.LogR($"[color=magenta][b][ConnectHandler][/b][/color] New connection: {connection.UserId}");
        await socket.ClearBuffer();
        return (true, connection);
    }

    public override async Task Handle(IConnectionWrapper wrapper, TcpSocket socket, object data)
    {
        if (data.IsNull() || data is not Connection connection) return;
        //Debug.LogR($"[color=magenta][b][ConnectHandler][/b][/color] Handed connection: {connection.UserId}");

        wrapper.InvokeConnect(connection);

        var lastServerSend = System.DateTime.UtcNow;

        Debug.LogR($"[b][color=seagreen]Connection established and validated[/color]: client@{connection.UserId}");

        while (active())
        {
            if ((System.DateTime.UtcNow - lastServerSend).TotalSeconds > 25)
            {
                await connection.Socket.SendAsync(1.Encode(), [0xFF]);
                lastServerSend = System.DateTime.UtcNow;
            }

            var packet = await connection.Socket.Receive(4);

            if (packet.Success == false || active() == false) continue;

            var length = packet.Buffer.Decode<int>();
            packet = await connection.Socket.Receive(length);

            if (packet.Success == false || active() == false) continue;

            // Is Heartbeat
            if (length == 1 && packet.Buffer[0] == 0xFF) continue;

            connection.ReceiveBuffer(packet.Buffer);
        }

        bool active() => connection != null && connection.Socket != null && connection.Socket.IsConnected;

        //Debug.LogR($"[color=indianred][i]Connection has been closed. [color=darkgray](Connection:{connection.NotNull()}, Socket:{connection.Socket.NotNull()} [{connection?.Socket?.IsConnected.ToString() ?? "<null>"}])");

        // Close connection
        if (connection.Kick() == false) socket?.Close();
    }
}