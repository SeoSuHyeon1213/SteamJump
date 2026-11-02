using UnityEngine;

// Reuses platforms, then places independent coin rows and probability-based potions.
public class PlatformSpawner : MonoBehaviour {
    public GameObject platformPrefab;
    public int count = 3;
    public float timeBetSpawnMin = 1.25f;
    public float timeBetSpawnMax = 2.25f;
    public float yMin = -3.5f;
    public float yMax = 1.5f;

    [SerializeField] private CoinSpawner coinSpawner;
    [SerializeField] private PotionSpawner potionSpawner;
    [Tooltip("Optional starting platform collider. Uses StartPlatform when unassigned.")]
    [SerializeField] private Collider2D startingPlatform;

    private float timeBetSpawn;
    private float xPos = 20f;
    private GameObject[] platforms;
    private Collider2D[] platformSurfaces;
    private int currentIndex;
    private readonly Vector2 poolPosition = new Vector2(0f, -25f);
    private float lastSpawnTime;
    private Collider2D previousPlatform;

    private void Start() {
        if(platformPrefab == null) {
            Debug.LogError("PlatformSpawner needs a platform prefab.", this);
            enabled = false;
            return;
        }

        if(coinSpawner == null) {
            coinSpawner = GetComponent<CoinSpawner>();
            if(coinSpawner == null) {
                coinSpawner = gameObject.AddComponent<CoinSpawner>();
            }
        }
        coinSpawner.Initialize(platformPrefab.GetComponent<Platform>());
        if(potionSpawner == null) {
            potionSpawner = GetComponent<PotionSpawner>();
            if(potionSpawner == null) {
                potionSpawner = gameObject.AddComponent<PotionSpawner>();
            }
        }
        potionSpawner.Initialize(platformPrefab.GetComponent<Platform>());

        int poolSize = Mathf.Max(2, count);
        platforms = new GameObject[poolSize];
        platformSurfaces = new Collider2D[poolSize];
        for(int i = 0; i < poolSize; i++) {
            platforms[i] = Instantiate(platformPrefab, poolPosition, Quaternion.identity);
            platforms[i].SetActive(false);
            platformSurfaces[i] = platforms[i].GetComponent<Collider2D>();
        }

        if(startingPlatform == null) {
            GameObject start = GameObject.Find("StartPlatform");
            if(start != null) {
                startingPlatform = start.GetComponent<Collider2D>();
            }
        }
        previousPlatform = startingPlatform;
        coinSpawner.SpawnOnPlatform(startingPlatform);
        potionSpawner.SpawnOnPlatform(startingPlatform);
        lastSpawnTime = Time.time;
        timeBetSpawn = 0f;
    }

    private void Update() {
        if(GameManager.instance == null || GameManager.instance.isGameover
            || Time.time < lastSpawnTime + timeBetSpawn) {
            return;
        }

        lastSpawnTime = Time.time;
        timeBetSpawn = Random.Range(timeBetSpawnMin, timeBetSpawnMax);
        float yPos = Random.Range(yMin, yMax);
        GameObject platform = platforms[currentIndex];

        coinSpawner.ReleaseForPlatform(platform.transform);
        potionSpawner.ReleaseForPlatform(platform.transform);
        platform.SetActive(false);
        platform.transform.position = new Vector2(xPos, yPos);
        platform.SetActive(true);

        // Bounds must describe the newly placed platform, not its old pool position.
        Physics2D.SyncTransforms();
        Collider2D surface = platformSurfaces[currentIndex];
        coinSpawner.SpawnBetween(previousPlatform, surface);
        potionSpawner.SpawnBetween(previousPlatform, surface);
        coinSpawner.SpawnOnPlatform(surface);
        potionSpawner.SpawnOnPlatform(surface);
        previousPlatform = surface;
        currentIndex = (currentIndex + 1) % platforms.Length;
    }
}
