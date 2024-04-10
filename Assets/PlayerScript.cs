using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerScript : MonoBehaviour
{
    public static Action<Vector3, int> OnMarchingCubesEvent;

    [Header("Player variables")]
    [SerializeField] float movementSpeed;
    [SerializeField] float cameraRotationSpeedX;
    [SerializeField] float cameraRotationSpeedY;
    [SerializeField] Vector2 lookAngleMinMax;
    [SerializeField] float gravity;
    [SerializeField] float jumpForce;

    [Header("Terraforming")]
    [SerializeField] float rayLength;
    [SerializeField] float radius;
    [SerializeField] LayerMask terrainMask;

    float verticalLookRotation;
    Vector3 desiredLocalVelocity;
    RaycastHit hit;
    RaycastHit[] hits;
    [SerializeField] List<Vector3> terraFormingHits = new List<Vector3>();
    Vector3 smoothMoveVelocity;

    [Header("Dependencies")]
    [SerializeField] MarchingCubesGPU planetInfo;
    [SerializeField] CinemachineVirtualCamera vCamera;
    [SerializeField] Rigidbody rigidBody;
    [SerializeField] UFOPlayer ufo;

    private void Awake()
    {
        Time.fixedDeltaTime = 1f / 60f;
    }

    private void OnEnable()
    {
        transform.position = ufo.transform.position;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void TriggerMarchingCubesEvent(Vector3 voxel, int isoValue)
    {
        OnMarchingCubesEvent?.Invoke(voxel, isoValue);
    }

    private void Update()
    {
        // Calculate movement:
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(inputX, 0, inputY).normalized;
        Vector3 targetMoveVelocity = moveDir * movementSpeed;
        desiredLocalVelocity = Vector3.SmoothDamp(desiredLocalVelocity, targetMoveVelocity, ref smoothMoveVelocity, .15f);

        if (Input.GetKeyDown(KeyCode.Space))
            if (Physics.Raycast(transform.position, -transform.up, 1f))
                rigidBody.AddForce(transform.up * jumpForce, ForceMode.Impulse);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            print("shooting");
            if (Physics.Raycast(vCamera.transform.position + vCamera.transform.forward,
                                vCamera.transform.forward, out hit, 1000, terrainMask))
            {
                TriggerMarchingCubesEvent(hit.point, 1000);
                terraFormingHits.Add(hit.point);
            }
        }

        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            print("shooting");
            if (Physics.Raycast(vCamera.transform.position + vCamera.transform.forward,
                                vCamera.transform.forward, out hit, 1000, terrainMask))
            {
                TriggerMarchingCubesEvent(hit.point, 0);
                terraFormingHits.Add(hit.point);
            }
        }
    }

    void OnDrawGizmos()
    {
        Vector3 rayDirection = vCamera.transform.forward * rayLength;

        Gizmos.DrawLine(vCamera.transform.position, vCamera.transform.position + rayDirection);

        foreach (var item in terraFormingHits)
        {
            Gizmos.DrawSphere(item, .5f);
        }

        if (hits != null)
        {
            Gizmos.color = Color.red;
            foreach (var hit in hits)
            {
                Gizmos.DrawSphere(hit.point, 0.5f); // Draw a small sphere at each hit point
            }

            // Draw the sphere cast
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(vCamera.transform.position + vCamera.transform.forward, 5f);
        }
    }

    private void FixedUpdate()
    {
        // Look rotation:
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * cameraRotationSpeedX);
        verticalLookRotation += Input.GetAxis("Mouse Y") * cameraRotationSpeedX;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, lookAngleMinMax.x, lookAngleMinMax.y);
        vCamera.transform.localEulerAngles = Vector3.left * verticalLookRotation;

        Vector3 gravityDirection = (Vector3.one * planetInfo.resolution / 2 - transform.position).normalized;

        rigidBody.AddForce(gravityDirection * gravity);

        // Calculate the rotation quaternion to align the capsule's -transform.up with the direction vector
        Quaternion rotation = Quaternion.FromToRotation(-transform.up, gravityDirection);

        // Apply the rotation to the capsule
        transform.rotation = rotation * transform.rotation;

        Vector3 localUp = LocalToWorldVector(rigidBody.rotation, Vector3.up);
        rigidBody.velocity = CalculateNewVelocity(localUp);
    }

    Vector3 CalculateNewVelocity(Vector3 localUp)
    {
        // Apply movement and gravity to rigidbody
        float deltaTime = Time.fixedDeltaTime;
        Vector3 currentLocalVelocity = WorldToLocalVector(rigidBody.rotation, rigidBody.velocity);

        float localYVelocity = currentLocalVelocity.y + (-gravity) * deltaTime;

        Vector3 desiredGlobalVelocity = LocalToWorldVector(rigidBody.rotation, desiredLocalVelocity);
        desiredGlobalVelocity += localUp * localYVelocity;
        return desiredGlobalVelocity;
    }

    // Transform vector from local space to world space (based on rotation)
    public static Vector3 LocalToWorldVector(Quaternion rotation, Vector3 vector)
    {
        return rotation * vector;
    }

    // Transform vector from world space to local space (based on rotation)
    public static Vector3 WorldToLocalVector(Quaternion rotation, Vector3 vector)
    {
        return Quaternion.Inverse(rotation) * vector;
    }
}
