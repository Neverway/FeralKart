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
using UnityEngine;

[CreateAssetMenu(menuName = "AuHo/Chatter Voice", fileName = "Voice_")]
[Serializable]
public class Char_ChatterVoice : ScriptableObject
{
    [Tooltip("The audio clip to use as the base for the voice")]
    public AudioClip textChatter;
    [Range(1,5)]
    [Tooltip("The amount of text characters to print between each voice clip")]
    public int chatterFrequency;
    [Range(-3,3)]
    [Tooltip("The min pitch the chatter can be played at")]
    public float chatterPitchMin;
    [Range(-3,3)]
    [Tooltip("The max pitch the chatter can be played at")]
    public float chatterPitchMax;
}
