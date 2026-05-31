using UnityEngine;
using System.Collections.Generic;

public class ExitTrigger : MonoBehaviour
{
    public CoopEpisodeManager episode;

    [Header("Debug")]
    public bool debug = false;

    private Dictionary<int, int> contacts = new Dictionary<int, int>();

    void Awake()
    {
        if (episode == null)
            episode = FindFirstObjectByType<CoopEpisodeManager>();
    }

    void OnEnable()
    {
        contacts.Clear();
    }

    public void ResetExit()
    {
        contacts.Clear();
    }

    int GetSlot(LabPlayerAgent agent)
    {
        if (episode == null) return -1;
        return episode.GetAgentSlot(agent);
    }

    void OnTriggerEnter(Collider other)
    {
        var agent = other.GetComponentInParent<LabPlayerAgent>();
        if (agent == null) return;

        int slot = GetSlot(agent);
        if (slot < 0) return;

        int before = contacts.ContainsKey(slot) ? contacts[slot] : 0;
        int after = before + 1;
        contacts[slot] = after;

        agent.SetOnExit(after > 0);

        if (before == 0 && after == 1)
        {
            if (episode != null) episode.NotifyAgentEnteredExit(slot);
            else Debug.LogError("[ExitTrigger] CoopEpisodeManager not found in scene.");
        }

        if (debug && episode != null)
            Debug.Log($"[ExitTrigger] ENTER slot={slot} count={after} required={episode.AgentCount}");
    }

    void OnTriggerExit(Collider other)
    {
        var agent = other.GetComponentInParent<LabPlayerAgent>();
        if (agent == null) return;

        int slot = GetSlot(agent);
        if (slot < 0) return;

        int before = contacts.ContainsKey(slot) ? contacts[slot] : 0;
        int after = Mathf.Max(0, before - 1);
        contacts[slot] = after;

        agent.SetOnExit(after > 0);

        if (before > 0 && after == 0)
        {
            if (episode != null) episode.NotifyAgentExitedExit(slot);
            else Debug.LogError("[ExitTrigger] CoopEpisodeManager not found in scene.");
        }

        if (debug && episode != null)
            Debug.Log($"[ExitTrigger] EXIT slot={slot} count={after} required={episode.AgentCount}");
    }
}