using System.Collections.Generic;
using UnityEngine;

// PlayerController는 플레이어 캐릭터로서 Player 게임 오브젝트를 제어한다.
public class PlayerController : MonoBehaviour {
   public AudioClip deathClip; // 사망시 재생할 오디오 클립
   public AudioClip damageClip; // 피격시 재생할 오디오 클립
   public AudioClip jumpClip; // 점프시 재생할 오디오 클립
   public float jumpForce = 700f; // 점프 힘

   [Header("Health")]
   [SerializeField, Min(1)] private int maxHealth = 3;
   [SerializeField, Min(0f)] private float damageInvulnerabilityTime = 1f;
   [SerializeField, Min(0f)] private float startInvulnerabilityTime = 2f;
   [Tooltip("Seconds per white/original phase while invulnerable.")]
   [SerializeField, Min(0.02f)] private float invulnerabilityFlashInterval = 0.15f;
   [Tooltip("Always lethal, including during invulnerability. Uses the existing Deadzone if unassigned.")]
   [SerializeField] private Collider2D instantDeathZone;

   [Header("Heart HUD")]
   [SerializeField] private Canvas healthCanvas;
   [SerializeField] private Sprite fullHeartSprite;
   [SerializeField] private Sprite emptyHeartSprite;
   [SerializeField] private Vector2 heartHudOffset = new Vector2(24f, -24f);
   [SerializeField, Min(1f)] private float heartSize = 36f;
   [SerializeField, Min(0f)] private float heartSpacing = 8f;

   public bool IsInvulnerable => !isDead && Time.time < nextDamageTime;
   public bool CanCollectItems => !isDead && isActiveAndEnabled
       && (GameManager.instance == null || !GameManager.instance.isGameover);
   public int CurrentHealth => currentHealth;
   public int MaxHealth => Mathf.Max(1, maxHealth);

   private int currentHealth;
   private float nextDamageTime = float.NegativeInfinity;
   private float invulnerabilityStartedAt;
   private SpriteRenderer[] playerSprites;
   private Material[] originalSpriteMaterials;
   private Material whiteFlashMaterial;
   private bool whiteFlashActive;
   private GameObject heartHud;
   private UnityEngine.UI.Image[] heartImages;
   [Header("Horizontal recovery")]
   [SerializeField, Min(0.01f)] private float horizontalReturnTime = 0.3f;
   [SerializeField, Min(0.01f)] private float maxHorizontalReturnSpeed = 5f;

   private float initialX;
   private readonly List<ContactPoint2D> recoveryContacts = new List<ContactPoint2D>(16);

   private int jumpCount = 0; // 누적 점프 횟수
   private bool isGrounded = false; // 바닥에 닿았는지 나타냄
   private bool isDead = false; // 사망 상태

   private Rigidbody2D playerRigidbody; // 사용할 리지드바디 컴포넌트
   private Animator animator; // 사용할 애니메이터 컴포넌트
   private AudioSource playerAudio; // 사용할 오디오 소스 컴포넌트

   private void Start() {
       // 초기화
        playerRigidbody = GetComponent<Rigidbody2D>();
        initialX = playerRigidbody.position.x;
        animator = GetComponent<Animator>();
        playerAudio = GetComponent<AudioSource>();
        currentHealth = MaxHealth;
        if(instantDeathZone == null) {
            // Preserve the existing scene's fall-death behavior without editing it.
            GameObject deadzone = GameObject.Find("Deadzone");
            if(deadzone != null) {
                instantDeathZone = deadzone.GetComponent<Collider2D>();
            }
        }
        CreateHeartHud();
        InitializeInvulnerabilityFlash();
        BeginInvulnerability(startInvulnerabilityTime);

    }

   private void Update() {
       // 사용자 입력을 감지하고 점프하는 처리
       if(isDead)
        {
            return;
        }
        if(Input.GetMouseButtonDown(0) && jumpCount < 2) // 마우스 왼쪽 버튼을 눌렀고, 누적 점프 횟수가 2회 미만일 때(점프 한 번만)
        {
            jumpCount++;
            playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, 0f); // 점프할 때 수직 속도만 초기화하고 수평 복귀 속도는 유지
            playerRigidbody.AddForce(new Vector2(0, jumpForce)); // 점프 힘을 가함
            playerAudio.clip = jumpClip;
            playerAudio.Play(); // 점프 사운드 재생
            //물리 법칙 작용을 하지 않고 점프를 일정하게 처리
        }
        else if(Input.GetMouseButtonUp(0) && playerRigidbody.linearVelocity.y > 0) //점프 중일 때 버튼을 떼면
        {
            playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, playerRigidbody.linearVelocity.y * 0.5f); // 버튼을 떼면 상승 속도만 절반으로 줄임
            //버튼을 누르는 시간에 따라 점프 높이가 달라지도록 하는 처리
        }
            
        animator.SetBool("Grounded", isGrounded);
    }

   private void FixedUpdate() {
       if(isDead) {
           return;
       }

       float distanceX = initialX - playerRigidbody.position.x;
       float directionX = Mathf.Sign(distanceX);
       bool blocked = false;

       // Check every current contact, including simultaneous floor/wall contacts.
       playerRigidbody.GetContacts(recoveryContacts);
       for(int i = 0; i < recoveryContacts.Count; i++) {
           Vector2 normal = recoveryContacts[i].normal;
           if(Mathf.Abs(normal.x) > 0.5f && normal.x * directionX < 0f) {
               blocked = true;
               break;
           }
       }

       Vector2 velocity = playerRigidbody.linearVelocity;
       // Exponential recovery slows near the target and cannot overshoot in one step.
       float returnFactor = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.01f, horizontalReturnTime));
       float returnSpeed = distanceX * returnFactor / Time.fixedDeltaTime;
       velocity.x = blocked ? 0f : Mathf.Clamp(returnSpeed,
           -Mathf.Max(0f, maxHorizontalReturnSpeed), Mathf.Max(0f, maxHorizontalReturnSpeed));
       playerRigidbody.linearVelocity = velocity;
   }

   private void Die() {
       if(isDead) {
           return;
       }
       SetWhiteFlash(false);
       currentHealth = 0;
       RefreshHeartHud();
       // 사망 처리
        animator.SetTrigger("Die"); // 사망 애니메이션 트리거
        playerAudio.clip = deathClip;
        playerAudio.Play(); // 사망 사운드 재생

        playerRigidbody.linearVelocity = Vector2.zero; // 사망 시 속도 초기화
        isDead = true; // 사망 상태로 설정

        GameManager.instance.OnPlayerDead(); // 게임 매니저의 사망 처리 메서드 호출

        Destroy(gameObject, 3f); // 3초 후에 게임 오브젝트 제거
    }

   /// <summary>Removes hearts once per invulnerability window.</summary>
   public void TakeDamage(int damage = 1) {
       if(isDead || damage <= 0 || IsInvulnerable) {
           return;
       }

       currentHealth = Mathf.Max(0, currentHealth - damage);
       RefreshHeartHud();
       playerAudio.clip = damageClip;
       playerAudio.Play(); // 피격 사운드 재생

       if(currentHealth == 0) {
           Die();
       }
       else {
           BeginInvulnerability(damageInvulnerabilityTime);
       }
   }
   // Future potion items can return this result from TryApplyEffect.
   // Full health, dead players and non-positive amounts do not consume a potion.
   public bool RestoreHealth(int amount) {
       if(!CanCollectItems || amount <= 0 || currentHealth >= MaxHealth) {
           return false;
       }
       currentHealth += Mathf.Min(amount, MaxHealth - currentHealth);
       RefreshHeartHud();
       return true;
   }

   public void PlayItemSound(AudioClip clip, float volume) {
       if(clip != null && playerAudio != null) {
           playerAudio.PlayOneShot(clip, Mathf.Clamp01(volume));
       }
   }

   private void InitializeInvulnerabilityFlash() {
       playerSprites = GetComponentsInChildren<SpriteRenderer>(true);
       originalSpriteMaterials = new Material[playerSprites.Length];
       for(int i = 0; i < playerSprites.Length; i++) {
           originalSpriteMaterials[i] = playerSprites[i].sharedMaterial;
       }

       // Resources guarantees the shader is included in the Web build.
       Shader shader = Resources.Load<Shader>("PlayerWhiteFlash");
       if(shader == null || !shader.isSupported) {
           Debug.LogWarning("PlayerWhiteFlash shader is unavailable; invulnerability still works.", this);
           return;
       }
       whiteFlashMaterial = new Material(shader) {
           name = "Player White Flash (Runtime)",
           hideFlags = HideFlags.DontSave
       };
   }

   private void BeginInvulnerability(float duration) {
       invulnerabilityStartedAt = Time.time;
       nextDamageTime = Time.time + Mathf.Max(0f, duration);
       UpdateInvulnerabilityFlash();
   }

   private void LateUpdate() {
       // Apply after animation has selected this frame's sprite.
       UpdateInvulnerabilityFlash();
   }

   private void UpdateInvulnerabilityFlash() {
       float interval = Mathf.Max(0.02f, invulnerabilityFlashInterval);
       bool white = IsInvulnerable
           && Mathf.FloorToInt((Time.time - invulnerabilityStartedAt) / interval) % 2 == 0;
       SetWhiteFlash(white);
   }

   private void SetWhiteFlash(bool white) {
       if(playerSprites == null || whiteFlashMaterial == null || white == whiteFlashActive) {
           return;
       }

       for(int i = 0; i < playerSprites.Length; i++) {
           if(playerSprites[i] != null) {
               playerSprites[i].sharedMaterial = white ? whiteFlashMaterial : originalSpriteMaterials[i];
           }
       }
       whiteFlashActive = white;
   }

   private void OnDisable() {
       SetWhiteFlash(false);
   }

   private void OnTriggerEnter2D(Collider2D other) {
       HandleHazardContact(other);
   }

   private void OnTriggerStay2D(Collider2D other) {
       // Remaining inside a hazard causes another hit after invulnerability ends.
       HandleHazardContact(other);
   }

   private void HandleHazardContact(Collider2D other) {
       if(isDead) {
           return;
       }

       ICollectibleItem item = other.GetComponentInParent<ICollectibleItem>();
       if(item != null) {
           item.TryCollect(this);
           return;
       }

       if(instantDeathZone != null && other.gameObject == instantDeathZone.gameObject) {
           Die();
       }
       else if(other.CompareTag("Dead")) {
           TakeDamage();
       }
   }

   private void CreateHeartHud() {
       if(healthCanvas == null && GameManager.instance != null
           && GameManager.instance.scoreText != null) {
           healthCanvas = GameManager.instance.scoreText.canvas;
       }
       if(healthCanvas == null) {
           healthCanvas = FindFirstObjectByType<Canvas>();
       }
       if(healthCanvas == null) {
           GameObject canvasObject = new GameObject("HealthCanvas", typeof(RectTransform),
               typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
               typeof(UnityEngine.UI.GraphicRaycaster));
           healthCanvas = canvasObject.GetComponent<Canvas>();
           healthCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
           UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
           scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
           scaler.referenceResolution = new Vector2(1280f, 720f);
           scaler.matchWidthOrHeight = 0.5f;
       }

       heartHud = new GameObject("HeartHUD", typeof(RectTransform),
           typeof(UnityEngine.UI.HorizontalLayoutGroup));
       RectTransform hudRect = heartHud.GetComponent<RectTransform>();
       hudRect.SetParent(healthCanvas.transform, false);
       hudRect.anchorMin = hudRect.anchorMax = new Vector2(0f, 1f);
       hudRect.pivot = new Vector2(0f, 1f);
       hudRect.anchoredPosition = heartHudOffset;
       float size = Mathf.Max(1f, heartSize);
       float spacing = Mathf.Max(0f, heartSpacing);
       hudRect.sizeDelta = new Vector2(MaxHealth * size + (MaxHealth - 1) * spacing, size);

       UnityEngine.UI.HorizontalLayoutGroup layout =
           heartHud.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
       layout.spacing = spacing;
       layout.childAlignment = TextAnchor.MiddleLeft;
       layout.childControlWidth = layout.childControlHeight = false;
       layout.childForceExpandWidth = layout.childForceExpandHeight = false;

       heartImages = new UnityEngine.UI.Image[MaxHealth];
       for(int i = 0; i < heartImages.Length; i++) {
           GameObject heart = new GameObject("Heart" + (i + 1), typeof(RectTransform),
               typeof(UnityEngine.UI.Image));
           RectTransform rect = heart.GetComponent<RectTransform>();
           rect.SetParent(hudRect, false);
           rect.sizeDelta = new Vector2(size, size);
           heartImages[i] = heart.GetComponent<UnityEngine.UI.Image>();
           heartImages[i].raycastTarget = false;
           heartImages[i].preserveAspect = true;
       }
       RefreshHeartHud();
   }

   private void RefreshHeartHud() {
       if(heartImages == null) {
           return;
       }

       for(int i = 0; i < heartImages.Length; i++) {
           bool full = i < currentHealth;
           UnityEngine.UI.Image image = heartImages[i];
           if(image == null) {
               continue;
           }
           image.sprite = full ? fullHeartSprite : (emptyHeartSprite != null ? emptyHeartSprite : fullHeartSprite);
           // Sprites retain their original colors; missing images use colored placeholders.
           image.color = full
               ? (fullHeartSprite != null ? Color.white : new Color(0.95f, 0.25f, 0.3f, 1f))
               : (emptyHeartSprite != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.45f));
       }
   }

   private void OnDestroy() {
       SetWhiteFlash(false);
       if(whiteFlashMaterial != null) {
           Destroy(whiteFlashMaterial);
       }
       if(heartHud != null) {
           Destroy(heartHud);
       }
   }

   private void OnCollisionEnter2D(Collision2D collision) {
       // 바닥에 닿았음을 감지하는 처리
       if(collision.contacts[0].normal.y > 0.7f) // 충돌한 표면이 거의 수평일 때(바닥으로 간주)
       {
           isGrounded = true; // 바닥에 닿았음
           jumpCount = 0; // 누적 점프 횟수 초기화
       }
    }

   private void OnCollisionExit2D(Collision2D collision) {
       // 바닥에서 벗어났음을 감지하는 처리
       isGrounded = false; // 바닥에서 벗어났음
   }
}