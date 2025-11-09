using System.Collections.Generic;
using UnityEngine;
[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("Multiplier for camera movement. (0,0) = fixed to camera, (1,1) = moves with camera.)")]
    public Vector2 parallaxMultiplier = new Vector2(0.5f, 0f);

    [Tooltip("Smoothing factor for parallax movement. 0 = instant.")]
    [Range(0f, 20f)]
    public float smoothing = 8f;

    [Tooltip("If true, attempts to tile horizontally for infinite scrolling.")]
    public bool tileHorizontal = false;

    [Tooltip("Width of a single tile in world units (used when tileHorizontal = true). If 0 it tries to infer from child SpriteRenderer.")]
    public float tileWidth = 0f;

    [Tooltip("If true, attempts to tile vertically for infinite scrolling.")]
    public bool tileVertical = false;

    [Tooltip("Height of a single tile in world units (used when tileVertical = true). If 0 it tries to infer from child SpriteRenderer.")]
    public float tileHeight = 0f;

    // internal state
    Transform _t;
    Vector3 _targetPosition;

    // tiles tracked for continuous tiling
    List<Transform> _tiles = new List<Transform>();

    void Awake()
    {
        _t = transform;
        _targetPosition = _t.position;
        // collect children as tiles
        RefreshTilesList();
        // try infer tile sizes if needed
        InferTileSizesIfNeeded();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _t = transform;
            RefreshTilesList();
            InferTileSizesIfNeeded();
        }
    }

    void Reset()
    {
        parallaxMultiplier = new Vector2(0.5f, 0f);
        smoothing = 8f;
    }

    void RefreshTilesList()
    {
        _tiles.Clear();
        if (_t == null) _t = transform;
        foreach (Transform c in _t)
        {
            _tiles.Add(c);
        }
        // sort by X then Y to have stable ordering
        _tiles.Sort((a, b) =>
        {
            int cmp = a.position.x.CompareTo(b.position.x);
            return cmp != 0 ? cmp : a.position.y.CompareTo(b.position.y);
        });
    }

    void InferTileSizesIfNeeded()
    {
        if ((tileHorizontal == false && tileVertical == false) || (_t == null)) return;

        var spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null)
        {
            if (tileWidth <= 0) tileWidth = spr.bounds.size.x;
            if (tileHeight <= 0) tileHeight = spr.bounds.size.y;
        }
    }

    // Called by Background each frame with camera delta
    public void ApplyParallax(Vector3 cameraDelta, Camera cam)
    {
        if (_t == null) _t = transform;

        Vector3 move = new Vector3(cameraDelta.x * parallaxMultiplier.x, cameraDelta.y * parallaxMultiplier.y, 0f);
        _targetPosition += move;

        if (smoothing <= 0f)
            _t.position = _targetPosition;
        else
            _t.position = Vector3.Lerp(_t.position, _targetPosition, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        if (tileHorizontal) HandleHorizontalTiling(cam);
        if (tileVertical) HandleVerticalTiling(cam);
    }

    void HandleHorizontalTiling(Camera cam)
    {
        if (tileWidth <= 0) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Use camera frustum (assume orthographic) to detect when tiles go off screen.
        float camHalfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : 10f;
        float leftEdge = cam.transform.position.x - camHalfWidth;
        float rightEdge = cam.transform.position.x + camHalfWidth;

        // Ensure tiles list is up-to-date
        if (_tiles.Count == 0) RefreshTilesList();

        // Repeatedly move any tile that is fully left of the view to the rightmost side and vice-versa
        // This loop handles fast camera movement by repeating until no tile needs moving.
        bool movedAny;
        int safety = 0;
        do
        {
            movedAny = false;
            if (_tiles.Count == 0) break;

            // find leftmost and rightmost tiles by X
            Transform leftmost = _tiles[0];
            Transform rightmost = _tiles[_tiles.Count - 1];

            // If the rightmost tile is left of the left edge (camera moved way to the left), move it to the left of leftmost
            if (rightmost.position.x + tileWidth * 0.5f < leftEdge)
            {
                rightmost.position = new Vector3(leftmost.position.x - tileWidth, rightmost.position.y, rightmost.position.z);
                RefreshTilesList();
                movedAny = true;
            }
            // If the leftmost tile is right of the right edge (camera moved way to the right), move it to the right of rightmost
            else if (leftmost.position.x - tileWidth * 0.5f > rightEdge)
            {
                leftmost.position = new Vector3(rightmost.position.x + tileWidth, leftmost.position.y, leftmost.position.z);
                RefreshTilesList();
                movedAny = true;
            }

            safety++;
        } while (movedAny && safety < 50);
    }

    void HandleVerticalTiling(Camera cam)
    {
        if (tileHeight <= 0) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float camHalfHeight = cam.orthographic ? cam.orthographicSize : 5f;
        float bottomEdge = cam.transform.position.y - camHalfHeight;
        float topEdge = cam.transform.position.y + camHalfHeight;

        if (_tiles.Count == 0) RefreshTilesList();

        bool movedAny;
        int safety = 0;
        do
        {
            movedAny = false;
            if (_tiles.Count == 0) break;

            // find bottommost and topmost tiles by Y
            Transform bottommost = _tiles[0];
            Transform topmost = _tiles[_tiles.Count - 1];

            // sort by Y for vertical checks
            _tiles.Sort((a, b) => a.position.y.CompareTo(b.position.y));
            bottommost = _tiles[0];
            topmost = _tiles[_tiles.Count - 1];

            if (topmost.position.y + tileHeight * 0.5f < bottomEdge)
            {
                topmost.position = new Vector3(topmost.position.x, bottommost.position.y - tileHeight, topmost.position.z);
                RefreshTilesList();
                movedAny = true;
            }
            else if (bottommost.position.y - tileHeight * 0.5f > topEdge)
            {
                bottommost.position = new Vector3(bottommost.position.x, topmost.position.y + tileHeight, bottommost.position.z);
                RefreshTilesList();
                movedAny = true;
            }

            safety++;
        } while (movedAny && safety < 50);
    }
}
