using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FadeinTilemap : MonoBehaviour
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
        var duration = 0.33f;
        float counter = 0;
        
        Vector3 startScale = gameObject.transform.localScale;
        var origScale = startScale;
        while (counter < duration)
        {
            counter += Time.deltaTime;
            float sc = Mathf.Lerp(0.0f, 1.0f, counter / duration);

            gameObject.transform.localScale = new Vector3(sc, sc, 1.0f);
            yield return null;
        }

        gameObject.transform.localScale = new Vector3(1.0f,1.0f,1.0f);
    }
}
