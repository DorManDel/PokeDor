using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//[UI Slider] → [SliderMusic.cs] → [AudioManager.Instance] → [AudioSource.volume]

public class SliderMusic : MonoBehaviour
{
    public Slider sliderMusic;
    public TextMeshProUGUI musicValue;  // Display Beside Slider
    public TextMeshProUGUI handleValue; // for handleSlider dynamicValue

    private void Start()
    {
        if (!AudioManager.Instance)
            Debug.LogError("AudioManager.Instance is NULL – check if AudioManager prefab is in scene and persistent.");
        if (!sliderMusic)
            Debug.LogError("SliderMusic: slider not assigned!");

        // Sync with current music volume
        sliderMusic.value = AudioManager.Instance.musicVolume;

        sliderMusic.onValueChanged.AddListener(OnMusicSliderChanged);
        // Update text on start too (Init)
        UpdateText(sliderMusic.value);

    }
    private void UpdateText(float value)
    {
        string percentText = (value * 100).ToString("0") + "%";

        if (musicValue != null)
            musicValue.text = percentText;

        if (handleValue != null)
            handleValue.text = percentText;
    }

    private void OnMusicSliderChanged(float value)
    {
        Debug.Log("MUSIC SLIDER CHANGED: " + value);
        musicValue.text = (value * 100).ToString("0") + "%";
        //AudioManager.Instance.SetMusicVolume(value * 100);
        //AudioManager.Instance.SetMusicVolume(value);
        AudioManager.Instance.SetMusic01(value); // new safe wrapper

        UpdateText(value);  // added for update -
    }


    private void OnDestroy()
    {
        sliderMusic.onValueChanged.RemoveListener(OnMusicSliderChanged);
    }
}
