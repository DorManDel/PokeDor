using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Btn_SinglePlayer : MonoBehaviour
{
    //[SerializeField] private GameObject _popupPreFightGO;   // assign Popup_PreFight prefab/GO
    //[SerializeField] private GameObject _gameModeManagerGO; // assign in inspector - not possible

    public void Click()
    {
        Debug.Log("[BTN] SinglePlayer pressed!");

        // Set mode -> single
        GameModeManager.SetModeSingle();
        Debug.Log("[MODE] Singleplayer selected");

        // load directly into your SP prefight or battle
        UnityEngine.SceneManagement.SceneManager.LoadScene("03_Battle");
    }


}
