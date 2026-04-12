using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlashlightRotator : MonoBehaviour
{
    public bool canMove = true;

    public float overshootFactor = 2f;  // How much the flashlight overshoots
    public float interpolationSpeed = 5f;  // How fast the flashlight returns to the intital rotation
    private Quaternion targetRotation;
    private Quaternion currentRotation;
    private Quaternion initialRotation;

    public float lookSpeed = 2f;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        if (canMove)
        {
            targetRotation = Quaternion.Euler(-Input.GetAxis("Mouse Y") * lookSpeed * overshootFactor, Input.GetAxis("Mouse X") * lookSpeed * overshootFactor, 0);
        }
        currentRotation = Quaternion.Slerp(currentRotation, targetRotation, Time.deltaTime * interpolationSpeed);

        transform.localRotation = initialRotation * currentRotation;
    }
}
