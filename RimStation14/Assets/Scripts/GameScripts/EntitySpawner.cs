using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json; // Use Newtonsoft for JSON parsing

[Serializable]
public class EntityDefinition
{
    public string id;
    public string name;
    public string rootPath; // Base folder for sprites
    public Dictionary<string, string> arguments;
    public LimbDefinition limb;

    public Dictionary<string, string> GetArgumentsDictionary()
    {
        return arguments ?? new Dictionary<string, string>();
    }
}

[Serializable]
public class LimbDefinition
{
    public string name;
    public List<ComponentDefinition> components;
    public List<LimbDefinition> children;
}

[Serializable]
public class ComponentDefinition
{
    public string type; // e.g. "Health", "SpriteRenderer", "DirectionalSprite"
    public Dictionary<string, string> properties; // e.g. { "maxHealth": "100" }
}

public class EntitySpawner : MonoBehaviour
{
    public static EntitySpawner instance;
    private void Awake()
    {
        instance = this;
    }
    public GameObject SpawnEntity(string json, Vector3 position, Dictionary<string, string> args = null)
    {
        // Deserialize JSON into entity definition
        EntityDefinition entityDef = JsonConvert.DeserializeObject<EntityDefinition>(json);
        if (entityDef == null)
        {
            Debug.LogError("Failed to parse entity JSON.");
            return null;
        }

        // Create root GameObject
        GameObject root = new GameObject(entityDef.name);
        root.transform.position = position;
        root.AddComponent<Entity>();

        Dictionary<string, string> arguments = args ?? entityDef.GetArgumentsDictionary();

        // Build the full limb hierarchy
        BuildLimb(entityDef.limb, root.transform, arguments, entityDef.rootPath);

        root.GetComponent<Entity>().FindComps();
        return root;
    }

    private void BuildLimb(LimbDefinition limbDef, Transform parent, Dictionary<string, string> args, string rootPath)
    {
        if (limbDef == null) return;

        GameObject limbObj = new GameObject(limbDef.name);
        limbObj.transform.SetParent(parent);
        limbObj.transform.localPosition = Vector3.zero;
        limbObj.AddComponent<SubEntity>().MainEntity = parent.GetComponentInParent<Entity>();
        // Add all components to this limb
        if (limbDef.components != null)
        {
            foreach (var comp in limbDef.components)
                AddComponent(limbObj, comp, args, rootPath);
        }

        // Recursively add children limbs
        if (limbDef.children != null)
        {
            foreach (var child in limbDef.children)
                BuildLimb(child, limbObj.transform, args, rootPath);
        }
    }

    private string ReplaceArguments(string path, Dictionary<string, string> args)
    {
        if (string.IsNullOrEmpty(path) || args == null)
            return path;

        foreach (var kvp in args)
            path = path.Replace("{" + kvp.Key + "}", kvp.Value);

        return path;
    }

    private void AddComponent(GameObject obj, ComponentDefinition def, Dictionary<string, string> args, string rootPath)
    {
        // Try to resolve the component type by name
        Type type = Type.GetType(def.type);
        if (type == null)
        {
            type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == def.type);

            if (type == null)
            {
                Debug.LogWarning($"Component type not found: {def.type}");
                return;
            }
        }

        Component comp = obj.AddComponent(type);

        // Apply all serialized properties to the component
        if (def.properties != null)
        {
            foreach (var kvp in def.properties)
            {
                string key = kvp.Key;
                string value = ReplaceArguments(kvp.Value, args);

                var field = type.GetField(key);
                var prop = type.GetProperty(key);

                try
                {
                    if (field != null)
                    {
                        field.SetValue(comp, ConvertTo(value, field.FieldType, rootPath));
                    }
                    else if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(comp, ConvertTo(value, prop.PropertyType, rootPath));
                    }
                    else
                    {
                        Debug.LogWarning($"Property or field '{key}' not found on {type.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to set '{key}' on {type.Name}: {ex.Message}");
                }
            }
        }
    }

    private object ConvertTo(string value, Type targetType, string rootPath)
    {
        if (targetType == typeof(string))
            return value;
        if (targetType == typeof(int))
            return int.Parse(value);
        if (targetType == typeof(float))
            return float.Parse(value);
        if (targetType == typeof(bool))
            return bool.Parse(value);

        if (targetType == typeof(Sprite))
        {
            string fullPath = rootPath + value;
            Sprite sprite = Resources.Load<Sprite>(fullPath);
            if (sprite == null)
                Debug.LogWarning($"Sprite not found at path: {fullPath}");
            return sprite;
        }

        if (targetType == typeof(Vector3))
        {
            string[] parts = value.Split(',');
            if (parts.Length == 3)
                return new Vector3(
                    float.Parse(parts[0]),
                    float.Parse(parts[1]),
                    float.Parse(parts[2]));
        }

        // Attempt a general conversion
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return null;
        }
    }
}
