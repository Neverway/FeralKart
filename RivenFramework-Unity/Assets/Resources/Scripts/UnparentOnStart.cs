using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnparentOnStart : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        this.gameObject.transform.SetParent(null);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
