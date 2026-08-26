//App.cs:
//global singleton that hold the data:
// DEX + PLAYESTATE + SCENE TRANSITIONS

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class App : MonoBehaviour
{
    public static App I { get; private set; }

    public int selectedIndex = 0;           // which starter the user picked
    public PokeDor playerPoke;              // runtime instance for battle
    public List<Species> Dex { get; private set; }

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Dex = PokeDex.CreatePokeDors();     // create our Pool of PokeDors ( our pokedex )
    }

    // called from menu
    public void StartSingleplayer()
    {
        //Explain:: playerPoke = new PokeDor(Dex[Mathf.Clamp(selectedIndex, 0, Dex.Count - 1)]);
        //SelectedIndex = Menu Selection by User in Indexform
        //Mathf.Clamp(x, min, max) forces a value into a safe range
        //If selectedIndex is −3 it becomes 0; if it’s 999 it becomes Dex.Count-1
        // Dex = species Defenition
        //new PokeDor(species) creates a runtime creature from that species (with fresh HP, etc.),
        // and stores it in playerPoke.
        playerPoke = new PokeDor(Dex[Mathf.Clamp(selectedIndex, 0, Dex.Count - 1)]);
        // SceneManager.LoadScene("02_Overworld");
    }

    // called from overworld triggers
    public void BeginBattle() => SceneManager.LoadScene("03_Battle");

    // called from battle UI (Back to Room) // inactive for now _ UnderConstruction
    //public void EndBattle() => SceneManager.LoadScene("02_Overworld");
    //Addon Helpers for SceneManagment(Wrapper)
    public void RestartBattleScene() => SceneManager.LoadScene("03_Battle");
    public void GoMenu() => SceneManager.LoadScene("01_Menu");
}
