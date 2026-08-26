using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;   // UGUI ( Slider )

public class SliderMultiplayer : MonoBehaviour
{
    public Slider sliderMultiplayer;
    public TextMeshProUGUI txtBudgetValue;

    private void Start()
    {
        sliderMultiplayer.onValueChanged.AddListener(OnBudgetChanged);
        OnBudgetChanged(sliderMultiplayer.value); // initialize on start
    }

    private void OnBudgetChanged(float value)
    {
        txtBudgetValue.text = value.ToString("0") + "$";    // show val of slider + $
    }

    private void OnDestroy()
    {
        sliderMultiplayer.onValueChanged.RemoveListener(OnBudgetChanged);
    }
}
