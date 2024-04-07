using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UFOPlayer : MonoBehaviour
{
    [Header("Variables")]
    [SerializeField] float rotateSpeed;
    [SerializeField] int spawnOffset;
    [SerializeField] float heightChangeSpeed;

    [Header("Dependencies")]
    [SerializeField] Transform planetPosition;
    [SerializeField] Transform planetPivot;
    [SerializeField] DeathCone deathCone;
    [SerializeField] GameObject player;
    [SerializeField] GameObject playerCamera;
    [SerializeField] Transform pivot;
    int planetResolution;
    float planetRadius;
    Vector3 planetRotation;
    float spawnHeight;

    RaycastHit hit;

    private void OnEnable()
    {
        planetResolution = planetPosition.gameObject.GetComponent<MarchingCubesGPU>().resolution;
        pivot.position = Vector3.one * planetResolution / 2;
        planetRadius = planetPosition.gameObject.GetComponent<MarchingCubesGPU>().isoLevel;
        spawnHeight = planetResolution + spawnOffset;
        transform.position = new Vector3(planetResolution / 2, spawnHeight, planetResolution / 2);
        planetRotation = new Vector3(0, 0, 0);
    }

    private void Update()
    {
        //transform.position = new Vector3(transform.position.x, Mathf.Clamp(transform.position.y, spawnHeight, spawnHeight + (deathCone.distance / 2)), transform.position.z);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            print("shooting");
            if (Physics.Raycast(transform.position, -transform.up, out hit))
            {
                // Get the voxel the player is looking at
                print(hit.point);
            }
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            player.GetComponent<PlayerScript>().enabled = true;
            playerCamera.SetActive(true);
            gameObject.GetComponent<UFOPlayer>().enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W))
            planetRotation += Vector3.left * rotateSpeed;

        if (Input.GetKey(KeyCode.S))
            planetRotation += Vector3.right * rotateSpeed;

        if (Input.GetKey(KeyCode.A))
            planetRotation += Vector3.back * rotateSpeed;

        if (Input.GetKey(KeyCode.D))
            planetRotation += Vector3.forward * rotateSpeed;

        if (Input.GetKey(KeyCode.Q))
            transform.position += Vector3.up * heightChangeSpeed;

        if (Input.GetKey(KeyCode.E))
            transform.position += Vector3.down * heightChangeSpeed;

        pivot.eulerAngles = planetRotation;
    }
}
