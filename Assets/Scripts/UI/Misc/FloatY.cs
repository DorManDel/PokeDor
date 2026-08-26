using UnityEngine;

public class FloatY : MonoBehaviour
{
    public float amplitude = 6f, speed = 1.2f;
    Vector3 basePos;
    void Awake() { basePos = transform.localPosition; }
    void Update()
    {
        transform.localPosition = basePos + new Vector3(Mathf.Sin(Time.time * speed) * 1.5f,
                                                         Mathf.Cos(Time.time * speed) * amplitude, 0f);
    }
}
