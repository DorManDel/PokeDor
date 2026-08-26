// Assets/Scripts/UI/GBOverlay.cs
// summary: Keeps one overlay alive across scenes and parents to the top canvas if needed.

// add: if want to use:
/*
  if (GBOverlay.Instance != null)
{
    GBOverlay.Instance.DoSomething();
}
 */

using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GBOverlay : MonoBehaviour
{
    static GBOverlay _instance;
    public static GBOverlay Instance => _instance;

    //Wrapper to Fix RayCast Memleak - because no Destroy
    GraphicRaycaster _ray;
    Canvas _canvas;
    //void OnEnable() { TryFindRay(); }
    void Start() { TryFindRay(); }
    private void Update()
    {
        if (!_ray) { TryFindRay(); return; }
    }

    void TryFindRay()
    {
        if (_ray && _canvas) return;
        // var canvas = FindObjectOfType<Canvas>();
        //if (canvas) _ray = canvas.GetComponent<GraphicRaycaster>();
        _canvas = FindObjectOfType<Canvas>();
        _ray = _canvas ? _canvas.GetComponent<GraphicRaycaster>() : null;

        //tryout to fix Null after destroy::        not use as singleton anymore
        //DontDestroyOnLoad(gameObject);
    }


    void Awake()
    {
        //Checks there is instance of GBOverlay and make sure no Destroy between Scenes
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        //DontDestroyOnLoad(gameObject);  // must be (prefab) Root
        
        //if (transform.parent == null) // only root survives - make sure parent ROOT
        //    DontDestroyOnLoad(gameObject);

        TryFindRay();
        ReparentToTopCanvas();
    }


    void OnEnable() => SceneManager.activeSceneChanged += (_, __) => ReparentToTopCanvas();
    void OnDisable() => SceneManager.activeSceneChanged -= (_, __) => ReparentToTopCanvas();

    private void ReparentToTopCanvas()
    {
        if (this == null || gameObject == null) return; // <---- ADD THIS GUARD

        Canvas topCanvas = FindObjectOfType<Canvas>();
        if (topCanvas != null)
        {
            transform.SetParent(topCanvas.transform, false);
            transform.SetAsLastSibling();
        }
    }

    void OnDestroy()
    {
        Debug.Log("[GBOverlay] Destroyed.");
        // Prevent static references from keeping a dead object
        if (_instance == this) _instance = null;
    }

}

/* 
 
    void ReparentToTopCanvasOld1()
    {
        var canvases = FindObjectsOfType<Canvas>(true);
        if (canvases == null || canvases.Length == 0) return;

        var top = canvases.OrderBy(c => c.sortingOrder).LastOrDefault();
        if (top && transform.parent != top.transform)
            transform.SetParent(top.transform, false);
    }

    void ReparentToTopCanvasOld()
    {
        if (transform.parent != null && transform.parent.GetComponent<Canvas>()) return;
        var canvases = FindObjectsOfType<Canvas>(true);
        var top = canvases.OrderBy(c => c.sortingOrder).LastOrDefault();
        if (top) transform.SetParent(top.transform, false);
    }
 */
