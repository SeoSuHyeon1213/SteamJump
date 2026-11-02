/// <summary>Shared pickup contract for coins, potions and other world items.</summary>
public interface ICollectibleItem {
    // Return false when the item cannot be consumed (for example, a potion at full health).
    bool TryCollect(PlayerController player);
}
