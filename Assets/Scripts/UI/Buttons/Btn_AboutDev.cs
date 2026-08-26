using UnityEngine;

public class Btn_AboutDev : MonoBehaviour
{
    public MenuLogic menuLogic;

    public void Click()
    {
        if (menuLogic != null)
            menuLogic.Btn_AboutDev();
    }
}
