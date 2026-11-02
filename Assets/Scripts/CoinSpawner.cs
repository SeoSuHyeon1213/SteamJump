using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CoinSpawner : MonoBehaviour {
    [Tooltip("Optional standalone coin prefabs. If empty, use the existing platform's coins.")]
    [SerializeField] private CoinItem[] coinPrefabs;
    [SerializeField, Min(0.1f)] private float coinSpacing = 1.5f;
    [SerializeField, Min(1)] private int maxCoinsPerRow = 10;
    [SerializeField, Min(0f)] private float heightAbovePlatform = 1.25f;
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;
    [SerializeField, Range(0f, 1f)] private float rowSpawnChance = 1f;
    [SerializeField, Min(1)] private int maxPooledCoins = 90;
    [Tooltip("Return coins to the pool once they pass this world X coordinate.")]
    [SerializeField] private float despawnX = -25f;

    [Header("On-platform spawning")]
    [SerializeField, Range(0f, 1f)] private float platformSpawnChance = 0.7f;
    [SerializeField, Min(0)] private int maxCoinsPerPlatform = 3;
    [SerializeField, Min(0f)] private float platformItemHeight = 0.9f;
    [SerializeField, Min(0f)] private float platformEdgeInset = 0.4f;
    [Tooltip("Reserved radius around each candidate; increase for larger item sprites.")]
    [SerializeField, Min(0.05f)] private float platformClearance = 0.6f;

    private sealed class Template {
        public GameObject source;
        public int legacyScore;
        public AudioClip sound;
        public float volume;
        public Vector3 scale;
    }

    private sealed class PooledCoin {
        public CoinItem item;
        public int templateIndex;
        public Transform anchor;
        public Vector3 lastAnchorPosition;
    }

    private readonly List<Template> templates = new List<Template>();
    private readonly List<PooledCoin> pool = new List<PooledCoin>();
    private Transform poolRoot;
    private bool initialized;

    public void Initialize(Platform platformTemplate) {
        if(initialized) {
            return;
        }
        initialized = true;
        GameObject root = new GameObject("AirborneCoins");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;

        if(coinPrefabs != null) {
            foreach(CoinItem prefab in coinPrefabs) {
                if(prefab != null) {
                    templates.Add(new Template {
                        source = prefab.gameObject,
                        scale = prefab.transform.localScale
                    });
                }
            }
        }

        // Migration: platform child coins remain asset templates; they no longer spawn there.
        if(templates.Count == 0 && platformTemplate != null && platformTemplate.coins != null) {
            AudioSource audio = platformTemplate.GetComponent<AudioSource>();
            for(int i = 0; i < platformTemplate.coins.Length; i++) {
                GameObject source = platformTemplate.coins[i];
                if(source == null) {
                    continue;
                }
                templates.Add(new Template {
                    source = source,
                    legacyScore = i == 1 ? 3 : (i == 2 ? 2 : 1),
                    sound = audio != null ? audio.clip : null,
                    volume = audio != null ? audio.volume : 1f,
                    scale = source.transform.lossyScale
                });
            }
        }
        if(templates.Count == 0) {
            Debug.LogWarning("CoinSpawner needs coin prefabs or platform coin templates.", this);
        }
    }

    public void SpawnBetween(Collider2D previous, Collider2D next) {
        if(!isActiveAndEnabled || previous == null || next == null || previous == next
            || templates.Count == 0 || Random.value >= rowSpawnChance) {
            return;
        }

        Bounds left = previous.bounds;
        Bounds right = next.bounds;
        float gap = right.min.x - left.max.x;
        // There is no airborne gap if the physical platforms overlap.
        if(gap <= 0f) {
            return;
        }

        Vector2 start = new Vector2(left.max.x, left.max.y + heightAbovePlatform);
        Vector2 end = new Vector2(right.min.x, right.max.y + heightAbovePlatform);
        float length = Vector2.Distance(start, end);
        int count = Mathf.Clamp(Mathf.FloorToInt(length / Mathf.Max(0.1f, coinSpacing)),
            1, Mathf.Max(1, maxCoinsPerRow));

        for(int i = 0; i < count; i++) {
            // Leave the exact edges free, so coins do not sit in the platform corners.
            float t = (i + 1f) / (count + 1f);
            Vector2 position = Vector2.Lerp(start, end, t);
            position.y += 4f * t * (1f - t) * Mathf.Max(0f, arcHeight);
            PooledCoin coin = RentCoin();
            if(coin == null) {
                break;
            }
            coin.anchor = next.transform;
            coin.lastAnchorPosition = next.transform.position;
            coin.item.transform.position = new Vector3(position.x, position.y, next.transform.position.z);
            coin.item.gameObject.SetActive(true);
        }
    }

    public void SpawnOnPlatform(Collider2D surface) {
        if(!isActiveAndEnabled || surface == null || templates.Count == 0) {
            return;
        }
        float probability = Mathf.Clamp01(platformSpawnChance);
        for(int i = 0; i < Mathf.Max(0, maxCoinsPerPlatform); i++) {
            if(probability <= 0f || (probability < 1f && Random.value >= probability)) {
                continue;
            }
            if(!ItemSpawnPlacement.TryFindOnPlatform(surface, platformItemHeight,
                platformEdgeInset, platformClearance, out Vector3 position)) {
                continue;
            }
            PooledCoin coin = RentCoin();
            if(coin == null) {
                break;
            }
            coin.anchor = surface.transform;
            coin.lastAnchorPosition = surface.transform.position;
            coin.item.transform.position = position;
            coin.item.gameObject.SetActive(true);
        }
    }

    // Call BEFORE moving a recycled platform, so coins cannot teleport with it.
    public void ReleaseForPlatform(Transform platform) {
        foreach(PooledCoin coin in pool) {
            if(coin.anchor == platform) {
                ReturnCoin(coin);
            }
        }
    }

    private PooledCoin RentCoin() {
        int templateIndex = Random.Range(0, templates.Count);
        foreach(PooledCoin candidate in pool) {
            if(candidate.templateIndex == templateIndex && !candidate.item.gameObject.activeSelf) {
                return candidate;
            }
        }
        if(pool.Count >= Mathf.Max(1, maxPooledCoins)) {
            return null;
        }

        Template template = templates[templateIndex];
        GameObject instance = Instantiate(template.source, poolRoot);
        instance.SetActive(false);
        instance.name = template.source.name + " (Pooled)";
        // Preserve the child template's world size, without inheriting platform movement.
        Vector3 parentScale = poolRoot.lossyScale;
        instance.transform.localScale = new Vector3(
            template.scale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            template.scale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            template.scale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        CoinItem item = instance.GetComponent<CoinItem>();
        if(item == null) {
            item = instance.AddComponent<CoinItem>();
        }
        if(template.legacyScore > 0) {
            item.ConfigureScore(template.legacyScore);
            item.ConfigureCollectionSound(template.sound, template.volume);
        }
        foreach(Collider2D collider in instance.GetComponentsInChildren<Collider2D>(true)) {
            collider.isTrigger = true;
        }

        PooledCoin created = new PooledCoin { item = item, templateIndex = templateIndex };
        pool.Add(created);
        return created;
    }

    private void LateUpdate() {
        if(GameManager.instance != null && GameManager.instance.isGameover) {
            return;
        }
        foreach(PooledCoin coin in pool) {
            if(!coin.item.gameObject.activeSelf) {
                coin.anchor = null;
                continue;
            }
            if(coin.anchor == null || !coin.anchor.gameObject.activeInHierarchy) {
                ReturnCoin(coin);
                continue;
            }
            // Follow actual platform displacement, including its speed increases.
            Vector3 anchorPosition = coin.anchor.position;
            coin.item.transform.position += anchorPosition - coin.lastAnchorPosition;
            coin.lastAnchorPosition = anchorPosition;
            if(coin.item.transform.position.x < despawnX) {
                ReturnCoin(coin);
            }
        }
    }

    private static void ReturnCoin(PooledCoin coin) {
        coin.item.gameObject.SetActive(false);
        coin.anchor = null;
    }

    private void OnDisable() {
        foreach(PooledCoin coin in pool) {
            ReturnCoin(coin);
        }
    }
}
