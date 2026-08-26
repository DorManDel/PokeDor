using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MenuIntro : MonoBehaviour
{
    //serialize + Header to expose in inspector
    [SerializeField] GameObject bootLayer;        // the “Nintendor” panel
    [SerializeField] AudioSource sfxBoot;         // ding sound
    [SerializeField] float showTime = 1.5f;

    static bool _played;

    IEnumerator Start()
    {
        if (_played) { if (bootLayer) bootLayer.SetActive(false); yield break; }

        _played = true;
        if (bootLayer) bootLayer.SetActive(true);
        if (sfxBoot) sfxBoot.Play();
        yield return new WaitForSeconds(showTime);
        if (bootLayer) bootLayer.SetActive(false);
        //Destroy(bootLayer );                      // to not lose the ref
    }
}
