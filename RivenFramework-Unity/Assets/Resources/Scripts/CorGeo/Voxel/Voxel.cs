//==========================================( Neverway 2025 )=========================================================//
// Author
//  Liz M.
//
// Contributors
//
// Created following this guide: https://youtu.be/EubjobNVJdM
//====================================================================================================================//

using UnityEngine;

/// <summary>
/// A volumetric pixel used to represent something like air, water, etc.
/// </summary>
public struct Voxel
{
    [Tooltip("Identifies what this voxel type is, like 0=air, 1=solid, 2=water, etc.")]
    public byte ID;

    [Tooltip("A solid block next to another will avoid drawing the overlapping faces by default, " +
             "this specifies what block id's are 'solid' and have backface culling, " +
             "this can be ignored by enabling doNotSkipGeneratingBackfaces in the VoxWorldManager")]
    public bool isSolid
    {
        get
        {
            return ID != 0;
        }
    }
}

/* VOXEL IDs
 0 air
 1 solid
 2 water
 3 lava
 4 oil
 5 fire
 6 geolight
 */
