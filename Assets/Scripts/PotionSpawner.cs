using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PotionSpawner : MonoBehaviour {
    [Header("Potion")]
    [Tooltip("Optional prefab. Without one, a temporary pixel bottle is created.")]
    [SerializeField] private HealthPotionItem potionPrefab;
    [SerializeField] private Sprite potionSprite;
    [SerializeField] private AudioClip collectionSound;
    [SerializeField, Min(1)] private int healAmount = 1;

    [Header("Probability and placement")]
    [Tooltip("Independent spawn probability for each possible potion in a gap. 0.2 = 20%.")]
    [SerializeField, Range(0f, 1f)] private float spawnChance = 0.2f;
    [SerializeField, Min(1)] private int maxPotionsPerGap = 1;
    [SerializeField, Min(0f)] private float heightAbovePlatform = 2f;
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;
    [SerializeField, Min(1)] private int maxPooledPotions = 12;
    [SerializeField] private float despawnX = -25f;

    [Header("On-platform spawning")]
    [SerializeField, Range(0f, 1f)] private float platformSpawnChance = 0.2f;
    [SerializeField, Min(0)] private int maxPotionsPerPlatform = 1;
    [SerializeField, Min(0f)] private float platformItemHeight = 0.9f;
    [SerializeField, Min(0f)] private float platformEdgeInset = 0.4f;
    [Tooltip("Reserved radius around each candidate; increase for larger item sprites.")]
    [SerializeField, Min(0.05f)] private float platformClearance = 0.6f;

    private sealed class PooledPotion {
        public HealthPotionItem item;
        public Transform anchor;
        public Vector3 lastAnchorPosition;
    }

    private readonly List<PooledPotion> pool = new List<PooledPotion>();
    private Transform poolRoot;
    private Material defaultMaterial;
    private int defaultSortingLayer;
    private int defaultSortingOrder;
    private Sprite fallbackSprite;
    private Texture2D fallbackTexture;

    public void Initialize(Platform platformTemplate) {
        if(poolRoot != null) {
            return;
        }
        GameObject root = new GameObject("AirbornePotions");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
        SpriteRenderer platformSprite = platformTemplate != null
            ? platformTemplate.GetComponent<SpriteRenderer>() : null;
        if(platformSprite != null) {
            defaultMaterial = platformSprite.sharedMaterial;
            defaultSortingLayer = platformSprite.sortingLayerID;
            defaultSortingOrder = platformSprite.sortingOrder + 2;
        }
    }

    public void SpawnBetween(Collider2D previous, Collider2D next) {
        if(!isActiveAndEnabled || poolRoot == null || previous == null || next == null
            || previous == next) {
            return;
        }
        Bounds left = previous.bounds;
        Bounds right = next.bounds;
        if(right.min.x <= left.max.x) {
            return;
        }

        Vector2 start = new Vector2(left.max.x, left.max.y + heightAbovePlatform);
        Vector2 end = new Vector2(right.min.x, right.max.y + heightAbovePlatform);
        int slots = Mathf.Max(1, maxPotionsPerGap);
        float probability = Mathf.Clamp01(spawnChance);
        for(int i = 0; i < slots; i++) {
            if(probability <= 0f || (probability < 1f && Random.value >= probability)) {
                continue;
            }
            PooledPotion potion = RentPotion();
            if(potion == null) {
                break;
            }
            // Separate candidate positions when more than one slot is configured.
            float t = Mathf.Lerp(0.2f, 0.8f, (i + Random.value) / slots);
            Vector2 position = Vector2.Lerp(start, end, t);
            position.y += 4f * t * (1f - t) * Mathf.Max(0f, arcHeight);
            potion.anchor = next.transform;
            potion.lastAnchorPosition = next.transform.position;
            potion.item.transform.position = new Vector3(position.x, position.y, next.transform.position.z);
            potion.item.ConfigureHealAmount(healAmount);
            if(collectionSound != null) {
                potion.item.ConfigureCollectionSound(collectionSound, 1f);
            }
            potion.item.gameObject.SetActive(true);
        }
    }

    public void SpawnOnPlatform(Collider2D surface) {
        if(!isActiveAndEnabled || poolRoot == null || surface == null) {
            return;
        }
        float probability = Mathf.Clamp01(platformSpawnChance);
        for(int i = 0; i < Mathf.Max(0, maxPotionsPerPlatform); i++) {
            if(probability <= 0f || (probability < 1f && Random.value >= probability)) {
                continue;
            }
            if(!ItemSpawnPlacement.TryFindOnPlatform(surface, platformItemHeight,
                platformEdgeInset, platformClearance, out Vector3 position)) {
                continue;
            }
            PooledPotion potion = RentPotion();
            if(potion == null) {
                break;
            }
            potion.anchor = surface.transform;
            potion.lastAnchorPosition = surface.transform.position;
            potion.item.transform.position = position;
            potion.item.ConfigureHealAmount(healAmount);
            if(collectionSound != null) {
                potion.item.ConfigureCollectionSound(collectionSound, 1f);
            }
            potion.item.gameObject.SetActive(true);
        }
    }

    public void ReleaseForPlatform(Transform platform) {
        foreach(PooledPotion potion in pool) {
            if(potion.anchor == platform) {
                ReturnPotion(potion);
            }
        }
    }

    private PooledPotion RentPotion() {
        foreach(PooledPotion potion in pool) {
            if(potion.item != null && !potion.item.gameObject.activeSelf) {
                return potion;
            }
        }
        if(pool.Count >= Mathf.Max(1, maxPooledPotions)) {
            return null;
        }

        HealthPotionItem item;
        if(potionPrefab != null) {
            item = Instantiate(potionPrefab, poolRoot);
            item.gameObject.SetActive(false);
        }
        else {
            GameObject instance = new GameObject("Health Potion (Pooled)");
            instance.SetActive(false);
            instance.transform.SetParent(poolRoot, false);
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = potionSprite != null ? potionSprite : GetFallbackSprite();
            if(defaultMaterial != null) {
                renderer.sharedMaterial = defaultMaterial;
            }
            renderer.sortingLayerID = defaultSortingLayer;
            renderer.sortingOrder = defaultSortingOrder;
            CircleCollider2D collider = instance.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.4f;
            item = instance.AddComponent<HealthPotionItem>();
        }

        SpriteRenderer spriteRenderer = item.GetComponentInChildren<SpriteRenderer>(true);
        if(potionSprite != null && spriteRenderer != null) {
            spriteRenderer.sprite = potionSprite;
        }
        foreach(Collider2D collider in item.GetComponentsInChildren<Collider2D>(true)) {
            collider.isTrigger = true;
        }
        PooledPotion created = new PooledPotion { item = item };
        pool.Add(created);
        return created;
    }

    private void LateUpdate() {
        if(GameManager.instance != null && GameManager.instance.isGameover) {
            return;
        }
        foreach(PooledPotion potion in pool) {
            if(potion.item == null) {
                continue;
            }
            if(!potion.item.gameObject.activeSelf) {
                potion.anchor = null;
                continue;
            }
            if(potion.anchor == null || !potion.anchor.gameObject.activeInHierarchy) {
                ReturnPotion(potion);
                continue;
            }
            Vector3 position = potion.anchor.position;
            potion.item.transform.position += position - potion.lastAnchorPosition;
            potion.lastAnchorPosition = position;
            if(potion.item.transform.position.x < despawnX) {
                ReturnPotion(potion);
            }
        }
    }

    private static void ReturnPotion(PooledPotion potion) {
        if(potion.item != null) {
            potion.item.gameObject.SetActive(false);
        }
        potion.anchor = null;
    }

    private void OnDisable() {
        foreach(PooledPotion potion in pool) {
            ReturnPotion(potion);
        }
    }

    // Small programmer-art placeholder; replace via Potion Sprite or Potion Prefab.
    private Sprite GetFallbackSprite() {
        if(fallbackSprite != null) {
            return fallbackSprite;
        }
        string[] pixels = {
            "................", ".....oooooo.....", ".....occcco.....",
            ".....oooooo.....", "......oggo......", "......oggo......",
            ".....oggggo.....", "....ogssgggo....", "...ogssssgggo...",
            "...ogrrrrrggo...", "...ogrsrrrggo...", "...ogrsrrrggo...",
            "...ogrrrrrggo...", "....oggggggo....", ".....oooooo.....",
            "................"
        };
        Color[] colors = new Color[16 * 16];
        for(int y = 0; y < 16; y++) {
            for(int x = 0; x < 16; x++) {
                char pixel = pixels[15 - y][x];
                colors[y * 16 + x] = pixel == 'o' ? new Color(0.2f, 0.16f, 0.25f)
                    : pixel == 'c' ? new Color(0.7f, 0.42f, 0.2f)
                    : pixel == 'g' ? new Color(0.7f, 0.9f, 0.95f)
                    : pixel == 's' ? Color.white
                    : pixel == 'r' ? new Color(0.95f, 0.2f, 0.35f) : Color.clear;
            }
        }
        fallbackTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false) {
            name = "Temporary Potion Icon", filterMode = FilterMode.Point
        };
        fallbackTexture.SetPixels(colors);
        fallbackTexture.Apply(false, false);
        fallbackSprite = Sprite.Create(fallbackTexture, new Rect(0, 0, 16, 16),
            new Vector2(0.5f, 0.5f), 16f);
        return fallbackSprite;
    }

    private void OnDestroy() {
        if(fallbackSprite != null) {
            Destroy(fallbackSprite);
        }
        if(fallbackTexture != null) {
            Destroy(fallbackTexture);
        }
    }
}
