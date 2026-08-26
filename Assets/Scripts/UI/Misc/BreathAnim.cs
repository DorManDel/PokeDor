using UnityEngine;
using UnityEngine.UI;

public class BreathAnim : MonoBehaviour
{
    public Sprite[] frames;  // slice atlas into frames in inspector
    public float fps = 10f;

    private Image img;
    private int index;

    void Awake() => img = GetComponent<Image>();

    void Update()
    {
        if (frames.Length == 0) return;
        index = (int)(Time.time * fps) % frames.Length;
        img.sprite = frames[index];
    }
}
