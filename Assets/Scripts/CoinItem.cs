using UnityEngine;

public class CoinItem : CollectibleItem {
    [SerializeField, Min(1)] private int scoreValue = 1;

    public void ConfigureScore(int value) {
        scoreValue = Mathf.Max(1, value);
    }

    protected override bool TryApplyEffect(PlayerController player) {
        if(GameManager.instance == null || GameManager.instance.isGameover) {
            return false;
        }
        GameManager.instance.AddScore(scoreValue);
        return true;
    }
}
