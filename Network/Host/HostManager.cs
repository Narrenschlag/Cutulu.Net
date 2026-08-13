namespace Cutulu.Network
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Net;
    using System;

    using Sockets;
    using Core;

    public partial class HostManager
    {
        public readonly Dictionary<byte, ConnectionHandler> ConnectionHandlers = new([
            FormatHandler(new ConnectHandler((byte)ConnectionTypeEnum.Connect)),
            FormatHandler(new PingHandler((byte)ConnectionTypeEnum.Ping)),
        ]);

        public readonly ConnectionContainer Connections = new();
        public readonly object _trafficLock = new();

        public readonly TcpHost TcpHost;
        public readonly UdpHost UdpHost;

        public byte[] PingBuffer;
        private long NextUID;

        public bool UseRouterPortForwarding = false;
        public int MaxClients = 0, TcpPort, UdpPort;

        public bool IsListening => TcpHost?.IsListening ?? false;

        public Action<Connection, short, byte[]> Received;
        public Action Started, Stopped;

        public HostManager()
        {
            TcpHost = new()
            {
                Started = StartEvent,
                Stopped = StoppedEvent,

                Connected = HandleNewClient,
                Disconnected = DisconnectEvent,
            };

            UdpHost = new()
            {
                Received = UdpReceiveEvent,
            };
        }

        public HostManager(int tcpPort, int udpPort) : this()
        {
            TcpPort = tcpPort;
            UdpPort = udpPort;
        }

        ~HostManager() => Stop();

        public Wrapper GetWrapper() => wrapper ??= new(this);
        private Wrapper wrapper;

        public Span<Connection> GetConnections() => Connections.GetConnections();

        public int GetConnectionCount() => Connections.ConnectionCount;

        #region Validators

        public bool TryGetHandler(byte key, out ConnectionHandler validator) => ConnectionHandlers.TryGetValue(key, out validator);

        public bool ContainsHandler(byte key) => ConnectionHandlers.ContainsKey(key);

        public KeyValuePair<byte, ConnectionHandler> RegisterHandler(ConnectionHandler validator)
        {
            var val = FormatHandler(validator);
            ConnectionHandlers[validator.Key] = validator;
            return val;
        }

        private static KeyValuePair<byte, ConnectionHandler> FormatHandler(ConnectionHandler validator)
        => new(validator.Key, validator);

        #endregion

        #region Callable Functions

        /// <summary>
        /// Starts host
        /// </summary>
        public virtual void Start()
        {
            Stop();

            Connections.ClearConnections();

            TcpHost.UseRouterPortForwarding = UseRouterPortForwarding;
            TcpHost.Start(TcpPort);

            UdpHost.UseRouterPortForwarding = UseRouterPortForwarding;
            UdpHost.Start(UdpPort);

            Debug.Log($"Started host on port tcp:{TcpPort} udp:{UdpPort}");
        }

        /// <summary>
        /// Stops host
        /// </summary>
        public virtual void Stop()
        {
            Connections.Enumerate(connection => connection?.Kick());

            TcpHost.Stop();
            UdpHost.Stop();
        }

        /// <summary>
        /// Sends data to connections
        /// </summary>
        public virtual void Send(short key, object obj, params Connection[] connections) => Send(key, obj, true, connections);

        /// <summary>
        /// Sends data to connections
        /// </summary>
        public virtual void Send(short key, object obj, bool reliable, params Connection[] connections)
        {
            var span = connections.IsEmpty() ? Connections.GetConnections() : connections;

            for (int i = 0; i < span.Length; i++)
                span[i]?.Send(key, obj, reliable);
        }

        /// <summary>
        /// Sends data to connections async
        /// </summary>
        public virtual async Task SendAsync(short key, object obj, params Connection[] connections) => await SendAsync(key, obj, true, connections);

        /// <summary>
        /// Sends data to connections async.
        /// </summary>
        public virtual async Task SendAsync(short key, object obj, bool reliable, params Connection[] connections)
        {
            connections = connections.IsEmpty() ? [.. Connections.GetConnections()] : connections;

            for (int i = 0; i < connections.Length; i++)
                await connections[i]?.SendAsync(key, obj, reliable);
        }

        /// <summary>
        /// Receive event, called by connections.
        /// </summary>
        public virtual bool ReadPacket(Connection connection, short key, byte[] buffer) => false;

        #endregion

        #region Event Handlers

        protected virtual void StartEvent(TcpHost host)
        {
            lock (this) Started?.Invoke();
        }

        protected virtual void StoppedEvent(TcpHost host)
        {
            lock (this) Stopped?.Invoke();
        }

        private async void HandleNewClient(TcpSocket socket)
        {
            var remoteEndPoint = socket?.Socket?.RemoteEndPoint;
            var packet = await socket.Receive(1);

            if (packet.Success == false)
            {
                Debug.LogError($"Failed to receive connection type from {remoteEndPoint}. Closing connection.");
                socket?.Close();
                return;
            }

            if (ConnectionHandlers.TryGetValue(packet.Buffer[0], out var handler) && handler.NotNull())
            {
                var (Status, Data) = await handler.Validate(GetWrapper(), socket);

                if (Status) await handler.Handle(GetWrapper(), socket, Data);
                else Debug.LogError($"Handler<{handler.GetType().Name}> does not approve of connection. Closing connection.");
            }

            else
            {
                Debug.LogError($"Unknown connection type({packet.Buffer[0]}) received from {remoteEndPoint}. No handler has been assigned. Closing connection.");
            }

            socket?.Close();
        }

        private void DisconnectEvent(TcpSocket socket)
        {
            if (Connections.TryGetConnection(socket, out var connection, false) == false) return;

            Connections._RemovedConnection(connection);

            socket.Close();
        }

        private void UdpReceiveEvent(IPEndPoint ip, byte[] buffer)
        {
            if (Connections.TryGetConnection(ip, out var connection, false))
                connection.ReceiveBuffer(buffer);
        }

        #endregion

        public class Wrapper(HostManager manager) : IConnectionWrapper, IConnectWrapper, IPingWrapper
        {
            public readonly HostManager Manager = manager;

            public void InvokeConnect(Connection connection) => Manager.Connections._InvokeConnectEvent(connection);

            public int GetMaxClientCount() => Manager.MaxClients;
            public int GetConnectionCount() => Manager.GetConnectionCount();

            public long NextUID() => Manager.NextUID++;

            public byte[] GetPingBuffer() => Manager.PingBuffer;

            public void AssignConnection(Connection connection)
            {
                Manager.Connections._AddedConnection(connection);
            }

            public Connection CreateConnection(TcpSocket socket, byte[] buffer)
            {
                return Manager.Connections._CreateConnection(Manager, socket, buffer);
            }
        }
    }
}