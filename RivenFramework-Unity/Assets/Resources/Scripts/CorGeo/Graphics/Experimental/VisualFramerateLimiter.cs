//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections.Generic;
using UnityEngine;

public class VisualFramerateLimiter : MonoBehaviour
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("The frames per second to update the visual representation of the object")]
    public float targetFPS = 12f;

    [Tooltip("Enable this if you want to set the FPS based on the object's velocity instead of being at a set rate")]
    public bool useVelocityBasedFPS;
    
    [Tooltip("The FPS to update the visual representation of the object based on it's velocity")]
    public List<Vector2> fpsPerVelocity;


    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    [Tooltip("")]
    private float frameTimer;
    [Tooltip("")]
    private Vector3 lastPosition;
    [Tooltip("")]
    private Quaternion lastRotation;
    [Tooltip("")]
    private Vector3 lastScale;
    [Tooltip("If there is no rigidbody, it uses this value to approximate a velocity")]
    private Vector3 prevPositionForSpeed;
    private float runtimeTargetFPS;


    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    [Tooltip("")]
    private Transform realTransform;
    [Tooltip("")]
    private Transform visualTransform;

    private Rigidbody rigidbody;


    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        realTransform = transform;
        
        // Create the object that will hold the visuals of the object
        var visualObject = new GameObject($"{gameObject.name}_VISUAL");
        visualObject.transform.SetParent(realTransform.parent, worldPositionStays: true);
        
        // Ensure the visual transform matches the original
        visualObject.transform.position = realTransform.transform.position;
        visualObject.transform.rotation = realTransform.transform.rotation;
        visualObject.transform.localScale = realTransform.transform.localScale;
        visualTransform = visualObject.transform;
        
        // Copy visual elements to clone (including for children)
        CopyVisualElements(realTransform, visualTransform);
        
        rigidbody = GetComponent<Rigidbody>();
        
        // store starting transform
        lastPosition = realTransform.position;
        lastRotation = realTransform.rotation;
        prevPositionForSpeed = realTransform.position;
        frameTimer = 0f;
        
        // Sort fpsPerVelocity by velocity
        if (fpsPerVelocity != null && fpsPerVelocity.Count > 1)
        {
            fpsPerVelocity.Sort((a, b) => a.x.CompareTo(b.x));
        }
    }

    private void Update()
    {
        if (useVelocityBasedFPS)
        {
            float speed = 0f;
            if (rigidbody != null)
            {
                speed = rigidbody.velocity.magnitude;
            }
            else
            {
                // fallback: approximate speed from positional delta
                float dt = Mathf.Max(Time.deltaTime, 1e-6f);
                speed = (realTransform.position - prevPositionForSpeed).magnitude / dt;
            }

            runtimeTargetFPS = EvaluateFPSForSpeed(speed);
        }
        else
        {
            runtimeTargetFPS = targetFPS;
        }

        // use a small epsilon to avoid division by zero
        float frameInterval = 1f / Mathf.Max(runtimeTargetFPS, 0.0001f);
        
        frameTimer += Time.deltaTime;

        if (frameTimer >= frameInterval)
        {
            frameTimer = 0f;
            lastPosition = realTransform.position;
            lastRotation = realTransform.rotation;
            lastScale = realTransform.localScale;
        }

        // Move visuals to stored transform
        visualTransform.position = lastPosition;
        visualTransform.rotation = lastRotation;
        visualTransform.localScale = lastScale;
        
        
        
        prevPositionForSpeed = realTransform.position;
    }

    private void OnDestroy()
    {
        if (visualTransform) Destroy(visualTransform.gameObject);
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    private void CopyVisualElements(Transform _source, Transform _targetParent)
    {
        // Get visual elements in target
        var meshFilter = _source.GetComponent<MeshFilter>();
        var meshRenderer = _source.GetComponent<MeshRenderer>();
        var skinnedMeshRenderer = _source.GetComponent<SkinnedMeshRenderer>();
        var animator = _source.GetComponent<Animator>();
        
        // Create visual clone object
        var newVisual = new GameObject(_source.name);
        newVisual.transform.SetParent(_targetParent);

        // Copy the transform of it's source
        newVisual.transform.position = _source.position;
        newVisual.transform.rotation = _source.rotation;
        newVisual.transform.localScale = _source.localScale;

        // Clone filter
        if (meshFilter)
        {
            var newMF = newVisual.AddComponent<MeshFilter>();
            newMF.sharedMesh = meshFilter.sharedMesh;
        }
        // Clone renderer
        if (meshRenderer)
        {
            var newMR = newVisual.AddComponent<MeshRenderer>();
            newMR.sharedMaterials = meshRenderer.sharedMaterials;
            meshRenderer.enabled = false;
        }
        // Clone skinned renderer
        if (skinnedMeshRenderer)
        {
            var newSMR = newVisual.AddComponent<SkinnedMeshRenderer>();
            newSMR.sharedMesh = skinnedMeshRenderer.sharedMesh;
            newSMR.sharedMaterials = skinnedMeshRenderer.sharedMaterials;
            newSMR.rootBone = skinnedMeshRenderer.rootBone;
            newSMR.bones = skinnedMeshRenderer.bones;
            skinnedMeshRenderer.enabled = false;
        }
        // Clone animator
        if (animator)
        {
            var newAnimator = newVisual.AddComponent<Animator>();
            newAnimator.runtimeAnimatorController = animator.runtimeAnimatorController;
            newAnimator.avatar = animator.avatar;
            newAnimator.applyRootMotion = animator.applyRootMotion;
            newAnimator.updateMode = animator.updateMode;
            newAnimator.cullingMode = animator.cullingMode;
            animator.enabled = false;
        }

        // Recursively check children
        foreach (Transform _child in _source)
        {
            CopyVisualElements(_child, newVisual.transform);
        }
    }
    
    /// <summary>
    /// Given a speed (units/sec), evaluate the FPS using fpsPerVelocity mapping.
    /// fpsPerVelocity stores Vector2 elements: x = speed threshold, y = fps at that threshold.
    /// The list must be sorted by x ascending (we sorted it at Start). If empty, fallback to targetFPS.
    /// We linearly interpolate between points for smooth transitions.
    /// </summary>
    private float EvaluateFPSForSpeed(float speed)
    {
        if (fpsPerVelocity == null || fpsPerVelocity.Count == 0)
            return targetFPS;

        // below first point
        if (speed <= fpsPerVelocity[0].x)
            return Mathf.Max(0.0001f, fpsPerVelocity[0].y);

        // between points -> interpolate
        for (int i = 0; i < fpsPerVelocity.Count - 1; i++)
        {
            Vector2 a = fpsPerVelocity[i];
            Vector2 b = fpsPerVelocity[i + 1];

            if (speed <= b.x)
            {
                float t = (speed - a.x) / Mathf.Max((b.x - a.x), 1e-6f);
                float fps = Mathf.Lerp(a.y, b.y, t);
                return Mathf.Max(0.0001f, fps);
            }
        }

        // above last point
        return Mathf.Max(0.0001f, fpsPerVelocity[fpsPerVelocity.Count - 1].y);
    }
    
    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/


    #endregion
}
