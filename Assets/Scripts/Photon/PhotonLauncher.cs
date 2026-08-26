// Assets/Scripts/Multiplayer/PhotonLauncher.cs - FINAL CLEAN VERSION
// connects, joins lobby, can also create room, then loads
// Photon flow (ApplyName, JoinRoom only after lobby, Ready check).
// connect -> join/create -> leave -> restart

// Photon Flow:
// MP_Panel -> Enter name (apply save name) + set Slider val = RoomNumber ->
// PhotonLauncher.SetPlayerNameAndRoom(name, roomID)

// Press Play = Connect & Join || Create room ->
// Play = PhotonLauncher.PlayMultiplayer() ; Connect; (PreFightUI in Scene: "03_Battle")
// onJoinedRoom() :: (PhotonNetwork.LoadLevel("03_Battle")
// Player 1 joins -> Waits ; Player 2 joins -> Both go for MP Scene 03 ;

// Scene3Prefight(popup) = must pick Pokedors(6) + TrainerSprite + Name (will be copied from scene 1 if empty?)
// ONLY when Bith Players hit ready -> Battle Begins!
// Ready = PhotonLauncher.ReadyUp()  - while Player.CustomProperties["Ready"] == true ->
// BattleLogic Start = BattleLogic.Instance.StartMultiplayerBattle();

// Game restarts if both players press restart -> else exit to main menu

/*
(if room 000 -> default room -> nothing;)
if roomNumber change -> Create room again + goScene03

Scene 1: MP_Panel
-> Player enters name + sets room via slider
-> Presses PLAY
-> Photon connects + joins/creates room with room ID
-> Loads Scene 3: "03_Battle" (PreFight UI)

Scene 3:
-> Player customizes 6 pokedors, trainer sprite, and (if needed) name
-> Presses READY
-> Wait for both players to be ready
-> MasterClient triggers StartMultiplayerBattle()

 */

using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;                   // uGUI (Slider, Button, Navigation)
using ExitGames.Client.Photon;          // Required for ExitGames.Client.Photon.Hashtable
using System.Collections;               // For Hashtable

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    #region Config & Helpers ================================================

    // --- STATIC DATA FIELDS (Persist across scenes) ---
    public static PhotonLauncher Instance;                      // For Singleton
    public static   string  PlayerNickname  { get; private set; } = "Player";
    public static   string  RoomFilterID    { get; private set; } = "Room_000";
    public const    string  PLAYER_READY_PROP = "IsReady";
    private const   int     _MAX_PLAYERS = 2;

    // --- INSPECTOR REFERENCES (Must be assigned in Scene 1/3) ---
    public Slider filterSlider;
    public TMP_InputField nameInput;
    public Button ReadyButton; // Must be assigned in Scene 3 (PreFight)
    // CurrentRoom = PhotonLauncher.RoomFilterID

    // --- INTERNAL STATE ---
    private bool _isConnecting = false;

    [SerializeField] private GameObject Popup_Prefight;     // keep Prefight for ref

    private Coroutine waitForOpponent;          // for not waiting forever if only 1 player in...
    
    // Track if this join came from pressing Play in the MP panel
    public static bool JoinFromMultiplayer = false;

    #endregion

    #region UNITY FUNCS ======================================================
    // Awake - checks if ref is there -> Destroy Ref / if its this -> DontDestroy
    private void Awake()
    {
        // Singleton:
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PHOTON] Duplicate PhotonLauncher destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[PHOTON] PhotonLauncher Awake - Singleton Set");

        // make it root OBJ for DontDestroy stop complain:
        if (transform.parent != null) transform.SetParent(null, false);

        DontDestroyOnLoad(gameObject);

        // --- Guard to avoid prefight auto-start ---
        
        // Only disconnect if this prefab was duplicated accidentally
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (SceneManager.GetActiveScene().name == "03_Battle")
        {
            Debug.Log("[PHOTON] Awake in Battle scene — keeping connection active.");
        }


    }

    //Singleton - Events
    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    #endregion
    // ========================================================================
    // SCENE 1 (MENU) - BUTTON ACTIONS
    // ========================================================================

    /// <summary>
    /// Saves the player name and room ID filter, then starts the connection process.
    /// Called from the 'Play' button in the Multiplayer Panel.
    /// Works like this: init default room 000 , not connected really to MP until change room number(filter)
    /// after slider change name of room(filter) -> get into Scene 3 and wait for 2 players to be ready->
    /// start BATTLE!!!
    /// </summary>
    public void ConnectAndJoinRoom()
    {
        // check if Refs are null before start:
        if (filterSlider == null || nameInput == null)
        {
            Debug.LogError("[PHOTON] UI refs missing! Assign Slider_Multiplayer and Input_PlayerName in the Inspector.");
            return;
        }
        // check if not in menu
        if (SceneManager.GetActiveScene().name != "01_Menu")
        {
            Debug.Log("[PHOTON] ConnectAndJoinRoom called outside menu, ignoring.");
            return;
        }


        Debug.Log("[PHOTON] Starting ConnectAndJoinRoom flow...");
        JoinFromMultiplayer = true;     // guard make sure only work on MP

        // Save player name
        if (string.IsNullOrEmpty(PlayerNickname))
            PlayerNickname = "Player" + UnityEngine.Random.Range(1000, 9999);
        PhotonNetwork.NickName = PlayerNickname;

        // Get new room name
        int filter = Mathf.RoundToInt(filterSlider.value);
        string newRoom = "Room_" + filter;

        // If already in wrong room, leave it
        if (PhotonNetwork.InRoom && RoomFilterID != newRoom)
        {
            Debug.LogWarning($"[PHOTON] In wrong room ({PhotonNetwork.CurrentRoom.Name}), leaving to join: {newRoom}");
            _pendingJoinRoom = true;
            _nextRoomName = newRoom;
            RoomFilterID = newRoom;     // Save for later rejoin
            PhotonNetwork.LeaveRoom();  // for roomChange - must LeaveRoom!
            return;
        }

        // Prevent double-connect
        if (_isConnecting)
        {
            Debug.LogWarning("[PHOTON] Already connecting, skipping request.");
            return;
        }

        RoomFilterID = newRoom; // Save new filter
        _isConnecting = true;

        // Cache RoomNumber + PlayerName :
         PlayerNickname = string.IsNullOrWhiteSpace(nameInput.text) 
            ? "Player" + UnityEngine.Random.Range(1000, 9999)
    :       nameInput.text.Trim();
        RoomFilterID = "Room_" + Mathf.RoundToInt(filterSlider.value);

        // Connect if needed
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        // Already connected
        if (PhotonNetwork.IsConnectedAndReady)
        {
            Debug.Log("[PHOTON] Already connected -> Joining Lobby...");
            PhotonNetwork.JoinLobby();  // Will trigger OnJoinedLobby
        }

    }

    public void SetPlayerNameAndRoom(string name, int roomID)
    {
        PlayerNickname = string.IsNullOrWhiteSpace(name) ? "Player" + Random.Range(1000, 9999) : name.Trim();
        RoomFilterID = "Room_" + roomID;
    }
    
    // on Scene01 - menu Load - find slider and input in Multiplayerpanel;
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "01_Menu")
        {
            // try to find by name:
            // wont work if the GOs are children or in hirarchy so maybe use findobjbytype? 
            filterSlider = GameObject.Find("Slider_Multiplayer")?.GetComponent<Slider>();
            nameInput = GameObject.Find("Input_PlayerName")?.GetComponent<TMP_InputField>();

            // If still null, fall back to "find any in scene (by type)"
            if (filterSlider == null)
                filterSlider = GameObject.FindObjectOfType<Slider>(true);
            if (nameInput == null)
                nameInput = GameObject.FindObjectOfType<TMP_InputField>(true);

        }
        StartCoroutine(EnsureMenuUIReady());

    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[Photon] Connected to Master, joining Lobby...");
        _isConnecting = false;
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log($"[Photon] Joined Lobby, now joining/creating room: {RoomFilterID}");

        RoomOptions opts = new RoomOptions { MaxPlayers = _MAX_PLAYERS };
        PhotonNetwork.JoinOrCreateRoom(RoomFilterID, opts, TypedLobby.Default);
    }

    //onJoinedRoom
    public override void OnJoinedRoom()
    {
        Debug.Log("[Photon] Joined room: " + PhotonNetwork.CurrentRoom.Name);
        if (JoinFromMultiplayer)
        {
            JoinFromMultiplayer = false;
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.LoadLevel("03_Battle");
            // add wait and check prefight + Scene :
            //StartCoroutine(WaitForSceneAndPrefight());
            StartCoroutine(WaitForSceneAndPrefightAfterLoad()); // better Corutine

            // waitForOpponent = StartCoroutine(OpponentTimeout()); // to not call it twice... called in waitForSce...
        }
        else
        {
            Debug.Log("[Photon] Joined room passively (ignore auto-load).");
        }
    }

    // timout = t seconds before disconnecting if alone in room
    private IEnumerator OpponentTimeout()
    {
        // check if joined from MP:
        if (!JoinFromMultiplayer)
            yield break;            // <-- guard

        float t = 120;      // set 120s to wait 
        // if 2 players -> play ; if 1 player >60seconds in room  alone -> Disconnect;
        // to keep track of time:
        while (t > 0f && PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            t -= Time.deltaTime;
            yield return null;
        }
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            Debug.Log("[Photon] Timeout: no opponent -> leaving room.");
            PhotonNetwork.LeaveRoom();
        }
    }

    void JoinTargetRoom()
    {
        string roomName = "Room_" + UnityEngine.Random.Range(10, 99); // or custom
        PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = 2 }, TypedLobby.Default);
    }

    private IEnumerator EnsureMenuUIReady()
    {
        yield return new WaitForSeconds(0.2f);
        if (SceneManager.GetActiveScene().name == "01_Menu")
        {
            filterSlider ??= GameObject.Find("Slider_Multiplayer")?.GetComponent<Slider>();
            nameInput ??= GameObject.Find("Input_PlayerName")?.GetComponent<TMP_InputField>();
        }
    }


    // ========================================================================
    // SCENE 3 (PREFIGHT) - BUTTON ACTION & LOGIC
    // ========================================================================

    private IEnumerator WaitAndCheckPrefightPopup()
    {
        yield return new WaitForSeconds(0.5f);
        if (SceneManager.GetActiveScene().name == "03_Battle")
        {
            Popup_Prefight = GameObject.Find("Popup_Prefight");
            if (Popup_Prefight == null)
            {
                Debug.LogWarning("[Photon] Prefight Popup missing – showing failsafe.");
                PhotonFailSafe.Instance?.ShowFailPanel();
            }
        }
    }

    private IEnumerator WaitForSceneAndPrefight()
    {
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "03_Battle");
        yield return new WaitForSeconds(0.3f);
        
        Popup_Prefight = GameObject.Find("Popup_Prefight");
        if (!Popup_Prefight)
        {
            Debug.LogWarning("[Photon] Prefight Popup missing – showing failsafe.");
            PhotonFailSafe.Instance?.ShowFailPanel();
        }

        // Destroy REF to stop null refs:
        if (BattleLogic.Instance != null)
            Destroy(BattleLogic.Instance.gameObject);

        waitForOpponent = StartCoroutine(OpponentTimeout());
    }

    private IEnumerator WaitForSceneAndPrefightAfterLoad()
    {
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "03_Battle");
        yield return new WaitForSeconds(0.4f);

        Popup_Prefight = GameObject.Find("Popup_Prefight");
        if (Popup_Prefight != null)
        {
            Debug.Log("[Photon] Prefight popup found, enabling multiplayer setup.");
            Popup_Prefight.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[Photon] Prefight popup missing — showing failsafe.");
            PhotonFailSafe.Instance?.ShowFailPanel();
        }

        // Start timeout only once
        if (waitForOpponent == null)
            waitForOpponent = StartCoroutine(OpponentTimeout());
    }

    // add button to cancel play MP:
    public void CancelMultiplayer()
    {
        Debug.Log("[Photon] Cancel pressed -> Leaving Lobby/Room.");
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

        // Prevent BattleLogic from running prefight when back in menu
        BattleLogic.StartBattleOnReady = false;
        
        JoinFromMultiplayer = false;

        // Destroy ref
        if (BattleLogic.Instance != null)
            Destroy(BattleLogic.Instance.gameObject);

        SceneManager.LoadScene("01_Menu");
    }


    /// <summary>
    /// Sets the player's 'Ready' property. Called by the ReadyButton in Scene 3.
    /// </summary>
    public void ReadyUp()
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PHOTON] ReadyUp ignored – not in a room yet.");
            return;
        }
        var props = new ExitGames.Client.Photon.Hashtable { { PLAYER_READY_PROP, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        CheckAllPlayersReady();
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey(PLAYER_READY_PROP))
        {
            Debug.Log($"[Photon] Player {targetPlayer.NickName} updated Ready status.");
            CheckAllPlayersReady();
        }
    }

    /// <summary>
    /// Checks if all players in the room have the 'IsReady' property set to true.
    /// </summary>
    private void CheckAllPlayersReady()
    {
        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount != _MAX_PLAYERS)
        {
            return;
        }

        bool allReady = true;
        foreach (Photon.Realtime.Player p in PhotonNetwork.PlayerList)
        {
            if (!(p.CustomProperties.TryGetValue(PLAYER_READY_PROP, out object isReady) && (bool)isReady))
            {
                /*
                allReady = false;
                break;
                */
                Debug.Log($"Player {p.NickName} Ready={isReady}");
            }
            else
                Debug.Log($"Player {p.NickName} has no Ready prop yet.");
        }

        // Check if All RDY:
        if (allReady)
        {
            Debug.Log("[Photon] All players ready!");
            
            // master client loads battle scene for both
            if (PhotonNetwork.IsMasterClient)
            {
                // Already in Scene 3, don't reload again
                if (SceneManager.GetActiveScene().name != "03_Battle")
                    PhotonNetwork.LoadLevel("03_Battle");
                else
                    BattleLogic.StartBattleOnReady = true;      // tell BattleLogic to start
            }
        }
        else
        {
            Debug.Log("[PHOTON] Waiting for the other player...");
        }
    }

    // ========================================================================
    // EXIT AND CLEANUP
    // ========================================================================

    /// <summary>
    /// Called by the 'Exit' button to clean up the Photon connection.
    /// </summary>
    public void OnExitClicked()
    {
        Debug.Log("[Photon] Exit requested.");

        RoomFilterID = "Room_000";              // reset to default

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();          // Leave room if in room
        else if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();         // Disconnect if connected

        SceneManager.LoadScene("01_Menu");      // return to Menu
    }

    // OnLeftRoom :  called after Photon leaves room
    public override void OnLeftRoom()
    {
        if (waitForOpponent != null) StopCoroutine(waitForOpponent);
        _isConnecting = false;
        StartCoroutine(ReturnToMenuAfterLeave());
    }
    private IEnumerator ReturnToMenuAfterLeave()
    {
        //  avoid a lingering coroutine.
        if (waitForOpponent != null) StopCoroutine(waitForOpponent);
        waitForOpponent = null;

        // while (true) inRoom - ignore
        while (PhotonNetwork.InRoom) yield return null;

        // Safety Guard Init RoomNum to default:
        RoomFilterID = "Room_000";
        GameModeManager.ResetPreFight();
        SceneManager.LoadScene("01_Menu");
    }
    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        Debug.Log($"[Photon] Disconnected: {cause}. Returning to main menu.");
        SceneManager.LoadScene("01_Menu");
    }


    #region Unused:             o----)=====================================>
    
    private bool _pendingJoinRoom = false;
    private string _nextRoomName = null;
    private bool pendingRoomJoin;

    public void ExitMultiplayer()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        SceneManager.LoadScene("01_Menu");
    }
    private IEnumerator ExitAndReturnToMenu()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            while (PhotonNetwork.InRoom) yield return null; // Wait until left
        }

        // If connected, optionally disconnect
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            while (PhotonNetwork.IsConnected) yield return null; // Wait until disconnect
        }

        SceneManager.LoadScene("01_Menu"); // Force go to Menu
    }
    void TryJoinMultiplayerRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            Debug.Log("[PHOTON] In room, leaving to change...");
            PhotonNetwork.LeaveRoom();
            pendingRoomJoin = true; // flag to track
        }
        else
        {
            JoinTargetRoom(); // first time / not in room
        }
    }

    #endregion
}

