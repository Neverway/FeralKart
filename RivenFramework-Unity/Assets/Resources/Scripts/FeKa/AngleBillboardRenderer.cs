using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using Unity.VisualScripting;
using UnityEngine;

public class AngleBillboardRenderer : MonoBehaviour
{
    public Camera activeCamera;
    public SpriteRenderer spriteRenderer;
    [SerializeField] public SpriteAngle[] spriteAngles;
    public Transform origin;
    public bool overrideRotation = false;
    public float zRotationOffset;

    // Update is called once per frame
    void Update()
    {
        if (!Initialized())
        {
            Initialize();
            return;
        }
        
        origin = transform;
        
        // Rotate the sprite to face the viewer
        spriteRenderer.transform.LookAt(activeCamera.transform, transform.up);
    
    
        spriteRenderer.sprite = GetSpriteFromAngle();
        
        // Apply Z rotation offset on top of the billboard rotation
        if (overrideRotation) spriteRenderer.transform.Rotate(0f, 0f, zRotationOffset, Space.Self);
    }

    /// <summary>
    /// Figure out the closest sprite in an array of sprite angles to a given angle
    /// </summary>
    private Sprite GetSpriteFromAngle()
    {
        Vector3 worldDir = activeCamera.transform.position - origin.position;
        worldDir.y = 0f;
        worldDir = worldDir.normalized;

        Vector3 localDir = Quaternion.Inverse(origin.rotation) * worldDir;

        Vector2 directionToCamera = new Vector2(-localDir.x, localDir.z).normalized;

        int closestSpriteAngle = -1;
        float closestPoint = -1f;

        for (int i = 0; i < spriteAngles.Length; i++)
        {
            Vector2 spriteAngle = spriteAngles[i].direction.normalized;

            float currentPoint = Vector2.Dot(spriteAngle, directionToCamera);
            if (currentPoint > closestPoint)
            {
                closestPoint = currentPoint;
                closestSpriteAngle = i;
            }
        }

        if (closestSpriteAngle == -1)
            throw new Exception("This should not have happened... WHAT DID YOU DO?!?!");

        return spriteAngles[closestSpriteAngle].sprite;
    }

    private bool Initialized()
    {
        return activeCamera != null;
    }

    private void Initialize()
    {
        activeCamera = GameInstance.Get<GI_PawnManager>().localPlayerCharacter.GetComponent<Pawn>().viewPoint.GetComponent<Camera>();
    }
}

[Serializable]
public struct SpriteAngle
{
    public Sprite sprite;
    public Vector2 direction;
}
