namespace SignalsEverywhere;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class PrefabTypeAnalyzer
{
    public static void AnalyzeFromMonoBehaviour(MonoBehaviour root)
    {
        if (root == null)
        {
            Debug.LogWarning("Analyzer: root is null");
            return;
        }

        var visitedObjects = new HashSet<UnityEngine.Object>();

        Debug.Log($"=== Prefab Type Analysis START ({root.name}) ===");

        AnalyzeTransform(root.transform, 0, visitedObjects);

        Debug.Log("=== Prefab Type Analysis END ===");
    }

    public static void AnalyzeParents(Transform transform)
    {
        var visitedObjects = new HashSet<UnityEngine.Object>();
        
        Debug.Log($"=== Prefab Parent Analysis START ({transform.name}) ===");
        AnalyzeParentTransform(transform, 0, visitedObjects);
        Debug.Log("=== Prefab Parent Analysis END ===");
    }
    
    private static void AnalyzeParentTransform(
        Transform transform,
        int depth,
        HashSet<UnityEngine.Object> visited)
    {
        string indent = new string(' ', depth * 2);

        Debug.Log($"{indent}GameObject: {transform.name}");

        // Components on this GameObject
        foreach (var component in transform.GetComponents<Component>())
        {
            if (component == null) continue;

            var type = component.GetType();
            Debug.Log($"{indent}  Component: {type.FullName}");

            AnalyzeComponentFields(component, depth + 2, visited);
        }
        
        if (transform.parent == null) return;
        AnalyzeParentTransform(transform.parent, depth + 1, visited);
    }

    private static void AnalyzeTransform(
        Transform transform,
        int depth,
        HashSet<UnityEngine.Object> visited)
    {
        string indent = new string(' ', depth * 2);

        Debug.Log($"{indent}GameObject: {transform.name}");

        // Components on this GameObject
        foreach (var component in transform.GetComponents<Component>())
        {
            if (component == null) continue;

            var type = component.GetType();
            Debug.Log($"{indent}  Component: {type.FullName}");

            AnalyzeComponentFields(component, depth + 2, visited);
        }

        // Recurse children
        foreach (Transform child in transform)
        {
            AnalyzeTransform(child, depth + 1, visited);
        }
    }

    private static void AnalyzeComponentFields(
        Component component,
        int depth,
        HashSet<UnityEngine.Object> visited)
    {
        var type = component.GetType();
        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic
        );

        string indent = new string(' ', depth * 2);

        foreach (var field in fields)
        {
            // Only UnityEngine.Object references matter
            if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                continue;

            UnityEngine.Object value;

            try
            {
                value = field.GetValue(component) as UnityEngine.Object;
            }
            catch
            {
                continue;
            }

            if (value == null)
                continue;

            if (!visited.Add(value))
                continue;

            Debug.Log($"{indent}Field: {field.Name} -> {value.GetType().FullName}");

            // If ScriptableObject, analyze its fields too
            if (value is ScriptableObject so)
            {
                AnalyzeScriptableObject(so, depth + 1, visited);
            }
        }
    }

    private static void AnalyzeScriptableObject(
        ScriptableObject so,
        int depth,
        HashSet<UnityEngine.Object> visited)
    {
        string indent = new string(' ', depth * 2);

        Debug.Log($"{indent}ScriptableObject: {so.GetType().FullName}");

        var fields = so.GetType().GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic
        );

        foreach (var field in fields)
        {
            if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                continue;

            UnityEngine.Object value;

            try
            {
                value = field.GetValue(so) as UnityEngine.Object;
            }
            catch
            {
                continue;
            }

            if (value == null)
                continue;

            if (!visited.Add(value))
                continue;

            Debug.Log($"{indent}  Field: {field.Name} -> {value.GetType().FullName}");

            if (value is ScriptableObject nested)
            {
                AnalyzeScriptableObject(nested, depth + 1, visited);
            }
        }
    }
}
