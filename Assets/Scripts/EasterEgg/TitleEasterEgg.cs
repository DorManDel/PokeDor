using UnityEngine;

public class TitleEasterEgg : MonoBehaviour
{
    private int tapCount = 0;
    private float timer = 0f;
    public float timeLimit = 10f;

    void Update()
    {
        if (tapCount > 0)
        {
            timer += Time.deltaTime;
            if (timer > timeLimit)
            {
                tapCount = 0;
                timer = 0;
            }
        }
    }

    public void OnTitleTapped()
    {
        tapCount++;
        if (tapCount >= 10)
        {
            Debug.Log("Easter Egg unlocked!");
            AudioManager.Instance?.PlaySfx("secret");
            tapCount = 0; // reset
        }
    }
}
