//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
//
//====================================================================================================================//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SAG_", menuName = "Neverway/SpriteAngleGroup")]
public class SpriteAngleGroup : ScriptableObject
{
    [SerializeField] public SpriteAngle[] spriteAngles;
}
