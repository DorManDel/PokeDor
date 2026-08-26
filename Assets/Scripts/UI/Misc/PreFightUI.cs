using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PreFightUI : MonoBehaviour
{
    // Public accessors (other scripts can use them)

    [Header("Wired externally (via BattleUIRefs)")] 
    public RectTransform listContent;       // { get; private set; }
    public GameObject btnTemplate;          // assign in Inspector!
    public TMP_Text txtSelectedCount;       // { get; private set; }
    public Button btnClear;                 // { get; private set; }
    public Button btnRandom;                // { get; private set; }
    public Button btnReady;                 // { get; private set; }

    [Header("Settings")]
    public int maxPicks = 6;

    private bool _initialized = false;

    // Call this manually from BattleLogic.RunPreFight()
    public void Initialize()
    {
        // falseCheck:
        if (_initialized) return;
        _initialized = true;

        Debug.Log("[PreFightUI] Initializing...");
        /*
        // Try to auto-find all required children by name
        listContent = transform.Find("List_Content")?.GetComponent<RectTransform>();
        txtSelectedCount = transform.Find("Txt_SelectedCount")?.GetComponent<TMP_Text>();
        btnClear = transform.Find("Btn_Clear")?.GetComponent<Button>();
        btnRandom = transform.Find("Btn_Random")?.GetComponent<Button>();
        btnReady = transform.Find("Btn_Ready")?.GetComponent<Button>();

        // Guard checks with detailed error logs
        if (listContent == null) Debug.LogError("[PreFightUI] Could not find List_Content under Popup_PreFight!");
        if (txtSelectedCount == null) Debug.LogError("[PreFightUI] Could not find Txt_SelectedCount!");
        if (btnClear == null) Debug.LogError("[PreFightUI] Could not find Btn_Clear!");
        if (btnRandom == null) Debug.LogError("[PreFightUI] Could not find Btn_Random!");
        if (btnReady == null) Debug.LogError("[PreFightUI] Could not find Btn_Ready!");
        */

        // Hook up listeners
        if (btnClear != null) btnClear.onClick.AddListener(ClearSelection);
        if (btnRandom != null) btnRandom.onClick.AddListener(PickRandom);
        if (btnReady != null) btnReady.onClick.AddListener(OnReady);

        UpdateCount();
    }

    // -----------------------
    // Example methods
    // -----------------------
    private void ClearSelection()
    {
        Debug.Log("[PreFightUI] ClearSelection called");
        
        // Clear old buttons
        foreach (Transform c in listContent)
        {
            if (c.gameObject != btnTemplate) Destroy(c.gameObject);
        }
        UpdateCount();
    }

    private void PickRandom()
    {
        Debug.Log("[PreFightUI] PickRandom called");
        // TODO: random selection logic
        UpdateCount();
    }

    private void OnReady()
    {
        Debug.Log("[PreFightUI] OnReady clicked");
        // TODO: trigger ready logic (call ReadyToBattle etc.)
    }

    public void UpdateCount()
    {
        if (txtSelectedCount != null)
            txtSelectedCount.text = $"0/{maxPicks}";
    }
    // INITfromDEX ***
    // ============================================
    //  Build the Dex buttons dynamically
    // ============================================
    public void InitializeFromDex(List<Species> dexList)
    {
        Debug.Log($"[PreFightUI] Building list from Dex ({dexList.Count} entries)");

        //nullcheck : is it missing in scene?
        if (listContent == null || btnTemplate == null)
        {
            Debug.LogError("[PreFightUI] Missing listContent or btnTemplate!");
            return;
        }

        // Clear old buttons
        foreach (Transform c in listContent)
        {
            if (c.gameObject != btnTemplate) Destroy(c.gameObject);
        }

        // Sort alphabetically
        var sorted = dexList.OrderBy(sp => sp.name, StringComparer.OrdinalIgnoreCase).ToList();

        //Creates the Buttons of Pokedors in Prefight Panel
        foreach (var spData in sorted)
        {
            //creates an instance of the button (replicates)
            var btn = Instantiate(btnTemplate, listContent);
            btn.SetActive(true);

            // Label
            var lbl = btn.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl) lbl.text = spData.name;

            // Icon
            var icon = btn.transform.Find("Img_Icon")?.GetComponent<Image>();
            if (icon)
            {
                var sp = Resources.Load<Sprite>($"PokeDors/{spData.name}");
                icon.enabled = sp != null;
                if (sp) icon.sprite = sp;
            }

            // Add click
            btn.GetComponent<Button>().onClick.AddListener(() => OnPickPokedor(btn));
        }

        UpdateCount();
    }



    // Example pick handler
    private void OnPickPokedor(GameObject btnObj)
    {
        Debug.Log($"[PreFightUI] Picked: {btnObj.name}");
        // TODO: Add to chosen dictionary, enforce maxPicks, update UI
    }

}
