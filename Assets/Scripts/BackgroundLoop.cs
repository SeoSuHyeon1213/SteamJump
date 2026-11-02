using UnityEngine;

// 2D 배경이 화면 왼쪽 밖으로 완전히 나가면 오른쪽 끝으로 재배치하는 스크립트
public class BackgroundLoop : MonoBehaviour {
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;

    private void Awake() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
    }

    private void Update() {
        if(mainCamera == null)
        {
            return;
        }

        float cameraLeftEdge = mainCamera.transform.position.x - mainCamera.orthographicSize * mainCamera.aspect;
        float backgroundRightEdge = spriteRenderer.bounds.max.x;

        // 배경의 오른쪽 끝이 카메라 왼쪽 밖으로 완전히 나갔을 때 위치를 리셋
        if(backgroundRightEdge <= cameraLeftEdge)
        {
            Reposition(); // 위치를 리셋하는 메서드 호출
        }
    }

    // 위치를 리셋하는 메서드
    private void Reposition() {
        BackgroundLoop[] backgrounds = transform.parent.GetComponentsInChildren<BackgroundLoop>();
        float rightEdge = float.MinValue;

        for(int i = 0; i < backgrounds.Length; i++)
        {
            if(backgrounds[i] == this)
            {
                continue;
            }

            SpriteRenderer otherRenderer = backgrounds[i].GetComponent<SpriteRenderer>();
            if(otherRenderer != null && otherRenderer.bounds.max.x > rightEdge)
            {
                rightEdge = otherRenderer.bounds.max.x;
            }
        }

        if(rightEdge == float.MinValue)
        {
            return;
        }

        float halfWidth = spriteRenderer.bounds.size.x / 2f;
        transform.position = new Vector2(rightEdge + halfWidth, transform.position.y);
    }
}
