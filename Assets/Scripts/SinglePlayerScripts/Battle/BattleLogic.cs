// Assets/Scripts/SinglePlayerScripts/Battle/BattleLogic.cs

// summary: Minimal single-player, turn-based battle loop (PokéDor).
// how:     Binds UI via BattleUIRefs, prints names/HP, wires 4 move buttons,
//          applies damage with TypeChart, enemy takes random turn, shows
//          Battle Over popup when someone hits 0 HP.

// notes:
//   • uGUI (Unity UI): Slider, Button, TMP_Text under a Canvas. We keep HP sliders
//     display-only (interactable=false + Navigation.None).
//   • Singleton: uses App.I (your global game state) to fetch Dex + player.
//   • Lists: player.baseData.moves & ui.moveButtons/labels are iterated lists/arrays.
//   • Actions: public event OnLog so other systems can listen to combat lines.
//   • IEnumerator: enemy turn uses a short coroutine delay for readable logs.
//   • Dictionary: your TypeChart likely uses maps internally (we just call it).
//   • Options popup is working ish - still under construction
//   • Scenes: Restart/Back buttons use SceneManager as a simple test fallback.

using System;                              // Action
using System.Collections;                  // IEnumerator
using System.Collections.Generic;          // Dictionary
using System.Linq;
using TMPro;                               // TMP_Text
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;            // EventSystem.SetSelectedGameObject
using UnityEngine.SceneManagement;         // simple restart/back during tests
using UnityEngine.UI;                      // uGUI (Slider, Button, Navigation)
using Photon.Pun;                          // Photon MultiPlayer
using Photon.Realtime;
using UnityEngine.Events;

public class BattleLogic : MonoBehaviour
{
    #region === Config & Fields =============================================

    [Header("UI (auto-bound by BattleUIRefs)")] // editor attribute to group fields in inspector
    public BattleUIRefs ui;                     // <- drag optional; auto-find if null

    [Header("Flow")]
    [Tooltip("Delay between player and enemy turn (sec).")]
    public float enemyTurnDelay = 0.25f;

    [Header("Log Typewriter")]                  // use for manage speed of type Scenario in Battle
    [Tooltip("Enable typewriter effect for log lines")]
    public bool useTypewriter = true;
    [Range(5f, 60f)] public float charsPerSecond = 30f; // editable in Inspector
    Coroutine typing;//
    [Tooltip("Seconds between characters (smaller = faster)")]
    public float typeCharDelay = 0.025f;
    
    //added for timer exposure
    [SerializeField] float baseTurnTime = 30f;          //default if no prefs
    Coroutine _turnTimer;                               //
    float timeLeft = 0f;                                // For LiveCountDown
    float turnDuration;                                 // max for this turn (for slider max)
    bool  timerOn  = false;                             //

    bool inputLocked = false;                           // block UI during enemy animations

    public static BattleLogic Instance;                 // for Singleton Fix :

    private string _roomName = "DefaultRoom";            // later: slider-based (maybe PW)
    private bool _panelAlreadyOpened = false;            // Guard for PanelPreFight Opens Randomly(ish)


    // Runtime Creatures
    // these are for Single Pokedor eachside actions:
    PokeDor player;                         // creature instance (hp, baseData, moves)
    PokeDor enemy;                          // opponent instance
    bool isOver;                            // true once someone loses
    bool isBusy;                            // guard: ignore clicks while a turn is running
    
    // === Party of 6 (switching like OG Pokémon) ================
    List<PokeDor> playerTeam = new();
    List<PokeDor> enemyTeam = new();
    int playerActive = 0;
    int enemyActive = 0;

    PokeDor PlayerActive => playerTeam[playerActive];
    PokeDor EnemyActive => enemyTeam[enemyActive];
    
    //added switch pokedors from list mechanism
    bool TrySwitch(List<PokeDor> team, ref int activeIdx, int newIdx)
    {
        if (newIdx < 0 || newIdx >= team.Count) return false;
        if (team[newIdx].hp <= 0) return false;
        if (newIdx == activeIdx) return false;
        activeIdx = newIdx;
        return true;
    }

    // Pre-fight selection state
    List<int> _selectedDexIdx = new();   // store Dex indexes player picked - in App.I.dex
    bool _teamLockedIn = false;          // **********************************************
    string trainerName = "PLAYER";       // can be overwritten in PreFight

    // === OG 4-button menu =======================================================
    enum MenuMode { Main, Moves }
    MenuMode menuMode = MenuMode.Main;
    bool confirmSurrenderPending;
    float confirmUntil;

    bool AllFainted(List<PokeDor> team)
    {
        for (int i = 0; i < team.Count; i++)
            if (team[i].hp > 0) return false;
        return true;
    }

    // *** anyone can subscribe to combat log lines ( Events ) ***
    public event Action<string> OnLog;

    // Sprite cache for Resources load - <Dictionary>
    readonly Dictionary<string, Sprite> spriteCache = new();

    // options helper
    PopupOptions pop;

    // Player and Enemy Pokedors Positions Helper: Cache baseline local pos so push anim resets each turn 
    private Vector3 _originalPlayerPos;
    private Vector3 _originalEnemyPos;
    // Keep references
    Coroutine _atkPush;

    // ADDON: SP/MP Switch ( Singleplayer/Multiplayer )
    bool isPlayerOne = PhotonNetwork.LocalPlayer.ActorNumber == 1;
    private bool multiplayerInitialized = false;

    public enum PlayerSlot
    {
        Player1,
        Player2
    }

    private PlayerSlot currentPlayer;
    
    public static bool StartBattleOnReady = false;          // Safety addon for PhotonInit MP


    #endregion

    #region === Unity Lifecycle =============================================

    void Awake()
    {
        if (!Scene03Check()) return;   // skip everything unless in 03_Battle

        // Binder: resilient — like TicTacToe, central "Logic" finds its "UI"
        if (!ui) ui = GetComponentInChildren<BattleUIRefs>(true);
        if (!ui) ui = gameObject.AddComponent<BattleUIRefs>();

        //Addon: Fix DontDestroyOnLoad Singleton AudioManager;dont keep refs to destroyed GOs
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        //AddonBinder
        // Auto-find if not set in Inspector : as the same as above but still...
        if (ui == null)
            ui = FindObjectOfType<BattleUIRefs>(true);

        if (ui == null)
            Debug.LogError("[BattleLogic] Could not find BattleUIRefs!");

        //Fix BattleLogic to be on Scene 03 ONLY
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (SceneManager.GetActiveScene().name == "03_Battle")
            DontDestroyOnLoad(gameObject);  // Keep only in battle
    }

    void Start()
    {
        // prevents it from running early in menu or default room.
        if (PhotonLauncher.Instance != null && PhotonNetwork.IsConnected && SceneManager.GetActiveScene().name != "03_Battle")
        {
            Debug.Log("[BattleLogic] Photon active outside battle scene — skipping Start().");
            return;
        }

        // Guard : make sure on Scene3
        if (!Scene03Check())
            return;
        // UI Guard
        if (ui == null || ui.popupPreFight == null) return;
        
        // another guard :
        if (SceneManager.GetActiveScene().name != "03_Battle")
        {
            Debug.Log("[BATTLE] Not in battle scene — skipping prefight start.");
            return;
        }

        //ADDON: FIX RunPreFight for scene3 only
        Debug.Log("[BATTLE] Scene 3 started, initializing UI refs...");

        //Guard - always find BattleUIRefs;
        if (!ui) ui = FindObjectOfType<BattleUIRefs>(true);

        // Check For UI REFs:
        if (ui == null)
        {
            ui = GetComponentInChildren<BattleUIRefs>();
            if (ui == null)
            {
                Debug.LogError("[BATTLE] No BattleUIRefs found in scene!");
                return;
            }
        }
        // Autostart Prefight Bug Fix:
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Ensure PhotonLauncher doesn't break SP init
        if (PhotonLauncher.Instance == null)
        {
            Debug.Log("[BATTLE] No PhotonLauncher detected — SP mode confirmed.");
        }

        // when cancel or DC - no prefightpanelpop
        if (!PhotonNetwork.InRoom && GameModeManager.IsMultiplayer)
        {
            Debug.Log("[BATTLE] Multiplayer prefight skipped (not in room).");
            return;
        }

        //Photon Multiplayer -------------------
        // Multiplayer Mode
        if (GameModeManager.IsMultiplayer && PhotonNetwork.InRoom)
        {
            Debug.Log("[BATTLE] Multiplayer Prefight -> showing popup");
            RunPreFight();
        }
        else
        {
            // Singleplayer (should also show pre-fight)
            RunPreFight();
        }

        //--------------------------------------

        // 1) Bind all UI widgets via the binder
        //ADDED WRAPPER TO MAKE SURE SCENE03 LOADED
        if (SceneManager.GetActiveScene().name == "03_Battle")
        {
            if (!ui.TryBind(out var err))
            {
                Debug.LogError(err, this);
                enabled = false;
                return;
            }
        }

        if (ui.btnMore)
        {
            ui.btnMore.onClick.RemoveAllListeners();
            ui.btnMore.onClick.AddListener(() =>
            {
                if (ui.popupOptions) ui.popupOptions.SetActive(true);
                ui.popupOptions.transform.SetAsLastSibling(); // bring to front
                AudioManager.Instance.PlaySfx("click");       // without? after instance
            });
        }


        if (ui.btnOptClose && ui.popupOptions)
        {
            ui.btnOptClose.onClick.RemoveAllListeners();
            ui.btnOptClose.onClick.AddListener(() =>
            {
                ui.popupOptions.SetActive(false);
                AudioManager.Instance.PlaySfx("click");
                StartYourTurn();   // resume buttons + timer
            });
        }

        //ADDON: add slider for typewriter Text speed
        if (ui.sldTextSpeed)
        {
            ui.sldTextSpeed.minValue = 5f;
            ui.sldTextSpeed.maxValue = 60f;
            ui.sldTextSpeed.wholeNumbers = true;
            ui.sldTextSpeed.value = charsPerSecond;
            ui.sldTextSpeed.onValueChanged.RemoveAllListeners();
            ui.sldTextSpeed.onValueChanged.AddListener(v => charsPerSecond = v);
        }

        // ADDON:: cache popup on the FIELD, not a local
        pop = ui.popupOptions ? ui.popupOptions.GetComponent<PopupOptions>() : null;
        if (pop) pop.Show(false);

        // *** Arrows Fix Addon: Ensure exists on Scene::
        if (!GameObject.Find("UIArrows"))
        {
            var prefab = Resources.Load<GameObject>("UI/UIArrows"); // put a prefab here if you have it
            if (prefab) { var go = Instantiate(prefab); go.name = "UIArrows"; DontDestroyOnLoad(go); }
        }

        //Prefight Addon Using Photon (avoid random openings)
        if (!GameModeManager.IsMultiplayer && ui.popupPreFight && ui.preFightListContent && ui.preFightBtnTemplate)
        {
            //Debug.Log("[BATTLE] Singleplayer prefight -> starting immediately");
            //RunPreFight();
            return;
        }
        else
        {
            Debug.Log("[BATTLE] Multiplayer prefight -> waiting for Photon Ready signals");
            // In multiplayer, PhotonLauncher.OnPlayerPropertiesUpdate will call
            // BattleLogic.Instance.StartMultiplayerBattle() once both players press Ready.
        }

        // 2) Data setup (PARTIES of 6; then map to existing 'player'/'enemy' vars)
        var dex = App.I.Dex;
        var pool = new List<Species>(dex);

        // helper to draw distinct species
        Species Draw()
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            var s = pool[i];
            pool.RemoveAt(i);
            return s;
        }

        // player party (keep chosen starter in slot 0 if exists)
        playerTeam.Clear(); enemyTeam.Clear();

        for (int i = 0; i < 6 && dex.Count > 0; i++)
        {
            var sp = (i == 0 && App.I.playerPoke != null)
                ? App.I.playerPoke.baseData
                : Draw();
            playerTeam.Add(new PokeDor(sp));
        }

        // enemy party (refill pool; distinct too)
        pool = new List<Species>(dex);
        for (int i = 0; i < 6 && dex.Count > 0; i++)
            enemyTeam.Add(new PokeDor(Draw()));

        // choose first living as active
        playerActive = 0; while (playerActive < playerTeam.Count && playerTeam[playerActive].hp <= 0) playerActive++;
        enemyActive = 0; while (enemyActive < enemyTeam.Count && enemyTeam[enemyActive].hp <= 0) enemyActive++;

        // IMPORTANT: remap the OG 1vs1 that i used before:
        player = PlayerActive;
        enemy = EnemyActive;

        // Addon : Wire OptionPopUp while Battle
        WireOptions();

        // 3) Init UI
        isOver = false;
        InitUI();

        //addon for the party of pokedors button: (use Event System)
        if (ui.btnOpenParty)
        {
            ui.btnOpenParty.onClick.RemoveAllListeners();
            ui.btnOpenParty.onClick.AddListener(OpenPartyPanel);
        }

        //Apply Sprites:(PokeDors & Trainers) ; trainers UNDER CONSTRUCTION
        ApplySprites();

        // 4) Wire the simple "Battle Over" buttons for fast iteration (optional)
        WireBattleOverButtons();

        // 5) Give DPAD/keyboard focus to first move (uGUI Navigation)
        FocusFirstSelectable();

        // fixer for Exit in Prefight
        var btnExit = ui.popupPreFight.transform.Find("Btn_Exit")?.GetComponent<Button>();
        if (btnExit)
        {
            btnExit.onClick.RemoveAllListeners();
            btnExit.onClick.AddListener(() =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("01_Menu");
            });
        }

        //
        // optional: inside popup, Close/Back just hides it
        if (ui.btnOptClose && pop)
        {
            ui.btnOptClose.onClick.RemoveAllListeners();
            ui.btnOptClose.onClick.AddListener(() => pop.Show(false));
            OnPlayerTurn();                 // resume menu + timer (only Interraction with buttons)
        }


        // keep seconds Timer Updated
        baseTurnTime = BattlePrefs.TurnSeconds;

        // Keep pokedors original pos as ref
        _basePosition = transform.localPosition;

        CheckEasterEggs();
        BeginPlayerTurn();
    }

    void Update()
    {
        if (!timerOn || isOver) return;
        timeLeft -= Time.deltaTime;
        UpdateTimerUI();
        if (timeLeft <= 0f)
        {
            timerOn = false;
            // out of time: pick the first enabled move or just end turn
            for (int i = 0; i < ui.moveButtons.Length; i++)
                if (ui.moveButtons[i] && ui.moveButtons[i].interactable) { OnClickMove(i); return; }
            StartCoroutine(EnemyTurn());
        }
        // b = back
        if (menuMode == MenuMode.Moves && Input.GetKeyDown(KeyCode.B))
            BackToMainMenu();

        // MP Multipayer check:
        if (StartBattleOnReady)
        {
            StartBattleOnReady = false;
            StartMultiplayerBattle();
        }
    }

    #endregion

    #region === UI Init & Refresh ===========================================

    /// <summary>Fills names, makes HP sliders display-only, and wires 4 move buttons.</summary>

    public void InitUI()
    {
        // Labels
        ui.txtPlayerName.text = $"PLAYER\n{player.baseData.name}";
        ui.txtEnemyName.text = $"ENEMY\n{enemy.baseData.name}";
        ui.txtPlayerName.text = $"{trainerName}\n{player.baseData.name}"; // can : \n<size=60%>


        // HP sliders (display-only)
        SetupHP(ui.sliderPlayerHP, player.baseData.maxHP, player.hp);
        SetupHP(ui.sliderEnemyHP, enemy.baseData.maxHP, enemy.hp);

        // Moves: set label + click handler *Wrapper
        for (int i = 0; i < ui.moveButtons.Length; i++)
        {
            var btn = ui.moveButtons[i];
            var label = (i < ui.moveLabels.Length) ? ui.moveLabels[i] : null;

            if (i < player.baseData.moves.Count)
            {
                Move mv = player.baseData.moves[i];
                if (label) label.text = mv.name;

                btn.interactable = true;
                btn.onClick.RemoveAllListeners();

                int moveIndex = i; // capture for the lambda
                btn.onClick.AddListener(() => OnClickMove(moveIndex));
            }
            else
            {
                if (label) label.text = "-";
                btn.interactable = false;
                btn.onClick.RemoveAllListeners();
            }
        }

        ui.txtLog.text = "Choose a move!";

        // Popups OFF while we validate combat loop
        if (ui.popupBattleOver) ui.popupBattleOver.SetActive(false);
        if (ui.popupOptions) ui.popupOptions.SetActive(false);

        UpdateHPTexts();            // updates HP text

        //ADDON: for deterministic DPAD movement
        WireGridNavigation();
    }
    
    // Simple Grip for Navigation ( Buttons on GBUI)
    void WireGridNavigation()
    {
        var b = ui.moveButtons;
        if (b.Length < 4) return;

        SetNav(b[0], up: null, down: b[2], left: null, right: b[1]);
        SetNav(b[1], up: null, down: b[3], left: b[0], right: null);
        SetNav(b[2], up: b[0], down: null, left: null, right: b[3]);
        SetNav(b[3], up: b[1], down: null, left: b[2], right: null);

        void SetNav(Button btn, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            var n = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
            btn.navigation = n;
        }
    }

    // ======================
    // Safe PreFight runner - aka = Play Singleplayer! (or Multi)
    // ======================
    public void RunPreFight()
    {

        //Wrapper for if not scene 3
        if (!Scene03Check()) return;   // exit if not in Scene03
        if (ui == null || ui.popupPreFight == null) return;
        
        // when cancel or DC - no prefightpanelpop
        if (!PhotonNetwork.InRoom && GameModeManager.IsMultiplayer)
        {
            Debug.Log("[BATTLE] Multiplayer prefight skipped (not in room).");
            return;
        }

        // Clear Indexs Nums :
        _selectedDexIdx.Clear();
        // Exists in Scene 03_Battle
        trainerName = ui.preFightInpTrainerName && !string.IsNullOrEmpty(ui.preFightInpTrainerName.text)
        ? ui.preFightInpTrainerName.text.Trim() : PhotonLauncher.PlayerNickname;
        //was ": "PLAYER";"

        // Fallback name if empty - NamePatch to carry it from scene 1
        if (ui.preFightInpTrainerName && string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text))
        {
            ui.preFightInpTrainerName.text = PhotonLauncher.PlayerNickname;
        }

        // Check POPUP_PREFIGHT EXISTS?:
        ui.popupPreFight.SetActive(true);
        //Addon for not being able to Interact with 4 main buttons while prefight:
        ui.popupPreFight.transform.SetAsLastSibling();
        //if (ui.mainButtonsGroup) { ui.mainButtonsGroup.interactable = false; ui.mainButtonsGroup.blocksRaycasts = false; }

        // Build a button per species
        var dex = App.I.Dex;  // Species list
        if (ui.preFightBtnTemplate) ui.preFightBtnTemplate.gameObject.SetActive(false);
        // Sorted list of pokedors by ABC:
        var dexSorted = App.I.Dex.OrderBy(sp => sp.name, System.StringComparer.OrdinalIgnoreCase).ToList();

        //if (ui.preFightBtnTemplate) ui.preFightBtnTemplate.gameObject.SetActive(false);

        // Clear old runtime items (keep the template) Destroyer::
        foreach (Transform c in ui.preFightListContent)
            if (ui.preFightBtnTemplate && c != ui.preFightBtnTemplate.transform)
                Destroy(c.gameObject);

        // Make instances of buttons like number of pokedors in database ( duplicate button + sprite + txt )
        for (int i = 0; i < dexSorted.Count; i++)
        {
            var btn = Instantiate(ui.preFightBtnTemplate, ui.preFightListContent);
            btn.name = $"Btn_DexItem_{i}";
            btn.gameObject.SetActive(true);

            //added for sorting List of Pokedors:
            var spData = dexSorted[i];

            // Label
            var lbl = btn.transform.Find("Txt_Label")?.GetComponent<TMPro.TMP_Text>()
                      ?? btn.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (lbl) lbl.text = spData.name;

            // Optional icon
            var icon = btn.transform.Find("Img_Icon")?.GetComponent<Image>();
            if (icon)
            {
                var sp = Resources.Load<Sprite>($"PokeDors/{spData.name}");
                icon.enabled = sp != null;
                if (sp) icon.sprite = sp;
            }

            // Click toggle
            int idx = i; // capture
            int dexIndex = App.I.Dex.IndexOf(spData);       // togglepick needs index of the masterDex - map back to OG List
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => TogglePick(idx, btn));
        }

        // Clear
        if (ui.preFightBtnClear)
        {
            ui.preFightBtnClear.onClick.RemoveAllListeners();
            ui.preFightBtnClear.onClick.AddListener(() =>
            {
                _selectedDexIdx.Clear();
                RefreshPreFightCount();
                // reset item tint
                foreach (Transform c in ui.preFightListContent)
                {
                    var b = c.GetComponent<Button>();
                    if (b && b != ui.preFightBtnTemplate) SetDexItemSelected(b, false);
                }
            });
        }
        /// new addon to maintain the Trainername
        // live-gate Ready when the input changes
        if (ui.preFightInpTrainerName)
            ui.preFightInpTrainerName.onValueChanged.AddListener(_ => RefreshPreFightCount());

        // apply / cancel name - used find and manual reffing ( used to turn prefight panel in scene 3 after ... )
        var btnApply = ui.popupPreFight.transform.Find("Inp_TrainerName/Btn_ApplyName")?.GetComponent<Button>();
        var btnCancel = ui.popupPreFight.transform.Find("Inp_TrainerName/Btn_CancelName")?.GetComponent<Button>();
        if (btnApply)
        {
            btnApply.onClick.RemoveAllListeners();
            btnApply.onClick.AddListener(() =>
            {
                var t = ui.preFightInpTrainerName ? ui.preFightInpTrainerName.text : "";
                if (!string.IsNullOrWhiteSpace(t)) trainerName = t.Trim();
                RefreshPreFightCount();
            });
        }
        if (btnCancel)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(() =>
            {
                if (ui.preFightInpTrainerName) ui.preFightInpTrainerName.text = trainerName;
                RefreshPreFightCount();
            });
        }
        ///End of new addon to maintain Trainername

        // ADDON : Keep SP & MP Seperated in PreFight - Wire Ready Individually
        //WireReadyButton(); (instead - we check if (Sp || MP) )
        if (GameModeManager.IsMultiplayer)
        {
            WireMPReadyButton();
        }
        else
        {
            WireSPReadyButton();
        }

        // Wire the Random Button
        WireRandomButton();
        // Wire Exit Btn :
        WireExitButton();

        RefreshPreFightCount();


        //
        // ---- Trainer picker (optional UI) ----
        var pickRoot = ui.popupPreFight.transform.Find("Inp_TrainerName");
        var imgT = pickRoot ? pickRoot.Find("Img_Trainer")?.GetComponent<UnityEngine.UI.Image>() : null;
        var bPrev = pickRoot ? pickRoot.Find("Btn_TPrev")?.GetComponent<UnityEngine.UI.Button>() : null;
        var bNext = pickRoot ? pickRoot.Find("Btn_TNext")?.GetComponent<UnityEngine.UI.Button>() : null;
        if (imgT && bPrev && bNext)
        {
            var sprites = Resources.LoadAll<Sprite>("Trainers");
            var names = sprites.Select(s => s.name).Distinct().OrderBy(n => n).ToList();
            if (names.Count == 0) names.Add("Default");

            int idx = Mathf.Clamp(names.IndexOf(PlayerPrefs.GetString("trainer_key", names[0])), 0, names.Count - 1);

            void ApplyTrainer()
            {
                var sp = Resources.Load<Sprite>($"Trainers/{names[idx]}");
                imgT.enabled = sp != null; if (sp) { imgT.sprite = sp; imgT.preserveAspect = true; }
                PlayerPrefs.SetString("trainer_key", names[idx]); PlayerPrefs.Save();
            }

            bPrev.onClick.RemoveAllListeners();
            bNext.onClick.RemoveAllListeners();
            bPrev.onClick.AddListener(() => { idx = (idx - 1 + names.Count) % names.Count; ApplyTrainer(); });
            bNext.onClick.AddListener(() => { idx = (idx + 1) % names.Count; ApplyTrainer(); });
            ApplyTrainer();
        }

        //
        RefreshPreFightCount();
        //FocusFirstSelectableIn(ui.preFightListContent); // added to fix UIArrows nav; removed for now
    }

    // Make sure 4 button exists ( menu in Battle )
    void RenderMainMenu()
    {
        menuMode = MenuMode.Main;
        for (int i = 0; i < ui.moveButtons.Length; i++)
        {
            var btn = ui.moveButtons[i];
            var label = (i < ui.moveLabels.Length) ? ui.moveLabels[i] : null;
            btn.onClick.RemoveAllListeners();

            switch (i)
            {
                case 0: if (label) label.text = "FIGHT"; btn.onClick.AddListener(() => OnMenuClick(0)); break;
                case 1: if (label) label.text = "POKEDORS"; btn.onClick.AddListener(() => OnMenuClick(1)); break;
                case 2: if (label) label.text = "POKEDEX"; btn.onClick.AddListener(() => OnMenuClick(2)); btn.interactable = ui.popupPokedex;/* Disable if missing */ break;
                case 3: if (label) label.text = "SURRENDER"; btn.onClick.AddListener(() => OnMenuClick(3)); break;
                default: btn.interactable = false; break;
            }
            btn.interactable = true;
        }
        ui.txtLog.text = $"{trainerName}, choose an action!";
        // StartTurnTimer();    // starts timer - maybe breaks timer when abusing surrender no
        FocusFirstSelectable(); // fix cursdor pos?
    }

    // Make the attacks by the pokedor chosen ( Moves buttons )
    void RenderMovesMenu()
    {
        menuMode = MenuMode.Moves;
        for (int i = 0; i < ui.moveButtons.Length; i++)
        {
            var btn = ui.moveButtons[i];
            var label = (i < ui.moveLabels.Length) ? ui.moveLabels[i] : null;
            btn.onClick.RemoveAllListeners();
            
            // last button is BACK
            if (i == ui.moveButtons.Length - 1)
            {
                if (label) label.text = "BACK";
                btn.onClick.AddListener(BackToMainMenu);
                btn.interactable = true;
                continue;
            }

            if (i < player.baseData.moves.Count)
            {
                var mv = player.baseData.moves[i];
                if (label) label.text = mv.name;
                int moveIndex = i;
                btn.onClick.AddListener(() => UseMove(moveIndex));
                btn.interactable = true;
            }
            else
            {
                if (label) label.text = "-";
                btn.interactable = false;
            }
        }
        ui.txtLog.text = "Choose a move (B = Back).";
    }
    
    // check on which button press - on Menu Clicks:
    void OnMenuClick(int idx)
    {
        if (inputLocked) return;    // ignore spam
        
        // check tim and surender state
        if (confirmSurrenderPending && Time.unscaledTime > confirmUntil)
            confirmSurrenderPending = false;

        if (menuMode != MenuMode.Main) return;

        switch (idx)
        {
            case 0: RenderMovesMenu(); break;             // FIGHT
            case 1: OpenPartyPanel(); break;              // POKÉDORS
            case 2: OpenPokedex(); break;                 // POKÉDEX (stub)
            case 3: HandleSurrender(); break;             // SURRENDER
        }
    }

    // If “Back/More” button already Exists , call this on it:
    void BackToMainMenu()
    {
        confirmSurrenderPending = false;
        menuMode = MenuMode.Main;
        RenderMainMenu();

        for (int i = 0; i < ui.moveButtons.Length; i++)
        {
            var btn = ui.moveButtons[i];
            var label = (i < ui.moveLabels.Length) ? ui.moveLabels[i] : null;
            btn.onClick.RemoveAllListeners();

            switch (i)
            {
                case 0: if (label) label.text = "FIGHT"; btn.onClick.AddListener(() => OnMenuClick(0)); break;
                case 1: if (label) label.text = "POKEDORS"; btn.onClick.AddListener(() => OnMenuClick(1)); break;
                case 2: if (label) label.text = "POKEDEX"; btn.onClick.AddListener(() => OnMenuClick(2)); break;
                case 3: if (label) label.text = "SURRENDER"; btn.onClick.AddListener(() => OnMenuClick(3)); break;
            }
            btn.interactable = true;
        }

        if (ui.txtLog) ui.txtLog.text = $"{trainerName}, choose an action!";
    }

    // Confirm Surrender Mechanism :
    void RenderConfirm(string prompt, System.Action yes, System.Action no)
    {
        ui.txtLog.text = prompt;
        for (int i = 0; i < ui.moveButtons.Length; i++)
        {
            var btn = ui.moveButtons[i];
            var label = (i < ui.moveLabels.Length) ? ui.moveLabels[i] : null;
            btn.onClick.RemoveAllListeners();
            btn.interactable = true;

            if (i == 0) { if (label) label.text = "YES"; btn.onClick.AddListener(() => { yes?.Invoke(); }); }
            else if (i == 1) { if (label) label.text = "NO"; btn.onClick.AddListener(() => { no?.Invoke(); }); }
            else { if (label) label.text = "-"; btn.interactable = false; }
        }
    }

    // Open Surrender Menu YES/NO
    void HandleSurrender()
    {
        RenderConfirm("Surrender?", () => EndBattle("You lost!"), BackToMainMenu);
    }

    // Surrender confirm: “No” must not reset timer (OLD)
    void OpenSurrenderConfirm()
    {
        var popup = transform.Find("Canvas/Popup_Confirm")?.gameObject;
        if (!popup) { HandleSurrender(); return; } // fail-safe
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();

        var yes = popup.transform.Find("Btn_Yes")?.GetComponent<UnityEngine.UI.Button>();
        var no = popup.transform.Find("Btn_No")?.GetComponent<UnityEngine.UI.Button>();

        if (yes)
        {
            yes.onClick.RemoveAllListeners();
            yes.onClick.AddListener(() => {
                popup.SetActive(false);
                HandleSurrender();              //  end logic
            });
        }
        if (no)
        {
            no.onClick.RemoveAllListeners();
            no.onClick.AddListener(() => {
                popup.SetActive(false);         // DO NOT touch timerOn or StartYourTurn
            });
        }
    }
    //

    void UseMove(int moveIndex)
    {
        StopTurnTimer();
        // same as your old OnClickMove(i), then after the enemy turn ends:
        // Back to main menu automatically for clarity
        // (the ApplyDamage / EnemyTurn logic remains yours)
        
        //Idle Wiggle
        if (_idleWiggle != null) { StopCoroutine(_idleWiggle); _idleWiggle = null; }

        OnClickMove(moveIndex);  // call your existing move handler
                                 // after OnClickMove finishes and it's the player's turn again:
        if (!isOver) RenderMainMenu();
        
    }

    void OpenPokedex()
    {
        //

        //
        if (!ui.popupPokedex || !ui.txtPokedex)
        {
            Append("Pokedex missing in scene.");
            return;
        }
        ui.popupPokedex.SetActive(true);
        ui.popupPokedex.transform.SetAsLastSibling();       // always on front

        var p = player.baseData;
        string type = p.type.ToString().ToUpper();
        // If you have TypeChart helpers for weaknesses/resists, plug here;
        // for now, just shows name, type and moves.
        var moves = string.Join(", ", p.moves.ConvertAll(m => m.name));
        ui.txtPokedex.text = $"{p.name}\nTYPE: {type}\nMOVES: {moves}";

        if (ui.btnPokedexClose)
        {
            ui.btnPokedexClose.onClick.RemoveAllListeners();
            ui.btnPokedexClose.onClick.AddListener(() =>
            {
                ui.popupPokedex.SetActive(false);
                RenderMainMenu(); // resume
                transform.SetAsLastSibling();   // set on top
            });
        }
    }
    // Fixed Ver::
    void OpenPokedexFor(PokeDor p)
    {
        if (!ui.popupPokedex) { Append("DoreDex screen coming up next (types, moves, weaknesses)."); return; }
        ui.popupPokedex.SetActive(true);
        ui.popupPokedex.transform.SetAsLastSibling();

        var title = ui.popupPokedex.transform.Find("Txt_Title")?.GetComponent<TMPro.TMP_Text>();
        var body = ui.popupPokedex.transform.Find("Txt_Body")?.GetComponent<TMPro.TMP_Text>();
        var close = ui.popupPokedex.transform.Find("Btn_Close")?.GetComponent<UnityEngine.UI.Button>();
        if (title) title.text = $"{p.baseData.name} — POKEDEX";
        if (body)
        {
            //var types = string.Join("/", p.baseData.PokeType.Select(t => t.ToString()));//
            var moves = string.Join(", ", p.baseData.moves.Select(m => m.name));
            // If you have a TypeChart class exposed, compute weaknesses. Otherwise show stub text.
            //body.text = $"TYPE: {types}\nMOVES: {moves}\n\nWeak/Resist: (coming soon)";
        }
        if (close) { close.onClick.RemoveAllListeners(); close.onClick.AddListener(() => ui.popupPokedex.SetActive(false)); }
    }

    // Example: build a simple PokeDex text for the current player pokedor
void ShowPokeDexFor(PokeDor p)
{
    var popup = GameObject.Find("Popup_Pokedex");
    if (!popup) { Append("DoreDex coming up next (types, moves, weaknesses)."); return; }

    popup.SetActive(true);
    popup.transform.SetAsLastSibling();

    var title = popup.transform.Find("Txt_Title")?.GetComponent<TMPro.TMP_Text>();
    var body  = popup.transform.Find("Txt_Body") ?.GetComponent<TMPro.TMP_Text>();
    var close = popup.transform.Find("Btn_Close")?.GetComponent<UnityEngine.UI.Button>();

    if (title) title.text = $"{p.baseData.name} – DOREDEX";
    if (body)
    {
        string type  = p.baseData.type.ToString();
        string moves = string.Join(", ", p.baseData.moves.ConvertAll(m => m.name));
        body.text = $"TYPE: {type}\nMOVES: {moves}\nWEAK/RESIST: (coming soon)";
    }
    if (close)
    {
        close.onClick.RemoveAllListeners();
        close.onClick.AddListener(() => popup.SetActive(false));
    }
}



    void TogglePick(int dexIndex, Button btn)
    {
        if (_selectedDexIdx.Contains(dexIndex))
            _selectedDexIdx.Remove(dexIndex);
        else
        {
            if (_selectedDexIdx.Count >= 6) { Append("You already picked 6."); return; }
            _selectedDexIdx.Add(dexIndex);
        }

        SetDexItemSelected(btn, _selectedDexIdx.Contains(dexIndex));
        
        // Added SFX Wiggle Moveement
        var rt = btn.GetComponent<RectTransform>();
        if (rt) StartCoroutine(PreFightWiggle(rt));

        RefreshPreFightCount();
    }

    void SetDexItemSelected(Button btn, bool selected)
    {
        // tint the button background to show selection
        var g = btn.targetGraphic;
        if (g) g.color = selected ? new Color(0.85f, 1f, 0.9f) : Color.white;
    }

    void RefreshPreFightCount()
    {
        if (ui.preFightTxtCount) ui.preFightTxtCount.text = $"{_selectedDexIdx.Count} / 6";
        bool nameOK = ui.preFightInpTrainerName && !string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text);
        if (ui.preFightBtnReady) ui.preFightBtnReady.interactable = (_selectedDexIdx.Count == 6) && nameOK;
    }


    void BuildTeamsFromSelection(List<int> picks)
    {
        var dex = App.I.Dex;

        // Player team = chosen 6
        playerTeam.Clear();
        foreach (var idx in picks)
            playerTeam.Add(new PokeDor(dex[idx]));

        // Enemy team = 6 random distinct
        var pool = new List<Species>(dex);
        Species Draw()
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            var s = pool[i];
            pool.RemoveAt(i);
            return s;
        }
        enemyTeam.Clear();
        for (int i = 0; i < 6 && pool.Count > 0; i++)
            enemyTeam.Add(new PokeDor(Draw()));

        // Set actives, remap legacy vars so the rest of your code works unchanged
        playerActive = 0; enemyActive = 0;
        player = playerTeam[playerActive];
        enemy = enemyTeam[enemyActive];
    }


    static void SetupHP(Slider sld, int max, int current)
    {
        if (!sld) return;
        sld.maxValue = max;
        sld.value = current;
        sld.interactable = false; // uGUI: don’t let the player drag it
        sld.navigation = new Navigation { mode = Navigation.Mode.None };
    }
    //
    void RefreshHP()
    {
        // clamp to 0
        player.hp = Mathf.Max(0, player.hp);
        enemy.hp = Mathf.Max(0, enemy.hp);

        // main HUD sliders
        if (ui.sliderPlayerHP) ui.sliderPlayerHP.value = player.hp;
        if (ui.sliderEnemyHP) ui.sliderEnemyHP.value = enemy.hp;

        // HUD numbers (next to sliders)
        var pTxt = ui.sliderPlayerHP ? ui.sliderPlayerHP.transform.Find("Txt_HP_amnt")?.GetComponent<TMP_Text>() : null;
        var eTxt = ui.sliderEnemyHP ? ui.sliderEnemyHP.transform.Find("Txt_HP_amnt")?.GetComponent<TMP_Text>() : null;
        if (pTxt) pTxt.text = $"HP: {player.hp}/{player.baseData.maxHP}";
        if (eTxt) eTxt.text = $"HP: {enemy.hp}/{enemy.baseData.maxHP}";
        
        // Update text
        UpdateHPTexts();

        // FIX HP_Sliders "Ghost" Color
        if (ui.sliderPlayerHP)
        {
            ui.sliderPlayerHP.value = Mathf.Max(0, player.hp);
            if (player.hp <= 0) ui.sliderPlayerHP.value = 0;
        }
        if (ui.sliderEnemyHP)
        {
            ui.sliderEnemyHP.value = Mathf.Max(0, enemy.hp);
            if (enemy.hp <= 0) ui.sliderEnemyHP.value = 0;
        }

        //Guard for Wiggle
        if (ui.imgPlayerPokeDor)
        {
            //ui.imgPlayerPokeDor.rectTransform.localPosition = _originalPlayerPos;
            _hpWigglePlayer = StartCoroutine(HPWiggle(ui.imgPlayerPokeDor.rectTransform, () => player.hp, () => player.baseData.maxHP));
        }
        if (ui.imgEnemyPokeDor)
        {
            //ui.imgEnemyPokeDor.rectTransform.localPosition = _originalEnemyPos;
            _hpWigglePlayer = StartCoroutine(HPWiggle(ui.imgEnemyPokeDor.rectTransform, () => player.hp, () => player.baseData.maxHP));
        }
    }

    // make sure Cursdor will be on first selectable
    public void FocusFirstSelectable()
    {
        if (EventSystem.current == null) return;
        if (ui.moveButtons == null || ui.moveButtons.Length == 0) return;

        StartCoroutine(FocusNextFrame(ui.moveButtons[0].gameObject));
    }
    // Focus arrow on interactable 
    IEnumerator FocusNextFrame(GameObject target)
    {
        yield return null; // wait one frame
        EventSystem.current.SetSelectedGameObject(target);
    }



    // -------- Popup Options wiring --------
    // summary: opens/closes Popup_Options, wires mute/restart/back buttons, and the slider for text speed.
    // notes:   We use SceneManager fallbacks so this compiles even if App singleton has no helpers yet.
    
    // related to PopupOptions (currently unavailable)
    void WireOptions()
    {
        // Open
        if (ui.btnMore /* && pop */)
        {
            ui.btnMore.onClick.RemoveAllListeners();
            //ui.btnMore.onClick.AddListener(() => pop.Show(true));
            //ui.btnOptMute.onClick.AddListener(() => AudioManager.Instance?.ToggleMute());
            ui.btnMore.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySfx("click");
                if (ui.popupOptions)
                {
                    ui.popupOptions.SetActive(true);
                    ui.popupOptions.transform.SetAsLastSibling();
                }
            });
        }

        // Close
        if (ui.btnOptClose  && ui.popupOptions /* && pop */)
        {
            //ui.btnOptClose.onClick.RemoveAllListeners();
            //ui.btnOptClose.onClick.AddListener(() => pop.Show(false));
            ui.btnOptClose.onClick.RemoveAllListeners();
            ui.btnOptClose.onClick.AddListener(() =>
            {
                if (ui.popupOptions) ui.popupOptions.SetActive(false);
                AudioManager.Instance.PlaySfx("click");     // feedback
                StartYourTurn();                             // resume buttons + timer
            });
        }

        // Mute (use AudioManager only + MuteButtonLabel) ***
        if (ui.btnOptMute)
        {
            ui.btnOptMute.onClick.RemoveAllListeners();
            ui.btnOptMute.onClick.AddListener(() =>
            {
                AudioManager.Instance?.ToggleMute();
            });
        }

        // Restart scene (reload scene 03) ***
        if (ui.btnOptBackToRoom)
        {
            ui.btnOptBackToRoom.onClick.RemoveAllListeners();
            ui.btnOptBackToRoom.onClick.AddListener(() =>
            {
                SceneManager.LoadScene("01_Menu");
            });
        }

        // Back to menu
        if (ui.btnOptBackToRoom)
        {
            ui.btnOptBackToRoom.onClick.RemoveAllListeners();
            ui.btnOptBackToRoom.onClick.AddListener(() => SceneManager.LoadScene("01_Menu"));
        }

        // Text-speed slider -> chars/sec
        if (ui.sldTextSpeed)
        {
            ui.sldTextSpeed.minValue = 10f;
            ui.sldTextSpeed.maxValue = 120f;
            ui.sldTextSpeed.wholeNumbers = true;
            ui.sldTextSpeed.value = charsPerSecond;
            ui.sldTextSpeed.onValueChanged.RemoveAllListeners();
            ui.sldTextSpeed.onValueChanged.AddListener(v => charsPerSecond = v);
        }

        pop?.Show(false);         //
        SetOptionsVisible(false); //same but simpler ***
    }

    //Wrapper for Scene03 - "03_Battle" Check
    public bool Scene03Check()
    {
        if (SceneManager.GetActiveScene().name != "03_Battle")
        {
            Debug.LogWarning("[RunPrefight] Called outside Scene 3, ignoring.");
            return false;
        }

        if (ui == null)
        {
            Debug.LogError("[RunPrefight] UI is NULL in Scene03");
            return false;
        }

        return true;
    }


    //added to not rename refs - search by names in scene (Find ynder canvas)
    void WireOptionsPopup()
    {
        var btnMore = transform.Find("Canvas/Btn_More")?.GetComponent<UnityEngine.UI.Button>();
        var popupOpts = transform.Find("Canvas/Popup_Options")?.gameObject;
        var btnBack = popupOpts ? popupOpts.transform.Find("Btn_Back")?.GetComponent<UnityEngine.UI.Button>() : null;

        if (btnMore && popupOpts)
        {
            btnMore.onClick.RemoveAllListeners();
            btnMore.onClick.AddListener(() => {
                AudioManager.Instance.PlaySfx("click");
                popupOpts.SetActive(true);
                popupOpts.transform.SetAsLastSibling(); // above sprites
            });
        }
        if (btnBack && popupOpts)
        {
            btnBack.onClick.RemoveAllListeners();
            btnBack.onClick.AddListener(() => {
                popupOpts.SetActive(false);
                AudioManager.Instance.PlaySfx("click");
            });
        }
    }

    //open / close Helpers popup
    void SetOptionsVisible(bool show)
    {
        if (!ui.popupOptions) return;


        if (pop) pop.Show(show);                // if has PopupOptions component
        else ui.popupOptions.SetActive(show);   // plain GameObject toggle

        // optional: move selection into popup *Fix3d?
        if (show && EventSystem.current)
        {
            var first = ui.btnOptMute ? ui.btnOptMute.gameObject : ui.btnOptClose?.gameObject;
            if (first) EventSystem.current.SetSelectedGameObject(first);
        }
    }

    // new addons for new logic of party of 6 pokedors:
    void OpenPartyPanel()
    {
        if (!ui.panelParty) { Append("Party panel missing."); return; }
        ui.panelParty.SetActive(true);
        //StopTurnTimer();
        ui.panelParty.transform.SetAsLastSibling(); // <- keep on top
        PopulatePartyPanel();


        // wire slot clicks each time we open
        for (int i = 0; i < ui.partyButtons.Length; i++)
        {
            int idx = i;
            if (!ui.partyButtons[i]) continue;
            ui.partyButtons[i].onClick.RemoveAllListeners();
            ui.partyButtons[i].onClick.AddListener(() => ChooseParty(idx, voluntary: true));
        }

        // optional close button - "Btn_PartyClose" by name: ( AutoWire )
        var close = ui.panelParty.transform.Find("Btn_PartyClose")?.GetComponent<Button>();
        if (close)
        {
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() => ui.panelParty.SetActive(false));
        }
    }

    // Populate party panel of pokedors - >onPickedone , HP + Slider + Sprite + FNT(when dead)
    void PopulatePartyPanel()
    {
        for (int i = 0; i < ui.partyButtons.Length && i < playerTeam.Count; i++)
        {
            var slotBtn = ui.partyButtons[i];
            var p = playerTeam[i];
            bool alive = p.hp > 0;
            bool current = (i == playerActive);

            // name with ▶
            if (i < ui.partyLabels.Length && ui.partyLabels[i])
                ui.partyLabels[i].text = (current ? "▶ " : "") + p.baseData.name;

            // HP bar + text + color thresholds
            if (i < ui.partyHP.Length && ui.partyHP[i])
            {
                var sld = ui.partyHP[i];
                sld.maxValue = p.baseData.maxHP;
                sld.value = p.hp;
                // color tier
                float pct = p.hp / (float)p.baseData.maxHP;
                var fill = sld.fillRect ? sld.fillRect.GetComponent<UnityEngine.UI.Image>() : null;
                if (fill) fill.color = (pct <= 0f) ? new Color(0.7f, 0.7f, 0.7f) :
                                            (pct < 0.25f) ? Color.red :
                                            (pct < 0.5f) ? new Color(1f, 0.5f, 0f) :   // orange
                                            (pct < 0.75f) ? Color.yellow : Color.green;
            }
            var hpTxt = slotBtn.transform.Find($"Txt_HP0{i}")?.GetComponent<TMPro.TMP_Text>();
            if (hpTxt) hpTxt.text = $"HP {p.hp}/{p.baseData.maxHP}";

            // icon
            var icon = slotBtn.transform.Find($"Img_Icon0{i}")?.GetComponent<Image>();
            if (icon)
            {
                var sp = Resources.Load<Sprite>($"PokeDors/{p.baseData.name}");
                icon.enabled = sp != null;
                if (sp) { icon.sprite = sp; icon.preserveAspect = true; }
                icon.color = alive ? Color.white : new Color(1f, 0.7f, 0.7f); // reddish KO
            }

            // FNT label
            var fnt = slotBtn.transform.Find($"Txt_Faint0{i}")?.GetComponent<TMP_Text>();
            if (fnt) fnt.gameObject.SetActive(!alive);

            slotBtn.onClick.RemoveAllListeners();
            int idx = i;
            slotBtn.onClick.AddListener(() =>
            {
                if (idx == playerActive) { ui.panelParty.SetActive(false); return; }
                ChooseParty(idx, voluntary: true);
            });
            //Add Wiggle in Party Panel
            // ************************

            // only interactable with alive pokedors
            slotBtn.interactable = alive && !current;
        }
    }

    // Party panel - switch between pokedors 
    void ChooseParty(int idx, bool voluntary)
    {
        if (!TrySwitch(playerTeam, ref playerActive, idx)) return;
        if (inputLocked) return;        // ignore spam

        // re-map to keep your existing code working
        player = PlayerActive;
        enemy = EnemyActive;

        Append($"Go, {player.baseData.name}!");
        if (ui.panelParty) ui.panelParty.SetActive(false);

        // refresh main HUD
        InitUI();
        ApplySprites();
        BeginPlayerTurn();

        // Voluntary switch uses your turn; forced switch (after faint) keeps your turn.
        if (voluntary)
            StartCoroutine(EnemyTurn());
        // voluntary=true when opened from your turn; false when forced after faint.
        // voluntary=true when you opened Party on your turn (enemy acts after).
        // voluntary=false when forced after faint (you keep your turn).
    }

    #endregion

    #region === Turn Flow (Player then Enemy) ================================

    /// <summary>Player clicked one of the move buttons (index 0..3).</summary>
    ///  added option to switch on faint or at will the pokedor you are currently using
    void OnClickMove(int i)
    {
        if (inputLocked) return;    // ignore spam
        timerOn = false;            // added to fix timer issues - work only when can be interactable by players
        if (isOver) return;
        if (i < 0 || i >= player.baseData.moves.Count) return;
        
        //AttackPush AFX

        var mv = player.baseData.moves[i];
        ApplyDamage(attacker: player, defender: enemy, mv: mv, attackerName: player.baseData.name);

        if (enemy.hp <= 0)
        {
            // ENEMY fainted -> auto-pick next alive or win
            int next = -1;
            for (int k = 0; k < enemyTeam.Count; k++)
                if (enemyTeam[k].hp > 0) { next = k; break; }

            // if no more pokedors -> bring Win!;
            if (next == -1) { EndBattle("You won!"); return; }

            enemyActive = next;
            enemy = EnemyActive; // re-map
            Append($"Enemy sent out {enemy.baseData.name}!");

            InitUI();
            ApplySprites();
            RenderMainMenu();
        }

        StartCoroutine(EnemyTurn());
    }

    IEnumerator EnemyTurn()
    {
        // block clicks while animating - AntiSpam
        SetButtonsInteractable(false);
        inputLocked = true;     

        yield return new WaitForSeconds(enemyTurnDelay);
        if (isOver) yield break;

        var emv = enemy.RandomMove();
        ApplyDamage(attacker: enemy, defender: player, mv: emv, attackerName: enemy.baseData.name);
        
        // Added Delay fix Anim
        yield return new WaitForSeconds(0.6f);

        if (player.hp <= 0)
        {
            // PLAYER fainted -> free switch (keep turn)
            int next = -1;
            for (int k = 0; k < playerTeam.Count; k++)
                if (playerTeam[k].hp > 0) { next = k; break; }

            if (next == -1) { EndBattle("You lost!"); yield break; }

            playerActive = next;
            player = PlayerActive; // re-map

            Append($"Go, {player.baseData.name}!");

            InitUI();
            ApplySprites();
            RenderMainMenu();   // You keep your turn
            StartYourTurn();    // added to Fix TimerBug ( i Hope )
            FocusFirstSelectable(); // there to Keep Cursdor on selectables
            
            // DO NOT do another enemy turn here: keep the turn.
            ui.txtLog.text = "Your turn!";
            OnLog?.Invoke(ui.txtLog.text);
            yield break;
        }

        //inputLocked = false;
        RenderMainMenu();             // 4 Buttons
        ui.txtLog.text = "Your turn!";
        OnLog?.Invoke(ui.txtLog.text);
        // Unlocks + reset Timer
        StartYourTurn();             // added to fix choises - not straight Attack buttons
        
    }

    //Start Your Turn : Fixed : ( Anim + InteractableButtons + Timer )
    public void StartYourTurn()
    {
        if (_idleWiggle != null) { StopCoroutine(_idleWiggle); _idleWiggle = null; }

        //AntiSpam Avoiders
        SetButtonsInteractable(true);
        inputLocked = false;

        // Timer  
        turnDuration = Mathf.Max(5f, baseTurnTime);
        timeLeft = turnDuration;
        timerOn = true;

        RenderMainMenu();          // show 4 buttons
        UpdateTimerUI();           // refresh timer UI
        StartTurnTimer();          // actually start countdown
        FocusFirstSelectable();    // ensure cursor resets every turn
    }

    void UpdateTimerUI()
    {
        if (ui.txtTurnTimer) ui.txtTurnTimer.text = Mathf.CeilToInt(Mathf.Max(0, timeLeft)).ToString();
        if (ui.sldTurnTime)
        {
            ui.sldTurnTime.maxValue     = turnDuration;
            ui.sldTurnTime.value        = timeLeft;
            ui.sldTurnTime.interactable = false;        // always display-only
            ui.sldTurnTime.navigation   = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        }
    }

    // added for Turns_timer
    void StartTurnTimer()
    {
        if (_turnTimer != null) StopCoroutine(_turnTimer);  // NlCheck
        //StopTurnTimer(); // try to see if it fixes TimerBug stucks gameFlow

        // wrapper – always safe entry - possibly created Overflow by callin alot
        //BeginPlayerTurn();
        if (ui.txtLog) ui.txtLog.text = "Choose a move!";
        
        //Slider Values for Timer
        float limit = Mathf.Max(5f, baseTurnTime);
        if (ui.sldTurnTime)
        {
            ui.sldTurnTime.maxValue     = limit;
            ui.sldTurnTime.value        = limit;
            ui.sldTurnTime.interactable = false;
        }
        _turnTimer = StartCoroutine(TurnTimer(limit));
    }
    void StopTurnTimer()
    {
        if (_turnTimer != null) { StopCoroutine(_turnTimer); _turnTimer = null; }
        if (ui.txtTurnTimer) ui.txtTurnTimer.text = "";
        if (ui.sldTurnTime) ui.sldTurnTime.value = 0f;
    }
    IEnumerator TurnTimer(float seconds)
    {
        float t = seconds;
        while (t > 0f && !isOver)
        {
            if (ui.txtTurnTimer) ui.txtTurnTimer.text = Mathf.CeilToInt(t).ToString();
            if (ui.sldTurnTime) ui.sldTurnTime.value = t;   // value Bar
            yield return null;
            t -= Time.unscaledDeltaTime;                    // unscaled so Options/pauses don’t break it
        }
        if (isOver) yield break;                            // avoid triggering after battle ends
        if (ui.txtTurnTimer) ui.txtTurnTimer.text = "0";

        // timeout: simple default – use first move or surrender
        if (player.baseData.moves.Count > 0) UseMove(0);
        else HandleSurrender();
    }

    //ADDON: For AnimationFix add delay in between Turns
    // New Corutine Method for fixing animation not working on player turn
    IEnumerator PlayerAttackAndDelay(int i)
    {
        // *** ORIGINAL LOGIC, CONVERTED TO A COROUTINE ***

        // Apply initial checks (optional, but good practice)
        if (isOver) yield break;

        inputLocked = true;
        var mv = player.baseData.moves[i];

        // --- START PLAYER ATTACK ANIMATION ---
        if (ui.imgPlayerPokeDor)
        {
            // 1. Start the Player's push/attack animation
            Coroutine playerAttackAnim = StartCoroutine(
                        AttackPushNew(ui.imgPlayerPokeDor.transform, _originalPlayerPos, isPlayer: true));

            // Let the animation start for a few frames before continuing.
            yield return null; // Wait 1 frame
        }

        // --- START ENEMY HIT ANIMATION (optional, but good to run in parallel) ---

        // 2. Start the Enemy's hit animation (this can run immediately after the player attack)
        if (ui.imgEnemyPokeDor)
        {
            Coroutine enemyHitAnim = StartCoroutine(
                AttackPushNew(ui.imgEnemyPokeDor.transform, _originalEnemyPos, isPlayer: false)
            );

            // Damage is applied *while* the hit animation is playing (or immediately after)
            ApplyDamage(attacker: player, defender: enemy, mv: mv, attackerName: player.baseData.name);

            // 3. WAIT for the enemy hit animation to finish
            yield return enemyHitAnim;
        }
        else
        {
            ApplyDamage(attacker: player, defender: enemy, mv: mv, attackerName: player.baseData.name);
        }

        // Small delay after damage/hit animation for visual clarity
        yield return new WaitForSeconds(0.4f);

        // *** ORIGINAL KO LOGIC ***
        if (enemy.hp <= 0)
        {
            // ENEMY fainted -> auto-pick next alive or win
            int next = -1;
            for (int k = 0; k < enemyTeam.Count; k++)
                if (enemyTeam[k].hp > 0) { next = k; break; }

            if (next == -1) { EndBattle("You won!"); yield break; }

            enemyActive = next;
            enemy = EnemyActive; // re-map
            Append($"Enemy sent out {enemy.baseData.name}!");

            InitUI();
            ApplySprites();
            RenderMainMenu();
        }

        // --- START ENEMY TURN ---
        StartCoroutine(EnemyTurn()); // This now starts *after* the animation delay
    }

    // Updated OnClickMove - Pos is Broken
    void OnClickMoveNew(int i)
    {
        if (inputLocked) return;
        timerOn = false;
        if (isOver) return;
        if (i < 0 || i >= player.baseData.moves.Count) return;

        // Lock input during the attack sequence
        inputLocked = true;

        // Start the new sequence that includes the animation and delay
        StartCoroutine(PlayerAttackAndDelay(i));

        // Note: The rest of the logic from your original OnClickMove has moved 
        // into PlayerAttackAndDelay(i) .
    }

    #endregion

    #region === Damage, End & Log ===========================================

    /// <summary>Apply damage, update HP UI, and add effectiveness messages.</summary>
    void ApplyDamage(PokeDor attacker, PokeDor defender, Move mv, string attackerName)
    {
        // 1) Type effectiveness
        float mult = TypeChart.Mult(mv.type, defender.baseData.type); // type matchup
        
        // 2) Base damage (at least 1), rounded to int
        int dmg = Mathf.Max(1, Mathf.RoundToInt(mv.power * mult));
        
        // 3) Reduce HP, clamp to 0
        defender.hp = Mathf.Max(0, defender.hp - dmg);

        // 4) Log lines for the battle text
        Append($"{attackerName} used {mv.name} for {dmg}!");
        if (mult > 1f) Append("It's super effective!");
        else if (mult < 1f) Append("It's not very effective...");

        // ADDON ::) SFX by Effectiveness of attack: 
        AudioManager.Instance.PlaySfx("attack");
        if (mult > 1f) AudioManager.Instance.PlaySfx("super");
        else if (mult < 1f) AudioManager.Instance.PlaySfx("weak");

        // 5) Refresh the HP sliders in UI
        RefreshHP();

        // 6) added tiny lungle + shake AnimationFX
        if (ui.imgPlayerPokeDor && ui.imgEnemyPokeDor)
        {
            //Fix: reset its position every time before starting the coroutine:
            RectTransform atk = (attacker == player) ? ui.imgPlayerPokeDor.rectTransform : ui.imgEnemyPokeDor.rectTransform;
            RectTransform def = (attacker == player) ? ui.imgEnemyPokeDor.rectTransform : ui.imgPlayerPokeDor.rectTransform;

            atk.localPosition = atk.localPosition; // reset to current
            StopCoroutine("AttackPush");           // stop old coroutine if running
            StartCoroutine(AttackPush(atk, def));
        }
        PopulatePartyPanel();
    }
    void ApplyDamageNew(PokeDor attacker, PokeDor defender, Move mv, string attackerName)
    {
        float mult = TypeChart.Mult(mv.type, defender.baseData.type);
        int dmg = Mathf.Max(1, Mathf.RoundToInt(mv.power * mult));
        defender.hp = Mathf.Max(0, defender.hp - dmg);

        Append($"{attackerName} used {mv.name} for {dmg}!");
        if (mult > 1f) Append("It's super effective!");
        else if (mult < 1f) Append("It's not very effective...");

        AudioManager.Instance.PlaySfx("attack");
        if (mult > 1f) AudioManager.Instance.PlaySfx("super");
        else if (mult < 1f) AudioManager.Instance.PlaySfx("weak");

        RefreshHP();
        PopulatePartyPanel();
    }


    public void EndBattle(string message)
    {
        StopTurnTimer();
        isOver = true;
        StopAllCoroutines();
        foreach (var b in ui.moveButtons) if (b) b.interactable = false;

        bool playerWon = player.hp > 0 && (enemy.hp <= 0 || enemyTeam.TrueForAll(e => e.hp <= 0));
        string title = playerWon ? "WINNER!" : "LOSER...";
        string pokeName = playerWon ? player.baseData.name : enemy.baseData.name;

        if (ui.popupBattleOver)
        {
            ui.popupBattleOver.SetActive(true);
            ui.popupBattleOver.transform.SetAsLastSibling();

            var txt = ui.popupBattleOver.transform.Find("Txt_Title")?.GetComponent<TMP_Text>();
            //if (txt) txt.text = $"{title}\n{pokeName.ToUpper()}";
            if (txt)
            {
                string who = playerWon ? trainerName : "ENEMY";
                string poke = playerWon ? player.baseData.name : enemy.baseData.name;
                //txt.text = $"{(playerWon ? "WINNER!" : "LOSER...")}\n{who} - {poke}";
                txt.text = playerWon
                    ? $"WINNER!\n{trainerName} won with {player.baseData.name}!"
                    : $"LOSER...\nENEMY won with {enemy.baseData.name}!";

            }

            var pokeImg = ui.popupBattleOver.transform.Find("Img_Winner")?.GetComponent<Image>();
            if (pokeImg) pokeImg.sprite = Resources.Load<Sprite>($"PokeDors/{pokeName}");

            var trainerImg = ui.popupBattleOver.transform.Find("Img_Trainer")?.GetComponent<Image>();
            //if (trainerImg)
            //{
            //    var tKey = PlayerPrefs.GetString("trainer_key", "Default");
            //    trainerImg.sprite = Resources.Load<Sprite>($"Trainers/{tKey}") ?? Resources.Load<Sprite>("Trainers/Default");
            //}

            // Trainer sprite
            if (ui.imgWinnerTrainer)
            {
                string trainerKey = PlayerPrefs.GetString("trainer_key", "Default");
                var sp = Resources.Load<Sprite>($"Trainers/{trainerKey}")
                         ?? Resources.Load<Sprite>("Trainers/Default");

                ui.imgWinnerTrainer.enabled = sp != null;
                if (sp)
                {
                    ui.imgWinnerTrainer.sprite = sp;
                    ui.imgWinnerTrainer.preserveAspect = true;
                }
            }

        }
        //ADDON:: fix for winner loser:
        //ShowBattleResult();

        Append(playerWon ? "YOU WON!" : "YOU LOST!");
        AudioManager.Instance.PlaySfx(playerWon ? "win" : "lose");

    }


    //ADDON: fix Typewriter FX on Txt - used Corutine to stop from trext go without limit(bounds)
    // === Typewriter state (uGUI, Coroutine, Lists, Actions) ======================
    [SerializeField] int maxLogLines = 3;       // how many lines to keep in the log
    readonly Queue<string> _pending = new();    // pending lines to type
    Coroutine _typing;
    string _fullLog = "";                        // entire current text content
    float CharsPerSecond => Mathf.Max(5f, charsPerSecond); // slider drives this

    public UnityAction<Scene, LoadSceneMode> OnSceneLoaded { get; private set; }

    void Append(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _pending.Enqueue(line);
        if (_typing == null) _typing = StartCoroutine(TypeLines());
    }
    IEnumerator TypeLines()
    {
        // We step by deltaTime so changing the slider affects the running line instantly.
        var sb = new System.Text.StringBuilder(256);

        while (_pending.Count > 0)
        {
            string next = _pending.Dequeue();
            // add next line to buffer, enforce rolling window of last N lines
            var lines = new List<string>(_fullLog.Split('\n', System.StringSplitOptions.RemoveEmptyEntries));
            lines.Add(next);
            while (lines.Count > maxLogLines) lines.RemoveAt(0);
            string target = string.Join("\n", lines);

            // type from _fullLog -> target
            int startLen = _fullLog.Length;
            int endLen = target.Length;
            float typed = 0f;

            while (startLen + (int)typed < endLen)
            {
                // how many chars this frame?
                typed += CharsPerSecond * Time.unscaledDeltaTime; // unscaled so pause times don’t affect it
                int chars = Mathf.Clamp((int)typed, 0, endLen - startLen);
                sb.Clear();
                sb.Append(target, 0, startLen + chars);
                ui.txtLog.text = sb.ToString();
                yield return null; // next frame, so slider changes apply immediately
            }

            _fullLog = target;              // snap to final
            ui.txtLog.text = _fullLog;
            OnLog?.Invoke(next);            // broadcast the new line
        }

        _typing = null;
    }

    // Converts "Flame Burst" -> "FLAMEBURST" (letters/digits only, uppercased)
    static string KeyFromMoveName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var filtered = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
            if (char.IsLetterOrDigit(c)) filtered.Append(char.ToUpperInvariant(c));
        return filtered.ToString();
    }

    #endregion

    #region === Sprites ======================================================
    
    // Loads PokeDor sprites by species name from Resources/PokeDors/<Name>.png
public void ApplySprites()
{
    if (ui.imgPlayerPokeDor)
    {
        var sp = Resources.Load<Sprite>($"PokeDors/{player.baseData.name}");
        if (sp)
        {
            ui.imgPlayerPokeDor.sprite = sp;
            ui.imgPlayerPokeDor.preserveAspect = true;

            // reset transform position + disable flip
            ui.imgPlayerPokeDor.rectTransform.anchoredPosition = new Vector2(-119f, 183f);
            ui.imgPlayerPokeDor.rectTransform.localScale = Vector3.one; // changed from NEW vector3(1,1,1)
        }

    }

    if (ui.imgEnemyPokeDor)
    {
        var sp = Resources.Load<Sprite>($"PokeDors/{enemy.baseData.name}");
        if (sp)
        {
          ui.imgEnemyPokeDor.sprite = sp;
          ui.imgEnemyPokeDor.preserveAspect = true;
            
          // Fix RectPositioning? 
          ui.imgEnemyPokeDor.rectTransform.localPosition = new Vector2(237f, 154f);
          ui.imgEnemyPokeDor.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // only enemy flipped
        }
    }
        // Breathing FX Via WiggleCode
        if (ui.imgPlayerPokeDor) StartCoroutine(Wiggle(ui.imgPlayerPokeDor.rectTransform));
        if (ui.imgEnemyPokeDor) StartCoroutine(Wiggle(ui.imgEnemyPokeDor.rectTransform));

    }

    Sprite LoadPokeSprite(string pokeName)
    {
        // Expects: Assets/Resources/PokeDors/<Name>.png
        // e.g. Resources/PokeDors/Emberkit
        var sp = Resources.Load<Sprite>($"PokeDors/{pokeName}");
        if (!sp) Debug.LogWarning($"[Sprites] Missing sprite for PokeDor: {pokeName} (Resources/PokeDors/{pokeName})", this);
        return sp;
    }
    Sprite LoadTrainerSprite(string trainerKey)
    {
        // Optional trainers (Assets/Resources/Players/Player.png, Enemy.png)
        var sp = Resources.Load<Sprite>($"Players/{trainerKey}");
        return sp;
    }

    // ------------- ANIMATIONS & FX ------------------ //

    // Wiggle Setup
    IEnumerator Wiggle(RectTransform rt, float amplitude = 4f, float speed = 3f)
    {
        if (!rt) yield break;
        Vector3 basePos = rt.anchoredPosition;   // <– cache
        while (rt && !isOver)
        {
            float t = Time.time * speed;
            rt.anchoredPosition = basePos + new Vector3(0, Mathf.Sin(t) * amplitude, 0);
            yield return null;
        }
        if (rt) rt.anchoredPosition = basePos;   // reset
    }

    //PrefightWiggleMethod:
    IEnumerator PreFightWiggle(RectTransform target)
    {
        Vector3 basePos = target.localPosition;
        float t = 0f;
        while (t < 0.25f)   // short jump
        {
            float offsetY = Mathf.Sin(t * 40f) * 5f;  // up and down
            target.localPosition = basePos + new Vector3(0, offsetY, 0);
            t += Time.deltaTime;
            yield return null;
        }
        target.localPosition = basePos;  // reset
    }

    // --- OLD HP Jiggle Wiggle ---
    [SerializeField] private float _wiggleLimit = 2f;    // adjust in inspector or via slider
    private Vector3 _basePosition;                       // to keep Original Location as ref
    // for exposed Slider Value:
    public void OnWiggleSliderChanged(float value)
    {
        _wiggleLimit = value;  // slider 0–10 for example
    }

    IEnumerator HPWiggle(RectTransform target, Func<int> getHP, Func<int> getMaxHP)
    {
        if (!target) yield break;
        Vector3 basePos = target.localPosition;

        while (!isOver)
        {
            float hp = getHP();
            float maxHp = getMaxHP();

            // intensity grows as HP decreases
            // -() will decrease movement [remove  - for growing intesity movement]
            float intensity = Mathf.Lerp(0.2f, 2f, 1 - (hp / maxHp));   
           
            intensity = Mathf.Clamp(intensity, 0.2f, 2f); // <- keeps it sane

            Vector3 offset = new Vector3(
                Mathf.Sin(Time.time * 12f) * intensity,
                Mathf.Cos(Time.time * 10f) * intensity,
                0);

            target.localPosition = basePos + offset;
            yield return null;
        }

        // reset when finished
        target.localPosition = basePos;
    }

    //Update Wiggle:
    void UpdateWiggle(Transform target)
    {
        float wiggle = Mathf.Sin(Time.time * 10f) * _wiggleLimit;
        target.localPosition = _basePosition + new Vector3(0, wiggle, 0);
    }
    //in player turn wiggle: ( Interaction with buttons + Wiggle )
    Coroutine _hpWigglePlayer, _hpWiggleEnemy;      // stop WiggleStackExponens

    void OnPlayerTurn()
    {
        if (ui.imgPlayerPokeDor)
        {
            _hpWigglePlayer = StartCoroutine(HPWiggle(ui.imgPlayerPokeDor.rectTransform, () => player.hp, () => player.baseData.maxHP));
        }

        if (_hpWigglePlayer != null) StopCoroutine(_hpWigglePlayer);
        if (_hpWiggleEnemy != null) StopCoroutine(_hpWiggleEnemy);

        if (ui.imgPlayerPokeDor)
            _hpWigglePlayer = StartCoroutine(HPWiggle(ui.imgPlayerPokeDor.rectTransform, () => player.hp, () => player.baseData.maxHP));
        if (ui.imgEnemyPokeDor)
            _hpWiggleEnemy = StartCoroutine(HPWiggle(ui.imgEnemyPokeDor.rectTransform, () => enemy.hp, () => enemy.baseData.maxHP));

        StartTurnTimer();


    }
    // Wiggle Via Coding
    Coroutine _idleWiggle;
    IEnumerator IdleLoop()
    {
        while (!isOver && player.hp > 0)
        {
            if (ui.imgPlayerPokeDor)
            {
                float hpPct = Mathf.Clamp01(player.hp / (float)player.baseData.maxHP);
                float amp = Mathf.Lerp(2f, 6f, hpPct); // less HP => smaller wiggle
                yield return Wiggle(ui.imgPlayerPokeDor.rectTransform, 0.35f, amp);
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }
    }
    //Breating FX via Coding
    IEnumerator Wiggle(RectTransform tr)
    {
        while (tr && !isOver)
        {
            float t = Mathf.Sin(Time.time * 3f) * 5f; // breath effect
            tr.localPosition = new Vector3(tr.localPosition.x, tr.localPosition.y + t * 0.01f, tr.localPosition.z);
            yield return null;
        }
    }

    void BeginPlayerTurn()
    {
        inputLocked = false;
        if (_idleWiggle != null) StopCoroutine(_idleWiggle);
        _idleWiggle = StartCoroutine(IdleLoop());
        RenderMainMenu();      // shows 4 buttons
        StartTurnTimer();
    }
    // Movement of Pokedor to the other Pokedor via ChangePos by RectTransform
    IEnumerator AttackPush(RectTransform attacker, RectTransform defender)
    {
        if (!attacker || !defender) yield break;
        
        // stop wiggle if active
        StopCoroutine("Wiggle");

        Vector3 basePos = attacker.localPosition;
        float dir = (attacker.localPosition.x < defender.localPosition.x) ? 1f : -1f;

        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            attacker.localPosition = basePos + new Vector3(Mathf.Sin(t * 25f) * 20f * dir, 0, 0);
            yield return null;
        }
        attacker.localPosition = basePos;
        // restart wiggle
        StartCoroutine(Wiggle(attacker));
    }

    private IEnumerator AttackPushNew(Transform target, Vector3 originalPos, bool isPlayer)
    {
        Vector3 start = originalPos;
        Vector3 targetPos = start + new Vector3(isPlayer ? 40f : -40f, 0, 0);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f;
            target.localPosition = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }

        // snap back
        target.localPosition = originalPos;
        // Debug check to confirm the Coroutine is finishing and resetting position
        Debug.Log($"AttackPushNew finished for {target.name}. Pos set to {originalPos}");

        yield break; // Explicitly exit the coroutine
    }


    // needed to be called in:
    //StartCoroutine(Wiggle(ui.imgPlayerPokeDor.rectTransform)); (on turn start?)
    //when player attacks?
    //if the player attacks -> StartCoroutine(AttackPush(ui.imgPlayerPokeDor.rectTransform, ui.imgEnemyPokeDor.rectTransform));

    #endregion

    #region PlayerPrefs and Helpers
    // check if its the right way:
    [Serializable]
    class BattleState
    {
        public string trainer;
        public int playerActive, enemyActive;
        public int[] playerHP, enemyHP;
        public string[] playerSpecies, enemySpecies;
        public float timeLeft;
    }
    //Anti Spam
    [SerializeField] private Button[] _actionButtons;  // drag from inspector
    public void SetButtonsInteractable(bool interactable)
    {
        foreach (var btn in _actionButtons)
            btn.interactable = interactable;
    }

    void SaveBattleState(float timeLeft)
    {
        var s = new BattleState
        {
            trainer = trainerName,
            playerActive = playerActive,
            enemyActive = enemyActive,
            playerHP = playerTeam.Select(p => p.hp).ToArray(),
            enemyHP = enemyTeam.Select(p => p.hp).ToArray(),
            playerSpecies = playerTeam.Select(p => p.baseData.name).ToArray(),
            enemySpecies = enemyTeam.Select(p => p.baseData.name).ToArray(),
            timeLeft = timeLeft
        };
        PlayerPrefs.SetString("battle_state", JsonUtility.ToJson(s));
        PlayerPrefs.Save();
    }

    bool TryLoadBattleState()
    {
        var raw = PlayerPrefs.GetString("battle_state", "");
        if (string.IsNullOrEmpty(raw)) return false;
        var s = JsonUtility.FromJson<BattleState>(raw);

        trainerName = s.trainer;
        playerTeam = s.playerSpecies.Select(n => new PokeDor(App.I.Dex.First(d => d.name == n)) { hp = 0 }).ToList();
        enemyTeam = s.enemySpecies.Select(n => new PokeDor(App.I.Dex.First(d => d.name == n)) { hp = 0 }).ToList();
        for (int i = 0; i < playerTeam.Count; i++) playerTeam[i].hp = s.playerHP[i];
        for (int i = 0; i < enemyTeam.Count; i++) enemyTeam[i].hp = s.enemyHP[i];
        playerActive = s.playerActive; enemyActive = s.enemyActive;
        player = PlayerActive; enemy = EnemyActive;

        PlayerPrefs.DeleteKey("battle_state");
        ApplySprites(); InitUI(); RenderMainMenu();
        _turnTimer = StartCoroutine(TurnTimer(Mathf.Max(5f, s.timeLeft))); // resume timer
        return true;
    }

    //static string trainerName; To Force Name Apply
    public void OnApplyTrainerName(TMP_InputField input, TMP_Text error)
    {
        var t = (input?.text ?? "").Trim();
        if (t.Length == 0) { if (error) error.text = "Please enter a name."; return; }
        trainerName = t;
        PlayerPrefs.SetString("trainer_name", trainerName);
        PlayerPrefs.Save();
        if (error) error.text = "";
    }

    //Update HP Text near Slider
    void UpdateHPTexts()
    {
        var pTxt = ui.sliderPlayerHP ? ui.sliderPlayerHP.transform.Find("Txt_HP_amnt")?.GetComponent<TMPro.TMP_Text>() : null;
        var eTxt = ui.sliderEnemyHP ? ui.sliderEnemyHP.transform.Find("Txt_HP_amnt")?.GetComponent<TMPro.TMP_Text>() : null;

        if (pTxt) pTxt.text = $"HP: {player.hp}/{player.baseData.maxHP}";
        if (eTxt) eTxt.text = $"HP: {enemy.hp}/{enemy.baseData.maxHP}";
    }

    #endregion

    #region Multiplayer ADDONS: =====================================


    public void TryReconnectOrExit()
    {
        // FIX: Use GameModeManager.IsMultiplayer instead of a local static bool
        if (GameModeManager.IsMultiplayer)
        {
            // FIX: Use the Instance to call the connection method
            if (PhotonLauncher.Instance != null)
            {
                // You mentioned the new method name is ConnectAndJoinRoom()
                PhotonLauncher.Instance.ConnectAndJoinRoom();
                // StartCoroutine(CheckIfRejoined()); // Optional: keep this if it's your custom reconnection logic
            }
            else
            {
                // This is a common error if the scene load order is wrong.
                Debug.LogError("[BattleLogic] Cannot reconnect: PhotonLauncher.Instance is NULL!");
                // Fallback to menu if the singleton is missing (as a safety measure)
                UnityEngine.SceneManagement.SceneManager.LoadScene("01_Menu");
            }
        }
        else
        {
            // Non-multiplayer fallback logic - EXIT
            UnityEngine.SceneManagement.SceneManager.LoadScene("01_Menu");
        }
    }

    private IEnumerator CheckIfRejoined()
    {
        float timeout = 5f; // Wait up to 5 seconds
        float timer = 0f;

        while (!PhotonNetwork.InRoom && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PHOTON] Failed to rejoin room. Returning to menu.");
            PhotonNetwork.Disconnect(); // Optional cleanup
            SceneManager.LoadScene("01_Menu");
        }
    }
    
    // maybe use: PhotonLauncher.CheckAllPlayersReady() instead?
    private IEnumerator WaitForBothPlayersReady()
    {
        Debug.Log("[MP] Waiting for both players to be ready...");

        bool bothReady = false;

        while (!bothReady)
        {
            yield return new WaitForSeconds(0.5f);

            if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
                continue;

            bool allReady = true;

            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (!p.CustomProperties.ContainsKey("Ready") || !(bool)p.CustomProperties["Ready"])
                {
                    allReady = false;
                    break;
                }
            }

            bothReady = allReady;
        }

        Debug.Log("[MP] Both players ready -> Starting multiplayer battle");
        StartMultiplayerBattle();
    }

    public void OnClickReady()
    {
        string serializedTeamData = GetSerializedTeamData(); // You implement this helper
        //PhotonLauncher.Instance.ReadyUp();    // sets "Ready" = true
        PhotonLauncher.Instance.ReadyUp();      // instead of onReadyClicked();

    }

    // Called by PhotonLauncher once all players set Ready = true
    public void StartMultiplayerBattle()
    {
        // Check if MP is Ready and Init :
        if (multiplayerInitialized) return;
        multiplayerInitialized = true;

        // use already built playerTeam
        if (playerTeam.Count == 0)
        {
            BuildTeamsFromSelection(_selectedDexIdx);
        }
        // TEMP: enemy team also random until you sync teams
        if (enemyTeam.Count == 0)
        {
            var dex = App.I.Dex;
            var pool = new List<Species>(dex);
            for (int i = 0; i < 6 && pool.Count > 0; i++)
            {
                int r = UnityEngine.Random.Range(0, pool.Count);
                enemyTeam.Add(new PokeDor(pool[r]));
                pool.RemoveAt(r);
            }
        }
        playerActive = 0;
        enemyActive = 0;
        player = PlayerActive;
        enemy = EnemyActive;

        InitUI();
        ApplySprites();
        WireBattleOverButtons();
        FocusFirstSelectable();
        StartYourTurn();
        /*
        Debug.Log("[BATTLE] Both players ready -> Running PreFight");
        if (PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log("[MP] I'm the MasterClient - I will be Player 1");
                // Setup local player as Player 1
                SetupPlayer(isPlayerOne: true);
            }
            else
            {
                Debug.Log("[MP] I'm NOT the MasterClient - I will be Player 2");
                // Setup local player as Player 2
                SetupPlayer(isPlayerOne: false);
            }
        }

        //  FixRunPreFight & not Init; Open Prefight in team selection when UI&popup RDY :
        if (ui != null && ui.popupPreFight)
        {
            Debug.Log("[BATTLE] Showing PreFight popup for team selection (Multiplayer)");
            RunPreFight();
        }
        */
    }

    public void SetupPlayer(bool isPlayerOne)
    {
        if (isPlayerOne)
        {
            Debug.Log("[Battle] Setup Player 1 (Local)");
            // Assign Player1 side, pokemon, etc.
            // For example:
            currentPlayer = PlayerSlot.Player1;
        }
        else
        {
            Debug.Log("[Battle] Setup Player 2 (Local)");
            // Assign Player2 side, etc.
            currentPlayer = PlayerSlot.Player2;
        }
    }

    private string GetSerializedTeamData()
    {
        // *** PLACEHOLDER *** // This must be replaced with code that converts your 6 selected Pokédors 
        // (e.g., a List<Pokédor> or array) into a single string (e.g., JSON).

        // For now, return a dummy string to fix the compilation error.
        return "DummyTeamData_001";
    }


    // ----------------------
    // Prefight Button Wiring
    // ----------------------

    // Wire Ready ------------ (in RunPreFight()) 
    private void WireReadyButton()
    {
        if (!ui.preFightBtnReady) return;

        ui.preFightBtnReady.onClick.RemoveAllListeners();
        ui.preFightBtnReady.interactable = true;

        if (GameModeManager.IsMultiplayer)
        {
            ui.preFightBtnReady.onClick.AddListener(() =>
            {
                if (_selectedDexIdx.Count != 6) { Append("Pick exactly 6."); return; }

                if (ui.preFightInpTrainerName && string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text))
                    ui.preFightInpTrainerName.text = PhotonLauncher.PlayerNickname;

                PhotonLauncher.Instance.ReadyUp();
                ui.preFightBtnReady.gameObject.SetActive(false); // MP only
            });
        }
        else
        {
            ui.preFightBtnReady.onClick.AddListener(() =>
            {
                if (_selectedDexIdx.Count != 6) { Append("Pick exactly 6."); return; }

                trainerName = (ui.preFightInpTrainerName && !string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text))
                    ? ui.preFightInpTrainerName.text.Trim()
                    : "PLAYER";

                BuildTeamsFromSelection(_selectedDexIdx);
                if (ui.popupPreFight) ui.popupPreFight.SetActive(false);

                isOver = false;
                InitUI();
                ApplySprites();
                WireBattleOverButtons();
                FocusFirstSelectable();
                StartYourTurn();
            });
        }
    }

    // Wire Random ----------- (in RunPreFight())
    private void WireRandomButton()
    {
        var btnRandom = ui.popupPreFight?.transform.Find("Btn_Random")?.GetComponent<Button>();
        if (!btnRandom) return;

        btnRandom.onClick.RemoveAllListeners();
        btnRandom.onClick.AddListener(() =>
        {
            playerTeam.Clear();
            _selectedDexIdx.Clear();

            var all = App.I.Dex.ToList();
            for (int i = 0; i < 6 && all.Count > 0; i++)
            {
                int r = UnityEngine.Random.Range(0, all.Count);
                var picked = all[r];
                _selectedDexIdx.Add(App.I.Dex.IndexOf(picked));
                all.RemoveAt(r);
            }
            RefreshPreFightCount();
        });
    }

    // Wire EXIT ------------- (in RunPreFight())
    private void WireExitButton()
    {
        var btnExit = ui.popupPreFight?.transform.Find("Btn_Exit")?.GetComponent<Button>();
        if (!btnExit) return;

        btnExit.onClick.RemoveAllListeners();

        if (GameModeManager.IsMultiplayer)
        {
            btnExit.onClick.AddListener(() =>
            {
                PhotonLauncher.Instance.OnExitClicked();
            });
        }
        else
        {
            btnExit.onClick.AddListener(() =>
            {
                SceneManager.LoadScene("01_Menu");
            });
        }
    }

    // Wire Singleplayer Ready (in RunPreFight())
    void WireSPReadyButton()
    {
        if (ui.preFightBtnReady)
        {
            ui.preFightBtnReady.onClick.RemoveAllListeners();
            // SP -> build teams and start immediately (no Photon)
            ui.preFightBtnReady.onClick.AddListener(() =>
            {
                if (_selectedDexIdx.Count != 6) { Append("Pick exactly 6."); return; }

                trainerName = (ui.preFightInpTrainerName && !string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text))
                    ? ui.preFightInpTrainerName.text.Trim()
                    : "PLAYER";

                BuildTeamsFromSelection(_selectedDexIdx);
                ui.popupPreFight.SetActive(false);

                // ====== SP FIX: restore full init ======
                isOver = false;
                InitUI();
                ApplySprites();
                WireBattleOverButtons();
                FocusFirstSelectable();
                StartYourTurn(); // restore menu loop
            });

        }
    }

    // Wire Multiplayer Ready  (in RunPreFight())
    private void WireMPReadyButton()
    {
        if (ui.preFightBtnReady)
        {
            ui.preFightBtnReady.onClick.RemoveAllListeners();
            ui.preFightBtnReady.onClick.AddListener(() =>
            {
                if (_selectedDexIdx.Count != 6)
                {
                    Append("Pick exactly 6.");
                    return;
                }

                // lock trainer name
                trainerName = string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text)
                    ? PhotonLauncher.PlayerNickname
                    : ui.preFightInpTrainerName.text.Trim();

                // save team (optional serialize)
                BuildTeamsFromSelection(_selectedDexIdx);

                // call Photon Ready
                PhotonLauncher.Instance.ReadyUp();

                // hide ready button for feedback
                ui.preFightBtnReady.gameObject.SetActive(false);
            });
        }

    }


    #endregion


    #region === Test Wiring (Restart / Back) ================================

    // notes: fast-iteration helpers while testing (from TicTacToe “Play Again”) :: Events
    public void WireBattleOverButtons()
    {
        if (ui.btnRestartBattle)
        {
            ui.btnRestartBattle.onClick.RemoveAllListeners();
            ui.btnRestartBattle.onClick.AddListener(() =>
            {
                // simplest: reload battle scene
                SceneManager.LoadScene("03_Battle");
            });
        }

        if (ui.btnBackToRoom)
        {
            ui.btnBackToRoom.onClick.RemoveAllListeners();
            ui.btnBackToRoom.onClick.AddListener(() =>
            {
                if (SceneManager.GetActiveScene().name == "01_Menu")
                {
                    var back = FindObjectOfType<Btn_Back>();
                    if (back != null) { back.Click(); }
                }
                // go back to overworld/menu during tests
                // replace with App.I.EndBattle() when overworld is Ready
                SceneManager.LoadScene("01_Menu");
            });
        }
        // Added Btn_More to Wired HERE via Events :
        if (ui.btnMore)
        {
            ui.btnMore.onClick.RemoveAllListeners();
            ui.btnMore.onClick.AddListener(() =>
            {
                if (ui.popupOptions)
                {
                    ui.popupOptions.SetActive(true);
                    //ui.popupOptions.Show(true);
                    ui.popupOptions.transform.SetAsLastSibling();
                }
                //if (ui.popupoptions) ui.popupoptions.Show(true);    // from battlerefui Exposed
            });
        }

        //

    }

    //
    public void OnMoreButton()
    {
        if (ui.popupOptions)
        {
            bool isActive = ui.popupOptions.activeSelf;
            ui.popupOptions.SetActive(!isActive);
            AudioManager.Instance.PlaySfx("menu"); // add click sfx
        }
    }
    
    //Easter Egg ADDON::
    void CheckEasterEggs()
    {
        if (trainerName.ToLower().Contains("dor"))
            Append("Easter Egg: Dor unlocked!");
        if (App.I.playerPoke?.baseData.name == "Mewt")
            AudioManager.Instance.PlaySfx("secret");
    }

    #endregion

    #region things i updated and i will delete (or keepas ref) : ==================================
    /* // Template Wire
      static void Wire(Button b, Action a)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => a?.Invoke());
    }

    void WireCommonButton(UnityEngine.UI.Button b)
    {
        if (!b) return;
        b.onClick.AddListener(() => AudioManager.Instance?.PlaySfx("click"));
        }
    */
    /* 
    // IF REFERENCE GBOVERLAY USE: 
    if (GBOverlay.Instance != null)
        {
            GBOverlay.Instance.DoSomething();
        }

     */
    // kept for Test and ref porpuses :
    /*
    void EndBattle1(string msg)
    {
        StopTurnTimer();
        isOver = true;
        timerOn = false;
        StopAllCoroutines();

        foreach (var b in ui.moveButtons) if (b) b.interactable = false;

        bool playerWon = enemy.hp <= 0 || enemyTeam.TrueForAll(p => p.hp <= 0);
        string titleText = playerWon ? "WINNER!" : "LOSER...";
        string name = playerWon ? player.baseData.name : enemy.baseData.name;

        // UI
        if (ui.popupBattleOver)
        {
            ui.popupBattleOver.SetActive(true);
            ui.popupBattleOver.transform.SetAsLastSibling();

            var title = ui.popupBattleOver.transform.Find("Txt_Title")?.GetComponent<TMPro.TMP_Text>();
            if (title) title.text = $"{titleText}\n{name.ToUpper()}";

            var pokedorSprite = Resources.Load<Sprite>($"PokeDors/{name}");
            if (ui.imgWinner) { ui.imgWinner.sprite = pokedorSprite; ui.imgWinner.enabled = pokedorSprite; }

            var tKey = PlayerPrefs.GetString("trainer_key", "Default");
            var trainerSprite = Resources.Load<Sprite>($"Trainers/{tKey}") ?? Resources.Load<Sprite>("Trainers/Default");
            //if (ui.imgWinnerTrainer) { ui.imgWinnerTrainer.sprite = trainerSprite; ui.imgWinnerTrainer.enabled = trainerSprite; }
            if (ui.imgWinnerTrainer)
            {
                tKey = PlayerPrefs.GetString("trainer_key", "Default");
                var sp = Resources.Load<Sprite>($"Trainers/{tKey}") ?? Resources.Load<Sprite>("Trainers/Default");
                ui.imgWinnerTrainer.enabled = (sp != null);
                if (sp) { ui.imgWinnerTrainer.sprite = sp; ui.imgWinnerTrainer.preserveAspect = true; }
            }
        }

        Append(playerWon ? "YOU WON!" : "YOU LOST!");
        AudioManager.Instance?.PlayBgm(playerWon ? "win" : "lose", false);
    }

    void EndBattle_1(string message)
    {
        StopTurnTimer();
        isOver = true;
        timerOn = false;
        StopAllCoroutines();        // added to stop all corutines since game over

        foreach (var b in ui.moveButtons) if (b) b.interactable = false;

        Append(message);

        // Popup visible and on top of UI
        if (ui.popupBattleOver)
        {
            ui.popupBattleOver.SetActive(true);
            ui.popupBattleOver.transform.SetAsLastSibling(); // make sure it's above Sprites
        }

        // Winner text + sprite
        bool playerWon = message.Contains("won");
        AudioManager.Instance?.PlayBgm(playerWon ? "win" : "lose", loop: false);
        string winnerName = playerWon ? player.baseData.name : enemy.baseData.name;

        var title = ui.popupBattleOver
            ? ui.popupBattleOver.transform.Find("Txt_Title")?.GetComponent<TMPro.TMP_Text>() : null;
        if (title) title.text = $"WINNER!\n{winnerName.ToUpper()}";

        // get winner sprite inside popup, find Img_Winner if added :
        var imgWinner = ui.popupBattleOver
            ? ui.popupBattleOver.transform.Find("Img_Winner")?.GetComponent<Image>() : null;
        // Added the trainer as well
        var imgTrainer = ui.popupBattleOver
            ? ui.popupBattleOver.transform.Find("Img_Trainer")?.GetComponent<UnityEngine.UI.Image>() : null;
        if (imgWinner)
        {
            // Fixed to contain WinnerTrainer and WinnerPokedor
            var sp = Resources.Load<Sprite>($"PokeDors/{winnerName}");
            ui.imgWinner.enabled = sp != null;
            if (sp) { ui.imgWinner.sprite = sp; ui.imgWinner.preserveAspect = true; }
        }

        if (ui.imgWinnerTrainer)
        {
            var tKey = PlayerPrefs.GetString("trainer_key", "Default");
            var sp = Resources.Load<Sprite>($"Trainers/{tKey}") ?? Resources.Load<Sprite>("Trainers/Default");
            ui.imgWinnerTrainer.enabled = sp;
            if (sp) { ui.imgWinnerTrainer.sprite = sp; ui.imgWinnerTrainer.preserveAspect = true; }
        }
        // addon to see if fixes the you lose 
        Append(playerWon ? "YOU WON!" : "YOU LOST!");
        AudioManager.Instance?.PlayBgm(playerWon ? "win" : "lose", loop: false);
    }

    void EndBattle_Old(string message)
    {
        isOver = true;

        // Freeze input
        foreach (var b in ui.moveButtons) if (b) b.interactable = false;

        ui.txtLog.text = message;
        OnLog?.Invoke(message);

        // Popup visible and on top of UI
        if (ui.popupBattleOver) ui.popupBattleOver.SetActive(true); // optional panel
        //Addon: Syncs Winner name Text on EndofGame :
        var winnerName = message.Contains("won") ? player.baseData.name : enemy.baseData.name;
        var title = ui.popupBattleOver ?
            ui.popupBattleOver.transform.Find("Txt_Title")?.GetComponent<TMPro.TMP_Text>() : null;
        if (title) title.text = $"WINNER!\n{winnerName.ToUpper()}";

    }
    */
    //Wiggle Via HP - Broke Pos
    /*
         //Fixed Wiggle connected to HP
    System.Collections.IEnumerator Wiggle(RectTransform rt, float baseAmp, float hpRatio)
    {
        if (!rt) yield break;
        float amp = Mathf.Lerp(0.5f, baseAmp, Mathf.Clamp01(hpRatio)); // low HP -> small wiggle
        while (!isOver)
        {
            float t = Time.time * 6f; // speed
            Vector3 p = new Vector3(Mathf.Sin(t) * amp, Mathf.Cos(t * 0.8f) * amp * 0.5f, 0f);
            rt.anchoredPosition3D = p;
            yield return null;
        }
    }
     */

    // void StartYourTurn() Old
    /*
         void StartYourTurn()
    {
        // Fix for Turns being stuck after wiggle get Killed
        if (_idleWiggle != null) { StopCoroutine(_idleWiggle); _idleWiggle = null; }
        inputLocked = false;

        turnDuration = Mathf.Max(5f, baseTurnTime);
        timeLeft = turnDuration;
        timerOn = true;
        inputLocked = false;    // player can interact with buttons

        RenderMainMenu();   // <- func that shows: FIGHT / POKEDEX / POKEDORS / SURRENDER
        //addon begin player turn to fix issues?
        //BeginPlayerTurn();  // **********************************************************
        if (ui.txtLog) ui.txtLog.text = "Choose a move!";

        UpdateTimerUI();
        // addedtryout fixes: ***********************************************************
        StartTurnTimer();
        RenderMainMenu();
        OnPlayerTurn();
    }
     */

    /* 
         IEnumerator AttackPush(RectTransform from, RectTransform to)
    {
        var start = from.anchoredPosition;
        // if 'from' is on the left side, lunge to the right, else to the left
        float dir = (from.anchoredPosition.x <= to.anchoredPosition.x) ? +1f : -1f;
        float dist = 24f;

        // lunge
        float t = 0f;
        while (t < 0.1f) { t += Time.unscaledDeltaTime; from.anchoredPosition = start + new Vector2(dir * Mathf.Lerp(0, dist, t / 0.1f), 0); yield return null; }
        // recoil
        t = 0f;
        while (t < 0.12f) { t += Time.unscaledDeltaTime; from.anchoredPosition = start + new Vector2(dir * Mathf.Lerp(dist, 0, t / 0.12f), 0); yield return null; }
        from.anchoredPosition = start;

        // defender hit shake
        if (to) yield return Wiggle(to, 0.25f, 6f);

    }
     */

    // Addon : Button Fix 2 :
    // === Wire Options Popup ===
    // Opens the popup when Btn_More is clicked, closes when Btn_Back inside popup is clicked.
    /*
    if (ui.btnMore && ui.popupOptions)
    {
        ui.btnMore.onClick.RemoveAllListeners();
        ui.btnMore.onClick.AddListener(() =>
        {
            ui.popupOptions.SetActive(true);
            ui.popupOptions.transform.SetAsLastSibling(); // bring to front
            AudioManager.Instance?.PlaySfx("click");
            // Note: do not stop timer here (esp. for multiplayer).
        });
    }
    */

    /*
        public void Scene03CheckOld()
    {
        if (SceneManager.GetActiveScene().name != "03_Battle")
        {
            Debug.LogWarning("[RunPreFight] Called outside Scene 3, ignoring.");
            return;
        }

        if (ui == null || ui.popupPreFight == null)
        {
            Debug.LogError("[RunPreFight] popupPreFight is NULL in Scene 3!");
            return;
        }
    }

        IEnumerator AttackPushOld(RectTransform target, bool isEnemy)
    {
        if (!target) yield break;
        Vector3 basePos = target.localPosition;
        float dir = isEnemy ? -20f : 20f;  // push direction

        float t = 0f;
        while (t < 0.2f) // quick push
        {
            t += Time.deltaTime;
            target.localPosition = basePos + new Vector3(dir * Mathf.Sin(t * 30f), 0, 0);
            yield return null;
        }
        target.localPosition = basePos;
    }
    IEnumerator AttackPushNew(Transform target, Vector3 originalPos, bool isPlayer)
    {
        float pushDist = isPlayer ? 40f : -40f;  // push direction
        Vector3 pushed = originalPos + new Vector3(pushDist, 0, 0);

        // Move out
        float t = 0;
        while (t < 1f)
        {
            target.localPosition = Vector3.Lerp(originalPos, pushed, t);
            t += Time.deltaTime * 8f;
            yield return null;
        }

        // Snap back
        t = 0;
        while (t < 1f)
        {
            target.localPosition = Vector3.Lerp(pushed, originalPos, t);
            t += Time.deltaTime * 8f;
            yield return null;
        }

        target.localPosition = originalPos; // ensure reset
    }

        public void FocusFirstSelectableOld()
    {
        if (EventSystem.current == null) return;
        if (ui.moveButtons == null || ui.moveButtons.Length == 0) return;
        var first = ui.moveButtons[0];
        if (first) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }


     */

    /* 
         //ADDON: For AnimationFix add delay in between Turns
    // 1. Create a new Coroutine to handle the delayed sequence
    IEnumerator PlayerAttackAndDelay(int i)
    {
        // *** YOUR ORIGINAL LOGIC, CONVERTED TO A COROUTINE ***

        // Apply initial checks (optional, but good practice)
        if (isOver) yield break;

        var mv = player.baseData.moves[i];

        // --- START PLAYER ATTACK ANIMATION ---
        if (ui.imgPlayerPokeDor)
        {
            // 1. Start the Player's push/attack animation
            StartCoroutine(AttackPushNew(ui.imgPlayerPokeDor.transform, _originalPlayerPos, isPlayer: true));

            // Let the animation start for a few frames before continuing.
            yield return null; // Wait 1 frame
        }

        // --- START ENEMY HIT ANIMATION (optional, but good to run in parallel) ---
        if (ui.imgEnemyPokeDor)
        {
            // 2. Start the Enemy's hit/shake animation
            StartCoroutine(AttackPushNew(ui.imgEnemyPokeDor.transform, _originalEnemyPos, isPlayer: false));
        }

        // --- APPLY DAMAGE AFTER ANIMATION START ---
        ApplyDamage(attacker: player, defender: enemy, mv: mv, attackerName: player.baseData.name);

        // *** KEY FIX: WAIT FOR ANIMATIONS TO FINISH ***
        // We assume the total animation time is around 0.5 seconds based on your AttackPushNew logic (t < 1f running at 8x speed ~ 0.25s out + 0.25s back = 0.5s total).
        yield return new WaitForSeconds(0.6f); // Wait a little longer than the animation time (0.5s)


        // *** YOUR ORIGINAL KO LOGIC ***
        if (enemy.hp <= 0)
        {
            // ENEMY fainted -> auto-pick next alive or win
            int next = -1;
            for (int k = 0; k < enemyTeam.Count; k++)
                if (enemyTeam[k].hp > 0) { next = k; break; }

            if (next == -1) { EndBattle("You won!"); yield break; }

            enemyActive = next;
            enemy = EnemyActive; // re-map
            Append($"Enemy sent out {enemy.baseData.name}!");

            InitUI();
            ApplySprites();
            RenderMainMenu();
        }

        // --- START ENEMY TURN ---
        StartCoroutine(EnemyTurn()); // This now starts *after* the animation delay
    }


    // 2. Update your OnClickMove to start the sequence
    void OnClickMoveNew(int i)
    {
        if (inputLocked) return;
        timerOn = false;
        if (isOver) return;
        if (i < 0 || i >= player.baseData.moves.Count) return;

        // Lock input during the attack sequence
        inputLocked = true;

        // Start the new sequence that includes the animation and delay
        StartCoroutine(PlayerAttackAndDelay(i));

        // Note: The rest of the logic from your original OnClickMove has moved 
        // into PlayerAttackAndDelay(i). You should ensure it's removed from here!
    }


        private void TryReconnectOrExit1()
    {
        PhotonLauncher launcher = FindObjectOfType<PhotonLauncher>();
        if (launcher != null)
        {
            PhotonLauncher.Instance.ConnectAndJoinRoom(); // 
            StartCoroutine(CheckIfRejoined());
        }
        else
        {
            Debug.LogError("[ERROR] PhotonLauncher not found — returning to menu.");
            SceneManager.LoadScene("01_Menu");
        }
    }

    private void WireReadyButton1()
    {
        if (GameModeManager.IsMultiplayer)
        {
            // Multiplayer -> only signal "Ready" to Photon
            ui.preFightBtnReady.onClick.AddListener(() =>
            {
                if (_selectedDexIdx.Count != 6) { Append("Pick exactly 6."); return; }

                if (ui.preFightInpTrainerName && string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text))
                    ui.preFightInpTrainerName.text = PhotonLauncher.PlayerNickname;

                PhotonLauncher.Instance.ReadyUp();
                ui.preFightBtnReady.gameObject.SetActive(false); // hide only in MP!
            });
        }
        else
        {
            // Singleplayer -> run prefight immediately, DO NOT hide the button
            ui.preFightBtnReady.onClick.AddListener(() =>
            {
                if (_selectedDexIdx.Count != 6) { Append("Pick exactly 6."); return; }

                trainerName = (ui.preFightInpTrainerName && !string.IsNullOrWhiteSpace(ui.preFightInpTrainerName.text))
                    ? ui.preFightInpTrainerName.text.Trim()
                    : "PLAYER";

                BuildTeamsFromSelection(_selectedDexIdx);

                // hide the prefight popup, not the button
                if (ui.popupPreFight) ui.popupPreFight.SetActive(false);

                // continue into battle
                isOver = false;
                InitUI();
                ApplySprites();
                WireBattleOverButtons();
                FocusFirstSelectable();
                StartYourTurn();
            });
        }

    }

        // was in multiplayer terms in Start():
            
        if (GameModeManager.IsMultiplayer)
        {
            Debug.Log("[BATTLE] Multiplayer Mode Active");

            if (PhotonNetwork.InRoom)
            {
                Debug.Log("[PHOTON] In room -> waiting for both players to Ready...");
                StartCoroutine(WaitForBothPlayersReady()); // This sets up the multiplayer pre-fight
            }
            else
            {
                Debug.LogWarning("[PHOTON] Expected to be in a room, but not in one!");
                // Optional: Try recconect 
                TryReconnectOrExit();
            }
        }
        

     */

    #endregion
}
