namespace Cutulu.Network.Sockets
{
    using System.Threading.Tasks;
    using System.Net.Sockets;
    using System.Threading;
    using System.IO;
    using System;

    using Core;

    public partial class TcpSocket
    {
        public TcpClient Client { get; private set; }

        public bool IsConnected => Socket != null && Socket.Connected;
        public Socket Socket => Client?.Client;

        private CancellationTokenSource TokenSource { get; set; }
        public CancellationToken Token { get; private set; }

        public bool Poll() => IsConnected && Socket.Poll(-1, SelectMode.SelectError) == false;

        public async Task ClearBuffer() => await Receive(Socket.Available);

        public string Address { get; private set; }
        public int Port { get; private set; }
        public long UID { get; set; }

        private bool Receiving { get; set; }
        private TcpHost Host { get; set; }

        private DateTime lastTcpSend = DateTime.UtcNow;
        private Timer heartbeatTimer;

        public Action<TcpSocket> Connected, Disconnected;

        /// <summary>
        /// Constructs simple tcp client capable of IPv4 and IPv6.
        /// </summary>
        public TcpSocket() { }

        /// <summary>
        /// Constructs simple tcp client capable of IPv4 and IPv6 using existing socket.
        /// </summary>
        public TcpSocket(TcpClient client, TcpHost host)
        {
            Client = client;
            Host = host;
        }

        #region Callable Functions

        /// <summary>
        /// Connect to host async.
        /// </summary>
        public virtual async Task<bool> Connect(string address, int port, int timeout = 5000)
        {
            Disconnect(1);

            // Wait until disconnected
            while (IsConnected) await Task.Delay(1);

            var _token = Token = (TokenSource = new()).Token;

            if (Client == null)
            {
                Client = new(AddressFamily.InterNetworkV6);

                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Socket.DualMode = true;

                // Increase buffer for reduced latency on packet loss
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 256 * 1024);
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 256 * 1024);

                // NoDelay for reduced latency (Nagle-Algorith OFF)
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                // Activates keep-alive (KeepAlive ON)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 2);
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);

                // Linger for clean disconnect (Linger ON)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(true, 2));
            }

            // Try connecting the client async so we can run the timeout
            async();
            async void async()
            {
                try
                {
                    await Client.ConnectAsync(address, port, _token);
                }

                catch (Exception ex)
                {
                    switch (ex)
                    {
                        case SocketException socketex when socketex.ErrorCode == 10056:
                            Debug.LogR($"[color=indianred]{GetType().Name.ToUpper()}_CONNECT_ERROR(ALREADY_CONNECTED)");
                            break;

                        case OperationCanceledException:
                            break;

                        case IOException:
                            Debug.LogError($"{GetType().Name.ToUpper()}_CONNECT_ERROR(IOException)");
                            Disconnect(253);
                            break;

                        default:
                            if (ex.StackTrace.Contains("CancellationToken")) break;

                            Debug.LogError($"{GetType().Name.ToUpper()}_CONNECT_ERROR({ex.GetType().Name}, {ex.Message})\n{ex.StackTrace}");
                            break;
                    }
                }
            }

            // Wait until timed out or connection established
            while (timeout-- > 0 && IsConnected == false && _token.IsCancellationRequested == false)
            {
                await Task.Delay(1);
            }

            // Was unable to connect
            if (_token.IsCancellationRequested || IsConnected == false)
            {
                Disconnect(2);
                return false;
            }

            // Connected successfully
            lock (this)
            {
                Receiving = false;
                Address = address;
                Port = port;

                // Start heartbeat timer
                lastTcpSend = DateTime.UtcNow;
                heartbeatTimer?.Dispose();
                heartbeatTimer = new Timer(SendHeartbeatIfNeeded, null, 30000, 30000);

                Connected?.Invoke(this);

                return true;
            }
        }

        /// <summary>
        /// Connect to host async.
        /// Tries again for given amount of runs, adding timeoutStep to the timeout each time.
        /// </summary>
        public virtual async Task<bool> Connect(string address, int port, int timeoutStep, int timeoutRuns)
        {
            // Runs connect until connected or timed out
            for (int i = 0; i < timeoutRuns && IsConnected == false; i++)
            {
                await Connect(address, port, timeoutStep * (i + 1));
            }

            return IsConnected;
        }

        /// <summary>
        /// Disconnects from host and terminates all running processes.
        /// </summary>
        public virtual void Disconnect(byte exitCode = 0)
        {
            TokenSource?.Cancel();

            Token = CancellationToken.None;
            TokenSource = null;

            // Clean up heartbeat timer
            heartbeatTimer?.Dispose();
            heartbeatTimer = null;

            // Check if socket still exists
            if (Socket != null)
            {
                // Dispose client
                Client.Close();
                Client = null;

                // Remove from hub if assigned
                Host?.SocketDisconnectEvent(this);

                lock (this) Disconnected?.Invoke(this);
            }
        }

        /// <summary>
        /// Disposes this client. Disconnects from host and terminates all processes.
        /// </summary>
        public virtual void Close()
        {
            Disconnect(3);
        }

        private void SendHeartbeatIfNeeded(object state)
        {
            try
            {
                if ((DateTime.UtcNow - lastTcpSend).TotalSeconds > 25 && IsConnected)
                {
                    Send(1.Encode(), [0xFF]); // Heartbeat-Byte
                }
            }
            catch
            {
                // Ignore errors in heartbeat - Disconnect is detected elsewhere! ;P
            }
        }

        /// <summary>
        /// Sends data to host.
        /// </summary>
        public virtual async Task<bool> SendAsync(params byte[][] buffers)
        {
            if (IsConnected == false || buffers.IsEmpty() || Client.GetStream() is not NetworkStream stream) return false;

            var _token = Token;

            // Track last tcp send
            lastTcpSend = DateTime.UtcNow;

            for (int i = 0; i < buffers.Length && _token.IsCancellationRequested == false; i++)
            {
                if (buffers[i].NotEmpty())
                    await stream.WriteAsync(buffers[i], _token);
            }

            if (_token.IsCancellationRequested == false)
                await stream.FlushAsync(_token);

            return _token.IsCancellationRequested == false;
        }

        /// <summary>
        /// Sends data to host async.
        /// </summary>
        public virtual bool Send(params byte[][] buffers)
        {
            if (IsConnected == false || buffers.IsEmpty() || Client.GetStream() is not NetworkStream stream) return false;

            // Track last tcp send
            lastTcpSend = DateTime.UtcNow;

            try
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    if (buffers[i].NotEmpty())
                        stream.Write(buffers[i]);
                }

                stream.Flush();
            }

            catch (Exception ex)
            {
                switch (ex)
                {
                    case IOException:
                        Debug.LogError($"{GetType().Name.ToUpper()}_CONNECTION_CLOSED_WHILE_SENDING");
                        Disconnect(254);
                        break;

                    default:
                        Debug.LogError($"{GetType().Name.ToUpper()}_ERROR({ex.GetType().Name}, {ex.Message})\n{ex.StackTrace}");
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// Receives data, writes bytes to memory.
        /// </summary>
        public virtual async Task<(bool Success, byte[] Buffer)> Receive(int length)
        {
            if (length <= 0 || !IsConnected || Client.GetStream() is not NetworkStream stream) return (false, []);

            var buffer = new byte[length];

            // Create timeout token (prohibits infinite blocking)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(Host.NotNull() ? 5 : 2));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(Token, timeoutCts.Token);

            try
            {
                var readTotal = 0;

                while (readTotal < length)
                {
                    int read = await stream.ReadAsync(
                        buffer.AsMemory(readTotal, length - readTotal),
                        linkedCts.Token //Token
                    );
                    if (read == 0) throw new IOException("Remote closed");
                    readTotal += read;
                }

                return (true, buffer);
            }

            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                var prefix = Host != null ? "HOST" : "CLIENT";
                prefix = $"{prefix}_{GetType().Name.ToUpper()}";

                Debug.LogR($"[color=orange]{prefix}_RECEIVE_TIMEOUT(10s)");
                Disconnect(255);

                return (false, []);
            }

            catch (Exception ex)
            {
                var prefix = Host != null ? "HOST" : "CLIENT";
                prefix = $"{prefix}_{GetType().Name.ToUpper()}";

                switch (ex)
                {
                    case IOException iox when iox.Message.StartsWith("LOST_CONNECTION"):
                        Debug.LogR($"[color=indianred]{prefix}_{ex.Message}");
                        break;

                    case IOException iox when iox.Message.Contains("unable", StringComparison.CurrentCultureIgnoreCase):
                        Debug.LogR($"[color=indianred]{prefix}_CONNECTION_FAILED: (propably host has been closed) [/color]{ex.Message}");
                        break;

                    case IOException iox when iox.Message.Contains("remote closed", StringComparison.CurrentCultureIgnoreCase):
                        Debug.LogR($"[color=indianred]{prefix}_CONNECTION_CLOSED: [/color]Disconnected.");
                        break;

                    case OperationCanceledException:
                        break;

                    default:
                        if (ex.StackTrace.Contains("CancellationToken")) break;

                        Debug.LogError($"{prefix}_ERROR({ex.GetType().Name}, {ex.Message})\n{ex.StackTrace}");
                        break;

                }

                Disconnect(255);
            }

            finally
            {
                Receiving = false;
            }

            return (false, []);
        }

        #endregion
    }
}