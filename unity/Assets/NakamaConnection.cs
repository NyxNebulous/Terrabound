using UnityEngine;
using Nakama;
using System.Threading.Tasks;

public class NakamaConnection : MonoBehaviour
{
    public static NakamaConnection Instance;

    private IClient client;
    private ISession session;
    private ISocket socket;

    public ISocket Socket => socket;

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Debug.Log("Connecting to Nakama...");

        client = new Client("http", "127.0.0.1", 7350, "defaultkey");

        session = await client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);

        socket = client.NewSocket();
        await socket.ConnectAsync(session);

        Debug.Log("Connected to Nakama WebSocket.");
    }

    public ISocket GetSocket()
    {
        return socket;
    }

    public IClient GetClient()
    {
        return client;
    }

    public ISession GetSession()
    {
        return session;
    }
}
