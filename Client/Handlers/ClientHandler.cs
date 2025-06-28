namespace Client_v.Handlers;

using System.Net.Sockets;
using System.Text;

public class ClientHandler
{
    private TcpClient client;
    public StreamReader Reader { get; private set; }
    public StreamWriter Writer { get; private set; }
    public bool IsRunning { get; private set; }

    public ClientHandler(TcpClient tcpClient)
    {
        client = tcpClient;

        NetworkStream stream = client.GetStream();
        Reader = new StreamReader(stream, Encoding.UTF8);
        Writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    }

    public void Run() => IsRunning = true;

    public void Close()
    {
        Reader.Close();
        Writer.Close();
        client.Close();
        IsRunning = false;
    }
}