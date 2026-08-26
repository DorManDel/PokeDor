//Created for Theme consitancy via Coding
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "PokeDor/Theme")]
public class ThemeSO : ScriptableObject
{
    public TMP_FontAsset font;
    public Color buttonNormal = Color.white;
    public Color buttonHighlight = new Color(0.9f, 0.9f, 0.9f);
    public Color buttonDisabled = new Color(0.6f, 0.6f, 0.6f);
    public Color textColor = Color.white;
}