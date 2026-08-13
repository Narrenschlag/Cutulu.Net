namespace Cutulu.Network;

using System.Collections.Generic;
using Cutulu.Core;
using System.Net;
using Sockets;
using System;

/// <summary>
/// Thread-safe connection container for host connection management.
/// </summary>
public class ConnectionContainer()
{
    private readonly Dictionary<IPEndPoint, ushort> ConnectionsByUdp = [];
    private readonly Dictionary<TcpSocket, ushort> ConnectionsByTcp = [];
    private readonly Dictionary<UNumber64, ushort> ConnectionsByUid = [];
    private readonly SwapbackArray<Connection> Connections = [];
    private readonly object _connectionLock = new();
    private UNumber64 NextUid;

    /// <summary> Called when a connection is established. </summary>
    public Action<Connection> Connected;
    /// <summary> Called when a connection is disconnected. </summary>
    public Action<Connection> Disconnected;

    /// <summary> Count of currently established connections. </summary>
    public int ConnectionCount => Connections.Count;

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

    /// <summary>
    /// Removes a connection from the container. This is a thread safe operation in terms of connections.
    /// </summary>
    public void _RemovedConnection(Connection connection)
    {
        if (connection is null) return;

        lock (_connectionLock)
        {
            _RemoveConnectionInternal(connection);
        }

        Disconnected?.Invoke(connection);
    }

    /// <summary>
    /// Creates a connection from a tcp socket and a buffer. This is a thread safe operation in terms of connections.
    /// </summary>
    public Connection _CreateConnection(HostManager manager, TcpSocket socket, byte[] buffer)
    {
        var ip = new IPEndPoint(((IPEndPoint)socket.Socket.RemoteEndPoint).Address, buffer.Decode<int>());

        UNumber64 userId;

        lock (_connectionLock)
        {
            if (TryGetConnection(ip, out var oldConnection, false))
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

    /// <summary>
    /// Adds a connection to the container. This is a thread safe operation in terms of connections.
    /// </summary>
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

    /// <summary>
    /// Invokes the connected event on the connection.
    /// </summary>
    public void _InvokeConnectEvent(Connection connection)
    {
        Connected?.Invoke(connection);
    }

    /// <summary>
    /// Returns all connections as a non-thread safe span.
    /// </summary>
    public Span<Connection> GetConnections() => Connections.AsSpan();

    /// <summary>
    /// Returns true and a connection if the connection is found. Otherwise false and null.
    /// If connectedOnly is true, the connection must be connected.
    /// </summary>
    public bool TryGetConnection(UNumber64 uid, out Connection connection, bool connectedOnly = true)
    {
        lock (_connectionLock)
        {
            if (ConnectionsByUid.TryGetValue(uid, out var idx))
                return TryGetConnectionByIdx(idx, out connection, connectedOnly);
        }

        connection = null;
        return false;
    }

    /// <summary>
    /// Returns true and a connection if the connection is found. Otherwise false and null.
    /// If connectedOnly is true, the connection must be connected.
    /// </summary>
    public bool TryGetConnection(TcpSocket socket, out Connection connection, bool connectedOnly = true)
    {
        lock (_connectionLock)
        {
            if (ConnectionsByTcp.TryGetValue(socket, out var idx))
                return TryGetConnectionByIdx(idx, out connection, connectedOnly);
        }

        connection = null;
        return false;
    }

    /// <summary>
    /// Returns true and a connection if the connection is found. Otherwise false and null.
    /// If connectedOnly is true, the connection must be connected.
    /// </summary>
    public bool TryGetConnection(IPEndPoint endpoint, out Connection connection, bool connectedOnly = true)
    {
        lock (_connectionLock)
        {
            if (ConnectionsByUdp.TryGetValue(endpoint, out var idx))
                return TryGetConnectionByIdx(idx, out connection, connectedOnly);
        }

        connection = null;
        return false;
    }

    private bool TryGetConnectionByIdx(ushort idx, out Connection connection, bool connectedOnly)
    {
        if (idx >= Connections.Count)
        {
            connection = null;
            return false;
        }

        connection = Connections[idx];
        return connectedOnly == false || (connection?.IsConnected ?? false);
    }

    /// <summary>
    /// Enumerates all connections. This is a thread safe operation in terms of connections.
    /// </summary>
    public void Enumerate(Action<Connection> action)
    {
        if (action is null) return;

        Connection[] snapshot;

        lock (_connectionLock)
        {
            snapshot = [.. GetConnections()];
        }

        foreach (var connection in snapshot)
            if (connection != null)
                action.Invoke(connection);
    }

    /// <summary>
    /// Clears all connections. Do not call this at runtime or you will corrupt your connection references.
    /// </summary>
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