using UnityEngine;

public class Btn_Multiplayer : MonoBehaviour
{
    public GameObject panelMainMenu;      // drag Panel_MainMenu in Inspector
    public GameObject panelMultiplayer;   // drag Panel_Multiplayer in Inspector

    public void Click()
    {
        // Hide Main Menu
        if (panelMainMenu != null)
            panelMainMenu.SetActive(false);         // hide menu

        // Show Multiplayer
        if (panelMultiplayer != null)
            panelMultiplayer.SetActive(true);       // show multiplayer

        Debug.Log("Main Menu hidden -> Multiplayer Panel opened.");
    }
    public void OnMultiplayerClick()
    {
        if (panelMainMenu) panelMainMenu.SetActive(false);
        if (panelMultiplayer) panelMultiplayer.SetActive(true);

        GameModeManager.SetModeMulti();
       // PhotonLauncher.IsMultiplayer = true;  // tell BattleLogic to run MP mode
    }
}
