namespace Cutulu.Network
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Net;
    using System;

    using Protocols;
    using Sockets;
    using Core;

    public partial class ClientManager
    {
        /// <summary> Keep in mind to lock(_listenerLock) for modification. This is not thread safe by default. </summary>
        public readonly HashSet<IListener> Listeners = [];
        public readonly object _listenerLock = new();

        public readonly TcpSocket TcpClient;
        public readonly UdpSocket UdpClient;

        public int ConnectionTimeout = 5000, ValidationTimeout = 0, TcpPort, UdpPort;
        private byte ThreadIdx;
        public string Address;

        public long UserID { get; private set; }

        public bool IsConnected => TcpClient != null && TcpClient.IsConnected && Validation == VALIDATION.COMPLETE;
        public VALIDATION Validation { get; private set; } = VALIDATION.INVALID;

        public Action<short, byte[]> Received;
        public Action Connected, Disconnected;

        public ClientManager()
        {
            TcpClient = new()
            {
                Connected = ConnectEvent,
                Disconnected = DisconnectEvent,
            };

            UdpClient = new();
        }

        public ClientManager(string address, int tcpPort, int udpPort) : this()
        {
            Address = address;
            TcpPort = tcpPort;
            UdpPort = udpPort;
        }

        ~ClientManager() => Stop(16);

        #region Callable Functions

        /// <summary>
        /// Starts client.
        /// </summary>
        public virtual async Task<bool> Start(int retryCount = 5, int retryDelay = 2000)
        {
            Stop(11);

            Debug.Log($"Started client connection to {Address}:{TcpPort}:{UdpPort}");

            Validation = VALIDATION.INVALID;
            ThreadIdx++;

            await UdpClient.Connect(Address, UdpPort);
            await TcpClient.Connect(Address, TcpPort, ConnectionTimeout);

            // Validation timeout
            if (TcpClient.IsConnected && Validation == VALIDATION.IN_PROGRESS)
            {
                var _token = TcpClient.Token;

                // Wait until validated or timeout
                if (ValidationTimeout > 0)
                {
                    var _timeout = ValidationTimeout;

                    // Wait until timed out or validation is not in progress
                    while (_timeout-- > 0 && Validation == VALIDATION.IN_PROGRESS && _token.IsCancellationRequested == false)
                    {
                        await Task.Yield();
                    }
                }

                // Wait until validation is not in progress
                else
                {
                    while (Validation == VALIDATION.IN_PROGRESS && _token.IsCancellationRequested == false)
                    {
                        await Task.Yield();
                    }
                }
            }

            if (IsConnected == false)
            {
                // Retry connection
                if (retryCount-- > 0)
                {
                    await Task.Delay(retryDelay);
                    return await Start(retryCount, retryDelay);
                }

                // Failed to connect
                else
                {
                    Stop(12);
                    return false;
                }
            }

            Debug.Log($"Connected client to ip:{Address} tcp:{TcpPort}:{((IPEndPoint)TcpClient.Socket.LocalEndPoint).Port} udp:{UdpPort}:{((IPEndPoint)UdpClient.Socket.LocalEndPoint).Port}");
            return true;
        }

        /// <summary>
        /// Stops client.
        /// </summary>
        public virtual void Stop(byte exitCode = 0)
        {
            Debug.Log($"Stopped client connection to {Address}:{TcpPort}:{UdpPort} [exitCode={exitCode}]");
            Validation = VALIDATION.INVALID;

            TcpClient.Disconnect(exitCode);
            UdpClient.Disconnect(exitCode);
        }

        /// <summary>
        /// Sends data to host.
        /// </summary>
        public virtual bool Send(short key, object obj, bool reliable = true)
        {
            if (IsConnected)
            {
                var packet = PacketProtocol.Pack(key, obj, out var length);

                if (reliable) return TcpClient.Send(length.Encode(), packet);
                else UdpClient.Send(packet);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends data to host async.
        /// </summary>
        public virtual async Task SendAsync(short key, object obj, bool reliable = true)
        {
            if (IsConnected)
            {
                var packet = PacketProtocol.Pack(key, obj, out var length);

                if (reliable) await TcpClient.SendAsync(length.Encode(), packet);
                else await UdpClient.SendAsync(packet);
            }
        }

        /// <summary>
        /// Receive event, called by ReceiveBuffer.
        /// </summary>
        public virtual bool ReadPacket(short key, byte[] buffer) => false;

        /// <summary>
        /// Receive event, called by client.
        /// </summary>
        protected virtual void ReceiveBuffer(byte[] _packet_buffer)
        {
            if (PacketProtocol.Unpack(_packet_buffer, out var _key, out var _buffer))
            {
                // First let the client read the packet
                if (ReadPacket(_key, _buffer)) return;

                using LocalDecoder localDecoder = new(_buffer);

                // Host didn't consume the packet, let the listeners read it
                IListener[] listeners = [.. Listeners];
                foreach (var _listener in listeners)
                {
                    localDecoder.ResetPosition();

                    if (_listener._Receive(_key, localDecoder)) return;
                }

                // No one consumed the packet, let the events read it
                Received?.Invoke(_key, _buffer);
            }
        }

        #endregion

        #region Event Handlers

        private async void ConnectEvent(TcpSocket socket)
        {
            await socket.SendAsync([(byte)ConnectionTypeEnum.Connect], UdpClient.GetLocalEndpoint().Port.Encode());
            Debug.Log($"Sent CONNECT({(byte)ConnectionTypeEnum.Connect}) packet to {socket.Socket.RemoteEndPoint}. Waiting for response.");
            Validation = VALIDATION.IN_PROGRESS;

            var (Success, Buffer) = await socket.Receive(1);
            if (Success == false || Buffer[0] != 1)
            {
                Debug.LogR($"[color=indianred][{GetType().Name}] Failed to connect to host.");

                Stop(13);
                return;
            }

            (Success, Buffer) = await socket.Receive(8);
            if (Success == false)
            {
                Debug.LogR($"[color=indianred][{GetType().Name}] Failed to read UID.");

                Stop(14);
                return;
            }

            UserID = Buffer.Decode<long>();
            Validation = VALIDATION.COMPLETE;

            lock (this) Connected?.Invoke();
            ReceiveData();
        }

        private void DisconnectEvent(TcpSocket socket)
        {
            lock (this) Disconnected?.Invoke();
        }

        /// <summary>
        /// Defines the logic to receive data from host.
        /// </summary>
        protected virtual async void ReceiveData()
        {
            if (TcpClient.IsConnected == false) return;
            var threadIdx = ThreadIdx;

            udp();
            async void udp()
            {
                var packet = await UdpClient.Receive();
                if (active() == false) return;

                if (packet.Success)
                {
                    lock (this) ReceiveBuffer(packet.Buffer);
                }

                udp();
            }

            // Receive tcp packages and disconnect if they cannot be received anymore
            while (active())
            {
                var packet = await TcpClient.Receive(4);

                if (packet.Success == false || active() == false) continue;

                var length = packet.Buffer.Decode<int>();
                packet = await TcpClient.Receive(length);

                if (active() == false) continue;

                if (packet.Success)
                {
                    // Heartbeat 
                    if (length == 1 && packet.Buffer[0] == 0xFF) continue;

                    lock (this) ReceiveBuffer(packet.Buffer);
                }
            }

            bool active() => TcpClient.IsConnected && UdpClient.IsConnected && ThreadIdx == threadIdx;

            // Stop if disconnected
            Stop(15);
        }

        #endregion

        public enum VALIDATION : byte
        {
            INVALID,
            IN_PROGRESS,
            COMPLETE,
        }
    }
}