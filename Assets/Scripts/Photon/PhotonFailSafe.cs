using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class PhotonFailSafe : MonoBehaviourPunCallbacks
{
    public static PhotonFailSafe Instance { get; private set; } // Singleton Instance 

    [SerializeField] private GameObject failPanel;      //  Assign this in Inspector -> (Panel_PhotonFailSafe)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ShowFailPanel()
    {
        if (failPanel != null)
        {
            failPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[PhotonFailSafe] Fail panel not assigned in Inspector!");
        }
    }

    public void HideFailPanel()
    {
        if (failPanel != null) failPanel.SetActive(false);
    }
    public void Retry()
    {
        failPanel.SetActive(false);
        PhotonNetwork.ReconnectAndRejoin();
    }

    public void BackToMenu()
    {
        failPanel.SetActive(false);
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("01_Menu");
    }
}
