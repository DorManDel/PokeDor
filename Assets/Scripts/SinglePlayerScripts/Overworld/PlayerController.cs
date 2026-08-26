using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public float step = 1f;
    public float stepTime = 0.12f;
    bool moving;

    void Update()
    {
        if (moving) return;
        Vector2 dir = Vector2.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir = Vector2.up;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir = Vector2.down;
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dir = Vector2.left;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir = Vector2.right;
        if (dir != Vector2.zero) StartCoroutine(Step(dir));
    }

    IEnumerator Step(Vector2 dir)
    {
        moving = true;
        Vector3 a = transform.position, b = a + (Vector3)(dir * step);
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime / stepTime; transform.position = Vector3.Lerp(a, b, t); yield return null; }
        moving = false;
    }
}
