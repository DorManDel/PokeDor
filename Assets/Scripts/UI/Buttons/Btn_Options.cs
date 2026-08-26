using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Btn_Options : MonoBehaviour
{
    public MenuLogic menuLogic;

    public void Click()
    {
        if (menuLogic != null)
            menuLogic.Btn_Options();
    }
}
