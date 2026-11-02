using UnityEngine;

// 게임 오브젝트를 계속 왼쪽으로 움직이는 스크립트
public class ScrollingObject : MonoBehaviour {
    public float speed = 10f; // 이동 속도
    public float countSpeed = 5f; //특정 시간 이상 플레이 하면 빨라짐
    public float timeCount = 20f;

    private void Update() {
        // 게임 오브젝트를 왼쪽으로 일정 속도로 평행 이동하는 처리
        // 프레임마다 이동하는 거리 계산하여 이동
        //transform.Translate(Vector3.left * speed * Time.deltaTime); 

        if (!GameManager.instance.isGameover)
        {
            transform.Translate(Vector3.left * speed * Time.deltaTime); // 프레임마다 이동하는 거리 계산하여 이동
        }
        timeCount -= Time.deltaTime;
        if (timeCount <= 0)
        {
            speed += countSpeed;
            timeCount = 20f;
        }
    }
}