// connects to Photon and tries to Join(connect to room) - Sync BattleLogic
// ONLY KEEP FOR CALLBACKS!
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetManager : MonoBehaviourPunCallbacks
{
    public static NetManager Instance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
        if (!PhotonNetwork.IsConnected)
            PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log(" [V] Connected to Photon Master Server.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError(" [XoX] Disconnected: " + cause);
    }
}

