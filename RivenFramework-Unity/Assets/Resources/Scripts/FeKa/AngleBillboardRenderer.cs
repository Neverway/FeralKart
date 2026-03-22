using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngleBillboardRenderer : MonoBehaviour
{
    public Camera activeCamera;
    public SpriteRenderer spriteRenderer;
    public SpriteAngle[] spriteAngles;
    public Transform origin;
    
    // Start is called before the first frame update
    void Start()
    {
        activeCamera = Camera.current;
        origin = transform;
    }

    // Update is called once per frame
    void Update()
    {
        // Rotate the sprite to face the viewer
        spriteRenderer.transform.LookAt(activeCamera.transform);

        
        spriteRenderer.sprite = GetSpriteFromAngle();
    }

    /// <summary>
    /// Figure out the closest sprite in an array of sprite angles to a given angle
    /// </summary>
    private Sprite GetSpriteFromAngle()
    {
        Vector2 directionToCamera = activeCamera.transform.position - origin.position;
        directionToCamera = directionToCamera.normalized;
        
        int closestSpriteAngle = -1;

        float closestPoint = -1;
        
        for (int i = 0; i < spriteAngles.Length; i++)
        {
            var spriteAngle = spriteAngles[i].direction.normalized;
            float currentPoint = Vector2.Dot(spriteAngle , directionToCamera);
            if (currentPoint > closestPoint)
            {
                closestPoint = currentPoint;
                closestSpriteAngle = i;
            }
        }

        if (closestSpriteAngle == -1) throw new Exception("This should not have happened... WHAT DID YOU DO?!?!");

        return spriteAngles[closestSpriteAngle].sprite;
    }
}

public struct SpriteAngle
{
    public Sprite sprite;
    public Vector2 direction;
}
