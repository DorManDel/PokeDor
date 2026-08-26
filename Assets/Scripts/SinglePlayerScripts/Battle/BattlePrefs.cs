#region Assets/Scripts/SinglePLayerScripts/Battle/BattlePrefs.cs
//tiny addon PlayerPrefs to keep everything updated if goes to options

#endregion
using UnityEngine;

public static class BattlePrefs
{
    const string KEY_TIMER = "battle_turn_seconds";
    public static float TurnSeconds
    {
        get => PlayerPrefs.GetFloat(KEY_TIMER, 30f);
        set { PlayerPrefs.SetFloat(KEY_TIMER, Mathf.Clamp(value, 5f, 90f)); PlayerPrefs.Save(); }
    }
}
