using UnityEngine;

public class GameQuit : MonoBehaviour
{
    public void QuitGame()
    {
#if UNITY_EDITOR
        // 에디터에서 테스트할 때는 Application.Quit()이 동작하지 않으므로 Play 모드를 대신 종료한다
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    public void Update()
    {
        // if (Application.platform == RuntimePlatform.Android)
        // {
        //     if (Input.GetKeyDown(KeyCode.Escape))
        //     {
        //         Application.Quit();
        //     }
        // }
    }
}
