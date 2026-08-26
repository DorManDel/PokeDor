#region Notes*   ==================================================================
// Assets/Scripts/SinglePlayerScripts/Battle/BattleUIRefs.cs

// summary: Auto-finds UI widgets in the Battle canvas by name/tag so you don’t drag refs.
// how:    Searches under a resolved "RootCanvas" and gathers move buttons from
//         "Buttons_Container" OR tag "UI_Buttons" (sorted by name).
// OUR Decoupler - seperates the buttons from scene wiring

// NOTE: RootCanvas
//   We first try "parent Canvas". If this object isn't under a Canvas (e.g., it’s a sibling),
//   we fall back to finding a Canvas in the scene (prefer a Canvas containing "UIInterface",
//   else the topmost by sortingOrder). That makes the binder work NO MATTER where the script sits.

// NOTE: Navigation
//   Unity UI Navigation controls keyboard/controller focus movement between widgets.
//   We keep HP sliders non-selectable in BattleLogic via:
//     slider.navigation = new Navigation { mode = Navigation.Mode.None };

// Concepts used here (quick refs):
//   • uGUI (Unity UI): Buttons, Sliders, TMP_Text under a Canvas + GraphicRaycaster(non/Interact)
//   • LINQ: FirstOrDefault / OrderBy / Where for concise lookups
//   • Lists/Arrays: gather, sort, and slice (“take first 4 buttons”)
//   • (Elsewhere) Singletons: App, AudioManager (one global instance)
//   • (Elsewhere) Events/Actions: BattleMove.OnClickMove broadcasts the move index
#endregion

using System;
using System.Collections.Generic;                           // Dictionary Library
using System.Linq;                                          // LINQ helpers (FirstOrDefault, OrderBy, etc.)
using TMPro;                                                // TextMeshPro (TMP_Text)
using UnityEngine;                                          // core Unity types
using UnityEngine.UI;                                       // UI components (Slider, Button, etc.)

public class BattleUIRefs : MonoBehaviour
{
    #region Required
    [Header("Names, HP, Log (required)")]                   // Header = editor attribute that groups fields in Inspector w/ title.
    public TMP_Text txtPlayerName, txtEnemyName, txtLog;    // TMP text Public [Labels] (init all 3)
    public Slider sliderPlayerHP, sliderEnemyHP;            // Init Sliders (Public)

    [Header("Move Buttons (required: 4)")]
    public Button[] moveButtons = new Button[0];            // the 4 buttons found
    public TMP_Text[] moveLabels = new TMP_Text[0];         // label inside buttons [child]

    [Header("Battle Over Popup (recommended)")]
    public GameObject popupBattleOver;                      // show on Win/Lose
    public Button btnRestartBattle, btnBackToRoom;          // Restart / Exit Battle

    // winner UI inside Popup_BattleOver
    public TMP_Text txtWinTitle;                            // "WINNER!"
    public Image imgWinner;                                 // winner sprite
    
    [Header("BattleOver (extra)")]
    public UnityEngine.UI.Image imgWinnerTrainer;           // WinnerTrainer Sprite; from:: resources/trainers;

    //Header that will hold all images - expose in inspector <image> /include_UI using::
    [Header("Images (optional)")]
    public Image imgPlayer, imgEnemy, imgPlayerPokeDor, imgEnemyPokeDor;

    //Prefight choosing 6 pokedors popup:
    [Header("PreFight (optional)")]
    public GameObject popupPreFight;                        // Popup Prefight - Choose your Poison!
    public Transform  preFightListContent;                  // List_Content
    public Button     preFightBtnTemplate;                  // Btn_DexItem (template; kept inactive)
    public TMP_Text   preFightTxtCount;                     // Txt_SelectedCount
    public Button     preFightBtnReady, preFightBtnClear;   // Ready and Clear Buttons
    public TMP_InputField preFightInpTrainerName;           // Input_TrainerName (optional)
    List<string> _trainerKeys = new();                      // Trainer Picker in Prefight (List)
    int _trainerIdx = 0;                                    // Trainer Index for the Prefight


    // holds the party of 6 pokedors ( like real bag of pokedors ):
    [Header("Party (optional)")]
    public GameObject panelParty;                           // root panel (inactive by default)
    public Button[] partyButtons = new Button[0];           // Btn_Party00..05
    public Slider[] partyHP = new Slider[0];                // Sld_PartyHP00..05
    public TMP_Text[] partyLabels = new TMP_Text[0];        // labels inside buttons

    // button to open party of PokeDors
    [Header("Party Open (optional)")]
    public Button btnOpenParty;                             // Button PokeDor - need to be added!!!

    //Timer section - txt and sld
    [Header("Turn Timer (optional)")]
    public TMPro.TMP_Text txtTurnTimer;                     // Add text at mid top of timer per turn
    public UnityEngine.UI.Slider sldTurnTime;               // shows slider that ,oves acoording to timer

    //Pokedex Button 
    [Header("Pokedex (optional)")]
    public GameObject popupPokedex;
    public TMP_Text txtPokedex;
    public Button btnPokedexClose;

    #endregion

    //--------------------------------------------------------------------------------------------

    #region Optional - Options Popup 
    [Header("Options (optional)")]
    public Button btnMore;                                  // opens / closes = popup_Options
    public GameObject popupOptions;                         // the panel itself (starts Inactive)
    public Button btnOptRestart, btnOptBackToRoom, btnOptExitMenu, btnOptMute, btnOptClose;
    public TMP_Text txtOptMuteLabel;                        // (optional) label near mute
    public Slider sldOptMusic, sldOptSFX;

    public PopupOptions PopupOptions;                       // fix for PopupOptions ?

    [Header("Options: extra controls (optional)")]
    public Slider sldTextSpeed;                             // binds the typewriter speed slider
    
    //[Header("HPLabel & HPSlidr")]
    //public TMP_Text hpLabelPlayer;
    //public TMP_Text hpLabelEnemy;

    #endregion

    //--------------------------------------------------------------------------------------------
    #region Binding Helpers (top-level to avoid compiler quirks)

    [Header("Manual override (optional)")]
    public Transform canvasRootOverride;                    //  can drag a Canvas transform here 

    //RootCanvas -
    /// if we didnt find in canvas itself (search in the children) - we go to search in the parents layer
    Transform ResolveCanvasRoot()
    {
        if (canvasRootOverride) return canvasRootOverride;
        var parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas) return parentCanvas.transform;
        var canvases = FindObjectsOfType<Canvas>(true);
        return (canvases != null && canvases.Length > 0) ? canvases[0].transform : transform;
    }

    static T FindByName<T>(Transform root, string name) where T : Component =>
        root.GetComponentsInChildren<T>(true).FirstOrDefault(x => x.name == name);

    static Transform FindT(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
    #endregion

    //--------------------------------------------------------------------------------------------

    #region Binding
    /// <summary>
    /// RootCanvas Used here:
    /// Locate all widgets by name/tag under the *Canvas root* and cache them in the fields.
    /// Returns true if all required pieces are found; else returns false + human-readable error.
    /// </summary>
    public bool TryBind(out string error)
    {
        error = null;
        
        // Scene guard – only bind in Battle scene:
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene != "03_Battle")
        {
            error = $"[UI] TryBind skipped – active scene is {scene}, not 03_Battle.";
            return false;
        }
        //create root
        var root = ResolveCanvasRoot();
        if (!root) { error = "No Canvas found for BattleUIRefs."; return false; }

        Debug.Log($"[UI] Binding under root: {root.name}", root);//:;

        // ADDON:: local helper – searches *under root*
        static T FindByName<T>(Transform root, string name) where T : Component =>
            root.GetComponentsInChildren<T>(true).FirstOrDefault(x => x.name == name);
        //var canvas = GetComponentInParent<Canvas>();    // we search for component canvas (no matter where script livwes)

        Debug.Log($"[UI] Binding under root: {root.name}", root); // tiny debug

        // required widgets - find objs by name and type FindBy
        txtPlayerName = txtPlayerName ?? FindByName<TMP_Text>(root, "Txt_PlayerName");
        txtEnemyName = txtEnemyName ?? FindByName<TMP_Text>(root, "Txt_EnemyName");
        txtLog = txtLog ?? FindByName<TMP_Text>(root, "Txt_Log");
        sliderPlayerHP = sliderPlayerHP ?? FindByName<Slider>(root, "Slider_PlayerHP");
        sliderEnemyHP = sliderEnemyHP ?? FindByName<Slider>(root, "Slider_EnemyHP");

        // Turn timer UI (optional) – search anywhere under Canvas - 
        txtTurnTimer = txtTurnTimer ?? FindByName<TMP_Text>(root, "Txt_Timer");
        sldTurnTime = sldTurnTime ?? FindByName<Slider>(root, "Sld_TurnTime");

        // move buttons: strictly Btn_Move00..Btn_Move03 inside "Buttons_Container"
        var container = FindT(root, "Buttons_Container");
        if (!container)
        {
            error = "Missing UI: Buttons_Container (place the 4 move buttons inside it)";
            return false;
        }

        var allBtns = container.GetComponentsInChildren<Button>(true).ToList();
        // Keep only the first 4 prefixed exactly with Btn_Move0X and sort by name for stability
        var filtered = allBtns
            .Where(b => b && b.name.StartsWith("Btn_Move0", StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.name)
            .Take(4)
            .ToArray();

        moveButtons = filtered;
        moveLabels = moveButtons.Select(b => b.GetComponentInChildren<TMP_Text>(true)).ToArray();

        // Pokedex Addon::
        popupPokedex = popupPokedex ?? FindT(root, "Popup_Pokedex")?.gameObject;
        if (popupPokedex)
        {
            txtPokedex = txtPokedex ?? popupPokedex.transform.Find("Txt_Pokedex")?.GetComponent<TMP_Text>();
            btnPokedexClose = btnPokedexClose ?? popupPokedex.transform.Find("Btn_Close")?.GetComponent<Button>();
        }

        // battle-over popup (optional)
        popupBattleOver = popupBattleOver ?? FindT(root, "Popup_BattleOver")?.gameObject;
        if (popupBattleOver)
        {
            var p = popupBattleOver.transform;
            btnRestartBattle = btnRestartBattle ?? p.Find("Btn_RestartBattle")?.GetComponent<Button>();
            btnBackToRoom = btnBackToRoom ?? p.Find("Btn_BackToRoom")?.GetComponent<Button>();
            txtWinTitle = txtWinTitle ?? p.Find("Txt_Title")?.GetComponent<TMP_Text>();
            imgWinner = imgWinner ?? p.Find("Img_Winner")?.GetComponent<Image>();
            imgWinnerTrainer = imgWinnerTrainer ?? p.Find("Img_WinnerTrainer")?.GetComponent<Image>();
        }


        // options popup (optional)
        btnMore = btnMore ?? FindByName<Button>(root, "Btn_More");
        popupOptions = popupOptions ?? FindT(root, "Popup_Options")?.gameObject;
        PopupOptions = PopupOptions ?? FindT(root, "Popup_Options")?.GetComponent<PopupOptions>();  // fix?

        if (popupOptions)
        {
            var p = popupOptions.transform;
            btnOptRestart = btnOptRestart ?? p.Find("Btn_Restart")?.GetComponent<Button>();
            btnOptBackToRoom = btnOptBackToRoom ?? p.Find("Btn_BackToRoom")?.GetComponent<Button>();
            btnOptExitMenu = btnOptExitMenu ?? p.Find("Btn_ExitToMenu")?.GetComponent<Button>();
            btnOptMute = btnOptMute ?? p.Find("Btn_Mute")?.GetComponent<Button>();
            btnOptClose = btnOptClose ?? p.Find("Btn_OptClose")?.GetComponent<Button>(); // close-only
            txtOptMuteLabel = txtOptMuteLabel ?? p.Find("Txt_MuteLabel")?.GetComponent<TMP_Text>();
            sldOptMusic = sldOptMusic ?? p.Find("Sld_Music")?.GetComponent<Slider>();
            sldOptSFX = sldOptSFX ?? p.Find("Sld_SFX")?.GetComponent<Slider>();

            //
            sldTextSpeed = sldTextSpeed ?? popupOptions.transform.Find("Sld_TextSpeed")?.GetComponent<Slider>();

            // --- add image lookups (pass *root*; keep names EXACT as in Hierarchy) ---
            // NOTE: RootCanvas — we search under the resolved canvas root, so this works
            // no matter where the script sits.
            imgPlayer = imgPlayer ?? FindByName<Image>(root, "Img_Player");
            imgEnemy = imgEnemy ?? FindByName<Image>(root, "Img_Enemy");
            imgPlayerPokeDor = imgPlayerPokeDor ?? FindByName<Image>(root, "Img_PlayerPokeDor");
            imgEnemyPokeDor = imgEnemyPokeDor ?? FindByName<Image>(root, "Img_EnemyPokeDor");
        }

        // Bind pokedor party of 6 (pokeDorTeam):
        // -------- Party popup (optional) --------
        btnOpenParty = btnOpenParty ?? FindByName<Button>(root, "Btn_Pokedor");   // optional HUD button
        panelParty = panelParty ?? FindT(root, "Panel_Party")?.gameObject;
        if (panelParty)
        {
            var p = panelParty.transform;
            partyButtons = Enumerable.Range(0, 6)
                .Select(i => p.Find($"Btn_Party0{i}")?.GetComponent<Button>())
                .Where(b => b != null).ToArray();

            partyLabels = partyButtons
                .Select(b => b.GetComponentInChildren<TMP_Text>(true))
                .ToArray();

            partyHP = Enumerable.Range(0, 6)
                .Select(i => p.Find($"Sld_HP0{i}")?.GetComponent<Slider>())
                .Where(s => s != null).ToArray();
        }

        // -------- PreFight popup (optional) --------
        popupPreFight = popupPreFight ?? FindT(root, "Popup_PreFight")?.gameObject;
        if (popupPreFight)
        {
            var p = popupPreFight.transform;
            preFightListContent = preFightListContent ?? p.Find("List_Content");
            preFightBtnTemplate = preFightBtnTemplate ?? p.Find("List_Content/Btn_DexItem")?.GetComponent<Button>();
            preFightTxtCount = preFightTxtCount ?? p.Find("Txt_SelectedCount")?.GetComponent<TMP_Text>();
            preFightBtnReady = preFightBtnReady ?? p.Find("Btn_Ready")?.GetComponent<Button>();
            preFightBtnClear = preFightBtnClear ?? p.Find("Btn_Clear")?.GetComponent<Button>();
            preFightInpTrainerName = preFightInpTrainerName ?? p.Find("Inp_TrainerName")?.GetComponent<TMP_InputField>();
        }

        // relate to the party of pokedors button - opener
        btnOpenParty = btnOpenParty ?? FindByName<Button>(root, "Btn_Pokedor");

        // Bind Image Winner (Trainer and pokedor):
        //ui.imgWinnerTrainer = ui.imgWinnerTrainer ?? ui.popupBattleOver.transform.Find("Img_WinnerTrainer")?.GetComponent<Image>();

        // Missing stuff handling:
        var miss = new List<string>();
        if (!txtPlayerName) miss.Add("Txt_PlayerName");
        if (!txtEnemyName) miss.Add("Txt_EnemyName");
        if (!txtLog) miss.Add("Txt_Log");
        if (!sliderPlayerHP) miss.Add("Slider_PlayerHP");
        if (!sliderEnemyHP) miss.Add("Slider_EnemyHP");
        if (moveButtons.Length < 4) miss.Add("Move buttons (Btn_Move00..Btn_Move03 under Buttons_Container)");

        if (miss.Count > 0) { error = "Missing UI: " + string.Join(", ", miss); return false; }
        return true;

    }
    #endregion

    //--------------------------------------------------------------------------------------------

}

