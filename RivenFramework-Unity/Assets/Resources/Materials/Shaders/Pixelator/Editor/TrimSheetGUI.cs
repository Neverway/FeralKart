// Save as Editor/TrimSheetGUI.cs
using UnityEngine;
using UnityEditor;

public class TrimSheetGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        Material mat = editor.target as Material;

        var mode        = FindProperty("_Mode",               props);
        var mainTex     = FindProperty("_MainTex",            props);
        var cropX       = FindProperty("_CropX",              props);
        var cropY       = FindProperty("_CropY",              props);
        var cropW       = FindProperty("_CropWidth",          props);
        var cropH       = FindProperty("_CropHeight",         props);
        var texW        = FindProperty("_TexWidth",           props);
        var texH        = FindProperty("_TexHeight",          props);
        var tpu         = FindProperty("_TexelsPerUnit",      props);
        var tilingX     = FindProperty("_TilingX",            props);
        var tilingY     = FindProperty("_TilingY",            props);
        var offsetX     = FindProperty("_OffsetX",            props);
        var offsetY     = FindProperty("_OffsetY",            props);
        var metallic    = FindProperty("_Metallic",           props);
        var glossiness  = FindProperty("_Glossiness",         props);
        var color       = FindProperty("_Color",              props);
        var cutoff      = FindProperty("_Cutoff",             props);
        var emColor     = FindProperty("_EmissionColor",      props);
        var emStrength  = FindProperty("_EmissionStrength",   props);
        var useEmission = FindProperty("_UseEmission",        props);
        var specular    = FindProperty("_SpecularHighlights", props);
        var reflections = FindProperty("_GlossyReflections",  props);

        // Sync hidden state on every repaint
        SetBlendMode(mat, (int)mode.floatValue);
        SetEmissionKeyword(mat, useEmission.floatValue > 0.5f);

        // Blend mode
        EditorGUILayout.LabelField("Blend Mode", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int newMode = EditorGUILayout.Popup("Mode", (int)mode.floatValue,
            new[] { "Opaque", "Cutout", "Transparent" });
        if (EditorGUI.EndChangeCheck())
        {
            mode.floatValue = newMode;
            SetBlendMode(mat, newMode);
        }
        if ((int)mode.floatValue == 1)
            editor.ShaderProperty(cutoff, "Alpha Cutoff");

        // Tileset
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tileset", EditorStyles.boldLabel);
        editor.ShaderProperty(mainTex, "Texture");
        editor.ShaderProperty(texW,    "Texture Width (px)");
        editor.ShaderProperty(texH,    "Texture Height (px)");

        // Crop
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crop Region (pixels)", EditorStyles.boldLabel);
        editor.ShaderProperty(cropX, "X");
        editor.ShaderProperty(cropY, "Y");
        editor.ShaderProperty(cropW, "Width");
        editor.ShaderProperty(cropH, "Height");

        // Scale
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scale", EditorStyles.boldLabel);
        editor.ShaderProperty(tpu,     "Texels Per Unit");
        editor.ShaderProperty(tilingX, "Tiling X");
        editor.ShaderProperty(tilingY, "Tiling Y");
        editor.ShaderProperty(offsetX, "Offset X");
        editor.ShaderProperty(offsetY, "Offset Y");

        // Surface
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel);
        editor.ShaderProperty(metallic,    "Metallic");
        editor.ShaderProperty(glossiness,  "Smoothness");
        editor.ShaderProperty(color,       "Tint");
        editor.ShaderProperty(specular,    "Specular Highlights");
        editor.ShaderProperty(reflections, "Reflections");

        // Emission
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Emission", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(useEmission, "Enable Emission");
        if (EditorGUI.EndChangeCheck())
            SetEmissionKeyword(mat, useEmission.floatValue > 0.5f);

        if (useEmission.floatValue > 0.5f)
        {
            editor.ShaderProperty(emColor,    "Color (HDR)");
            editor.ShaderProperty(emStrength, "Strength");
        }
    }

    static void SetBlendMode(Material mat, int mode)
    {
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");

        switch (mode)
        {
            case 0: // Opaque
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.renderQueue = -1;
                break;
            case 1: // Cutout
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.renderQueue = 2450;
                mat.EnableKeyword("_ALPHATEST_ON");
                break;
            case 2: // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_ALPHABLEND_ON");
                break;
        }
    }

    static void SetEmissionKeyword(Material mat, bool enabled)
    {
        if (enabled)
            mat.EnableKeyword("_EMISSION");
        else
            mat.DisableKeyword("_EMISSION");
    }
}