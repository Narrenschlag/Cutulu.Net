namespace Cutulu.Network
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Net;
    using System;

    using Protocols;
    using Core;

    public partial class Connection(long uid, HostManager host, Sockets.TcpSocket socket, IPEndPoint endpoint) : ITagable
    {
        public Sockets.TcpSocket Socket { get; private set; } = socket;
        public IPEndPoint EndPoint { get; private set; } = endpoint;
        public HostManager Host { get; private set; } = host;

        public long UserId { get; private set; } = uid;

        /// <summary> Keep in mind to lock(_listenerLock) for modification. This is not thread safe by default. </summary>
        public readonly HashSet<IListener> Listeners = [];
        public readonly object _listenerLock = new();

        public bool IsConnected => Socket != null && Socket.IsConnected;
        long ITagable.GetUniqueTagID() => UserId;

        public event Action<short, byte[]> Received;

        /// <summary>
        /// Kicks/Cancels connection from host side.
        /// </summary>
        public virtual bool Kick()
        {
            try
            {
                Debug.Log($"Kicked {GetType().Name}[{UserId}]");
                Socket.Close();
                return true;
            }

            catch (Exception ex)
            {
                Debug.LogR($"[color=indianred]Failed to kick connection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends data to client.
        /// </summary>
        public virtual void Send(short key, object obj, bool reliable = true)
        {
            if (IsConnected)
            {
                var packet = PacketProtocol.Pack(key, obj, out var length);

                if (reliable) Socket?.Send(length.Encode(), packet);
                else Host.UdpHost.Listener?.Send([EndPoint], packet);
            }
        }

        /// <summary>
        /// Sends data to client async.
        /// </summary>
        public virtual async Task SendAsync(short key, object obj, bool reliable = true)
        {
            if (IsConnected)
            {
                var packet = PacketProtocol.Pack(key, obj, out var length);

                if (reliable) await Socket?.SendAsync(length.Encode(), packet);
                else await Host.UdpHost.Listener?.SendAsync(EndPoint, packet);
            }
        }

        /// <summary>
        /// Receive event, called by client.
        /// </summary>
        public virtual void ReceiveBuffer(byte[] buffer)
        {
            if (PacketProtocol.Unpack(buffer, out var key, out var unpackedBuffer))
            {
                // First let the host read the packet
                lock (Host._trafficLock)
                {
                    if (Host.ReadPacket(this, key, unpackedBuffer))
                        return;
                }

                // Host didn't consume the packet, let the listeners read it
                lock (_listenerLock)
                {
                    LocalDecoder decoder = new(unpackedBuffer);

                    foreach (var _listener in Listeners)
                    {
                        decoder.ResetPosition();

                        if ((bool)(_listener?._Receive(key, decoder))) return;
                    }

                    Received?.Invoke(key, unpackedBuffer);
                }

                lock (Host._trafficLock)
                {
                    Host.Received?.Invoke(this, key, unpackedBuffer);
                }
            }
        }
    }
}