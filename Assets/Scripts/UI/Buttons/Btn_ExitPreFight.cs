using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class Btn_ExitPrefight : MonoBehaviour
{
    public void OnClickExit()
    {
        if (GameModeManager.IsMultiplayer)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            SceneManager.LoadScene("01_Menu");
        }

        GameModeManager.ResetPreFight();
    }
    public void OnExitPreFight()
    {
        if (GameModeManager.IsMultiplayer)
            PhotonNetwork.LeaveRoom();

        GameModeManager.ResetPreFight();
        UnityEngine.SceneManagement.SceneManager.LoadScene("01_Menu");
    }
}
