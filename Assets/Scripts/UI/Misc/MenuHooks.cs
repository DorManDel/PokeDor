// Assets/Scripts/UI/MenuHooks.cs
using UnityEngine;
using UnityEngine.SceneManagement;
/// <MenuHook_SUMMURY>
/// UnityButton -> public Methods on components .
/// singleton <APP> knows how to start SinglePLayer to load scene,
/// menuHook = thin adapter to allow menu to call  the methods without couplingUI to AppInternals
///


public class MenuHooks : MonoBehaviour
{
    // called by the "SINGLEPLAYER" button onClick
    public void GoSinglePlayer()
    {
        SceneManager.LoadScene("03_Battle");
    }

    public void GoMenu()
    {
        SceneManager.LoadScene("01_Menu");
    }
}
