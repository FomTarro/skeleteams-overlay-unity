using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Utils;
using UnityEngine;

public class HttpTester : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < 15; i++)
        {
            int temp = i;
            StartCoroutine(
                HttpUtils.GetRequest(
                    "https://www.skeletom.net",
                    new HttpUtils.HttpHeaders()
                    {
                        customHeaders = {
                            {"x-header", $"{temp}" }
                        }
                    },
                    (succ) => { Debug.Log(temp); },
                    (err) => { }
                )
            );
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
