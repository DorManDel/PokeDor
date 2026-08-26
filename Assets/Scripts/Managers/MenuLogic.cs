using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MenuLogic : MonoBehaviour
{
    // Dictionary Mapping -= Map of all UI panels
    private Dictionary<string, GameObject> _screensDictionary;
    
    private enum ScreensStates  //  Panels aka Screens (List)  
    {
        MainMenu, SinglePlayer , MultiPlayer , AboutDev , Loading , Options , Play 
    };
    
    private ScreensStates _currentScreen;
    private ScreensStates _prevScreen;
    //Stack - for Back navigation
    private Stack<ScreensStates> _screenHistory = new Stack<ScreensStates>();
    //----------------------------------------------------------------
    // F U N C T I O N S ___________________(UNITY)
    private void OnEnable()
    {
        //
    }
    private void OnDisble()
    {
        //
    }
    private void Awake()
    {
        InitAwake();
        //InitInstance();
    }
    void Start()
    {
        Debug.Log("start" + name );
        InitStart();
    }
    void Update()
    {
        //EasterEgg Related --------------------------------------
        if (tapTimer > 0f)
        {
            tapTimer -= Time.deltaTime;
            if (tapTimer <= 0f) titleTapCount = 0; // reset
        }
        // ------------------------------------------------------
    }
    /*
     * Fix SingleTon - no Duplicates of certain OBJs/Scripts
    private void InitInstance()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    */

    // ---------------------------------------------------------------
    // F U N C T I O N S ___________________(ASSISTANCE)
    private void InitAwake()
    {
        _screensDictionary = new Dictionary<string, GameObject>();   
        GameObject[] screensList = GameObject.FindGameObjectsWithTag("ScreenPanels");// change tag to ScreenPanels
        foreach (GameObject obj in screensList)
        {
            // if there’s a duplicate name, last one wins; guard if you prefer
            _screensDictionary.Add(obj.name, obj);    // add objs to list
        }
        Debug.Log("Tagged objects: " + _screensDictionary.Count);          //counter of tags
    }

    private void InitStart()
    {
        // make sure MainMenu will be active on Start;
        _currentScreen = ScreensStates.MainMenu;
        _prevScreen = _currentScreen;   // safe default
        _screensDictionary["Panel_MainMenu"].SetActive(true);

        /* string = name of Panel , GameOBJ = the actual OBJ in scene
        * pair.Key = the name of the panel (e.g., "Panel_SinglePlayer")
        * pair.Value = the actual GameObject(UI panel) 
        */

        //AutoDisable - All Panels Except MainMenu Panel:
        foreach (KeyValuePair<string, GameObject> pair in _screensDictionary)
        {
            if (pair.Key == "Panel_MainMenu")
                pair.Value.SetActive(true);
            else
                pair.Value.SetActive(false);
        }
        Debug.Log("StartGameManager");

    }
    // _______BUTTONS________ //
    public void Btn_SinglePlayer()
    {
        Debug.Log("Btn_SinglePlayer");
        GameModeManager.SetModeSingle();        //  Mark as singleplayer
        ChangeScreen(ScreensStates.SinglePlayer);
    }
    public void Btn_MultiPlayer()
    {
        Debug.Log("Btn_MultiPlayer");
        GameModeManager.SetModeMulti();         // play will send to loading
        ChangeScreen(ScreensStates.MultiPlayer);
    }
    public void Btn_AboutDev()
    {
        Debug.Log("Btn_AboutDev");
        ChangeScreen(ScreensStates.AboutDev);
    }
    public void Btn_Options()
    {
        Debug.Log("Btn_Options");
        ChangeScreen(ScreensStates.Options);
    }
    public void Btn_Play()
    {
        Debug.Log("Btn_Play()");
        ChangeScreen(ScreensStates.Loading);// under construction
    }

    //Generic Back Button
    public void Btn_Back()    
    {
        if (_screenHistory.Count > 0)
        {
            _screensDictionary["Panel_" + _currentScreen].SetActive(false);
            _currentScreen = _screenHistory.Pop();
            _screensDictionary["Panel_" + _currentScreen].SetActive(true);

            Debug.Log("Back to: " + _currentScreen);
        }
        else
        {
            Debug.Log("No previous screen.");
        }
    }
    // _________________________________
    private void ChangeScreen(ScreensStates toScreen)
    {
        //Generic Switch between screens - Stack Style
        _prevScreen = _currentScreen;
        _screenHistory.Push(_currentScreen); // push current into History stack  - keep track 

        _screensDictionary["Panel_" + _currentScreen].SetActive(false);
        _screensDictionary["Panel_" + toScreen].SetActive(true);

        _currentScreen = toScreen;

        Debug.Log("Switched to: " + toScreen);

    }
    #region EASTEREGG Wrapper
    int titleTapCount = 0;
    float tapTimer = 0f;



    public void OnTitleClicked()
    {
        if (tapTimer <= 0f) tapTimer = 10f; // start window
        titleTapCount++;

        if (titleTapCount >= 10)
        {
            AudioManager.Instance?.PlaySfx("secret"); // add secret.wav in Resources/SFX
            Debug.Log("SECRET EGG UNLOCKED!");
            titleTapCount = 0;
            tapTimer = 0f;
        }
    }

    #endregion
}
