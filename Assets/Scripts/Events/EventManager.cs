using UnityEngine;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    [Header("PaintingChanging")]
    [Tooltip("Assign all painting objects (Painting component) that the module may affect.")]
    public List<Painting> paintings = new List<Painting>();

    [Tooltip("Player manager to locate player cameras. If empty, Camera.main will be used.")]
    public PlayerManager playerManager;

    [Tooltip("Sanity threshold below which paintings can start to change.")]
    public float sanityThreshold = 50f;

    [Tooltip("Base chance per second used for both applying and resetting (module scales this).")]
    public float baseChancePerSecond = 0.02f;

    [Tooltip("How long it takes (seconds) to reach the module's maximum chance scaling.")]
    public float timeToMaxChance = 120f;

    [Tooltip("Minimum randomized time a painting must remain distorted (seconds).")]
    public float resetMinSeconds = 20f;

    [Tooltip("Maximum randomized time a painting must remain distorted (seconds).")]
    public float resetMaxSeconds = 30f;

    [Tooltip("Max angle (degrees) for 'player is looking at painting' checks.")]
    public float paintingLookAngle = 40f;

    [Header("Runtime (can be updated by your player stats)")]
    [Tooltip("Current player sanity (0-100). Update this from your player stats each frame.")]
    public float playerSanity = 100f;

    [Header("Debug")]
    [Tooltip("Enable verbose painting event logs to help debug why paintings don't change.")]
    public bool debugPaintings = true;

    [Header("ShadowSpawning")]
    [Tooltip("Shadow prefab to spawn (must contain ShadowLogic).")]
    public GameObject shadowPrefab;
    [Tooltip("Sanity threshold below which shadows may start to spawn.")]
    public float shadowSpawnSanityThreshold = 70f;
    [Tooltip("Maximum number of active shadows allowed at once.")]
    public int shadowMaxActive = 2;
    [Tooltip("Cooldown (seconds) after a successful shadow spawn before another can spawn.")]
    public float shadowSpawnCooldownSeconds = 120f;
    [Tooltip("Maximum chance per second to spawn (0..1). Final chance scales with sanity and time below threshold.")]
    public float shadowMaxChance = 0.5f;
    [Tooltip("Time (seconds) until the spawn chance reaches maximum.")]
    public float shadowTimeToMax = 10f;
    [Tooltip("Radius from player (units) where the shadow will be spawned (attempted).")]
    public float shadowSpawnRadius = 12f;

    [Header("Statues")]
    [Tooltip("Assign all statue objects (Statue component) that the module may affect.")]
    public List<Statue> statues = new List<Statue>();
    [Tooltip("Sanity threshold below which statue heads will follow the player.")]
    public float statueFollowSanityThreshold = 50f;
    [Tooltip("If true, statue heads only move when the player is NOT looking (visibility check).")]
    public bool statueRequireNotSeen = false;

    [Header("CameraEvent (Camera entity on monitors)")]
    [Tooltip("Prefab with CameraEntity component. Will be instantiated at targeted camera spawn and kept hidden until that group is selected.")]
    public GameObject cameraEntityPrefab;
    [Tooltip("Sanity threshold below which the camera entity event may occur.")]
    public float cameraEventSanityThreshold = 60f;
    [Tooltip("Cooldown in seconds after camera event spawns before it can spawn again.")]
    public float cameraEventCooldownSeconds = 120f;
    [Tooltip("Multiplier applied to baseChancePerSecond while player is on the cameras (increases chance).")]
    public float cameraEventChanceMultiplierOnCamera = 5f;

    // list of CamGroup instances discovered at Awake (populated early so non-Mono modules can read it)
    [HideInInspector] public List<CamGroup> allCamGroups = new List<CamGroup>();

    // internal
    private List<IEventModule> modules = new List<IEventModule>();

    // direct reference to the singleton PlayerStats
    private PlayerStats playerStats;

    void Awake()
    {
        // populate CamGroup list early (modules are created immediately afterwards and may use it)
        allCamGroups = new List<CamGroup>(FindObjectsOfType<CamGroup>());

        // create modules here. Keep EventManager minimal by delegating logic into modules.
        modules.Add(new PaintingEventModule(this));
        modules.Add(new ShadowSpawnEventModule(this));
        modules.Add(new StatueEventModule(this));

        // camera event module
        modules.Add(new CameraEventModule(this));

        // debug: list modules created
        var names = new System.Collections.Generic.List<string>();
        foreach (var m in modules) names.Add(m != null ? m.GetType().Name : "null");
        Debug.Log($"[EventManager] Awake - created modules: {string.Join(", ", names.ToArray())}");

        // initialize modules
        foreach (var m in modules) m.OnAwake();
    }

    void Start()
    {
        // Use the singleton PlayerStats instance directly
        playerStats = PlayerStats.Instance;
        if (playerStats == null)
        {
            Debug.LogWarning("[EventManager] PlayerStats.Instance is null. Falling back to manual updates of playerSanity.");
        }
        else
        {
            // initialize sanity immediately
            playerSanity = playerStats.Sanity;
            if (debugPaintings) Debug.Log("[EventManager] Using PlayerStats.Instance for sanity updates.");
        }

        foreach (var m in modules) m.OnStart();
    }

    void Update()
    {
        // Read sanity directly from PlayerStats singleton if available
        if (playerStats != null)
        {
            playerSanity = playerStats.Sanity;
        }

        foreach (var m in modules) m.OnUpdate();
    }

    /// <summary>
    /// Helper to obtain the player camera used for visibility checks.
    /// </summary>
    public Camera GetPlayerCamera()
    {
        if (playerManager != null)
        {
            if (playerManager.rayCam != null) return playerManager.rayCam;
            if (playerManager.playerCamera != null) return playerManager.playerCamera;
        }
        return Camera.main;
    }
}
