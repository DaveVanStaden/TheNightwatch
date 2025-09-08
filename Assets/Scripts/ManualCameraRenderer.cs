using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ManualCameraRenderer : MonoBehaviour
{
    public int fps;
    float elapsed;
    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed > 1f / fps)
        {
            elapsed = 0f;
            cam.Render();
        }
    }
}