namespace Cutulu.Network;

using System.Threading.Tasks;
using System.Threading;
using System.IO.Pipes;
using Cutulu.Core;
using System.IO;
using System;

public static class NamedPipe
{
    // Server
    public static async Task<Socket> StartHost(string name, Action<Socket, UNumber32, LocalDecoder> receive, CancellationToken token = default, bool startListening = true)
    {
        var server = new NamedPipeServerStream(
            name,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            transmissionMode: PipeTransmissionMode.Byte
        );

        try
        {
            await server.WaitForConnectionAsync(token);
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }

        var pipe = new Socket(server);
        if (receive is not null) pipe.Received.Bind(pipe, receive);
        if (startListening) pipe.StartListening();
        return pipe;
    }

    // Client
    public static async Task<Socket> Connect(string name, Action<Socket, UNumber32, LocalDecoder> receive, string serverName = ".", CancellationToken token = default, bool startListening = true, int maxRetries = 20, int retryDelayMs = 500)
    {
        NamedPipeClientStream client = new(
            serverName, // "." = local machine
            name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous
        );

        for (int i = 0; i < maxRetries; i++)
        {
            await client.ConnectAsync(token);

            var pipe = new Socket(client);
            if (receive is not null) pipe.Received.Bind(pipe, receive);
            if (startListening) pipe.StartListening();

            if (pipe?.Stream?.IsConnected == true) return pipe;
            else await Task.Delay(retryDelayMs);
        }

        return null;
    }

    public class Socket(PipeStream stream)
    {
        public readonly PipeStream Stream = stream;
        private readonly BinaryWriter Writer = new(stream);

        public readonly Notification<Socket, UNumber32, LocalDecoder> Received = new();
        public readonly Notification Disconnected = new();
        private CancellationTokenSource? TokenSource;
        private Task? Listener;

        public bool IsConnected => Stream?.IsConnected ?? false;

        public void Send(UNumber32 key, object obj)
        {
            var encoder = new LocalEncoder();
            encoder.Encode(key);
            encoder.Encode(obj);

            byte[] buffer = encoder.GetBuffer();
            Writer.Write(buffer.Length);
            Writer.Write(buffer);
            Writer.Flush();
        }

        public void StartListening()
        {
            if (Listener is not null) return;
            TokenSource = new CancellationTokenSource();
            Listener = ListenTask(TokenSource.Token);
        }

        public void StopListening()
        {
            if (Listener is null) return;
            TokenSource?.Cancel();
            Listener.Dispose();
            Listener = null;
        }

        private async Task ListenTask(CancellationToken token)
        {
            byte[] lengthBuffer = new byte[4];
            byte[] dataBuffer;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Stream.ReadExactlyAsync(lengthBuffer.AsMemory(), token);
                    if (token.IsCancellationRequested) break;

                    int length = BitConverter.ToInt32(lengthBuffer, 0);
                    dataBuffer = new byte[length];

                    await Stream.ReadExactlyAsync(dataBuffer.AsMemory(), token);
                    if (token.IsCancellationRequested) break;

                    var decoder = new LocalDecoder(dataBuffer);
                    Received.Invoke(this, decoder.Decode<UNumber32>(), decoder);
                }
            }
            catch (OperationCanceledException) { /* clean shutdown */ }
            catch (EndOfStreamException) { /* pipe closed remotely */ Disconnected?.Invoke(); }
            catch (IOException) { /* pipe broken */ Disconnected?.Invoke(); }
        }
    }
}