using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//same logic as sliderMusic
//[UI Slider] → [SliderMusic.cs] → [AudioManager.Instance] → [AudioSource.volume]

public class SliderSFX : MonoBehaviour
{
    public Slider sliderSfx;
    public TextMeshProUGUI sfxValue;

    private void Start()
    {
        sliderSfx.value = AudioManager.Instance.sfxVolume;
        sliderSfx.onValueChanged.AddListener(OnSFXSliderChanged);
    }

    private void OnSFXSliderChanged(float value)
    {
        sfxValue.text = (value * 100).ToString("0") + "%"; // Display %
        //AudioManager.Instance.SetSFXVolume(value);  // set volume to val of Slider
        AudioManager.Instance.SetSfx01(value);        // Safe Wrapper 

    }

    private void OnDestroy()
    {
        sliderSfx.onValueChanged.RemoveListener(OnSFXSliderChanged);
    }
}
