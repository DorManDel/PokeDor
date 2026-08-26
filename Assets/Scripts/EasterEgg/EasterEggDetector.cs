using UnityEngine;
using UnityEngine.Events;

public class EasterEggDetector : MonoBehaviour
{
    public enum GBKey { Up, Down, Left, Right, A, B, Start, Select }
    /* 
     i use Listener to UI Buttons (EventsSystem)
    when it detects the correct Sequence -> turns Event in  inspector(SFX + Change Panel)
    works with onClick()
     saved the current correct sequense as "new GBKey[]"
   
    * KMP = Knuth–Morris–Pratt, a classic pattern-matching algorithm.
      smart matching that never re-checks work.

    * LPS = Longest Proper Prefix = Suffix.
      the precomputed “fallback map” that tells you how far to keep your progress on a mismatch.
      LPS runs on Awake once, checks Overlap Lenghts

    Eg.:
    index:    0  1  2  3  4  5  6  7  8  9
    token:   Up Up Do Do Le Ri Le Ri  B  A
    lps:      0  1  0  0  0  0  0  0  0  0

    saves our Sequence as Array of Buttons we pressed - 
    if it fits what we have - we do Action
    
     */
    [Header("Sequence (editable)")]
    [SerializeField]
    private GBKey[] sequence = new GBKey[] {
        GBKey.Up, GBKey.Up, GBKey.Down, GBKey.Down,
        GBKey.Left, GBKey.Right, GBKey.Left, GBKey.Right,
        GBKey.B, GBKey.A
        // add GBKey.Start here if i want the GB variant to end with >Start
    };

    [Header("Timing")]
    [Tooltip("Max seconds between presses before progress resets")]
    [SerializeField] private float inputTimeout = 4f;

    [Header("Events")]
    public UnityEvent easterEggUnlock;
    //for test purposes - see the buttons i press
    [Header("Debug")]
    public bool logSteps = false;

    // --- runtime ---
    private int[] lps;               // KMP prefix table
    private int progress = 0;        // how many correct keys matched so far
    private float lastTime = -999f;

    void Awake()
    {
        BuildLPS();
    }

    void BuildLPS()
    {
        lps = new int[sequence.Length]; //helper array for KPM (use repeats of the same button - avoid resseting)
        int len = 0;
        for (int i = 1; i < sequence.Length;)
        {
            if (sequence[i] == sequence[len]) { lps[i++] = ++len; }
            else if (len != 0) { len = lps[len - 1]; }
            else { lps[i++] = 0; }
        }
    }

    void Register(GBKey key)
    {
        //Handles Single press, resets if gap too long

        // timeout
        if (Time.time - lastTime > inputTimeout) progress = 0;
        lastTime = Time.time;

        // KMP advance
        while (progress > 0 && sequence[progress] != key)
            progress = lps[progress - 1];

        if (sequence[progress] == key) progress++;

        if (logSteps) Debug.Log($"[Konami] key={key}, progress={progress}/{sequence.Length}");

        if (progress >= sequence.Length)
        {
            progress = 0; // reset so it can be done again
            if (logSteps) Debug.Log(" Easter Egg Unlocked");
            easterEggUnlock?.Invoke();
        }

    }

    // --- No-arg methods for Inspector wiring ---
    public void PressUp() => Register(GBKey.Up);
    public void PressDown() => Register(GBKey.Down);
    public void PressLeft() => Register(GBKey.Left);
    public void PressRight() => Register(GBKey.Right);
    public void PressA() => Register(GBKey.A);
    public void PressB() => Register(GBKey.B);
    public void PressStart() => Register(GBKey.Start);
    public void PressSelect() => Register(GBKey.Select);
}
