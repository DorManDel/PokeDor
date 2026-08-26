// Assets/Scripts/UI/UIRaycastProbe.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastProbe : MonoBehaviour
{
    //What am i Clicking on the Canvas???
    GraphicRaycaster _ray;
    PointerEventData _pe;
    Canvas _canvas;
    readonly List<RaycastResult> _hits = new();

    void OnEnable() { TryFindRay(); }
    void Start() { TryFindRay(); }

    void TryFindRay()
    {
        if (_ray && _canvas) return;
        //var canvas = FindObjectOfType<Canvas>();
        //if (canvas) _ray = canvas.GetComponent<GraphicRaycaster>();
        _canvas = FindObjectOfType<Canvas>();
        _ray = _canvas ? _canvas.GetComponent<GraphicRaycaster>() : null;

    }

    void Update()
    {
        if (!_ray) { TryFindRay(); return; }
        if (EventSystem.current == null) return;

        _pe ??= new PointerEventData(EventSystem.current);
        _pe.position = Input.mousePosition;

        _hits.Clear();
        _ray.Raycast(_pe, _hits);

        // Addon for Raycast Blocker Handler (DEBUG:)
        if (Input.GetMouseButtonDown(0))
        {
            foreach (var hit in _hits)
                Debug.Log($"Hit {hit.gameObject.name} (raycast target={hit.gameObject.GetComponent<Graphic>()?.raycastTarget})");
        }


        // IMPORTANT: no per-frame spam. Only log on click (or remove this entirely).
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) && _hits.Count > 0)
            Debug.Log($"UI hit: {_hits[0].gameObject.name}");
#endif
    }
}
