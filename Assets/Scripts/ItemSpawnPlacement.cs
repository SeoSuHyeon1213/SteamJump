using UnityEngine;

/// <summary>Chooses an unoccupied position above the visible part of a platform.</summary>
public static class ItemSpawnPlacement {
    private static readonly Collider2D[] overlaps = new Collider2D[32];

    public static bool TryFindOnPlatform(Collider2D surface, float height,
        float edgeInset, float clearance, out Vector3 position) {
        position = Vector3.zero;
        if(surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy) {
            return false;
        }

        // Include just-spawned/reused items and moved obstacles in overlap queries.
        Physics2D.SyncTransforms();
        Bounds bounds = surface.bounds;
        float left = bounds.min.x;
        float right = bounds.max.x;
        SpriteRenderer sprite = surface.GetComponent<SpriteRenderer>();
        if(sprite != null && sprite.enabled && sprite.sprite != null) {
            // Extended landing colliders must not place items over invisible edges.
            left = Mathf.Max(left, sprite.bounds.min.x);
            right = Mathf.Min(right, sprite.bounds.max.x);
        }

        float radius = Mathf.Max(0.05f, clearance);
        float margin = Mathf.Max(0f, edgeInset) + radius;
        left += margin;
        right -= margin;
        if(left > right) {
            return false;
        }

        float y = bounds.max.y + Mathf.Max(height, radius + 0.05f);
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        const int maxAttempts = 20;
        for(int attempt = 0; attempt < maxAttempts; attempt++) {
            Vector2 candidate = new Vector2(Random.Range(left, right), y);
            int count = Physics2D.OverlapCircle(candidate, radius, filter, overlaps);
            bool occupied = count == overlaps.Length;
            for(int i = 0; i < count && !occupied; i++) {
                // This is the supporting surface; every other collider blocks placement.
                if(overlaps[i] != null && overlaps[i] != surface) {
                    occupied = true;
                }
            }
            if(!occupied) {
                position = new Vector3(candidate.x, candidate.y, surface.transform.position.z);
                return true;
            }
        }
        // Never force a placement into an obstacle when no clear position was found.
        return false;
    }
}
