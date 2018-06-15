using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor {
    private MapGenerator mapGenerator;

    public override void OnInspectorGUI() {
        DrawDefaultInspector();
        mapGenerator = (MapGenerator) target;
        if (GUILayout.Button("Generate")) {
            mapGenerator.generateMap();
        }
    }
}
