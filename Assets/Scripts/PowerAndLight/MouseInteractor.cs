using UnityEngine;
using UnityEngine.InputSystem;

public class MouseInteractor : MonoBehaviour
{
    [Tooltip("World object that represents the mouse cursor (must have Collider/Trigger if you prefer).")]
    [SerializeField] public Transform mouseObject;

    [Tooltip("Distance from the interaction camera to place the mouse object along the screen ray.")]
    [SerializeField] private float distanceFromCamera = 2f;

    [Tooltip("Radius used to detect buttons under the mouse object.")]
    [SerializeField] private float sphereRadius = 0.05f;

    [Tooltip("Layers that contain BreakerButton colliders.")]
    [SerializeField] private LayerMask interactableMask = ~0;

    private Camera interactionCamera;
    private PlayerInputManager inputActions;
    private bool active;
    private BreakerButton hoveredButton;

    private void Awake()
    {
        if (mouseObject != null)
            mouseObject.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!active || interactionCamera == null || mouseObject == null)
            return;

        // Get mouse position (Input System preferred)
        Vector2 mousePos = Vector2.zero;
        if (Mouse.current != null)
            mousePos = Mouse.current.position.ReadValue();
        else
            mousePos = Input.mousePosition;

        // Convert to a world point in front of the interaction camera
        Ray ray = interactionCamera.ScreenPointToRay(mousePos);
        Vector3 worldPos = ray.origin + ray.direction * distanceFromCamera;
        mouseObject.position = worldPos;
        mouseObject.rotation = Quaternion.LookRotation(-interactionCamera.transform.forward, Vector3.up);

        // Detect BreakerButton under cursor using OverlapSphere (triggers included)
        Collider[] cols = Physics.OverlapSphere(worldPos, sphereRadius, interactableMask, QueryTriggerInteraction.Collide);
        BreakerButton found = null;
        for (int i = 0; i < cols.Length; i++)
        {
            var b = cols[i].GetComponentInParent<BreakerButton>();
            if (b != null)
            {
                found = b;
                break;
            }
        }

        if (found != hoveredButton)
        {
            hoveredButton = found;
            // Optionally add hover feedback here
        }

        // Click detection: left mouse or Fire action
        bool click = false;
        if (Mouse.current != null)
            click |= Mouse.current.leftButton.wasPressedThisFrame;
        click |= Input.GetMouseButtonDown(0);

        if (!click && inputActions != null)
        {
            // safe try: if action exists, read it
            try { click |= inputActions.Player.Fire.triggered; } catch { }
        }

        if (click && hoveredButton != null)
        {
            hoveredButton.Toggle();
        }
    }

    // Called by BreakerBox when an interaction with its camera starts
    public void Enable(Camera cam, PlayerInputManager actions)
    {
        interactionCamera = cam;
        inputActions = actions;
        active = true;
        if (mouseObject != null)
            mouseObject.gameObject.SetActive(true);
    }

    // Called by BreakerBox when interaction ends
    public void Disable()
    {
        active = false;
        hoveredButton = null;
        interactionCamera = null;
        inputActions = null;
        if (mouseObject != null)
            mouseObject.gameObject.SetActive(false);
    }

    // Gizmo to visualize detection sphere in editor (when selected)
    private void OnDrawGizmosSelected()
    {
        if (mouseObject == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(mouseObject.position, sphereRadius);
    }
}