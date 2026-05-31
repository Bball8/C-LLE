using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public class CoopEpisodeManager : MonoBehaviour
{
    [Header("Agents (optional: leave empty to auto-find)")]
    public LabPlayerAgent[] agents;

    [Header("Spawn Points (optional)")]
    public Transform[] spawnPoints;

    [Header("Gems (optional)")]
    public GameObject[] gems;

    [Header("Exit")]
    public Transform exitTransform;

    [Header("Zone Shaping")]
    public ZoneTrigger[] zoneTriggers;

    [Header("Lasers")]
    public LaserYellow yellowLaser;
    public LaserRed redLaser;
    public LaserGreen greenLaser;

    [Header("Episode settings")]
    public int maxSteps = 2000;

    [Header("Rewards (shared)")]
    public float gemReward = 1f;
    public float exitEnterReward = 1f;
    public float winReward = 1f;

    [Header("Reward Shaping")]
    public float blockingReward = 0.5f;
    public float zoneReward = 0.1f;

    [Header("Step penalty")]
    public float stepPenalty = 0f;

    [Header("Win Rate Tracking")]
    public int winRateWindow = 100;

    [Header("Debug")]
    public bool debug = true;

    private Vector3[] defaultSpawnPos;
    private Quaternion[] defaultSpawnRot;

    private int stepCount;
    private bool ending;
    private bool initialized;

    private bool[] onExit;
    private bool[] exitRewardGiven;

    private Queue<float> _winHistory = new Queue<float>();

    private ExitTrigger exitTrigger;

    public int AgentCount => (agents != null) ? agents.Length : 0;

    void Awake() => EnsureInitialized();
    void OnEnable() => EnsureInitialized();

    void EnsureInitialized()
    {
        if (initialized) return;

        if (agents == null || agents.Length == 0)
        {
            agents = FindObjectsByType<LabPlayerAgent>(FindObjectsSortMode.None);
            Array.Sort(agents, (a, b) => a.playerIndex.CompareTo(b.playerIndex));
        }

        if (agents.Length == 0)
        {
            Debug.LogError("[Episode] No LabPlayerAgent found.");
            return;
        }

        defaultSpawnPos = new Vector3[agents.Length];
        defaultSpawnRot = new Quaternion[agents.Length];
        onExit = new bool[agents.Length];
        exitRewardGiven = new bool[agents.Length];

        if (gems == null || gems.Length == 0)
        {
            var gemScripts = FindObjectsByType<Gem>(FindObjectsSortMode.None);
            gems = new GameObject[gemScripts.Length];
            for (int i = 0; i < gemScripts.Length; i++)
                gems[i] = gemScripts[i].gameObject;
        }

        exitTrigger = FindFirstObjectByType<ExitTrigger>();
        if (exitTransform == null && exitTrigger != null)
            exitTransform = exitTrigger.transform;

        if (zoneTriggers == null || zoneTriggers.Length == 0)
            zoneTriggers = FindObjectsByType<ZoneTrigger>(FindObjectsSortMode.None);

        if (yellowLaser == null) yellowLaser = FindFirstObjectByType<LaserYellow>();
        if (redLaser == null) redLaser = FindFirstObjectByType<LaserRed>();
        if (greenLaser == null) greenLaser = FindFirstObjectByType<LaserGreen>();

        int nZones = (zoneTriggers != null) ? zoneTriggers.Length : 0;
        for (int i = 0; i < agents.Length; i++)
        {
            defaultSpawnPos[i] = agents[i].transform.position;
            defaultSpawnRot[i] = agents[i].transform.rotation;
            agents[i].episode = this;
            agents[i].SetOnExit(false);
            agents[i].InitShapingObs(nZones);
        }

        initialized = true;
        if (debug) Debug.Log($"[Episode] Initialized. Agents={agents.Length}, Gems={gems.Length}, Zones={nZones}");
    }

    public int GetAgentSlot(LabPlayerAgent agent)
    {
        EnsureInitialized();
        if (!initialized || agents == null || agent == null) return -1;
        for (int i = 0; i < agents.Length; i++)
            if (agents[i] == agent) return i;
        return -1;
    }

    void FixedUpdate()
    {
        EnsureInitialized();
        if (!initialized || ending) return;

        stepCount++;

        if (stepPenalty != 0f)
            for (int i = 0; i < agents.Length; i++)
                agents[i].AddReward(stepPenalty);

        if (stepCount >= maxSteps)
        {
            if (debug) Debug.Log("[Episode] TIMEOUT");
            EndAndReset(interrupted: true, reason: "timeout");
        }
    }

    public void NotifyGemCollected(GameObject gem)
    {
        EnsureInitialized();
        if (!initialized || ending) return;

        if (debug) Debug.Log("[Episode] GEM +" + gemReward);
        for (int i = 0; i < agents.Length; i++)
            agents[i].AddReward(gemReward);

        if (gem != null) gem.SetActive(false);
    }

    public void NotifyAgentEnteredExit(int slot)
    {
        EnsureInitialized();
        if (!initialized || ending) return;
        if (slot < 0 || slot >= agents.Length) return;

        onExit[slot] = true;

        if (!exitRewardGiven[slot])
        {
            exitRewardGiven[slot] = true;
            if (debug) Debug.Log($"[Episode] EXIT ENTER slot {slot} +" + exitEnterReward);
            for (int i = 0; i < agents.Length; i++)
                agents[i].AddReward(exitEnterReward);
        }

        if (AllOnExit())
            NotifyTeamWin();
    }

    public void NotifyAgentExitedExit(int slot)
    {
        if (slot < 0 || slot >= agents.Length) return;
        onExit[slot] = false;
    }

    bool AllOnExit()
    {
        for (int i = 0; i < onExit.Length; i++)
            if (!onExit[i]) return false;
        return true;
    }

    public void NotifyAgentBlocking(LabPlayerAgent agent)
    {
        if (!initialized || ending) return;
        if (debug) Debug.Log($"[Episode] BLOCKING +{blockingReward}");
        agent.AddReward(blockingReward);
    }

    public void NotifyAgentEnteredZone(int slot, int zoneIndex)
    {
        if (!initialized || ending) return;
        if (slot < 0 || slot >= agents.Length) return;

        float reward = zoneReward * (zoneIndex + 1);
        if (debug) Debug.Log($"[Episode] ZONE {zoneIndex} agent {slot} +{reward}");
        agents[slot].AddReward(reward);

        agents[slot].SetZoneVisited(zoneIndex);
    }

    public void NotifyTeamWin()
    {
        EnsureInitialized();
        if (!initialized || ending) return;

        if (debug) Debug.Log("[Episode] WIN +" + winReward);
        for (int i = 0; i < agents.Length; i++)
            agents[i].AddReward(winReward);

        EndAndReset(interrupted: false, reason: "win");
    }

    public void NotifyTeamDeath()
    {
        EnsureInitialized();
        if (!initialized || ending) return;

        float penalty = -1f;
        if (debug) Debug.Log($"[Episode] DEATH => {penalty}");
        for (int i = 0; i < agents.Length; i++)
            agents[i].AddReward(penalty);

        EndAndReset(interrupted: false, reason: "death");
    }

    void EndAndReset(bool interrupted, string reason)
    {
        if (ending) return;
        ending = true;

        float sum = 0f;
        for (int i = 0; i < agents.Length; i++)
        {
            float r = agents[i].GetCumulativeReward();
            sum += r;
            Debug.Log($"episode finished | steps={stepCount} | reason={reason} | agentSlot={i} | episodeReward={r:0.###}");
        }
        float avg = (agents.Length > 0) ? (sum / agents.Length) : 0f;
        Debug.Log($"episode finished | steps={stepCount} | reason={reason} | teamAvgEpisodeReward={avg:0.###}");

        _winHistory.Enqueue(reason == "win" ? 1f : 0f);
        if (_winHistory.Count > winRateWindow)
            _winHistory.Dequeue();

        float winRate = 0f;
        foreach (float w in _winHistory) winRate += w;
        winRate /= _winHistory.Count;
        Academy.Instance.StatsRecorder.Add("Environment/WinRate", winRate);

        for (int i = 0; i < agents.Length; i++)
        {
            if (interrupted) agents[i].EpisodeInterrupted();
            else agents[i].EndEpisode();
        }

        ResetWorld();
        ending = false;
    }

    void ResetWorld()
    {
        stepCount = 0;

        for (int i = 0; i < onExit.Length; i++)
        {
            onExit[i] = false;
            exitRewardGiven[i] = false;
        }

        if (exitTrigger == null)
            exitTrigger = FindFirstObjectByType<ExitTrigger>();
        exitTrigger?.ResetExit();

        if (exitTransform == null && exitTrigger != null)
            exitTransform = exitTrigger.transform;

        int nZones = (zoneTriggers != null) ? zoneTriggers.Length : 0;
        for (int i = 0; i < agents.Length; i++)
        {
            var a = agents[i];
            var rb = a.rb != null ? a.rb : a.GetComponentInChildren<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            a.SetOnExit(false);
            a.motor?.SetMoveInput(Vector2.zero);

            a.InitShapingObs(nZones);

            if (spawnPoints != null && spawnPoints.Length == agents.Length && spawnPoints[i] != null)
            {
                a.transform.position = spawnPoints[i].position;
                a.transform.rotation = spawnPoints[i].rotation;
            }
            else
            {
                a.transform.position = defaultSpawnPos[i];
                a.transform.rotation = defaultSpawnRot[i];
            }
        }

        Physics.SyncTransforms();

        for (int i = 0; i < gems.Length; i++)
        {
            if (gems[i] == null) continue;
            var g = gems[i].GetComponent<Gem>();
            if (g != null) g.ResetGem();
            else gems[i].SetActive(true);
        }

        if (zoneTriggers != null)
            foreach (var zone in zoneTriggers)
                if (zone != null) zone.ResetZone();

        yellowLaser?.ResetLaser();
        redLaser?.ResetLaser();
        greenLaser?.ResetLaser();

        if (debug) Debug.Log("[Episode] World reset");
    }
}