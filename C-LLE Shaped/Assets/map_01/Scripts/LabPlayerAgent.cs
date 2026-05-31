using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class LabPlayerAgent : Agent
{
    [Header("Identity")]
    public int playerIndex; // 0..3

    [Header("Refs (optional: leave empty to auto-find)")]
    public Rigidbody rb;
    public PlayerMovementWithRigidbodyVelocity motor;
    [HideInInspector] public CoopEpisodeManager episode;

    [Header("Key Bindings (Manual Test / Heuristic)")]
    public KeyCode upKey = KeyCode.Z;
    public KeyCode downKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.Q;
    public KeyCode rightKey = KeyCode.D;

    [Header("Observation Settings")]
    public int maxObservedGems = 4;
    public float mapNorm = 20f;

    public int maxObservedZones = 3;

    private Vector2 heldInput = Vector2.zero;
    private int holdCounter = 0;

    [Header("Control Smoothing")]
    public int actionRepeat = 4;

    private bool onExit;

    [HideInInspector] public bool[] zoneVisited;

    public void SetOnExit(bool v) => onExit = v;

    public void InitShapingObs(int nZones)
    {
        zoneVisited = new bool[nZones];
    }

    public void SetZoneVisited(int zoneIndex)
    {
        if (zoneVisited != null && zoneIndex < zoneVisited.Length)
            zoneVisited[zoneIndex] = true;
    }

    public override void Initialize()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) rb = GetComponentInChildren<Rigidbody>(true);
        if (rb == null) rb = GetComponentInParent<Rigidbody>(true);

        if (motor == null) motor = GetComponent<PlayerMovementWithRigidbodyVelocity>();
        if (motor == null) motor = GetComponentInChildren<PlayerMovementWithRigidbodyVelocity>(true);
        if (motor == null) motor = GetComponentInParent<PlayerMovementWithRigidbodyVelocity>(true);

        if (rb == null)
        {
            Debug.LogError($"{name}: Rigidbody missing");
            enabled = false;
            return;
        }

        if (motor == null)
        {
            Debug.LogError($"{name}: PlayerMovementWithRigidbodyVelocity missing");
            enabled = false;
            return;
        }

        if (episode == null)
            episode = FindFirstObjectByType<CoopEpisodeManager>();

        if (zoneVisited == null)
            zoneVisited = new bool[maxObservedZones];
    }

    public override void OnEpisodeBegin()
    {
        heldInput = Vector2.zero;
        holdCounter = 0;

        motor.SetMoveInput(Vector2.zero);
        SetOnExit(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (zoneVisited != null)
            for (int i = 0; i < zoneVisited.Length; i++)
                zoneVisited[i] = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {

        sensor.AddObservation(playerIndex == 0 ? 1f : 0f);
        sensor.AddObservation(playerIndex == 1 ? 1f : 0f);
        sensor.AddObservation(playerIndex == 2 ? 1f : 0f);
        sensor.AddObservation(playerIndex == 3 ? 1f : 0f);

        Vector3 p = transform.position;
        float posRight = p.z / mapNorm;
        float posUp = -p.x / mapNorm;
        sensor.AddObservation(posRight);
        sensor.AddObservation(posUp);

        Vector3 v = rb.linearVelocity;
        float velRight = v.z / 10f;
        float velUp = -v.x / 10f;
        sensor.AddObservation(velRight);
        sensor.AddObservation(velUp);

        sensor.AddObservation(onExit ? 1f : 0f);

        Transform exitT = (episode != null) ? episode.exitTransform : null;
        if (exitT != null)
        {
            Vector3 d = exitT.position - p;
            sensor.AddObservation(d.z / mapNorm);
            sensor.AddObservation(-d.x / mapNorm);
            sensor.AddObservation(Mathf.Clamp01(d.magnitude / mapNorm));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        GameObject[] gemList = (episode != null) ? episode.gems : null;
        float anyGemRemaining = 0f;
        Vector3 bestD = Vector3.zero;
        float bestDistSqr = float.PositiveInfinity;

        if (gemList != null)
        {
            for (int i = 0; i < gemList.Length; i++)
            {
                GameObject g = gemList[i];
                if (g == null || !g.activeInHierarchy) continue;
                anyGemRemaining = 1f;
                Vector3 dg = g.transform.position - p;
                float distSqr = dg.sqrMagnitude;
                if (distSqr < bestDistSqr) { bestDistSqr = distSqr; bestD = dg; }
            }
        }

        if (anyGemRemaining > 0f)
        {
            sensor.AddObservation(bestD.z / mapNorm);
            sensor.AddObservation(-bestD.x / mapNorm);
            sensor.AddObservation(Mathf.Clamp01(Mathf.Sqrt(bestDistSqr) / mapNorm));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        sensor.AddObservation(anyGemRemaining);

        for (int i = 0; i < maxObservedGems; i++)
        {
            float collected = 1f;
            if (gemList != null && i < gemList.Length && gemList[i] != null)
                collected = gemList[i].activeInHierarchy ? 0f : 1f;
            sensor.AddObservation(collected);
        }

        for (int i = 0; i < maxObservedZones; i++)
        {
            float visited = (zoneVisited != null && i < zoneVisited.Length && zoneVisited[i]) ? 1f : 0f;
            sensor.AddObservation(visited);
        }

    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        if (a.Length < 2) return;

        if (holdCounter <= 0)
        {
            float h = Mathf.Clamp(a[0], -1f, 1f);
            float v = Mathf.Clamp(a[1], -1f, 1f);
            heldInput = new Vector2(-v, h);
            if (heldInput.sqrMagnitude > 1f) heldInput.Normalize();
            holdCounter = Mathf.Max(1, actionRepeat);
        }

        holdCounter--;
        motor.SetMoveInput(heldInput);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        float h = 0f, v = 0f;

        if (Input.GetKey(leftKey)) h -= 1f;
        if (Input.GetKey(rightKey)) h += 1f;
        if (Input.GetKey(upKey)) v += 1f;
        if (Input.GetKey(downKey)) v -= 1f;

        Vector2 hv = new Vector2(h, v);
        if (hv.sqrMagnitude > 1f) hv.Normalize();

        var a = actionsOut.ContinuousActions;
        if (a.Length < 2) return;
        a[0] = hv.x;
        a[1] = hv.y;
    }
}