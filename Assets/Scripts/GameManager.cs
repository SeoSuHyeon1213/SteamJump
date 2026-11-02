using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

// 게임 오버 상태를 표현하고, 게임 점수와 UI를 관리하는 게임 매니저
// 씬에는 단 하나의 게임 매니저만 존재할 수 있다.
public class GameManager : MonoBehaviour {
    public static GameManager instance; // 싱글톤을 할당할 전역 변수

    public bool isGameover = false; // 게임 오버 상태
    public TextMeshProUGUI scoreText; // 점수를 출력할 UI 텍스트
    public GameObject gameoverUI; // 게임 오버시 활성화 할 UI 게임 오브젝트

    //public GameObject exit; // 나가기 버튼

    public AudioSource bgmSound; // 배경 음악 사운드
    public AudioClip deadSound; // 사망 사운드
    

    private int score = 0; // 게임 점수
   
    // 게임 시작과 동시에 싱글톤을 구성
    void Awake() {
        // 싱글톤 변수 instance가 비어있는가?
        if (instance == null)
        {
            // instance가 비어있다면(null) 그곳에 자기 자신을 할당
            instance = this;
        }
        else
        {
            // instance에 이미 다른 GameManager 오브젝트가 할당되어 있는 경우

            // 씬에 두개 이상의 GameManager 오브젝트가 존재한다는 의미.
            // 싱글톤 오브젝트는 하나만 존재해야 하므로 자신의 게임 오브젝트를 파괴
            Debug.LogWarning("씬에 두개 이상의 게임 매니저가 존재합니다!");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 게임 시작시 배경 음악 재생
        bgmSound.Play();
       
    }

    void Update() {
       
        // if (isGameover && Input.GetMouseButtonDown(0))
        // {
          

        //     SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        // }
    }

    // 나가기 버튼의 OnClick()에 연결할 메서드
  

    // 점수를 증가시키는 메서드
    public void AddScore(int newScore) {
        if (!isGameover)
        {
            score += newScore; // 점수 증가
            scoreText.text = "Score: " + score; // 증가된 점수를 UI에 표시
        }
    }

    // 플레이어 캐릭터가 사망시 게임 오버를 실행하는 메서드
    public void OnPlayerDead() {
        bgmSound.clip = deadSound; // 사망 사운드 재생
        bgmSound.Play(); // 사망 사운드 재생

        isGameover = true; // 게임 오버 상태로 설정
        gameoverUI.SetActive(true); // 게임 오버 UI 활성화
    }
}