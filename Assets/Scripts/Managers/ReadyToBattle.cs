// Attach Script in Scene3 - Prefight ( on _GO_ )
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class ReadyToBattle : MonoBehaviourPunCallbacks
{
    public IndexManager indexManager;
    // Expanded so Update Checks if Both Players are ready:
    private void Update()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            bool allReady = true;

            foreach (Player p in PhotonNetwork.PlayerList)
            {
                if (!p.CustomProperties.ContainsKey("Ready") || !(bool)p.CustomProperties["Ready"])
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                // Everyone ready -> load battle scene
                if (PhotonNetwork.IsMasterClient)
                {
                    Debug.Log("[MP] Both players ready → starting battle...");
                    PhotonNetwork.LoadLevel("03_Battle");
                }
            }
        }
    }

    public void OnReady()
    {
        var picked = indexManager.GetPickedNames();
        if (picked.Count < 6)
        {
            Debug.Log("Pick exactly 6 before ready!");
            return;
        }

        // Save locally (or in GameData)
        PlayerPrefs.SetString("MyTeam", string.Join(",", picked));

        // Now connect to Photon
        if (GameModeManager.IsMultiplayer)
        {
            // MULTI -> go through Photon
            if (!PhotonNetwork.IsConnected)
            {
                Debug.Log("[MP] Connecting to Photon...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                Debug.Log("[MP] Already connected → joining random room");
                PhotonNetwork.JoinRandomRoom();
            }
        }
        else
        {
            // Just close popup and continue singleplayer
            Debug.Log("[SP] Starting local battle...");
            BattleLogic.Instance.InitUI();
            BattleLogic.Instance.ApplySprites();
            BattleLogic.Instance.WireBattleOverButtons();
            BattleLogic.Instance.FocusFirstSelectable();
            BattleLogic.Instance.StartYourTurn();  
        }
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 2 });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room with team: " + PlayerPrefs.GetString("MyTeam"));
        // Load multiplayer scene or battle start here
    }
}
