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

[CreateAssetMenu(fileName = "FighterData_", menuName = "FeKa/FighterData")]
[Serializable]
public class FighterData : ScriptableObject
{
    public string fighterName;
    public string fighterSubtitle;
    public Color primaryColor;
    public Color secondaryColor;
    public Color tertiaryColor;
    public Sprite fighterIcon;
    public FeKaItem finalStrike;
    public FeKaItem ability;
    public FeKaItem passive;
}
