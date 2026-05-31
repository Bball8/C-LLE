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

    private Vector2 heldInput = Vector2.zero;
    private int holdCounter = 0;

    [Header("Control Smoothing")]
    public int actionRepeat = 4; // hold same action

    private bool onExit;

    public void SetOnExit(bool v) => onExit = v;

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
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Coordinate frame used for control/actions in your project:
        // right = +Z, up = -X, down = +X
        // We'll express key observations (pos/vel/goals) in this same (right, up) frame.

        // Id one-hot (4)
        sensor.AddObservation(playerIndex == 0 ? 1f : 0f);
        sensor.AddObservation(playerIndex == 1 ? 1f : 0f);
        sensor.AddObservation(playerIndex == 2 ? 1f : 0f);
        sensor.AddObservation(playerIndex == 3 ? 1f : 0f);

        // Position in control frame: (right, up)
        Vector3 p = transform.position;
        float posRight = p.z / mapNorm;
        float posUp = -p.x / mapNorm;
        sensor.AddObservation(posRight);
        sensor.AddObservation(posUp);

        // Velocity in control frame: (right, up)
        Vector3 v = rb.linearVelocity;
        float velRight = v.z / 10f;
        float velUp = -v.x / 10f;
        sensor.AddObservation(velRight);
        sensor.AddObservation(velUp);

        // On-exit
        sensor.AddObservation(onExit ? 1f : 0f);

        // --- Exit relative features in control frame (right, up, dist) ---
        Transform exitT = (episode != null) ? episode.exitTransform : null;
        if (exitT != null)
        {
            Vector3 d = exitT.position - p;
            float exitRight = d.z / mapNorm;
            float exitUp = -d.x / mapNorm;
            sensor.AddObservation(exitRight);
            sensor.AddObservation(exitUp);
            sensor.AddObservation(Mathf.Clamp01(d.magnitude / mapNorm));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Gems list
        GameObject[] gemList = (episode != null) ? episode.gems : null;

        // --- Nearest active gem relative features in control frame (right, up, dist, anyRemaining) ---
        float anyGemRemaining = 0f;
        Vector3 bestD = Vector3.zero;
        float bestDistSqr = float.PositiveInfinity;

        if (gemList != null)
        {
            for (int i = 0; i < gemList.Length; i++)
            {
                GameObject g = gemList[i];
                if (g == null || !g.activeInHierarchy) continue; // active = not collected

                anyGemRemaining = 1f;
                Vector3 dg = g.transform.position - p;
                float distSqr = dg.sqrMagnitude;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    bestD = dg;
                }
            }
        }

        if (anyGemRemaining > 0f)
        {
            float gemRight = bestD.z / mapNorm;
            float gemUp = -bestD.x / mapNorm;
            sensor.AddObservation(gemRight);
            sensor.AddObservation(gemUp);
            sensor.AddObservation(Mathf.Clamp01(Mathf.Sqrt(bestDistSqr) / mapNorm));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        sensor.AddObservation(anyGemRemaining);

        // Existing gem status flags (1 = collected, 0 = not)
        for (int i = 0; i < maxObservedGems; i++)
        {
            float collected = 1f;
            if (gemList != null && i < gemList.Length && gemList[i] != null)
                collected = gemList[i].activeInHierarchy ? 0f : 1f;

            sensor.AddObservation(collected);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        if (a.Length < 2) return;

        // Only sample a new action every N steps
        if (holdCounter <= 0)
        {
            float h = Mathf.Clamp(a[0], -1f, 1f); // right/left
            float v = Mathf.Clamp(a[1], -1f, 1f); // up/down

            // axis remap to motor input: +Z right, +X down  => motorInput = (worldX, worldZ) = (-v, h)
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