using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MuteButtonLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (AudioManager.Instance != null)
        {
            //UpdateLabel(AudioManager.Instance.IsMuted);
            AudioManager.Instance.OnMuteChanged += RefreshLabel;
        }
            
        //UpdateLabel(AudioManager.Instance.IsMuted);
        RefreshLabel();
    }

    private void OnDisable()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnMuteChanged -= RefreshLabel;
    }

    public void OnClickMute()
    {
        AudioManager.Instance?.ToggleMute();
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (label && AudioManager.Instance)
            label.text = AudioManager.Instance.IsMuted ? "Unmute" : "Mute";
    }
    //added since audiomanager can be destroyed or not existed - so we wait for him
    private IEnumerator Start()
    {
        yield return null; // wait one frame so AudioManager initializes
        RefreshLabel();
    }

}
