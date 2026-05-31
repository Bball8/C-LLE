using UnityEngine;

public class LaserYellow : MonoBehaviour
{
    public CoopEpisodeManager episode;

    private LineRenderer lr;
    public float laserRange = 100f;

    // Re-arm only when beam is no longer hitting a player
    private bool triggered;

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
                hit.collider.CompareTag("PlayerGreen") ||
                hit.collider.CompareTag("PlayerRed");

            if (!triggered && hitPlayerThisFrame)
            {
                triggered = true;
                if (episode != null) episode.NotifyTeamDeath();
                else Debug.LogError("LaserYellow: CoopEpisodeManager not found in scene.");
            }
        }
        else
        {
            lr.SetPosition(1, transform.position - transform.forward * laserRange);
        }

        // Re-arm when beam is clear of players
        if (triggered && !hitPlayerThisFrame)
            triggered = false;
    }
}