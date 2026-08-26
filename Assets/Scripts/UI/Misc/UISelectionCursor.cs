using UnityEngine;
using UnityEngine.EventSystems;

public class UISelectionCursor : MonoBehaviour
{
    public RectTransform cursdor;              // the arrow image
    public Vector2 offset = new Vector2(0f, 0f);
    [Tooltip("Anything under this is ignored (e.g., UIInterface with the GB controls).")]
    public Transform ignoreRoot;               // drag UIInterface here
    [Tooltip("Keep showing last valid target when selection is null or ignored.")]
    public bool stickToLastValid = true;

    GameObject lastValid;

    void LateUpdate()
    {
        var es = EventSystem.current;
        var go = es ? es.currentSelectedGameObject : null;

        // Ignore selection if it's under the GB controls hierarchy
        if (go && ignoreRoot && go.transform.IsChildOf(ignoreRoot))
            go = null;

        if (!go)
        {
            if (!stickToLastValid)
            {
                if (cursdor) cursdor.gameObject.SetActive(false);
                lastValid = null;
            }
            return;
        }

        var rt = go.GetComponent<RectTransform>();
        if (!rt || !cursdor) return;

        // Re-parent cursor to selected button so it follows layout
        cursdor.gameObject.SetActive(true);
        cursdor.SetParent(rt, worldPositionStays: false);
        cursdor.anchoredPosition = offset;
        lastValid = go;
    }
}
