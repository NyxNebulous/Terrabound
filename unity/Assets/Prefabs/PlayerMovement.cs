using UnityEngine;
using Nakama;
using System.Text;
using System.Threading.Tasks;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private ISocket socket;
    private string localUserId;

    private void Start()
    {
        if (NakamaConnection.Instance == null)
        {
            Debug.LogError("NakamaConnection instance not found!");
            return;
        }

        socket = NakamaConnection.Instance.Socket;
        localUserId = NakamaConnection.Instance.GetSession().UserId;

        if (socket == null)
        {
            Debug.LogError("Nakama socket not connected!");
        }
    }

    private void Update()
    {
        // Local movement
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(moveX, moveY, 0f).normalized;
        transform.position += move * moveSpeed * Time.deltaTime;

        // Send position update
        if (socket != null)
        {
            SendMovementUpdate(transform.position.x, transform.position.y);
        }
    }

    private async void SendMovementUpdate(float x, float y)
    {
        if (socket == null) return;

        var update = new PlayerUpdate
        {
            user_id = localUserId,
            x = x,
            y = y
        };

        string json = JsonUtility.ToJson(update);
        byte[] payload = Encoding.UTF8.GetBytes(json);

        try
        {
            // OpCode 1
            await socket.SendMatchStateAsync("movement_match", 1, payload);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to send movement update: " + e.Message);
        }
    }

    [System.Serializable]
    private class PlayerUpdate
    {
        public string user_id;
        public float x;
        public float y;
    }
}
