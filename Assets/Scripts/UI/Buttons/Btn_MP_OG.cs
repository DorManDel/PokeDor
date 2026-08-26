using UnityEngine;

public class Btn_MP_OG : MonoBehaviour
{
    public MenuLogic menuLogic; // drag in Inspector

    public void Click()
    {
        if (menuLogic != null)
        {
            menuLogic.Btn_MultiPlayer();
        }
        else
        {
            Debug.LogWarning("MenuLogic not set on Btn_MultiPlayer");
        }
    }
    public void OnMultiplayerClick()
    {
        GameModeManager.SetModeMulti();
        // Show Multiplayer panel where player enters name + presses Play
    }
}
