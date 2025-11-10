using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class SpriteMetaFile
{
    public int version;
    public string license;
    public string copyright;
    public Size size;
    public List<State> states;

    [System.Serializable]
    public class Size
    {
        public int x;
        public int y;
    }

    [System.Serializable]
    public class State
    {
        public string name;
        public int directions;
        public List<List<float>> delays;
    }
}

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteScript : MonoBehaviour
{
    [Header("Setup")]
    public Sprite Sprite;
    public SpriteRenderer Renderer;
    public int Sort = 0;

    [Header("Behavior")]
    [Tooltip("animated = animation, directional = direction set")]
    public string SpriteType = "directional"; // "animated" or "directional"
    [Tooltip("Optional: name of the state to use from meta.json")]
    public string StateName = "directional";
    public bool Loop = false;
    public bool DestroyOnStop = false;
    public bool RandomStart = false;

    [Header("Runtime")]
    private SpriteMetaFile meta;
    public Sprite[] frames;
    private float[] frameDurations;
    private int currentFrame = 0;
    private float timer = 0f;
    private bool playing = true;

    void Awake()
    {
        if (Renderer == null)
            Renderer = GetComponent<SpriteRenderer>();
        Renderer.sortingOrder = Sort;
    }

    void Start()
    {
        LoadMetaForSprite();
        SliceSprite();
        ApplyFrame(RandomStart ? UnityEngine.Random.Range(0, frames.Length) : 0);
    }

    void Update()
    {
        if (SpriteType != "animated" || !playing || frames == null || frames.Length == 0)
            return;

        timer += Time.deltaTime;
        float dur = (frameDurations != null && currentFrame < frameDurations.Length) ? frameDurations[currentFrame] : 0.1f;
        if (dur <= 0f) dur = 0.1f;

        if (timer >= dur)
        {
            timer -= dur;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (Loop)
                    currentFrame = 0;
                else
                {
                    Stop();
                    return;
                }
            }

            ApplyFrame(currentFrame);
        }
    }

    void LoadMetaForSprite()
    {
#if UNITY_EDITOR
        if (Sprite == null)
        {
            Debug.LogWarning("SpriteScript: No sprite assigned!");
            return;
        }

        string spritePath = UnityEditor.AssetDatabase.GetAssetPath(Sprite);
        string directory = Path.GetDirectoryName(spritePath);
        string metaPath = Path.Combine(directory, "meta.json");

        if (!File.Exists(metaPath)) return;

        try
        {
            string json = File.ReadAllText(metaPath);
            meta = JsonUtility.FromJson<SpriteMetaFile>(json);

            // Try manual delays extraction
            foreach (var s in meta.states)
                if (s.delays == null)
                    s.delays = TryExtractDelaysForState(json, s.name);
        }
        catch (Exception e)
        {
            Debug.LogError($"SpriteScript: Failed to parse meta.json: {e.Message}");
        }
#endif
    }

    List<List<float>> TryExtractDelaysForState(string json, string stateName)
    {
        var result = new List<List<float>>();
        int found = json.IndexOf($"\"{stateName}\"", StringComparison.OrdinalIgnoreCase);
        if (found < 0) return null;
        int delaysPos = json.IndexOf("\"delays\"", found, StringComparison.OrdinalIgnoreCase);
        if (delaysPos < 0) return null;
        int open = json.IndexOf('[', delaysPos);
        if (open < 0) return null;
        int depth = 0;
        int i = open;
        for (; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']')
            {
                depth--;
                if (depth == 0) break;
            }
        }
        if (i >= json.Length) return null;

        string block = json.Substring(open, i - open + 1).Replace(" ", "").Replace("\n", "");
        if (!block.StartsWith("[[")) return null;

        int pos = 1;
        while (pos < block.Length)
        {
            int start = block.IndexOf('[', pos);
            if (start < 0) break;
            int end = block.IndexOf(']', start);
            if (end < 0) break;
            string inner = block.Substring(start + 1, end - start - 1);
            var numbers = inner.Split(',');
            var row = new List<float>();
            foreach (var n in numbers)
            {
                if (float.TryParse(n, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
                    row.Add(f);
            }
            result.Add(row);
            pos = end + 1;
        }
        return result;
    }

    void SliceSprite()
    {
        if (Sprite == null) return;
        Texture2D tex = Sprite.texture;
        Rect rect = Sprite.rect;
        int frameSize = 32;
        if (meta != null && meta.size != null)
            frameSize = meta.size.x;

        int cols = Mathf.Max(1, Mathf.FloorToInt(rect.width / frameSize));
        int rows = Mathf.Max(1, Mathf.FloorToInt(rect.height / frameSize));
        int total = cols * rows;

        SpriteMetaFile.State state = null;
        if (meta != null && meta.states != null)
        {
            foreach (var s in meta.states)
            {
                if (!string.IsNullOrEmpty(StateName) && s.name.Equals(StateName, StringComparison.OrdinalIgnoreCase))
                {
                    state = s;
                    break;
                }
                if (state == null && Sprite.name.ToLower().Contains(s.name.ToLower()))
                {
                    state = s;
                }
            }
        }

        if (SpriteType == "animated")
        {
            // use delays
            var delays = (state != null && state.delays != null && state.delays.Count > 0) ? state.delays[0] : null;
            int count = delays != null ? delays.Count : total;
            count = Mathf.Min(count, total);
            frames = new Sprite[count];
            frameDurations = new float[count];
            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = rect.x + col * frameSize;
                float y = rect.y + rect.height - (row + 1) * frameSize;
                frames[i] = Sprite.Create(tex, new Rect(x, y, frameSize, frameSize), new Vector2(0.5f, 0.5f), Sprite.pixelsPerUnit);
                frameDurations[i] = (delays != null && i < delays.Count) ? delays[i] : 0.1f;
            }
        }
        else // directional
        {
            int dirs = (state != null && state.directions > 0) ? state.directions : total;
            dirs = Mathf.Min(dirs, total);
            frames = new Sprite[dirs];
            frameDurations = new float[dirs];
            for (int i = 0; i < dirs; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = rect.x + col * frameSize;
                float y = rect.y + rect.height - (row + 1) * frameSize;
                frames[i] = Sprite.Create(tex, new Rect(x, y, frameSize, frameSize), new Vector2(0.5f, 0.5f), Sprite.pixelsPerUnit);
            }
        }
    }

    void ApplyFrame(int idx)
    {
        if (frames == null || frames.Length == 0) return;
        idx = Mathf.Clamp(idx, 0, frames.Length - 1);
        Renderer.sprite = frames[idx];
    }

    // --- Public API ---
    public void SetFrame(int idx) => ApplyFrame(idx);
    public void Play() => playing = true;
    public void Stop()
    {
        playing = false;
        if (DestroyOnStop)
            Destroy(GetComponent<SubEntity>().MainEntity.gameObject);
    }
    public void SetState(string name)
    {
        StateName = name;
        SliceSprite();
        ApplyFrame(0);
    }
}
