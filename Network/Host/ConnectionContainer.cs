namespace Cutulu.Network;

using System.Collections.Generic;
using Cutulu.Core;
using System.Net;
using Sockets;
using System;

public class ConnectionContainer()
{
    private readonly Dictionary<IPEndPoint, ushort> ConnectionsByUdp = [];
    private readonly Dictionary<TcpSocket, ushort> ConnectionsByTcp = [];
    private readonly Dictionary<UNumber64, ushort> ConnectionsByUid = [];
    private readonly SwapbackArray<Connection> Connections = [];
    private readonly object _connectionLock = new();

    public Action<Connection> Connected, Disconnected;

    public int ConnectionCount => Connections.Count;
    private UNumber64 NextUid;

    private void _RegisterConnection(Connection connection, ushort idx)
    {
        ConnectionsByUdp[connection.EndPoint] = idx;
        ConnectionsByTcp[connection.Socket] = idx;
        ConnectionsByUid[connection.UserId] = idx;
    }

    // Must be called while already holding _connectionLock.
    private void _RemoveConnectionInternal(Connection connection)
    {
        ConnectionsByUdp.Remove(connection.EndPoint);
        ConnectionsByTcp.Remove(connection.Socket);

        if (ConnectionsByUid.TryGetValue(connection.UserId, out var idx)
            && idx < Connections.Count
            && ReferenceEquals(Connections[idx], connection))
        {
            ConnectionsByUid.Remove(connection.UserId);

            bool willSwap = idx < Connections.Count - 1;
            Connection swapped = willSwap ? Connections[Connections.Count - 1] : null;

            Connections.RemoveAt(idx);

            if (willSwap) _RegisterConnection(swapped, idx);
        }
    }

    public void _RemovedConnection(Connection connection)
    {
        if (connection is null) return;

        lock (_connectionLock)
        {
            _RemoveConnectionInternal(connection);
        }

        Disconnected?.Invoke(connection);
    }

    public Connection _CreateConnection(HostManager manager, TcpSocket socket, byte[] buffer)
    {
        var ip = new IPEndPoint(((IPEndPoint)socket.Socket.RemoteEndPoint).Address, buffer.Decode<int>());

        UNumber64 userId;

        lock (_connectionLock)
        {
            if (TryGetConnection(ip, out var oldConnection))
            {
                userId = oldConnection.UserId;

                // Fully evict the old connection from every structure NOW,
                // synchronously, before the new one is added.
                _RemoveConnectionInternal(oldConnection);

                oldConnection.Socket?.Close();
            }
            else
            {
                userId = NextUid++;
            }
        }

        return new Connection(userId, manager, socket, ip);
    }

    public void _AddedConnection(Connection connection)
    {
        if (connection is null) return;

        lock (_connectionLock)
        {
            ushort idx = (ushort)Connections.Count;
            Connections.Add(connection);

            _RegisterConnection(connection, idx);
        }
    }

    public void _InvokeConnectEvent(Connection connection)
    {
        Connected?.Invoke(connection);
    }

    public Span<Connection> GetConnections() => Connections.AsSpan();

    public bool TryGetConnection(UNumber64 uid, out Connection connection)
    {
        lock (_connectionLock)
        {
            if (ConnectionsByUid.TryGetValue(uid, out var idx))
                return TryGetConnectionByIdx(idx, out connection);
        }

        connection = null;
        return false;
    }

    public bool TryGetConnection(TcpSocket socket, out Connection connection)
    {
        lock (_connectionLock)
        {
            if (ConnectionsByTcp.TryGetValue(socket, out var idx))
                return TryGetConnectionByIdx(idx, out connection);
        }

        connection = null;
        return false;
    }

    public bool TryGetConnection(IPEndPoint endpoint, out Connection connection)
    {
        lock (_connectionLock)
        {
            if (ConnectionsByUdp.TryGetValue(endpoint, out var idx))
                return TryGetConnectionByIdx(idx, out connection);
        }

        connection = null;
        return false;
    }

    private bool TryGetConnectionByIdx(ushort idx, out Connection connection)
    {
        if (idx >= Connections.Count)
        {
            connection = null;
            return false;
        }

        connection = Connections[idx];
        return true;
    }

    public void Enumerate(Action<Connection> action)
    {
        if (action is null) return;

        lock (_connectionLock)
        {
            foreach (var connection in GetConnections())
                action.Invoke(connection);
        }
    }

    public void ClearConnections()
    {
        lock (_connectionLock)
        {
            ConnectionsByUdp.Clear();
            ConnectionsByTcp.Clear();
            ConnectionsByUid.Clear();
            Connections.Clear();

            NextUid = 0;
        }
    }
}