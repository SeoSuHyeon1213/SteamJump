using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public abstract class CollectibleItem : MonoBehaviour, ICollectibleItem {
    [SerializeField] private AudioClip collectionSound;
    [SerializeField, Range(0f, 1f)] private float collectionVolume = 1f;
    private bool collected;

    protected virtual void OnEnable() {
        collected = false;
    }

    public bool TryCollect(PlayerController player) {
        if(collected || !isActiveAndEnabled || player == null || !player.CanCollectItems) {
            return false;
        }

        // Guard before applying the reward: the player can have several colliders.
        collected = true;
        if(!TryApplyEffect(player)) {
            collected = false;
            return false;
        }

        player.PlayItemSound(collectionSound, collectionVolume);
        gameObject.SetActive(false);
        return true;
    }

    public void ConfigureCollectionSound(AudioClip clip, float volume) {
        collectionSound = clip;
        collectionVolume = Mathf.Clamp01(volume);
    }

    protected abstract bool TryApplyEffect(PlayerController player);
}
