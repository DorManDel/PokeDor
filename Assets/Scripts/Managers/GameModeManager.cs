// in charge of switch between singleplayer and multiplayer
// ==========================
// Global GameMode Manager
// ==========================

/// <summary>
/// Holds whether we are in Single or Multiplayer mode.
/// Static so we can check it from anywhere.
/// </summary>
using UnityEngine;

public enum GameMode { SinglePlayer, MultiPlayer }

public static class GameModeManager
{
    public static bool IsMultiplayer { get; private set; }
    public static GameMode CurrentMode { get; internal set; }

    //add guard for preFight not Popping Randomly from other methods
    private static bool _preFightOpened = false;

    public static void SetModeSingle()
    {
        IsMultiplayer = false;
        CurrentMode = GameMode.SinglePlayer;
        _preFightOpened = false; // reset guard
        ResetPreFight();         // Extra guard
        Debug.Log("[MODE] Singleplayer selected");
    }

    public static void SetModeMulti()
    {
        IsMultiplayer = true;
        CurrentMode = GameMode.MultiPlayer;
        _preFightOpened = false; // reset guard
        ResetPreFight();         // extra guard
        Debug.Log("[MODE] Multiplayer selected");
    }
    /// <summary>
    /// Only let Prefight run once per game entry.
    /// Call this from BattleLogic.RunPreFight()
    /// </summary>
    public static bool TryOpenPreFight()
    {
        if (_preFightOpened) return false;
        _preFightOpened = true;
        return true;
    }

    public static void ResetPreFight()
    {
        _preFightOpened = false;
    }
}
