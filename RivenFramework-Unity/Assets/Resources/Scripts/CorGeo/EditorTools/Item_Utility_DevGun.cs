//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// This is the item script for the developer tool gun
/// </summary>
public class Item_Utility_DevGun : Item
{
    #region========================================( Variables )======================================================//
    /*-----[ Inspector Variables ]------------------------------------------------------------------------------------*/
    public ToolMode[] toolModes;
    
    /*-----[ External Variables ]-------------------------------------------------------------------------------------*/


    /*-----[ Internal Variables ]-------------------------------------------------------------------------------------*/
    private bool primaryHeld;
    private bool secondaryHeld;
    private bool tertiaryHeld;
    
    private bool primaryInputCooldown;
    private bool secondaryInputCooldown;
    private bool tertiaryInputCooldown;
    
    private int range = 100;
    private RaycastHit hit;
    private GameObject attachedObject;
    private float attachedObjectDistance;
    private GameObject copiedObject;
    
    public int currentToolMode;
    
    /*-----[ Reference Variables ]------------------------------------------------------------------------------------*/
    private Animator animator;
    [SerializeField] private GameObject[] toolFX;
    [SerializeField] private TMP_Text toolText;
    [SerializeField] private SpriteRenderer toolSprite;
    [SerializeField] private Material laserMaterial;

    #endregion


    #region=======================================( Functions )=======================================================//
    /*-----[ Mono Functions ]-----------------------------------------------------------------------------------------*/
    private void Start()
    {
        UpdateToolDisplay();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (attachedObject) attachedObject.transform.position = transform.position + transform.forward * attachedObjectDistance;
        
        switch (currentToolMode)
        {
            case 0:
                PhysGunUpdate();
                break;
            case 1:
                GravGunUpdate();
                break;
            case 2:
                PortalGunUpdate();
                break;
            case 3:
                DuplicatorUpdate();
                break;
            case 4:
                RemoverUpdate();
                break;
        }
    }

    /*-----[ Internal Functions ]-------------------------------------------------------------------------------------*/
    /// <summary>
    /// Used to get an actor that was selected with the toolgun
    /// </summary>
    /// <returns>Returns the selected actor if one was found</returns>
    private GameObject GetHitTarget()
    {
        if (Physics.Raycast(transform.parent.position, transform.parent.forward, out hit, range))
        {
            var parentActors = hit.collider.GetComponentsInParent<Actor>();
            Actor targetActor = null;
            if (parentActors.Length != 0) targetActor = parentActors[parentActors.Length-1];
            if (targetActor)
            {
                return targetActor.gameObject;
            }/*
            targetActor = hit.collider.transform.parent.GetComponent<Actor>();
            if (targetActor)
            {
                return targetActor.gameObject;
            }
            targetActor = hit.collider.transform.parent.transform.parent.GetComponent<Actor>();
            if (targetActor)
            {
                return targetActor.gameObject;
            }*/
        }

        return null;
    }    
    
    /// <summary>
    /// Used to update the raycast hit, mainly so we can get the hit point if this returns true
    /// 
    /// PS I would have called this function GetHitPoint and have it return the raycast's hit position,
    /// However, you can't return null for a position, so if the raycast failed it would return the world origin
    /// (x0,y0,z0) which would be bad    ~Liz M.
    /// </summary>
    /// <returns>Returns true if the raycast hit something</returns>
    private bool CheckHit()
    {
        if (Physics.Raycast(transform.parent.position, transform.parent.forward, out hit, range))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private IEnumerator InputDelay()
    {
        primaryInputCooldown = true;
        yield return new WaitForSeconds(0.2f);
        primaryInputCooldown = false;
    }

    private void SwitchToolMode()
    {
        if (currentToolMode + 1 >= toolModes.Length) currentToolMode = 0;
        else currentToolMode++;
        UpdateToolDisplay();
    }

    private void UpdateToolDisplay()
    {
        toolText.text = toolModes[currentToolMode].name;
        toolSprite.sprite = toolModes[currentToolMode].toolIcon;
        toolText.color = toolModes[currentToolMode].toolColor;
        toolSprite.color = toolModes[currentToolMode].toolColor;
        laserMaterial.color = toolModes[currentToolMode].toolColor;
    }

    private void PhysGunUpdate()
    {
        if (CheckHit()) toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,Vector3.Distance(transform.position, hit.point)));
        else toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,range));
        
        if (primaryHeld)
        {
            animator.SetBool("Firing", true);
            toolFX[0].SetActive(true);
            if (GetHitTarget() && !attachedObject)
            {
                attachedObjectDistance = Vector3.Distance(transform.position, GetHitTarget().transform.position);
                attachedObject = GetHitTarget();
            }
        }
        else
        {
            animator.SetBool("Firing", false);
            toolFX[0].SetActive(false);
            if (attachedObject is null) return;
            if (attachedObject.GetComponent<Rigidbody>())
            {
                attachedObject.GetComponent<Rigidbody>().velocity = new Vector3();
            }
            attachedObject = null;
        }

        if (secondaryHeld)
        {
            if (attachedObject is null) return;
            if (attachedObject.GetComponent<Rigidbody>()) attachedObject.GetComponent<Rigidbody>().isKinematic = !attachedObject.GetComponent<Rigidbody>().isKinematic;
        }
    }
    
    private void GravGunUpdate()
    {
        if (primaryHeld)
        {
            if (attachedObject is null) return;
            animator.SetBool("Firing", true);
            attachedObject.GetComponent<Rigidbody>().velocity = new Vector3();
            attachedObject.GetComponent<Rigidbody>().AddForce(transform.forward*50,ForceMode.VelocityChange);
            attachedObject = null;
            toolFX[2].SetActive(true);
        }
        else
        {
            animator.SetBool("Firing", false);
        }

        if (secondaryHeld)
        {
            animator.SetBool("AltFiring", true);
            toolFX[1].SetActive(true);
            if (GetHitTarget())
            {
                if (!GetHitTarget().GetComponent<Rigidbody>()) return;
                if (!attachedObject)
                {
                    secondaryInputCooldown = true;
                    attachedObjectDistance = 2;
                    attachedObject = GetHitTarget();
                }
                else if (attachedObject && !secondaryInputCooldown)
                {
                    secondaryInputCooldown = true;
                    attachedObject.GetComponent<Rigidbody>().velocity = new Vector3();
                    attachedObject = null;
                }
            }
        }
        
        else if (secondaryHeld is false)
        {
            animator.SetBool("AltFiring", false);
            toolFX[1].SetActive(false);
            if (secondaryInputCooldown) secondaryInputCooldown = false;
        }
    }

    private void PortalGunUpdate()
    {
        if (CheckHit()) toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,Vector3.Distance(transform.position, hit.point)));
        else toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,range));
        
        if (primaryHeld)
        {
            animator.SetBool("Firing", true);
            toolFX[0].SetActive(true);
            if (primaryInputCooldown is false)
            {
                primaryInputCooldown = true;
                if (CheckHit())
                {
                    transform.parent.GetComponent<Pawn_Inventory>().owner.transform.position = hit.point;
                }
            }
        }

        else if (primaryHeld is false && primaryInputCooldown)
        {
            animator.SetBool("Firing", false);
            toolFX[0].SetActive(false);
            primaryInputCooldown = false;
        }
    }

    private void DuplicatorUpdate()
    {
        if (CheckHit()) toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,Vector3.Distance(transform.position, hit.point)));
        else toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,range));
        
        if (primaryHeld)
        {
            animator.SetBool("Firing", true);
            toolFX[0].SetActive(true);
            if (primaryInputCooldown is false)
            {
                primaryInputCooldown = true;
                if (CheckHit() && copiedObject)
                {
                    Instantiate(copiedObject, hit.point, new Quaternion(), null);
                }
            }
        }

        else if (primaryHeld is false && primaryInputCooldown)
        {
            animator.SetBool("Firing", false);
            toolFX[0].SetActive(false);
            primaryInputCooldown = false;
        }
        
        if (secondaryHeld)
        {
            animator.SetBool("AltFiring", true);
            toolFX[0].SetActive(true);
            
            if (secondaryInputCooldown is false)
            {
                secondaryInputCooldown = true;
                if (GetHitTarget())
                {
                    copiedObject = GetHitTarget().gameObject;
                }
            }
        }

        else if (secondaryHeld is false && secondaryInputCooldown)
        {
            animator.SetBool("AltFiring", false);
            toolFX[0].SetActive(false);
            secondaryInputCooldown = false;
        }
    }
    
    private void RemoverUpdate()
    {
        if (CheckHit()) toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,Vector3.Distance(transform.position, hit.point)));
        else toolFX[0].GetComponent<LineRenderer>().SetPosition(1, new Vector3(0,0,range));
        
        if (primaryHeld)
        {
            
            if (primaryInputCooldown is false)
            {
                animator.SetBool("Firing", true);
                toolFX[0].SetActive(true);
                primaryInputCooldown = true;
                if (GetHitTarget())
                {
                    Destroy(GetHitTarget().gameObject);
                }
            }
        }

        else if (primaryHeld is false && primaryInputCooldown)
        {
            animator.SetBool("Firing", false);
            toolFX[0].SetActive(false);
            primaryInputCooldown = false;
        }
    }


    /*-----[ External Functions ]-------------------------------------------------------------------------------------*/
    public override void UsePrimary(string _mode = "press")
    {
        switch (_mode)
        {
            case "press":
                primaryHeld = true;
                break;
            case "release":
                primaryHeld = false;
                break;
        }
    }
    
    public override void UseSecondary(string _mode = "press")
    {
        switch (_mode)
        {
            case "press":
                secondaryHeld = true;
                break;
            case "release":
                secondaryHeld = false;
                break;
        }
    }
    
    public override void UseTertiary(string _mode = "press")
    {
        switch (_mode)
        {
            case "press":
                tertiaryHeld = true;
                SwitchToolMode();
                break;
            case "release":
                tertiaryHeld = false;
                break;
        }
    }

    #endregion
}

[Serializable]
public class ToolMode
{
    public string name;
    public Sprite toolIcon;
    public Color toolColor;
}