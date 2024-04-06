using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlayerScript : MonoBehaviour
{
    public float speed = 5.0f; // Player's speed
    public float maxSpeed;
    public Rigidbody rb; // Player's Rigidbody
    public Transform planet; // The planet the player is on
    public float rotateSpeed; // The speed at which the player rotates
    public Transform pivotTransform; // The pivot point for the player's rotation
    public float gravityMultiplier; // The strength of the gravity
    public MarchingCubesGPU marchingCubesGPU;

    private float rotationX = 0.0f;
    private float rotationY = 0.0f;

    private void OnEnable()
    {
        transform.position = new Vector3(marchingCubesGPU.resolution / 2, marchingCubesGPU.resolution, marchingCubesGPU.resolution / 2);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        rb.velocity = new Vector3(
            Mathf.Clamp(rb.velocity.x, -0, maxSpeed),
            Mathf.Clamp(rb.velocity.y, -0, maxSpeed),
            Mathf.Clamp(rb.velocity.z, -0, maxSpeed)
            );

        Vector3 currentRotation = pivotTransform.transform.rotation.eulerAngles;
        currentRotation.x = Mathf.Clamp(currentRotation.x, 20, 50);
        pivotTransform.transform.rotation = Quaternion.Euler(currentRotation);
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W))
        {
            rb.AddForce(Camera.main.transform.forward * speed, ForceMode.VelocityChange);
        }

        // Rotate the camera based on mouse movement
        rotationX -= Input.GetAxis("Mouse Y") * rotateSpeed;
        rotationY += Input.GetAxis("Mouse X") * rotateSpeed;

        // Clamp the rotation of the camera along the X axis to avoid flipping
        rotationX = Mathf.Clamp(rotationX, 20, 120f);

        // Apply rotation to the camera
        //pivotTransform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);

        // Calculate the direction of the gravity
        Vector3 planetCenter = planet.position + new Vector3(marchingCubesGPU.resolution / 2, marchingCubesGPU.resolution / 2, marchingCubesGPU.resolution / 2);
        Vector3 gravityDirection = (planetCenter - transform.position).normalized;

        // Apply the gravity to the player's Rigidbody
        rb.AddForce(9.81f * gravityMultiplier * -transform.up);

        // Rotate the player to align with the planet's surface
        Quaternion toRotation = Quaternion.FromToRotation(-transform.up, gravityDirection) * transform.rotation;
        transform.rotation = toRotation;
    }
}
