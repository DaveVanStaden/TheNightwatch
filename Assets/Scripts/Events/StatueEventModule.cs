using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Makes statue heads follow the player when the player is NOT looking and sanity is below threshold.
/// Heads smoothly rotate toward the player but their yaw is clamped to ±100 degrees from their initial yaw.
/// </summary>
public class StatueEventModule : IEventModule
{
    private struct State
    {
        public Statue statue;
        public Quaternion initialLocalRot;
        public float currentYawDeg; // current yaw relative to initial
    }

    private readonly List<State> states = new List<State>();

    // tuning
    private const float yawClampDegrees = 100f;
    private const float rotateSpeedDegPerSec = 120f; // how fast head turns
    private const float returnSpeedDegPerSec = 120f;

    public StatueEventModule(EventManager manager) : base(manager) { }

    public override void OnAwake()
    {
        BuildStates();
        if (manager != null && manager.debugPaintings)
            Debug.Log("[StatueEventModule] OnAwake - module initialized.");
    }

    public override void OnStart()
    {
        BuildStates();
        if (manager != null && manager.debugPaintings)
            Debug.Log("[StatueEventModule] OnStart - module started.");
    }

    public override void OnUpdate()
    {
        // low-rate debug so you can confirm the module is updating in Play mode
        if (manager != null && manager.debugPaintings)
        {
            debugAccum += Time.deltaTime;
            if (debugAccum >= 1f)
            {
                debugAccum = 0f;
                Debug.Log($"[StatueEventModule] OnUpdate running. states={states.Count} inspectorList={(manager.statues!=null?manager.statues.Count.ToString():"null")}, sanity={manager.playerSanity:F1}");
            }
        }

        // Ensure we have states (auto-discover statues if manager.statues is empty)
        if (states.Count == 0)
            BuildStates();

        // If still no states found, nothing to update
        if (states.Count == 0) return;

        // If the manager has an explicit statues list and its size changed, resync states
        if (manager.statues != null && states.Count != manager.statues.Count)
            BuildStates();

        Camera cam = manager.GetPlayerCamera();
        if (cam == null) return;

        float dt = Time.deltaTime;
        float sanity = manager.playerSanity;

        // Replace the for-loop in OnUpdate(...) with this version that requires the player NOT look at the statue
        // before the head rotates. Rotation logic is unchanged otherwise.
        for (int i = 0; i < states.Count; i++)
        {
            var st = states[i];
            var statue = st.statue;
            if (statue == null) continue;
            var head = statue.GetHeadTransform();
            if (head == null) continue;

            Transform headParent = head.parent != null ? head.parent : statue.transform;

            // If sanity is high, smoothly return head to initial rotation
            if (manager.statueRequireNotSeen && manager.playerSanity >= manager.statueFollowSanityThreshold)
            {
                Quaternion curLocal = head.localRotation;
                Quaternion targetLocal = st.initialLocalRot;
                Quaternion next = Quaternion.RotateTowards(curLocal, targetLocal, returnSpeedDegPerSec * Time.deltaTime);
                head.localRotation = next;

                // update stored yaw (recompute relative to initial)
                Vector3 e = (next * Vector3.forward);
                var initForward = st.initialLocalRot * Vector3.forward;
                float yaw = Vector3.SignedAngle(new Vector3(initForward.x, 0f, initForward.z).normalized,
                                                new Vector3(e.x, 0f, e.z).normalized,
                                                Vector3.up);
                st.currentYawDeg = yaw;
                states[i] = st;
                continue;
            }

            // If the player is looking at the statue, do NOT rotate the head.
            bool playerLooking = false;
            if (manager.statueRequireNotSeen)
            {
                playerLooking = IsStatueVisibleToCamera(statue, cam, manager.paintingLookAngle);
                if (manager.debugPaintings) Debug.Log($"[StatueEventModule] '{statue.name}' playerLooking={playerLooking}");
            }
            else
            {
                if (manager.debugPaintings) Debug.Log($"[StatueEventModule] Visibility gating disabled (statueRequireNotSeen=false) for '{statue.name}'");
            }
            // When the player is looking, freeze the head and capture the current yaw so we don't snap when unobserved.
            if (playerLooking)
            {
                // compute current forward in parent's local space from the actual head.localRotation
                Vector3 curForwardParent = head.localRotation * Vector3.forward;
                Vector3 initForwardParent = st.initialLocalRot * Vector3.forward;

                // project to horizontal plane and guard against degenerate cases
                Vector3 flatCurDir = new Vector3(curForwardParent.x, 0f, curForwardParent.z);
                Vector3 flatInitDir = new Vector3(initForwardParent.x, 0f, initForwardParent.z);
                if (flatCurDir.sqrMagnitude > 1e-6f && flatInitDir.sqrMagnitude > 1e-6f)
                {
                    flatCurDir.Normalize();
                    flatInitDir.Normalize();
                    float yaw = Vector3.SignedAngle(flatInitDir, flatCurDir, Vector3.up);
                    st.currentYawDeg = Mathf.Clamp(yaw, -yawClampDegrees, yawClampDegrees);
                }

                // keep the current local rotation as-is (freeze) and save state
                states[i] = st;
                continue;
            }

            // --- Rotation logic unchanged (yaw-only toward player) ---
            Camera playerCam = manager.GetPlayerCamera();
            if (playerCam == null)
            {
                states[i] = st;
                continue;
            }

            Vector3 playerTargetWorld = playerCam.transform.position + playerCam.transform.forward * 1.5f;
            Vector3 targetLocalPoint = headParent.InverseTransformPoint(playerTargetWorld);
            Vector3 headLocalPoint = headParent.InverseTransformPoint(head.position);
            Vector3 toTargetLocal = targetLocalPoint - headLocalPoint;
            if (toTargetLocal.sqrMagnitude < 0.0001f)
            {
                states[i] = st;
                continue;
            }

            Vector3 initForwardLocal = st.initialLocalRot * Vector3.forward;
            Vector3 flatInitLocal = new Vector3(initForwardLocal.x, 0f, initForwardLocal.z);
            Vector3 flatTargetLocal = new Vector3(toTargetLocal.x, 0f, toTargetLocal.z);

            if (flatInitLocal.sqrMagnitude < 1e-6f || flatTargetLocal.sqrMagnitude < 1e-6f)
            {
                states[i] = st;
                continue;
            }

            flatInitLocal.Normalize();
            flatTargetLocal.Normalize();

            float desiredYaw = Vector3.SignedAngle(flatInitLocal, flatTargetLocal, Vector3.up);
            desiredYaw = Mathf.Clamp(desiredYaw, -yawClampDegrees, yawClampDegrees);

            float maxDelta = rotateSpeedDegPerSec * Time.deltaTime;
            float newYaw = Mathf.MoveTowards(st.currentYawDeg, desiredYaw, maxDelta);
            st.currentYawDeg = newYaw;

            Quaternion initialWorld = headParent.rotation * st.initialLocalRot;
            Quaternion yawWorld = Quaternion.AngleAxis(newYaw, headParent.up);
            Quaternion targetWorld = yawWorld * initialWorld;
            Quaternion newLocal = Quaternion.Inverse(headParent.rotation) * targetWorld;

            head.localRotation = newLocal;

            if (manager.debugPaintings) Debug.Log($"[StatueEventModule] {statue.name} desiredYaw={desiredYaw:F1} appliedYaw={newYaw:F1}");

            states[i] = st;
        }

        // TEMPORARY TEST: confirm module runs and force-rotate first statue with F9
        if (manager != null && manager.debugPaintings)
        {
            Debug.Log($"[StatueEventModule TEST] OnUpdate states={states.Count} playerSanity={manager.playerSanity:F1}");
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (states.Count == 0)
            {
                Debug.Log("[StatueEventModule TEST] No states available to test-rotate.");
            }
            else
            {
                var testState = states[0];
                if (testState.statue == null)
                {
                    Debug.Log("[StatueEventModule TEST] Test statue is null.");
                }
                else
                {
                    var head = testState.statue.GetHeadTransform();
                    if (head == null)
                    {
                        Debug.Log("[StatueEventModule TEST] Test statue headTransform is null.");
                    }
                    else
                    {
                        var camForTest = manager.GetPlayerCamera();
                        if (camForTest == null)
                        {
                            Debug.Log("[StatueEventModule TEST] Player camera is null for test-rotate.");
                        }
                        else
                        {
                            // Compute the exact yaw the module would apply, store it, then set head.localRotation
                            Transform headParent = head.parent != null ? head.parent : testState.statue.transform;
                            Vector3 playerTargetWorld = camForTest.transform.position + camForTest.transform.forward * 1.5f;
                            Vector3 targetLocalPoint = headParent.InverseTransformPoint(playerTargetWorld);
                            Vector3 headLocalPoint = headParent.InverseTransformPoint(head.position);
                            Vector3 toTargetLocal = targetLocalPoint - headLocalPoint;

                            if (toTargetLocal.sqrMagnitude < 1e-6f)
                            {
                                Debug.Log("[StatueEventModule TEST] toTargetLocal too small; cannot compute yaw.");
                            }
                            else
                            {
                                // compute desired yaw relative to stored initialLocalRot
                                Vector3 initForwardParent = testState.initialLocalRot * Vector3.forward;
                                Vector3 flatInit = new Vector3(initForwardParent.x, 0f, initForwardParent.z);
                                Vector3 flatTarget = new Vector3(toTargetLocal.x, 0f, toTargetLocal.z);
                                if (flatInit.sqrMagnitude < 1e-6f || flatTarget.sqrMagnitude < 1e-6f)
                                {
                                    Debug.Log("[StatueEventModule TEST] degenerate flat vectors; cannot compute yaw.");
                                }
                                else
                                {
                                    flatInit.Normalize();
                                    flatTarget.Normalize();
                                    float desiredYaw = Vector3.SignedAngle(flatInit, flatTarget, Vector3.up);
                                    desiredYaw = Mathf.Clamp(desiredYaw, -yawClampDegrees, yawClampDegrees);

                                    // store the yaw so the module won't snap it back next frame
                                    testState.currentYawDeg = desiredYaw;
                                    states[0] = testState;

                                    // apply the same rotation the module uses
                                    Quaternion initialWorld = headParent.rotation * testState.initialLocalRot;
                                    Quaternion yawWorld = Quaternion.AngleAxis(desiredYaw, headParent.up);
                                    Quaternion targetWorld = yawWorld * initialWorld;
                                    Quaternion newLocal = Quaternion.Inverse(headParent.rotation) * targetWorld;
                                    head.localRotation = newLocal;

                                    Debug.Log($"[StatueEventModule TEST] Forced rotate applied to '{testState.statue.name}' head='{head.name}' desiredYaw={desiredYaw:F1}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void BuildStates()
    {
        states.Clear();

        // Use inspector list if provided, otherwise auto-discover all Statue components in the scene
        List<Statue> sourceList = (manager.statues != null && manager.statues.Count > 0)
            ? manager.statues
            : new List<Statue>(Object.FindObjectsOfType<Statue>());

        if (sourceList == null || sourceList.Count == 0)
        {
            if (manager != null && manager.debugPaintings) Debug.Log("[StatueEventModule] No statues found to build states.");
            return;
        }

        foreach (var s in sourceList)
        {
            if (s == null) continue;
            var head = s.GetHeadTransform();
            var init = head != null ? head.localRotation : Quaternion.identity;
            states.Add(new State { statue = s, initialLocalRot = init, currentYawDeg = 0f });
        }

        if (manager != null && manager.debugPaintings) Debug.Log($"[StatueEventModule] Built {states.Count} statue states.");
    }

    // Add this helper method (place it in the class, e.g. below BuildStates).
    // It performs the camera->head check (angle + raycast) similar to Painting.IsPlayerLooking.
    private bool IsCameraLookingAt(Statue statue, Camera cam, float maxAngleDegrees)
    {
        if (statue == null || cam == null) return false;
        var head = statue.GetHeadTransform();
        if (head == null) return false;

        Vector3 camPos = cam.transform.position;
        Vector3 toHead = head.position - camPos;
        float distance = toHead.magnitude;
        if (distance <= 0.001f) return false;

        float angle = Vector3.Angle(cam.transform.forward, toHead);
        if (angle > maxAngleDegrees) return false;

        Ray ray = new Ray(camPos, toHead.normalized);
        if (Physics.Raycast(ray, out RaycastHit hit, distance + 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            var statueCollider = statue.GetComponent<Collider>();
            if (statueCollider != null)
            {
                return hit.collider == statueCollider || hit.collider.transform.IsChildOf(statue.transform) || statue.transform.IsChildOf(hit.collider.transform);
            }
            else
            {
                // no root collider: accept a hit on any child collider
                return hit.collider != null && hit.collider.transform.IsChildOf(statue.transform);
            }
        }

        // raycast missed -> not looking
        return false;
    }

    // Replace or add this helper in the same file (near the other helper methods).
    // Determines if any renderer on the statue is visible from the camera within angle threshold.
    // Uses frustum test + raycast to renderer center. Returns true if statue is considered looked-at.
    private bool IsStatueVisibleToCamera(Statue statue, Camera cam, float maxAngleDegrees)
    {
        if (statue == null || cam == null) return false;

        var renderers = statue.GetComponentsInChildren<Renderer>();
        // fallback to head if no renderers found
        List<Vector3> samplePoints = new List<Vector3>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                samplePoints.Add(r.bounds.center);
            }
        }
        else
        {
            var head = statue.GetHeadTransform();
            if (head != null) samplePoints.Add(head.position);
        }

        if (samplePoints.Count == 0) return false;

        Vector3 camPos = cam.transform.position;
        Vector3 camForward = cam.transform.forward;

        foreach (var sample in samplePoints)
        {
            // viewport test (quick reject if off-screen or behind camera)
            Vector3 vp = cam.WorldToViewportPoint(sample);
            if (vp.z <= 0f) continue;
            if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;

            // angle check to ensure roughly within the look cone
            Vector3 toSample = sample - camPos;
            float angle = Vector3.Angle(camForward, toSample);
            if (angle > maxAngleDegrees) continue;

            float dist = Mathf.Max(0.001f, toSample.magnitude);
            Vector3 dir = toSample.normalized;

            // Raycast to sample to detect occluders
            if (Physics.Raycast(new Ray(camPos, dir), out RaycastHit hit, dist + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && (hit.collider.transform.IsChildOf(statue.transform) || statue.transform.IsChildOf(hit.collider.transform)))
                {
                    // hit the statue (or a child) -> visible
                    return true;
                }

                // hit something else -> treat as occluded for this sample; try other samples
                continue;
            }

            // Raycast missed -> no occluder -> visible
            return true;
        }

        return false;
    }

    // debug helper
    private float debugAccum = 0f;
}
