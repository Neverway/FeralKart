using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionMethods_Coroutines
{
    //DUNNO IF THIS ACTUALLY WORKS? FIRST VALUE SEEMS TO BE OFF
    public static IEnumerator UsingSeed(this IEnumerator enumerator, int seed)
    {
        var oldSeedState = Random.state;

        Random.InitState(seed);
        var enumeratorSeedState = Random.state;

        Random.state = oldSeedState;

        while (true)
        {
            oldSeedState = Random.state;

            Random.state = enumeratorSeedState;
            if (!enumerator.MoveNext())
            {
                Random.state = oldSeedState;
                yield break;
            }
            yield return enumerator.Current;
            enumeratorSeedState = Random.state;

            Random.state = oldSeedState;
        }
    }
    //Needs to be tested to see if this works
    public static IEnumerator ThenDo(this IEnumerator enumerator, params IEnumerator[] otherEnumerators)
    {
        yield return enumerator;
        foreach (IEnumerator current in otherEnumerators)
        {
            yield return current;
        }
    }
}