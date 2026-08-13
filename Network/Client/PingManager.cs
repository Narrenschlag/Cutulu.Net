namespace Cutulu.Network
{
    using System.Threading.Tasks;
    using System;

    using Sockets;
    using Core;

    public static class PingManager
    {
        public static async Task<(bool Success, byte[] Buffer)> Ping(string address, int port)
        {
            var socket = new TcpSocket();

            if (await socket.Connect(address, port))
            {
                await socket.SendAsync([(byte)ConnectionTypeEnum.Ping]);

                try
                {
                    var length = await socket.Receive(4);

                    if (length.Success)
                    {
                        var buffer = await socket.Receive(length.Buffer.Decode<int>());

                        socket.Close();
                        return (true, buffer.Buffer);
                    }
                }

                catch { }
            }

            socket.Close();
            return (false, Array.Empty<byte>());
        }
    }
}