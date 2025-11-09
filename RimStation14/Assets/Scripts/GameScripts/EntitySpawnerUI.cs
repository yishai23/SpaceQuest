using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime GUI spawner with folder navigation:
/// - Shows all folders and JSON files under Resources/Entities
/// - Click folder to enter it, click "Back" to go up
/// - Select a JSON, set a simple "gender" argument, spawn at mouse
/// </summary>
[DisallowMultipleComponent]
public class EntitySpawnerUI : MonoBehaviour
{
    [Header("Resources")]
    [Tooltip("Folder under Resources to load entity jsons from (no leading/trailing slashes).")]
    public string rootFolder = "Entities";

    [Header("Spawn Controls")]
    public KeyCode spawnKey = KeyCode.E;
    public bool useMousePosition = true;

    [Header("GUI")]
    public Vector2 windowSize = new Vector2(320, 400);
    public int windowPadding = 10;

    // runtime
    private string currentFolder;
    private List<TextAsset> jsons = new List<TextAsset>();
    private List<string> folders = new List<string>();
    private int selectedIndex = -1;
    private string genderArg = "f";
    private Vector2 listScroll = Vector2.zero;
    private string statusMessage = "";
    private EntitySpawner foundSpawner = null;

    void Awake()
    {
        currentFolder = rootFolder;
        LoadCurrentFolder();
        foundSpawner = FindObjectOfType<EntitySpawner>();
        if (foundSpawner == null)
            Debug.LogWarning("EntitySpawner not found. A fallback spawner will be used.");
    }

    void LoadCurrentFolder()
    {
        jsons.Clear();
        folders.Clear();

        // Load all TextAssets in this folder
        TextAsset[] assets = Resources.LoadAll<TextAsset>(currentFolder);
        if (assets != null && assets.Length > 0)
            jsons.AddRange(assets);

        // Load all subfolders (Resources doesn't directly support folders, so we use AssetDatabase in Editor)
#if UNITY_EDITOR
        string resPath = "Assets/Resources/" + currentFolder;
        if (System.IO.Directory.Exists(resPath))
        {
            string[] dirs = System.IO.Directory.GetDirectories(resPath);
            foreach (string dir in dirs)
            {
                string folderName = System.IO.Path.GetFileName(dir);
                folders.Add(folderName);
            }
        }
#endif
        Array.Sort(jsons.ToArray(), (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        folders.Sort();
        if (jsons.Count > 0 && selectedIndex < 0) selectedIndex = 0;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            LoadCurrentFolder();
            statusMessage = $"Reloaded {currentFolder}";
        }

        if (Input.GetKeyDown(spawnKey))
        {
            if (selectedIndex >= 0 && selectedIndex < jsons.Count)
                TrySpawn(jsons[selectedIndex].text, GetMouseWorldPosition());
            else
                statusMessage = "No entity selected!";
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return Vector3.zero;

        Vector3 worldPos = Vector3.zero;
        if (useMousePosition)
        {
            Vector3 screenPos = Input.mousePosition;
            if (cam.orthographic)
            {
                screenPos.z = Mathf.Abs(cam.transform.position.z);
                worldPos = cam.ScreenToWorldPoint(screenPos);
                worldPos.z = 0f;
            }
            else
            {
                Ray ray = cam.ScreenPointToRay(screenPos);
                Plane ground = new Plane(Vector3.up, Vector3.zero);
                if (ground.Raycast(ray, out float enter))
                    worldPos = ray.GetPoint(enter);
                else
                    worldPos = cam.transform.position + cam.transform.forward * 5f;
            }
        }
        else
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, Mathf.Abs(cam.transform.position.z));
            worldPos = cam.ScreenToWorldPoint(screenCenter);
            if (cam.orthographic) worldPos.z = 0f;
        }
        return worldPos;
    }

    void TrySpawn(string jsonText, Vector3 position)
    {
        var args = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(genderArg)) args["gender"] = genderArg;

        if (foundSpawner != null)
        {
            try
            {
                GameObject go = foundSpawner.SpawnEntity(jsonText, position, args);
                statusMessage = go != null ? $"Spawned {go.name} at {position}" : "Spawn failed!";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                statusMessage = "Exception in EntitySpawner.SpawnEntity. See console.";
            }
            return;
        }

        // fallback placeholder
        try
        {
            string fallbackName = $"Entity_{selectedIndex}";
            TextAsset ta = jsons[selectedIndex];
            if (ta != null && !string.IsNullOrEmpty(ta.name)) fallbackName = ta.name;

            GameObject placeholder = new GameObject(fallbackName);
            placeholder.transform.position = position;
            var spr = placeholder.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            spr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            statusMessage = $"[Fallback] Spawned '{fallbackName}' at {position}";
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            statusMessage = "Fallback spawn failed.";
        }
    }

    // --- GUI ---
    Rect WindowRect() => new Rect(windowPadding, windowPadding, windowSize.x, windowSize.y);

    void OnGUI()
    {
        GUI.Window(123456, WindowRect(), DrawWindow, $"Entity Spawner ({currentFolder})");
    }

    void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        // Back button if not root
        if (currentFolder != rootFolder)
        {
            if (GUILayout.Button("< Back"))
            {
                int lastSlash = currentFolder.LastIndexOf('/');
                currentFolder = lastSlash >= 0 ? currentFolder.Substring(0, lastSlash) : rootFolder;
                LoadCurrentFolder();
                selectedIndex = -1;
            }
        }

        // List folders
        if (folders.Count > 0)
        {
            GUILayout.Label("Folders:");
            for (int i = 0; i < folders.Count; i++)
            {
                string folder = folders[i];
                if (GUILayout.Button(folder, GUILayout.ExpandWidth(true)))
                {
                    currentFolder = currentFolder + "/" + folder;
                    LoadCurrentFolder();
                    selectedIndex = -1;
                    break; // exit loop immediately since we've changed the list
                }
            }

        }

        // List JSONs
        GUILayout.Label("Entities:");
        listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(200));
        for (int i = 0; i < jsons.Count; i++)
        {
            TextAsset ta = jsons[i];
            string label = ta != null ? ta.name : $"<null:{i}>";
            GUIStyle style = (i == selectedIndex) ? GUI.skin.button : GUI.skin.box;
            if (GUILayout.Button(label, style)) selectedIndex = i;
        }
        GUILayout.EndScrollView();

        // Selected JSON options
        if (selectedIndex >= 0 && selectedIndex < jsons.Count)
        {
            TextAsset sel = jsons[selectedIndex];
            GUILayout.Label($"Selected: {sel.name}");
            GUILayout.BeginHorizontal();
            GUILayout.Label("gender:", GUILayout.Width(50));
            genderArg = GUILayout.TextField(genderArg, GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.Label("Preview (first 300 chars):");
            string preview = sel.text.Length > 300 ? sel.text.Substring(0, 300) + "..." : sel.text;
            GUILayout.TextArea(preview, GUILayout.Height(80));
        }
        else
        {
            GUILayout.Label("No entity selected");
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Spawn (Key: {spawnKey})"))
        {
            if (selectedIndex >= 0 && selectedIndex < jsons.Count)
                TrySpawn(jsons[selectedIndex].text, GetMouseWorldPosition());
            else
                statusMessage = "No selection to spawn.";
        }
        if (GUILayout.Button("Refresh list (R)"))
        {
            LoadCurrentFolder();
        }
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.Label("Status: " + statusMessage);
        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
}
