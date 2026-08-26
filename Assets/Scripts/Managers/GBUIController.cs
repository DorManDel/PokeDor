using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GBUIControllerAuto : MonoBehaviour
{
    // AutoPanels Recognizer by Label
    [Header("Auto-discovery")]
    public RectTransform canvasRoot;          // drag your root Canvas (or it will auto-find the first Canvas)
    public string panelNamePrefix = "Panel_"; // panels are any objects whose name starts with this
    public string PopupNamePrefix = "Popup_"; // panels are any objects whose name starts with this

    [Header("Optional")]
    public Button backButton;                 // invoked when pressing B
    public bool includeInactiveButtons = false; // usually false for menus

    // --- internal state (cached) ---
    private Transform[] _panels = Array.Empty<Transform>();
    private Transform _currentPanel;
    private Button[] _buttons = Array.Empty<Button>();
    private int _buttonCount = 0;
    private int _index = 0;

    void Awake()
    {
        if (!canvasRoot)
        {
            var cv = FindObjectOfType<Canvas>();
            if (cv) canvasRoot = cv.GetComponent<RectTransform>();
        }
        DiscoverPanels();
        BindToActivePanel();
    }

    void Update()
    {
        var active = GetActivePanelTopmost();
        if (active != _currentPanel)
        {
            _currentPanel = active;
            RebindButtons();
        }
    }

    // ---------- public API (hook these from Game Boy UI buttons) ----------
    public void OnUp() { Move(-1); }
    public void OnDown() { Move(+1); }
    public void OnA() { if (_buttonCount > 0) _buttons[_index].onClick.Invoke(); }
    public void OnB() { if (backButton) backButton.onClick.Invoke(); }
    public void OnStart() { /* optional: same as A */ OnA(); }
    public void OnSelect() { /* optional: open options etc. */ }

    // ---------- internals ----------
    void DiscoverPanels()
    {
        if (!canvasRoot) return;

        // collect all RectTransforms under canvasRoot whose name starts with prefix
        var all = canvasRoot.GetComponentsInChildren<RectTransform>(true);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i].name.StartsWith(panelNamePrefix, StringComparison.Ordinal))
                count++;

        var panels = new Transform[count];
        int k = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i].name.StartsWith(panelNamePrefix, StringComparison.Ordinal))
                panels[k++] = all[i].transform;

        _panels = panels;
    }

    Transform GetActivePanel()
    {
        for (int i = 0; i < _panels.Length; i++)
        {
            var p = _panels[i];
            if (p && p.gameObject.activeInHierarchy) return p; // first active panel wins
        }
        return null;
    }
    // Choose the visible (top-most) active panel among all panels we know.
    Transform GetActivePanelTopmost()
    {
        Transform best = null;
        int bestIdx = -1;

        for (int i = 0; i < _panels.Length; i++)
        {
            var p = _panels[i];
            if (!p || !p.gameObject.activeInHierarchy) continue;

            int s = p.GetSiblingIndex(); // higher = drawn later = on top
            if (best == null || s > bestIdx) { best = p; bestIdx = s; }
        }
        return best;
    }


    void BindToActivePanel()
    {
        _currentPanel = GetActivePanelTopmost();
        RebindButtons();
    }

    void RebindButtons()
    {
        _buttons = Array.Empty<Button>();
        _buttonCount = 0;
        _index = 0;

        if (!_currentPanel)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            return;
        }

        // Get ALL buttons under this panel (any depth)
        var raw = _currentPanel.GetComponentsInChildren<Button>(true); // allocate only on panel change
        // Filter and copy into array without Linq
        var tmp = new Button[raw.Length];
        int n = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            var b = raw[i];
            if (!b) continue;
            if (!includeInactiveButtons && !b.gameObject.activeInHierarchy) continue;
            if (!b.interactable) continue;
            tmp[n++] = b;
        }
        // Sort top->bottom by y (screen space world y works in overlay)
        Array.Sort(tmp, 0, n, new ButtonYDescComparer());

        // shrink to exact size
        _buttons = new Button[n];
        Array.Copy(tmp, _buttons, n);
        _buttonCount = n;

        if (_buttonCount > 0) Select(0);
        else EventSystem.current?.SetSelectedGameObject(null);

        Debug.Log($"[GBUI] Bound {_buttonCount} buttons from '{_currentPanel.name}' -> {string.Join(", ", Array.ConvertAll(_buttons, b => b.name))}");
    }

    void Move(int delta)
    {
        if (_buttonCount == 0) return;
        _index = (_index + delta + _buttonCount) % _buttonCount;
        Select(_index);
    }

    void Select(int i)
    {
        var go = _buttons[i].gameObject;
        _buttons[i].Select();
        EventSystem.current?.SetSelectedGameObject(go);
    }

    sealed class ButtonYDescComparer : System.Collections.Generic.IComparer<Button>
    {
        public int Compare(Button a, Button b)
        {
            if (a == b) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            float ay = ((RectTransform)a.transform).position.y;
            float by = ((RectTransform)b.transform).position.y;
            // descending (top first)
            return by.CompareTo(ay);
        }
    }
}
