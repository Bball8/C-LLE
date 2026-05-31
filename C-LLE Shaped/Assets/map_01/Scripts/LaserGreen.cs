using UnityEngine;

public class LaserGreen : MonoBehaviour
{
    public CoopEpisodeManager episode;

    private LineRenderer lr;
    public float laserRange = 100f;

    private bool triggered;
    private bool blockingRewardGiven;

    void Awake()
    {
        if (episode == null)
            episode = FindFirstObjectByType<CoopEpisodeManager>();
    }

    void Start()
    {
        lr = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (lr == null) return;

        lr.SetPosition(0, transform.position);
        bool hitPlayerThisFrame = false;

        if (Physics.Raycast(transform.position, -transform.forward, out RaycastHit hit, laserRange))
        {
            lr.SetPosition(1, hit.point);

            hitPlayerThisFrame =
                hit.collider.CompareTag("PlayerBlue") ||
                hit.collider.CompareTag("PlayerYellow") ||
                hit.collider.CompareTag("PlayerRed");

            if (!triggered && hitPlayerThisFrame)
            {
                triggered = true;
                if (episode != null) episode.NotifyTeamDeath();
                else Debug.LogError("LaserGreen: CoopEpisodeManager not found in scene.");
            }

            if (!blockingRewardGiven && hit.collider.CompareTag("PlayerGreen"))
            {
                blockingRewardGiven = true;
                var agent = hit.collider.GetComponentInParent<LabPlayerAgent>();
                if (agent != null && episode != null)
                    episode.NotifyAgentBlocking(agent);
            }
        }
        else
        {
            lr.SetPosition(1, transform.position - transform.forward * laserRange);
        }

        if (triggered && !hitPlayerThisFrame)
            triggered = false;
    }

    public void ResetLaser()
    {
        triggered = false;
        blockingRewardGiven = false;
    }
}