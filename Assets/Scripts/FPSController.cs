using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 10f;


    public float lookSpeed = 2f;
    public float lookXLimit = 45f;

    public float interactionRange = 100f;
    private PlayerInputManager inputActions;


    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    public bool canMove = true;

    public bool inView = false;
    public int currentLayer = 0;

    public Interactable lastInteraction;
    private int interactionCount = 0; 


    CharacterController characterController;
    private RaycastHit hit;

    private void Awake()
    {
        inputActions = new PlayerInputManager();
    }
    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

    }

    void Update()
    {

        #region Handles Movment
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        // Press Left Shift to run
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float curSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (inputActions.Player.Interact.triggered && canMove)
        {
            Ray ray = GameObject.Find("RayCam").GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            //Debug.DrawRay(ray.origin, transform.forward, Color.green);
            if (Physics.Raycast(ray, out hit, interactionRange))
            {
                Debug.DrawLine(ray.origin, hit.point, Color.green);
                if (hit.collider.GetComponent<Interactable>())
                {
                    lastInteraction = hit.collider.GetComponent<Interactable>();
                    lastInteraction.DoTheThing();
                }
                else if (hit.collider.GetComponent<GrabPhone>())
                {
                    GrabPhone phone = hit.collider.GetComponent<GrabPhone>();
                    if (!phone.isAttatched)
                    {
                        StartCoroutine(phone.AttatchToFace());
                    }
                }
                //Debug.Log(hit.transform.name);
            }
        }
        if (Input.GetKeyDown(KeyCode.M) && canMove)
        {
            GrabPhone phone = FindObjectOfType<GrabPhone>();
            if (phone.isAttatched == true)
            {
                phone.SkipCall();
            }
        }
        if (inView)
        {
            if(!lastInteraction == false || interactionCount == 0)
            {
                if(interactionCount == 0) 
                    interactionCount++;
                if (!lastInteraction.busy)
                {
                    if (Input.GetKeyDown(KeyCode.W))
                    {
                        currentLayer = 1;
                        StopAllCoroutines();
                        StartCoroutine(lastInteraction.SetAngle(1));
                    }
                    if (Input.GetKeyDown(KeyCode.A))
                    {
                        currentLayer = 1;
                        StopAllCoroutines();
                        StartCoroutine(lastInteraction.SetAngle(2));
                    }
                    if (Input.GetKeyDown(KeyCode.D))
                    {
                        currentLayer = 1;
                        StopAllCoroutines();
                        StartCoroutine(lastInteraction.SetAngle(3));
                    }
                    if (Input.GetKeyDown(KeyCode.S))
                    {
                        if (currentLayer >= 1)
                        {
                            StopAllCoroutines();
                            StartCoroutine(lastInteraction.SetAngle(0));
                            currentLayer--;
                        }
                        else
                        {
                            StopAllCoroutines();
                            StartCoroutine(lastInteraction.LeaveTheThing());
                            lastInteraction = null;
                        }
                    }
                }
            }
            
        }

        #endregion

        #region Handles Jumping
        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        #endregion

        #region Handles Rotation
        characterController.Move(moveDirection * Time.deltaTime);

        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);


        }

        #endregion
    }
}