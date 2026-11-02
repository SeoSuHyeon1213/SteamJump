using UnityEngine;

// 발판으로서 필요한 동작을 담은 스크립트
public class Platform : MonoBehaviour {
    public GameObject[] obstacles; // 장애물 오브젝트들

    ///public AudioSource playerAudio; //코인 오디오
    
    public GameObject[] coins; // 코인 오브젝트들
    public GameObject[] gearPrefabs; // 플랫폼에 붙일 기어 프리팹들
    public Vector2[] gearLocalPositions = {
        new Vector2(-2.5f, 0.45f),
        new Vector2(0f, 0.45f),
        new Vector2(2.5f, 0.45f)
    };
    
    public int gearSpawnChance = 2; // 숫자가 낮을수록 자주 등장함. 2 = 50%

    private bool stepped = false; // 플레이어 캐릭터가 밟았었는가
    private GameObject[] gears; // 생성된 기어 오브젝트들

    // 게임 오브젝트가 생성될 때 한 번만 실행되는 메서드
    private void Awake() {
        CreateGears();
    }        

    // 컴포넌트가 활성화될때 마다 매번 실행되는 메서드
    private void OnEnable() { //발판이 활성화될 때마다 발판을 리셋하는 처리(초기화 할 때마다 실행)
        // 발판을 리셋하는 처리
        stepped = false;

        for(int i =0; i < obstacles.Length; i++){
            if(Random.Range(0, 3) == 0){ // 33% 확률로 장애물을 활성화
                obstacles[i].SetActive(true);
            }
            else
            {
                obstacles[i].SetActive(false);
            }
        }

        DisableLegacyCoins();
        ResetGears();
        ///playerAudio = GetComponent<AudioSource>();

    }

    private void DisableLegacyCoins() {
        // CoinSpawner uses these objects as visual templates for independent airborne coins.
        if(coins == null) {
            return;
        }
        foreach(GameObject coin in coins) {
            if(coin != null) {
                coin.SetActive(false);
            }
        }
    }
    private void CreateGears(){
        if(gearPrefabs == null || gearPrefabs.Length == 0){
            return;
        }

        gears = new GameObject[gearPrefabs.Length];

        for(int i = 0; i < gearPrefabs.Length; i++){
            if(gearPrefabs[i] == null){
                continue;
            }

            gears[i] = Instantiate(gearPrefabs[i], transform);
            gears[i].transform.localPosition = GetGearLocalPosition(i);
            gears[i].transform.localRotation = Quaternion.identity;
            gears[i].SetActive(false);

            SpriteRenderer spriteRenderer = gears[i].GetComponent<SpriteRenderer>();
            if(spriteRenderer != null){
                spriteRenderer.sortingOrder = 1;
            }
        }
    }

    private Vector2 GetGearLocalPosition(int index){
        if(gearLocalPositions != null && gearLocalPositions.Length > 0){
            return gearLocalPositions[index % gearLocalPositions.Length];
        }

        return Vector2.zero;
    }

    private void ResetGears(){
        if(gears == null || gears.Length == 0){
            return;
        }

        for(int i = 0; i < gears.Length; i++){
            if(gears[i] != null){
                gears[i].SetActive(false);
            }
        }

        if(gearSpawnChance <= 0 || Random.Range(0, gearSpawnChance) != 0){
            return;
        }

        int gearIndex = Random.Range(0, gears.Length);
        if(gears[gearIndex] != null){
            int positionIndex = 0;
            if(gearLocalPositions != null && gearLocalPositions.Length > 0){
                positionIndex = Random.Range(0, gearLocalPositions.Length);
            }

            gears[gearIndex].transform.localPosition = GetGearLocalPosition(positionIndex);
            gears[gearIndex].SetActive(true);
        }
    }

    void OnCollisionEnter2D(Collision2D collision) {
        // 플레이어 캐릭터가 자신을 밟았을때 점수를 추가하는 처리
        if(collision.collider.tag == "Player")
        {
            if(!stepped)
            {
                stepped = true;
                GameManager.instance.AddScore(1); // 점수 1점 추가
            }
        }
    }

}
