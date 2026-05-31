using UnityEngine;

public class Gem : MonoBehaviour
{
    public CoopEpisodeManager episode;
    private bool collected;

    void Awake()
    {
        if (episode == null)
            episode = FindFirstObjectByType<CoopEpisodeManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        var agent = other.GetComponentInParent<LabPlayerAgent>();
        if (agent == null) return;

        collected = true;

        if (episode != null)
            episode.NotifyGemCollected(gameObject);
        else
            Debug.LogError("Gem: CoopEpisodeManager not found in scene.");
    }

    public void ResetGem()
    {
        collected = false;
        gameObject.SetActive(true);
    }
}