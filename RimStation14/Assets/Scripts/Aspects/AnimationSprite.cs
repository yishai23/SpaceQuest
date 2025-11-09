// AnimationSprite.cs
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AnimationSprite : MonoBehaviour
{
    [Header("Sprite + Render")]
    public Sprite Sprite;
    public SpriteRenderer Renderer;
    public int Sort = 0;

    [Header("Runtime")]
    [Tooltip("Name of the state to use from meta.json (matches state.name). If empty, uses Sprite.name substring match.")]
    public string StateName;

    // Animation data
    private Sprite[] frames;          // the extracted frames (could be directions or animation frames)
    private float[] frameDurations;   // per-frame duration (if animated). If all zero -> not animated.
    private int currentFrame = 0;
    private float timer = 0f;
    public bool playing = true;
    public bool isAnimated = false;
    public bool Loop = false;
    public bool DestroyOnStop = false;
    private SpriteMetaFile meta;

    void Awake()
    {
        if (Renderer == null)
            Renderer = GetComponent<SpriteRenderer>();

        Renderer.sortingOrder = Sort;
    }

    void Start()
    {
        LoadMetaForSprite();
        SplitSpriteAndInit();
        ApplyFrame(0);
    }

   

    void Update()
    {
        if (!playing || !isAnimated || frames == null || frames.Length == 0) return;

        timer += Time.deltaTime;
        float dur = frameDurations != null && frameDurations.Length > 0 ? frameDurations[currentFrame] : 0.1f;
        if (dur <= 0f) dur = 0.1f;

        if (timer >= dur)
        {
            timer -= dur;

            // Advance but don't wrap — stop at the last frame if Loop == false
            if (currentFrame + 1 < frames.Length)
            {
                currentFrame = currentFrame + 1;
                ApplyFrame(currentFrame);
            }
            else
            {
                // reached the final frame
                if (Loop)
                {
                    currentFrame = 0;
                    ApplyFrame(currentFrame);
                }
                else
                {
                    // ensure last frame is applied and stop playback
                    currentFrame = frames.Length - 1;
                    ApplyFrame(currentFrame);
                    Stop();
                    timer = 0f;
                }
            }
        }
    }

    void LoadMetaForSprite()
    {
        if (Sprite == null)
        {
            Debug.LogWarning("AnimationSprite: No Sprite assigned.");
            return;
        }

#if UNITY_EDITOR
        string spritePath = UnityEditor.AssetDatabase.GetAssetPath(Sprite);
        if (string.IsNullOrEmpty(spritePath))
        {
            Debug.LogWarning($"AnimationSprite: Couldn't determine asset path for sprite: {Sprite.name}");
            return;
        }

        string directory = Path.GetDirectoryName(spritePath);
        string metaPath = Path.Combine(directory, "meta.json");

        if (!File.Exists(metaPath))
        {
            // no meta -> leave meta null, fallbacks will use default frame size
            return;
        }

        try
        {
            string json = File.ReadAllText(metaPath);

            // Unity's JsonUtility cannot directly parse nested arrays into List<List<float>>
            // So we parse the file twice: first into a lightweight object for states without delays,
            // then manually parse delays entries with a small helper.
            meta = JsonUtility.FromJson<SpriteMetaFile>(json);

            // Manual parse of delays field per-state (if the meta contains delays)
            foreach (var s in meta.states)
            {
                if (s.delays == null)
                {
                    s.delays = TryExtractDelaysForState(json, s.name);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AnimationSprite: Failed to parse meta.json: {e.Message}");
            meta = null;
        }
#endif
    }

    // Very small, forgiving parser to extract List<List<float>> for a state name from the JSON.
    private List<List<float>> TryExtractDelaysForState(string json, string stateName)
    {
        var result = new List<List<float>>();

        int found = json.IndexOf($"\"{stateName}\"", System.StringComparison.OrdinalIgnoreCase);
        if (found < 0) return null;

        int delaysPos = json.IndexOf("\"delays\"", found, System.StringComparison.OrdinalIgnoreCase);
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

        string delaysBlock = json.Substring(open, i - open + 1);

        string trimmed = delaysBlock.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");

        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        else return null;

        int pos = 0;
        while (pos < trimmed.Length)
        {
            int sOpen = trimmed.IndexOf('[', pos);
            if (sOpen < 0) break;
            int d = 0;
            int j = sOpen;
            for (; j < trimmed.Length; j++)
            {
                if (trimmed[j] == '[') d++;
                else if (trimmed[j] == ']')
                {
                    d--;
                    if (d == 0) break;
                }
            }
            if (j >= trimmed.Length) break;
            string inner = trimmed.Substring(sOpen + 1, j - sOpen - 1); // contents between [ ]
            var numbers = inner.Split(',');
            var row = new List<float>();
            foreach (var n in numbers)
            {
                if (string.IsNullOrEmpty(n)) continue;
                if (float.TryParse(n, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
                    row.Add(f);
            }
            result.Add(row);
            pos = j + 1;
            if (pos < trimmed.Length && trimmed[pos] == ',') pos++;
        }

        return result.Count > 0 ? result : null;
    }

    void SplitSpriteAndInit()
    {
        if (Sprite == null) return;

        Texture2D tex = Sprite.texture;
        Rect spriteRect = Sprite.rect;

        int frameSize = 32;
        if (meta != null && meta.size != null)
            frameSize = meta.size.x;

        // compute how many columns and rows exist in the sprite rect
        int cols = Mathf.Max(1, Mathf.FloorToInt(spriteRect.width / frameSize));
        int rows = Mathf.Max(1, Mathf.FloorToInt(spriteRect.height / frameSize));
        int totalPossibleFrames = cols * rows;

        // pick state: explicit StateName else try matching sprite name -> state.name substring
        SpriteMetaFile.State state = null;
        if (meta != null && meta.states != null)
        {
            if (!string.IsNullOrEmpty(StateName))
            {
                foreach (var s in meta.states)
                    if (s.name.Equals(StateName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        state = s;
                        break;
                    }
            }

            if (state == null)
            {
                string spriteName = Sprite.name.ToLower();
                foreach (var s in meta.states)
                {
                    if (!string.IsNullOrEmpty(s.name) && spriteName.Contains(s.name.ToLower()))
                    {
                        state = s;
                        break;
                    }
                }
            }
        }

        // Determine mode: animated if state has delays, otherwise if state has directions > 0 treat as directional
        if (state != null && state.delays != null && state.delays.Count > 0)
        {
            // Use the first delays row for frame durations
            var delaysRow = state.delays[0];
            int framesCount = Mathf.Max(1, delaysRow.Count);

            // If the delays row indicates more frames than physically present, clamp and warn.
            if (framesCount > totalPossibleFrames)
            {
                Debug.LogWarning($"AnimationSprite: delays indicate {framesCount} frames but sprite has space for {totalPossibleFrames} frames. Clamping to available frames.");
                framesCount = totalPossibleFrames;
            }

            frames = new Sprite[framesCount];
            frameDurations = new float[framesCount];
            isAnimated = true;

            for (int f = 0; f < framesCount; f++)
            {
                // map frame index to col,row (left-to-right, top-to-bottom)
                int col = f % cols;
                int row = f / cols; // 0 = top row in this mapping we'll convert below

                // spriteRect.y is the bottom of the rect in texture space, but Sprite.rect uses y as bottom.
                // We want top-left mapping: compute y from top-down.
                float x = spriteRect.x + col * frameSize;
                // row 0 is top row -> y = spriteRect.y + spriteRect.height - (row+1)*frameSize
                float y = spriteRect.y + spriteRect.height - (row + 1) * frameSize;

                // Validate bounds
                if (x < 0 || y < 0 || x + frameSize > tex.width || y + frameSize > tex.height)
                {
                    Debug.LogError($"AnimationSprite: Computed frame rect (x={x}, y={y}, w={frameSize}, h={frameSize}) is outside texture bounds ({tex.width}x{tex.height}). Skipping frame {f}.");
                    frames[f] = null;
                    frameDurations[f] = (f < delaysRow.Count) ? delaysRow[f] : 0.1f;
                    continue;
                }

                Rect rect = new Rect(x, y, frameSize, frameSize);
                frames[f] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Sprite.pixelsPerUnit);
                frameDurations[f] = (f < delaysRow.Count) ? delaysRow[f] : 0.1f;
            }
            currentFrame = 0;
            timer = 0f;
            playing = true;
        }
        else
        {
            // non-animated: fallback to directional behavior (directions)
            int dirs = 1;
            if (state != null && state.directions > 0)
                dirs = state.directions;

            // Determine rows/cols layout for directional frames:
            // If dirs == 4 -> commonly 2x2, otherwise try single row if fits, or multiple rows if needed.
            int dirCols = cols;
            int dirRows = rows;

            // If the sprite area has fewer cells than requested dirs, cap and warn.
            if (dirs > totalPossibleFrames)
            {
                Debug.LogWarning($"AnimationSprite: requested {dirs} directional frames but sprite only contains {totalPossibleFrames} cells. Clamping to available cells.");
                dirs = totalPossibleFrames;
            }

            frames = new Sprite[dirs];
            frameDurations = new float[dirs];
            isAnimated = false;
            playing = false;

            for (int i = 0; i < dirs; i++)
            {
                // Map direction index to col/row left-to-right, top-to-bottom.
                int col = i % cols;
                int row = i / cols;

                float x = spriteRect.x + col * frameSize;
                float y = spriteRect.y + spriteRect.height - (row + 1) * frameSize;

                if (x < 0 || y < 0 || x + frameSize > tex.width || y + frameSize > tex.height)
                {
                    Debug.LogError($"AnimationSprite: Computed directional rect (x={x}, y={y}, w={frameSize}, h={frameSize}) is outside texture bounds ({tex.width}x{tex.height}). Skipping direction {i}.");
                    frames[i] = null;
                    frameDurations[i] = 0f;
                    continue;
                }

                Rect rect = new Rect(x, y, frameSize, frameSize);
                frames[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Sprite.pixelsPerUnit);
                frameDurations[i] = 0f;
            }
            currentFrame = 0;
            ApplyFrame(0);
        }
    }

    private void ApplyFrame(int idx)
    {
        if (frames == null || frames.Length == 0) return;
        idx = Mathf.Clamp(idx, 0, frames.Length - 1);
        if (frames[idx] == null)
        {
            Debug.LogWarning($"AnimationSprite: frame {idx} is null, not applying.");
            return;
        }
        Renderer.sprite = frames[idx];
    }

    // Public API
    public void Play()
    {
        if (frames == null || frames.Length == 0) return;
        if (!isAnimated) return;
        playing = true;
    }

    public void Stop()
    {
        playing = false;
        if (DestroyOnStop)
        {
            Destroy(GetComponentInParent<Entity>().transform.gameObject);
        }
    }

    public void SetFrame(int idx)
    {
        if (frames == null || frames.Length == 0) return;
        currentFrame = Mathf.Clamp(idx, 0, frames.Length - 1);
        timer = 0f;
        ApplyFrame(currentFrame);
    }

    // change to a different state at runtime (re-slices sprite rect)
    public void SetState(string stateName)
    {
        StateName = stateName;
        SplitSpriteAndInit();
        ApplyFrame(0);
    }
}
