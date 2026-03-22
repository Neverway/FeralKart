using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Graphics_RiftPreviewEffects : MonoBehaviour
{
    //[SerializeField] private Alt_Item_Geodesic_Utility_GeoGun geoGun;

    [SerializeField] private Material riftMaterial;
    [SerializeField] private float defaultEmissionStrength = 6;
    [SerializeField] private float collapseExpandEmissionStrength = 9;

    //public float edgeStrength = 1.3f;
    //public float opacity = 0.05f;
    //public float edgeStrengthFactorPower = 1f;
    //public float opacityFactorPower = 2f;
    //public float burstFactorPower = 0.7f;
    private RiftManager riftManager;

    private void OnEnable()
    {
        RiftManager_StateHandler.OnStateChanged += OnStateChanged;
    }
    private void OnDisable()
    {
        RiftManager_StateHandler.OnStateChanged -= OnStateChanged;
    }
    private void OnDestroy()
    {
        riftMaterial.SetFloat("_EffectTime", 0);
        riftMaterial.SetFloat("_SphereSize", 0);
        riftMaterial.SetFloat("_EmissionStrength", collapseExpandEmissionStrength);
    }
    public void Update()
    {
        //Graphics_NixieBulbEffects bulb = Graphics_NixieBulbEffects.firstBulb;
        //if (bulb == null)
        //    return;

        //float lerpFactor = bulb.glowFactor * Mathf.Pow(bulb.previewBurstFactor, burstFactorPower);

        //GameObject obj = geoGun.cutPreviews[0];
        //Material mat = obj.GetComponentInChildren<Renderer>().sharedMaterial;

        //mat.SetFloat("_edgeStrength", edgeStrength * Mathf.Pow(lerpFactor, edgeStrengthFactorPower));
        //mat.SetFloat("_opacity", opacity * Mathf.Pow(lerpFactor, opacityFactorPower));

        //float newEdgeStrength = edgeStrength * Mathf.Pow(lerpFactor, edgeStrengthFactorPower);
        //float newOpacity = opacity * Mathf.Pow(lerpFactor, opacityFactorPower);

        //if (Alt_Item_Geodesic_Utility_GeoGun.currentState == RiftState.Closed)
        //{
        //    newEdgeStrength *= 1.5f;
        //    newOpacity *= 1.5f;
        //}

        //SetPreview(geoGun.cutPreviews[0], newEdgeStrength, newOpacity);
        //SetPreview(geoGun.cutPreviews[1], newEdgeStrength, newOpacity);

        //geoGun.cutPreviews[0].SetActive(Alt_Item_Geodesic_Utility_GeoGun.currentState != RiftState.Closed);
    }

    void OnStateChanged()
    {
        // Whoops, we need this reference, but it's not here!
        if (riftManager is null) riftManager = FindObjectOfType<RiftManager>();
        // Still didn't find it? Okay, stop everything else
        if (riftManager is null) return;
        
        // YIPEE! Do the rift dance!
        if (riftManager.stateHandler.currentState is not RiftState_None && riftManager.stateHandler.previousState is RiftState_None)
        {
            // Moved the code from here to preview
        }

        switch (riftManager.stateHandler.currentState)
        {
            case RiftState_None:
                StopAllCoroutines();
                riftMaterial.SetFloat("_EffectTime", 0);
                riftMaterial.SetFloat("_SphereSize", 0);
                riftMaterial.SetFloat("_EmissionStrength", defaultEmissionStrength);
                break;
            case RiftState_Preview:
                StopAllCoroutines();
                StartCoroutine(OnRiftCreated(riftManager));
                break;
            case RiftState_Collapsing:
                riftMaterial.SetFloat("_EmissionStrength", collapseExpandEmissionStrength);
                break;
            case RiftState_Closed:
                riftMaterial.SetFloat("_EmissionStrength", defaultEmissionStrength);
                break;
            case RiftState_Expanding:
                riftMaterial.SetFloat("_EmissionStrength", collapseExpandEmissionStrength);
                break;
            case RiftState_Idle:
                riftMaterial.SetFloat("_EmissionStrength", defaultEmissionStrength);
                break;
            default:
                break;
        }
    }

    public IEnumerator OnRiftCreated(RiftManager _riftManager)
    {
        riftMaterial.SetFloat("_EffectTime", 0);
        riftMaterial.SetFloat("_SphereSize", 0);
        riftMaterial.SetFloat("_EmissionStrength", defaultEmissionStrength);

        //Vector3 bulbA = //geoGun.cutPreviews[0].transform.position;
        //Vector3 bulbB = //geoGun.cutPreviews[1].transform.position;
        var markerA = _riftManager.markerA.transform.position;
        var markerB = _riftManager.markerB.transform.position;

        riftMaterial.SetVector("_BulbsCenter", (markerA + markerB) * 0.5f);
        riftMaterial.SetFloat("_SphereSize", Vector3.Distance(markerA, markerB) * 0.5f);

        float effectTimer = 0;

        while (effectTimer < 1)
        {
            riftMaterial.SetFloat("_EffectTime", effectTimer);
            effectTimer += Time.deltaTime;
            yield return null;
        }

        riftMaterial.SetFloat("_EffectTime", 1);
    }
    public void SetPreview(GameObject preview, float edgeStrength, float opacity)
    {
        Material mat = preview.GetComponentInChildren<Renderer>().sharedMaterial;

        mat.SetFloat("_edgeStrength", edgeStrength);
        mat.SetFloat("_opacity", opacity);
    }
}
