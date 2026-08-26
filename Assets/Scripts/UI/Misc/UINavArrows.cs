// Assets/Scripts/UI/UINavArrows.cs
// summary: Makes your on-screen D-Pad (UP/DOWN/LEFT/RIGHT/A/B) drive uGUI focus.
// how:     Moves the currently selected Selectable using Navigation links.
// notes:   Works with cursor script that follows EventSystem selection.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UINavArrows : MonoBehaviour
{
    public Button up, down, left, right, a, b;

    // Example inside your OnArrowPressed
    //_idx = Mathf.Clamp(idx, 0, names.Count - 1);

    private int _idx = 0;
    public List<string> names = new List<string>(); // or List<GameObject> if you're navigating objects
    [SerializeField] private List<Button> _pokedorButtons;

    void OnArrowPressed(int dir)
    {
        _idx += dir; 
        // dir = +1 (down/right), -1 (up/left)
        _idx = Mathf.Clamp(_idx, 0, names.Count - 1);

        Debug.Log("Current index: " + _idx);
    }


    void Awake()
    {
        //Wire(up, () => Move(Vector2.up));
        //Wire(down, () => Move(Vector2.down));
        Wire(up, () => OnArrowPressed(-1));
        Wire(down, () => OnArrowPressed(+1));
        Wire(left, () => Move(Vector2.left));
        Wire(right, () => Move(Vector2.right));
        Wire(a, Submit);
        Wire(b, Cancel);
    }

    void Wire(Button btn, System.Action act)
    {
        if (!btn) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => act?.Invoke());
    }

    Selectable Current()
    {
        var go = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        return go ? go.GetComponent<Selectable>() : null;
    }

    void Move(Vector2 dir)
    {
        if (dir == Vector2.up) _idx--;
        if (dir == Vector2.down) _idx++;

        //_idx = Mathf.Clamp(_idx, 0, pokedorButtons.Count - 1);

        var cur = Current();
        if (!cur)
        {
            Debug.LogWarning("No current selection for UINavArrows");
            // no selection? pick any interactable Selectable
            var first = GameObject.FindObjectOfType<Selectable>();
            if (first && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            return;
        }

        Selectable next = null;
        if (dir == Vector2.up) next = cur.FindSelectableOnUp();
        else if (dir == Vector2.down) next = cur.FindSelectableOnDown();
        else if (dir == Vector2.left) next = cur.FindSelectableOnLeft();
        else if (dir == Vector2.right) next = cur.FindSelectableOnRight();

        if (next && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(next.gameObject);
    }

    void Submit()
    {
        var go = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (go)
            ExecuteEvents.Execute(go, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }

    void Cancel()
    {
        var go = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (go)
            ExecuteEvents.Execute(go, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
    }
}

/* 
     void Move(Vector2 dir)
    {
        var cur = Current();
        if (!cur)
        {
            Debug.LogWarning("No current selection for UINavArrows");
            // no selection? pick any interactable Selectable
            var first = GameObject.FindObjectOfType<Selectable>();
            if (first && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            return;
        }
 */
