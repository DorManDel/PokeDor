// summary: Simple show/hide controller for the Options popup.
// how:     Uses CanvasGroup so it blocks raycasts and interaction when visible.
// notes:   Call Show(true/false) from BattleLogic (Btn_More opens, Back closes).

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class PopupOptions : MonoBehaviour
{
    CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        // start hidden (safe even if already hidden)
        Show(false);
    }

    /// <summary>Enable/disable + block raycasts so buttons behind don’t get clicks.</summary>
    public void Show(bool show)
    {
        gameObject.SetActive(show);
        gameObject.SetActive(true);                 // active so CanvasGroup works
        cg.alpha = show ? 1f : 0f;
        cg.interactable = show;
        cg.blocksRaycasts = show;
        //if (!show) gameObject.SetActive(false);     // hide fully
    }
}
