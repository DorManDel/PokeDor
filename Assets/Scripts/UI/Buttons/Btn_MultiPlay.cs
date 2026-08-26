//actuall playing multiplayer ( from Play in multiplayerPanel )
// Singleton - Hold Ref from Scene 1 to Cross Scenes
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Btn_MultiPlay : MonoBehaviour
{
    public PhotonLauncher launcher; // drag the PhotonLauncher prefab/GO here
    public GameObject menuPanel;
    public GameObject panelMultiplayer;
    private Button _button;

    [Header("UI Refs (Scene 1 Only)")]
    public static Btn_MultiPlay Instance;   // For Singleton
    public TMP_InputField nameInput;        // pass UI values name
    public Slider filterSlider;             // pass UI values RoomNumber
    [SerializeField] private Button playButton;   // drag Btn_Play here (or leave blank if this script sits on Btn_Play)


    void Awake()
    {
        // This is a UI helper, do NOT Persist
        if (!playButton) playButton = GetComponent<Button>();
        if (playButton)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnClick);
        }
    }
    private void Start()
    {
        StartCoroutine(WaitForLauncher());
    }


    public void OnMultiplayerClick()
    {
        Debug.Log("[BTN] Multiplayer pressed! Setting mode and connecting...");

        if (PhotonLauncher.Instance == null)
        {
            Debug.LogError("[BTN] PhotonLauncher.Instance missing!");
            return;
        }

        string playerName = nameInput ? nameInput.text : "";
        int room = filterSlider ? Mathf.RoundToInt(filterSlider.value) : 0;

        Debug.Log($"[BTN] Player={playerName}, Room={room}");

        PhotonLauncher.Instance.SetPlayerNameAndRoom(playerName, room);
        PhotonLauncher.Instance.ConnectAndJoinRoom();
    }
    private IEnumerator WaitForLauncher()
    {
        while (PhotonLauncher.Instance == null)
        {
            yield return null; // wait 1 frame
        }

        Debug.Log("[Btn_MultiPlay] Found PhotonLauncher instance!");
        launcher = PhotonLauncher.Instance;
    }
    void OnClick()
    {
        if (PhotonLauncher.Instance == null) { Debug.LogError("[BTN] PhotonLauncher.Instance missing"); return; }

        string playerName = nameInput ? nameInput.text : string.Empty;
        int room = filterSlider ? Mathf.RoundToInt(filterSlider.value) : 0;

        PhotonLauncher.Instance.SetPlayerNameAndRoom(playerName, room);
        PhotonLauncher.Instance.ConnectAndJoinRoom();
    }

}



/*
    public void PlayMultiplayer()
    {
        Debug.Log("[BTN] Joining or creating Multiplayer Room...");
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        RoomOptions opts = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.JoinOrCreateRoom("DefaultRoom", opts, TypedLobby.Default);
    }

//REF MISSING!
public void OnMultiplayerClickNew()
{
    //REF MISSING!
    if (launcher == null)
    {
        Debug.LogError("PhotonLauncher reference missing!");
        return;
    }

    launcher.ConnectAndJoinRoom();
}

public void OnMultiplayerClick1()
{
    Debug.Log("[BTN] Multiplayer pressed! Setting mode and connecting...");

    // FIX 1: Set game mode to Multi using the central manager
    //GameModeManager.SetModeMulti();

    // The logic to set name, room ID, and start connection now lives
    // in the dedicated PhotonLauncher method.

    if (PhotonLauncher.Instance != null)
    {
        // FIX 2: Call the correct entry point method in the singleton
        PhotonLauncher.Instance.ConnectAndJoinRoom();
        //PhotonLauncher.Instance.JoinTargetRoom();
    }
    else
    {
        Debug.LogError("[BTN] PhotonLauncher.Instance is NULL! Cannot start multiplayer.");
    }
}

public void OnMultiplayerClick_Oldbutnew()
{
    if (PhotonLauncher.Instance == null)
    {
        Debug.LogError("[BTN] PhotonLauncher.Instance missing!");
        return;
    }

    string name = string.IsNullOrEmpty(nameInput.text) ? null : nameInput.text;
    int room = Mathf.RoundToInt(filterSlider.value);

    PhotonLauncher.Instance.SetPlayerNameAndRoom(name, room);
    PhotonLauncher.Instance.ConnectAndJoinRoom();
}

//// ------------------------------
       private void Awake()
    {
        // PhotonLauncher Singleton Ref NullCheck:
        if (PhotonLauncher.Instance == null)
            Debug.LogError("[Btn_MultiPlay] No PhotonLauncher instance found!");
        
        // Btn Singleton::
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnMultiplayerClick);
    }


    private IEnumerator WaitForLauncherOld()
    {
        while (PhotonLauncher.Instance == null)
            yield return null; // wait one frame

        Debug.Log("[BTN] - [PLAY] PhotonLauncher found!");

        // Now safe to bind button
        var button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnMultiplayerClick);
    }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnMultiplayerClick);
        }

        if (PhotonLauncher.Instance == null)
            Debug.LogError("[Btn_MultiPlay] No PhotonLauncher instance found!");
        StartCoroutine(WaitForLauncher());
    }



*/


