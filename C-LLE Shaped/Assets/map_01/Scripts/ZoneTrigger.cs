using UnityEngine;
using System.Collections.Generic;

public class ZoneTrigger : MonoBehaviour
{
    public CoopEpisodeManager episode;
    public int zoneIndex;
    private HashSet<int> _rewarded = new HashSet<int>();

    void Awake()
    {
        if (episode == null)
            episode = FindFirstObjectByType<CoopEpisodeManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<LabPlayerAgent>();
        if (agent == null) return;

        int slot = episode.GetAgentSlot(agent);
        if (slot < 0) return;

        if (_rewarded.Contains(slot)) return;
        _rewarded.Add(slot);

        if (episode != null)
            episode.NotifyAgentEnteredZone(slot, zoneIndex);
    }

    public void ResetZone()
    {
        _rewarded.Clear();
    }
}