using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WB_Countdown : MonoBehaviour
{
    public Animator animator;
    public TMP_Text[] texts;
    
    
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(PlayFrame());
    }

    private IEnumerator PlayFrame()
    {
        animator.Play("PlayFrame");
        foreach (var text in texts) text.text = "3";
        yield return new WaitForSeconds(1);
        animator.Play("PlayFrame");
        foreach (var text in texts) text.text = "2";
        yield return new WaitForSeconds(1);
        animator.Play("PlayFrame");
        foreach (var text in texts) text.text = "1";
        yield return new WaitForSeconds(1);
        animator.Play("PlayFrame");
        foreach (var text in texts) text.text = "Strife!";
        yield return new WaitForSeconds(1);
        Destroy(gameObject);
    }
}
