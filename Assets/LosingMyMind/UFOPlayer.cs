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
    [SerializeField] GameObject playerHUD;
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
        if (Input.GetKeyDown(KeyCode.G))
        {
            player.GetComponent<PlayerScript>().enabled = true;
            playerCamera.SetActive(true);
            gameObject.GetComponent<UFOPlayer>().enabled = false;
            playerHUD.SetActive(true);
        }
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W))
            planetRotation += Vector3.right * rotateSpeed;

        if (Input.GetKey(KeyCode.S))
            planetRotation += Vector3.left * rotateSpeed;

        if (Input.GetKey(KeyCode.A))
            planetRotation += Vector3.forward * rotateSpeed;

        if (Input.GetKey(KeyCode.D))
            planetRotation += Vector3.back * rotateSpeed;

        pivot.eulerAngles = planetRotation;
    }
}
