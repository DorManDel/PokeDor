using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Btn_Play : MonoBehaviour
{
    public MenuLogic menuLogic; // connecting to avoid null ref
    public void Click()
    {
        if (menuLogic != null)
            menuLogic.Btn_Play();
        else
        {
            Debug.LogWarning("MenuLogic reference not set on Btn_Play");
        }
    }
}
