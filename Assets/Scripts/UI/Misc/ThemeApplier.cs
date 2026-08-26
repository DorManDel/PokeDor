using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ThemeApplier : MonoBehaviour
{
    public ThemeSO theme;
    void Awake()
    {
        if (!theme) return;

        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (theme.font) t.font = theme.font;
            t.color = theme.textColor;
        }
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            var c = b.colors;
            c.normalColor = theme.buttonNormal;
            c.highlightedColor = theme.buttonHighlight;
            c.disabledColor = theme.buttonDisabled;
            b.colors = c;
        }
    }
}
