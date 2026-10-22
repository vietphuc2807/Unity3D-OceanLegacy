using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Randomize Selected Trees
/// Chon nhieu cay trong Hierarchy roi chay: moi cay se duoc doi
/// Scale ngau nhien va xoay ngau nhien quanh truc Y. Co Undo (Ctrl+Z).
/// </summary>
public static class RandomizeTrees
{
    private const float MinScale = 0.8f;
    private const float MaxScale = 1.25f;

    // Lech nhe chieu rong so voi chieu cao cho do giong nhau.
    private const float WidthVariation = 0.1f;

    [MenuItem("Tools/Randomize Selected Trees")]
    private static void Randomize()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            Debug.LogWarning("[RandomizeTrees] Chua chon cay nao.");
            return;
        }

        Undo.SetCurrentGroupName("Randomize Trees");
        int group = Undo.GetCurrentGroup();

        foreach (GameObject go in selected)
        {
            Transform t = go.transform;
            Undo.RecordObject(t, "Randomize Tree");

            float height = Random.Range(MinScale, MaxScale);
            float width = height * Random.Range(1f - WidthVariation, 1f + WidthVariation);

            t.localScale = new Vector3(width, height, width);

            Vector3 euler = t.localEulerAngles;
            euler.y = Random.Range(0f, 360f);
            t.localEulerAngles = euler;
        }

        Undo.CollapseUndoOperations(group);

        Debug.Log($"[RandomizeTrees] Da random {selected.Length} cay.");
    }
}