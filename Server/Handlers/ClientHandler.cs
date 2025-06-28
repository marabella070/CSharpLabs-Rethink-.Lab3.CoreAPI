using System.Net.Sockets;
using System.Text;

namespace Server.Handlers;

public class ClientHandler
{
    private TcpClient client;
    public int Id { get; private set; }
    public string UserName { get; set; }
    public StreamReader Reader { get; private set; }
    public StreamWriter Writer { get; private set; }

    public ClientHandler(TcpClient tcpClient, int userId)
    {
        client = tcpClient;
        Id = userId;
        UserName = string.Empty;

        NetworkStream stream = client.GetStream();
        Reader = new StreamReader(stream, Encoding.UTF8);
        Writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    }

    public void Close()
    {
        Reader.Close();
        Writer.Close();
        client.Close();
    }
}