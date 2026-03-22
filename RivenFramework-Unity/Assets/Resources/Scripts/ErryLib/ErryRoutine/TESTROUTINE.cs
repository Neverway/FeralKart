using UnityEngine;
using ErryLib.ErryRoutines;
using ErryLib.MonoTasks;
using System.Threading.Tasks;
using System.Collections;


public class TESTROUTINE : MonoBehaviour
{
    private void Start()
    {
        SomeMethod();
    }

    public void Update()
    {
        Debug.Log("Update called");
    }

    async void SomeMethod()
    {
        while (true)
        {
            Debug.Log("SomeMethod called");
            await SomeOtherMethod();
        }
    }
    async Task SomeOtherMethod()
    {
        await For.NextFrame;
    }
}
