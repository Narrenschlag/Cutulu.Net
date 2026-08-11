namespace Cutulu.Network.Sockets;

public interface IConnectionWrapper
{
    public void InvokeConnect(Connection connection);
    public void InvokeDisconnect(TcpSocket socket);
    public long NextUID();

    public int GetMaxClientCount();
    public int GetConnectionCount();
}

public interface IPingWrapper : IConnectionWrapper
{
    // Ping
    public byte[] GetPingBuffer();
}

public interface IConnectWrapper : IConnectionWrapper
{
    // Connection
    public void AssignConnection(Connection connection, TcpSocket socket);
    public Connection CreateConnection(TcpSocket socket, byte[] buffer);
}