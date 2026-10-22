using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlanetSettings))]
public class PlanetSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Validate", GUILayout.Height(30)))
        {
            ((PlanetSettings)target).Validate();
        }
    }
}

[CustomEditor(typeof(Planet))]
public class PlanetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Validate", GUILayout.Height(30)))
        {
            ((Planet)target).Regenerate();
        }
    }
}