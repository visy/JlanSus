using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Fadein : MonoBehaviour
{
    void Start() 
    {

    }

    void OnEnable()
    {
        StartCoroutine("DoFadein");
    }

    IEnumerator DoFadein()
    {
        var duration = 0.5f;
        float counter = 0;
        
        Color spriteColor = gameObject.GetComponent<Image>().color;
        var origColor = spriteColor;
        while (counter < duration)
        {
            counter += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, counter / duration);

            gameObject.GetComponent<Image>().color = new Color(spriteColor.r, spriteColor.g, spriteColor.b, alpha);
            yield return null;
        }

    }
}
