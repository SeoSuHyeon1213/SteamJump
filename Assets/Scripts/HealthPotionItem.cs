using UnityEngine;

public class HealthPotionItem : CollectibleItem {
    [SerializeField, Min(1)] private int healAmount = 1;

    public void ConfigureHealAmount(int amount) {
        healAmount = Mathf.Max(1, amount);
    }

    protected override bool TryApplyEffect(PlayerController player) {
        // The base class leaves the potion available if health is already full.
        return player.RestoreHealth(Mathf.Max(1, healAmount));
    }
}
