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
    int planetResolution;
    float planetRadius;
    Vector3 planetRotation;

    private void OnEnable()
    {
        planetResolution = planetPosition.gameObject.GetComponent<MarchingCubesGPU>().resolution;
        planetRadius = planetPosition.gameObject.GetComponent<MarchingCubesGPU>().isoLevel;
        float spawnHeight = planetResolution - planetRadius + spawnOffset;
        transform.position = new Vector3(0, spawnHeight, 0);
        planetRotation = new Vector3(0, 0, 0);
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

        if (Input.GetKey(KeyCode.Q))
            transform.position += Vector3.up * heightChangeSpeed;

        if (Input.GetKey(KeyCode.E))
            transform.position += Vector3.down * heightChangeSpeed;
        planetPivot.eulerAngles = planetRotation;
    }
}
