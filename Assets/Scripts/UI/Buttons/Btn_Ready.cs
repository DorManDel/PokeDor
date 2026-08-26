using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;  // for Hashtable
using TMPro; // if using TMP input for name

public class Btn_Ready : MonoBehaviour
{
    public TMP_InputField nameInput;   // assign in inspector
    public GameObject preFightPanel;   // assign in inspector
    public AudioClip readySfx;         // assign in inspector

    public void OnReadyClick()
    {
        // 1. Set player nickname
        string playerName = string.IsNullOrEmpty(nameInput.text) ? "Player" : nameInput.text;
        PhotonNetwork.NickName = playerName;

        // 2. Mark player as ready
        Hashtable props = new Hashtable { { "Ready", true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // 3. Show pre-fight panel (your lobby UI)
        if (preFightPanel) preFightPanel.SetActive(true);

        // 4. Play SFX
        if (readySfx) AudioSource.PlayClipAtPoint(readySfx, Vector3.zero);
    }
}
