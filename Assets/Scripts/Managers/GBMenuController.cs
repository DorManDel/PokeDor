using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GBMenuController : MonoBehaviour
{
    [Header("Menu order (top to bottom)")]
    public Button[] menuButtons; // assign: SinglePlayer, MultiPlayer, About Dev, Options
    public int currentIndex = 0;

    private void Start()
    {
        Select(currentIndex);
    }

    // NOTE! DOnt Forget Hook Buttons from your UI_Btn_* OnClick events:
    public void OnUp()
    {
        if (menuButtons.Length == 0) return;
        currentIndex = (currentIndex - 1 + menuButtons.Length) % menuButtons.Length;
        Select(currentIndex);
    }

    public void OnDown()
    {
        if (menuButtons.Length == 0) return;
        currentIndex = (currentIndex + 1) % menuButtons.Length;
        Select(currentIndex);
    }

    public void OnA() // confirm
    {
        if (menuButtons.Length == 0) return;
        menuButtons[currentIndex].onClick.Invoke();
    }

    public void OnB() // back/cancel (optional)
    {
        // Do whatever "Back" means in your menu
        // Example: if you have a Back button, call it:
        // backButton.onClick.Invoke();
    }

    private void Select(int index)
    {
        var go = menuButtons[index].gameObject;
        EventSystem.current.SetSelectedGameObject(go);
        // Optional: also visually indicate selection by changing colors/animator on the selected button.
    }
}
