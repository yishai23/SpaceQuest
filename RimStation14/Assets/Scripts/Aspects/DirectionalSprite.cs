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
        public int directions; // for older/directional metas
        // delays: list of lists (BYOND style). We'll parse as nested arrays; JsonUtility requires wrapper,
        // so we parse this portion manually in the loader if needed (see code below).
        public List<List<float>> delays;
    }
}


public class DirectionalSprite : MonoBehaviour
{
    public int Sort = 0;
    public Sprite Sprite;
    public SpriteRenderer Renderer;

    public bool RandomSprite = false;

    public Sprite[] directions;
    private SpriteMetaFile meta;

    void Start()
    {

        if (Renderer == null)
            Renderer = gameObject.AddComponent<SpriteRenderer>();

        Renderer.sortingOrder = Sort;

        LoadMetaForSprite();
        SplitSprite();


        if (RandomSprite)
        {
            int i = UnityEngine.Random.Range(0, directions.Length - 1);
            UpdateSprite(i);
            GetComponentInParent<Entity>().Direction = i;
        }
        else
        {
           UpdateSprite(0);
        }


    }

    void LoadMetaForSprite()
    {
        if (Sprite == null)
        {
            Debug.LogError("DirectionalSprite: No sprite assigned!");
            return;
        }

#if UNITY_EDITOR
        string spritePath = UnityEditor.AssetDatabase.GetAssetPath(Sprite);
        string directory = Path.GetDirectoryName(spritePath);
        string metaPath = Path.Combine(directory, "meta.json");

        if (!File.Exists(metaPath))
        {
            Debug.LogWarning($"No meta.json found near {spritePath}, using 2x2 default.");
            return;
        }

        try
        {
            string json = File.ReadAllText(metaPath);
            meta = JsonUtility.FromJson<SpriteMetaFile>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse meta.json: {e.Message}");
        }
#endif
    }

    void SplitSprite()
    {
        if (Sprite == null) return;

        Texture2D tex = Sprite.texture;
        Rect spriteRect = Sprite.rect;
        int frameSize = 32; // default fallback

        if (meta != null && meta.size != null)
            frameSize = meta.size.x; // assume square frame

        // Find the matching state entry for this sprite
        SpriteMetaFile.State state = null;
        if (meta != null && meta.states != null)
        {
            string spriteName = Sprite.name.ToLower();
            foreach (var s in meta.states)
            {
                if (spriteName.Contains(s.name.ToLower()))
                {
                    state = s;
                    break;
                }
            }
        }

        // Default to 1 direction if not defined
        int dirs = 1;
        if (state != null && state.directions > 0)
            dirs = state.directions;

        directions = new Sprite[dirs];

        int rows = (dirs == 4) ? 2 : 1;
        int cols = Mathf.CeilToInt((float)dirs / rows);

        for (int i = 0; i < dirs; i++)
        {
            int row = i % rows; // vertical first
            int col = i / rows;

            float x = spriteRect.x + col * frameSize;
            float y = spriteRect.y + (rows - 1 - row) * frameSize;

            Rect rect = new Rect(x, y, frameSize, frameSize);

            directions[i] = Sprite.Create(
                tex,
                rect,
                new Vector2(0.5f, 0.5f),
                Sprite.pixelsPerUnit
            );
        }
    }

    public void UpdateSprite(int dir)
    {
        if (directions == null || directions.Length == 0) return;
        dir = Mathf.Clamp(dir, 0, directions.Length - 1);
        Renderer.sprite = directions[dir];
    }
}
