using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FadeoutAndDisable : MonoBehaviour
{
    void Start() 
    {

    }

    void OnEnable()
    {
        StartCoroutine("FadeOut");
    }

    IEnumerator FadeOut()
    {
        var duration = 3.0f;
        float counter = 0;
        
        Color spriteColor = gameObject.GetComponent<Image>().color;
        var origColor = spriteColor;
        while (counter < duration)
        {
            counter += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, counter / duration);

            gameObject.GetComponent<Image>().color = new Color(spriteColor.r, spriteColor.g, spriteColor.b, alpha);
            yield return null;
        }

        gameObject.GetComponent<Image>().color = origColor;
        gameObject.SetActive(false);
    }
}
