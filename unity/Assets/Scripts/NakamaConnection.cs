using UnityEngine;
using Nakama;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NakamaConnection : MonoBehaviour
{
    private string scheme = "http";
    private string host = "localhost";
    private int port = 7350;
    private string serverKey = "defaultkey";
    private IClient _client;
    private ISession _session;

    async void Start()
    {
        _client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
        _session = await _client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);

        Debug.Log("_client"+_client);
        Debug.Log("_session"+_session);

    }
}
