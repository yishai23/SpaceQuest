using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public class EntityDefinition
{
    public string id;
    public string name;
    public string rootPath;
    public Dictionary<string, string> arguments;
    public LimbDefinition limb;

    // allow inheritance
    public string extends;

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
    public string type;
    public Dictionary<string, string> properties;
}

public class EntitySpawner : MonoBehaviour
{
    public static EntitySpawner instance;

    // Caches to avoid repeated Resources loads and merges
    private Dictionary<string, EntityDefinition> rawLoadCache = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, EntityDefinition> mergedCache = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// Spawn an entity from a JSON string. The JSON can optionally include "extends".
    /// </summary>
    public GameObject SpawnEntity(string json, Vector3 position, Dictionary<string, string> args = null)
    {
        // Deserialize override JSON
        EntityDefinition overrideDef = JsonConvert.DeserializeObject<EntityDefinition>(json);
        if (overrideDef == null)
        {
            Debug.LogError("Failed to parse entity JSON.");
            return null;
        }

        // Resolve extends chain and merge into finalDef (with caching)
        EntityDefinition finalDef = ResolveAndMergeDefinition(overrideDef);

        // Create root GameObject
        GameObject root = new GameObject(finalDef.name ?? "Entity");
        root.transform.position = position;
        root.AddComponent<Entity>();

        Dictionary<string, string> arguments = args ?? finalDef.GetArgumentsDictionary();

        // Build full limb hierarchy
        BuildLimb(finalDef.limb, root.transform, arguments, finalDef.rootPath);

        root.GetComponent<Entity>().FindComps();
        return root;
    }

    // ======================================================
    // Loading / resolving / merging with caching
    // ======================================================

    /// <summary>
    /// Resolve extends chain and return merged definition. Caches merged results.
    /// CacheKey uses override's id (or name) + root extends chain to avoid re-merging repeatedly.
    /// </summary>
    private EntityDefinition ResolveAndMergeDefinition(EntityDefinition overrideDef)
    {
        if (overrideDef == null) return null;

        // If the override has no extends, nothing to resolve — still clone to avoid mutating input
        if (string.IsNullOrEmpty(overrideDef.extends))
            return CloneEntityDefinition(overrideDef);

        // Build cache key: "<extends>::<id or name>"
        string keyIdentifier = (overrideDef.id ?? overrideDef.name ?? Guid.NewGuid().ToString()).ToLowerInvariant();
        string cacheKey = $"{overrideDef.extends.ToLowerInvariant()}::{keyIdentifier}";

        if (mergedCache.TryGetValue(cacheKey, out var cached))
            return CloneEntityDefinition(cached);

        // Load base (resolving its own extends recursively)
        EntityDefinition baseDef = LoadEntityDefinitionFromResources(overrideDef.extends);
        if (baseDef == null)
        {
            Debug.LogWarning($"Base template '{overrideDef.extends}' not found in Resources/Entities or Resources/Entities/base.");
            // fallback: return override as-is
            var fallback = CloneEntityDefinition(overrideDef);
            mergedCache[cacheKey] = fallback;
            return CloneEntityDefinition(fallback);
        }

        // If base itself extends another, resolve it
        if (!string.IsNullOrEmpty(baseDef.extends))
            baseDef = ResolveAndMergeDefinition(baseDef);

        // Merge base + override
        EntityDefinition merged = MergeEntityDefinitions(baseDef, overrideDef);
        mergedCache[cacheKey] = CloneEntityDefinition(merged);

        return CloneEntityDefinition(merged);
    }

    /// <summary>
    /// Loads an entity definition from Resources. Tries "Entities/{name}" then "Entities/base/{name}".
    /// Uses rawLoadCache to avoid repeated Resources.Load calls.
    /// </summary>
    private EntityDefinition LoadEntityDefinitionFromResources(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;

        string resourceKey = resourceName.ToLowerInvariant();
        if (rawLoadCache.TryGetValue(resourceKey, out var cached))
            return CloneEntityDefinition(cached);

        // Try direct path first
        TextAsset ta = Resources.Load<TextAsset>($"Entities/{resourceName}");
        if (ta == null)
        {
            // Try base subfolder
            ta = Resources.Load<TextAsset>($"Entities/base/{resourceName}");
        }
        if (ta == null)
        {
            // Try trimming any ".json" if present and retry
            string trimmed = resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? resourceName.Substring(0, resourceName.Length - 5)
                : resourceName;
            if (!trimmed.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
            {
                ta = Resources.Load<TextAsset>($"Entities/{trimmed}") ?? Resources.Load<TextAsset>($"Entities/base/{trimmed}");
            }
        }

        if (ta == null)
        {
            Debug.LogWarning($"Failed to load entity definition '{resourceName}' from Resources/Entities or Resources/Entities/base.");
            rawLoadCache[resourceKey] = null;
            return null;
        }

        EntityDefinition parsed = JsonConvert.DeserializeObject<EntityDefinition>(ta.text);
        rawLoadCache[resourceKey] = CloneEntityDefinition(parsed);
        return CloneEntityDefinition(parsed);
    }

    // ======================================================
    // Merge logic (same rules as before)
    // ======================================================

    private EntityDefinition MergeEntityDefinitions(EntityDefinition baseDef, EntityDefinition overrideDef)
    {
        if (baseDef == null) return CloneEntityDefinition(overrideDef);
        if (overrideDef == null) return CloneEntityDefinition(baseDef);

        EntityDefinition result = new EntityDefinition();

        result.id = string.IsNullOrEmpty(overrideDef.id) ? baseDef.id : overrideDef.id;
        result.name = string.IsNullOrEmpty(overrideDef.name) ? baseDef.name : overrideDef.name;
        result.rootPath = string.IsNullOrEmpty(overrideDef.rootPath) ? baseDef.rootPath : overrideDef.rootPath;
        result.extends = null; // merged result shouldn’t have extends

        // Merge arguments
        result.arguments = new Dictionary<string, string>();
        if (baseDef.arguments != null)
        {
            foreach (var kv in baseDef.arguments)
                result.arguments[kv.Key] = kv.Value;
        }
        if (overrideDef.arguments != null)
        {
            foreach (var kv in overrideDef.arguments)
                result.arguments[kv.Key] = kv.Value;
        }

        // Merge limbs recursively
        result.limb = MergeLimbDefinitions(baseDef.limb, overrideDef.limb);

        return result;
    }

    private LimbDefinition MergeLimbDefinitions(LimbDefinition baseLimb, LimbDefinition overrideLimb)
    {
        if (baseLimb == null) return CloneLimb(overrideLimb);
        if (overrideLimb == null) return CloneLimb(baseLimb);

        LimbDefinition merged = new LimbDefinition();
        merged.name = string.IsNullOrEmpty(overrideLimb.name) ? baseLimb.name : overrideLimb.name;

        // Merge components (by type)
        var compMap = new Dictionary<string, ComponentDefinition>(StringComparer.OrdinalIgnoreCase);
        if (baseLimb.components != null)
        {
            foreach (var c in baseLimb.components)
                compMap[c.type] = CloneComponent(c);
        }
        if (overrideLimb.components != null)
        {
            foreach (var oc in overrideLimb.components)
            {
                if (compMap.TryGetValue(oc.type, out var existing))
                {
                    var mergedProps = new Dictionary<string, string>(existing.properties ?? new Dictionary<string, string>());
                    if (oc.properties != null)
                    {
                        foreach (var kv in oc.properties)
                            mergedProps[kv.Key] = kv.Value;
                    }
                    existing.properties = mergedProps;
                    compMap[oc.type] = existing;
                }
                else
                {
                    compMap[oc.type] = CloneComponent(oc);
                }
            }
        }
        merged.components = compMap.Values.ToList();

        // Merge children (by name)
        var childMap = new Dictionary<string, LimbDefinition>(StringComparer.OrdinalIgnoreCase);
        if (baseLimb.children != null)
        {
            foreach (var c in baseLimb.children)
                childMap[c.name] = CloneLimb(c);
        }
        if (overrideLimb.children != null)
        {
            foreach (var oc in overrideLimb.children)
            {
                if (childMap.TryGetValue(oc.name, out var existing))
                {
                    childMap[oc.name] = MergeLimbDefinitions(existing, oc);
                }
                else
                {
                    childMap[oc.name] = CloneLimb(oc);
                }
            }
        }
        merged.children = childMap.Values.ToList();

        return merged;
    }

    // ======================================================
    // Cloning helpers (avoid mutating cached objects)
    // ======================================================

    private EntityDefinition CloneEntityDefinition(EntityDefinition src)
    {
        if (src == null) return null;
        return new EntityDefinition
        {
            id = src.id,
            name = src.name,
            rootPath = src.rootPath,
            arguments = src.arguments != null ? new Dictionary<string, string>(src.arguments) : null,
            limb = CloneLimb(src.limb),
            extends = src.extends
        };
    }

    private LimbDefinition CloneLimb(LimbDefinition src)
    {
        if (src == null) return null;
        return new LimbDefinition
        {
            name = src.name,
            components = src.components?.Select(CloneComponent).ToList(),
            children = src.children?.Select(CloneLimb).ToList()
        };
    }

    private ComponentDefinition CloneComponent(ComponentDefinition src)
    {
        if (src == null) return null;
        return new ComponentDefinition
        {
            type = src.type,
            properties = src.properties != null
                ? new Dictionary<string, string>(src.properties)
                : new Dictionary<string, string>()
        };
    }

    // ======================================================
    // GameObject building (same as your original)
    // ======================================================

    private void BuildLimb(LimbDefinition limbDef, Transform parent, Dictionary<string, string> args, string rootPath)
    {
        if (limbDef == null) return;

        GameObject limbObj = new GameObject(limbDef.name);
        limbObj.transform.SetParent(parent);
        limbObj.transform.localPosition = Vector3.zero;
        limbObj.AddComponent<SubEntity>().MainEntity = parent.GetComponentInParent<Entity>();

        // Add components
        if (limbDef.components != null)
        {
            foreach (var comp in limbDef.components)
                AddComponent(limbObj, comp, args, rootPath);
        }

        // Recursively build children
        if (limbDef.children != null)
        {
            foreach (var child in limbDef.children)
                BuildLimb(child, limbObj.transform, args, rootPath);
        }
    }

    private void AddComponent(GameObject obj, ComponentDefinition def, Dictionary<string, string> args, string rootPath)
    {
        if (def == null || string.IsNullOrEmpty(def.type)) return;

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

    private string ReplaceArguments(string path, Dictionary<string, string> args)
    {
        if (string.IsNullOrEmpty(path) || args == null)
            return path;

        foreach (var kvp in args)
            path = path.Replace("{" + kvp.Key + "}", kvp.Value);

        return path;
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
