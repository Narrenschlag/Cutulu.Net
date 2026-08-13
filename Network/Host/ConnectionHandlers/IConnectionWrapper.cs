namespace Cutulu.Network.Sockets;

public interface IConnectionWrapper
{
    public void InvokeConnect(Connection connection);
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
    public void AssignConnection(Connection connection);
    public Connection CreateConnection(TcpSocket socket, byte[] buffer);
}