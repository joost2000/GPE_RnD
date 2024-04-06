using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class PlayerScript : MonoBehaviour
{
    public static Action<Vector3> OnMarchingCubesEvent;

    [Header("Player variables")]
    [SerializeField] float movementSpeed;
    [SerializeField] float cameraRotationSpeedX;
    [SerializeField] float cameraRotationSpeedY;
    [SerializeField] Vector2 lookAngleMinMax;
    [SerializeField] float gravity;

    [Header("Terraforming")]
    [SerializeField] float rayLength;
    [SerializeField] float radius;

    float verticalLookRotation;
    Vector3 desiredLocalVelocity;
    Vector3 smoothDampVelocity;
    RaycastHit hit;
    List<Vector3> terraFormingHits = new List<Vector3>();

    [Header("Dependencies")]
    [SerializeField] MarchingCubesGPU planetInfo;
    [SerializeField] CinemachineVirtualCamera vCamera;
    [SerializeField] Rigidbody rigidBody;
    [SerializeField] UFOPlayer ufo;

    private void OnEnable()
    {
        transform.position = ufo.transform.position;
        print(transform.position);
        print(ufo.transform.position);
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void TriggerMarchingCubesEvent(Vector3 voxel)
    {
        OnMarchingCubesEvent?.Invoke(voxel);
    }

    private void Update()
    {
        // Calculate movement:
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(inputX, 0, inputY).normalized;
        Vector3 targetMoveVelocity = moveDir * movementSpeed;
        desiredLocalVelocity = Vector3.SmoothDamp(desiredLocalVelocity, targetMoveVelocity, ref smoothDampVelocity, .15f);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            print("shooting");

            if (Physics.Raycast(vCamera.transform.position, vCamera.transform.forward, out hit))
            {
                MeshCollider meshCollider = hit.collider as MeshCollider;
                if (meshCollider == null || meshCollider.sharedMesh == null)
                    return;

                Mesh mesh = meshCollider.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;

                // Get the local positions of the vertices of the hit triangle
                Vector3 v0 = vertices[triangles[hit.triangleIndex * 3 + 0]];
                Vector3 v1 = vertices[triangles[hit.triangleIndex * 3 + 1]];
                Vector3 v2 = vertices[triangles[hit.triangleIndex * 3 + 2]];

                // Interpolate the local position of the hit point
                Vector3 hitPointLocal = (1 - hit.barycentricCoordinate.x - hit.barycentricCoordinate.y) * v0
                    + hit.barycentricCoordinate.x * v1
                    + hit.barycentricCoordinate.y * v2;

                print(hitPointLocal);
                terraFormingHits.Add(hitPointLocal);
                TriggerMarchingCubesEvent(hitPointLocal);
            }
        }
    }

    void OnDrawGizmos()
    {
        Vector3 rayDirection = vCamera.transform.forward * rayLength;

        Gizmos.DrawLine(vCamera.transform.position, vCamera.transform.position + rayDirection);

        foreach (var item in terraFormingHits)
        {
            Gizmos.DrawSphere(item - (Vector3.one * (planetInfo.resolution / 2)), 1f);
        }

    }

    private void FixedUpdate()
    {
        // Look rotation:
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * cameraRotationSpeedX);
        verticalLookRotation += Input.GetAxis("Mouse Y") * cameraRotationSpeedY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, lookAngleMinMax.x, lookAngleMinMax.y);
        vCamera.transform.localEulerAngles = Vector3.left * verticalLookRotation;

        Vector3 planetCentre = Vector3.zero;
        Vector3 gravityUp = (rigidBody.position - planetCentre).normalized;

        // Align body's up axis with the centre of planet
        Vector3 localUp = rigidBody.rotation * Vector3.up;
        rigidBody.rotation = Quaternion.FromToRotation(localUp, gravityUp) * rigidBody.rotation;

        Vector3 currentLocalVelocity = Quaternion.Inverse(rigidBody.rotation) * rigidBody.velocity;

        float localYVelocity = currentLocalVelocity.y - gravity;

        Vector3 desiredGlobalVelocity = rigidBody.rotation * desiredLocalVelocity;
        desiredGlobalVelocity += localUp * localYVelocity;
        rigidBody.velocity = desiredGlobalVelocity;
    }
}
