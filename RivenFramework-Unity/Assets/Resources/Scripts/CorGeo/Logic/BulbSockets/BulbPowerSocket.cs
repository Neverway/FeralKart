using System.Collections;
using UnityEngine;

public class BulbPowerSocket : MonoBehaviour, BulbCollisionBehaviour
{
    //=-----------------=
    // Public Variables
    //=-----------------=
    public LogicOutput<bool> isPowered = new(false);

    //=-----------------=
    // Private Variables
    //=-----------------=
    private Projectile_Marker fittedBulb;

    //=-----------------=
    // Reference Variables
    //=-----------------=
    [field: SerializeField] public Transform attachPoint { get; private set; }

    //=-----------------=
    // Mono Functions
    //=-----------------=

    private IEnumerator Start()
    {
        yield return null;
        
        // Unparents the attachment point and makes sure that the corgeo actor homeparent is also unparented
        attachPoint.SetParent(null);
        if (attachPoint.TryGetComponent<CorGeo_Actor> (out var actor))
        {
            actor.homeParent = null;
        }
    }
    private void Update()
    {
        isPowered.Set(fittedBulb != null);
    }

    //=-----------------=
    // Internal Functions
    //=-----------------=


    //=-----------------=
    // External Functions
    //=-----------------=
    public bool OnBulbCollision(Projectile_Marker bulb, RaycastHit hit)
    {
        if (fittedBulb != null)
        {
            bulb.MarkerBreak();
            return false;
        }

        fittedBulb = bulb;
        bulb.MarkerPinAt(attachPoint.position, attachPoint.forward);
        return true;
    }
}
