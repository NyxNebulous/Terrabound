using UnityEngine;
using TMPro;

public class PlayerScript : MonoBehaviour
{
    public string playerName;
    public TMP_Text nameText;

    void Start()
    {
        if (string.IsNullOrEmpty(playerName))
            playerName = "Player" + Random.Range(1000, 9999);

        nameText.text = playerName;
    }

    void Update()
    {
        nameText.text = playerName;
    }
}
