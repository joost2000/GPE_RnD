using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Cube
{
    public Vector3 position;
    public Color color;
}

public class ComputeShaderStuff : MonoBehaviour
{

    [SerializeField] ComputeShader computeShader;
    ComputeBuffer buffer;
    public Cube[] data;
    public List<Cube> cubeInfo = new List<Cube>();

    public List<GameObject> cubes = new List<GameObject>();

    private int size = 20;

    public void CreateCubes(int x, int y)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(x, y, UnityEngine.Random.Range(0.5f, -0.5f));
        Color color = UnityEngine.Random.ColorHSV();
        cube.GetComponent<Renderer>().material.color = color;

        cubes.Add(cube);
        cubeInfo.Add(new Cube
        {
            position = cube.transform.position,
            color = cube.GetComponent<Renderer>().material.color
        });

    }

    public void OnRandomizeGPU()
    {
        int colorSize = sizeof(float) * 4;
        int vectorSize = sizeof(float) * 3;
        int stride = colorSize + vectorSize;

        ComputeBuffer buffer = new ComputeBuffer(cubes.Count, stride);
        buffer.SetData(cubeInfo.ToArray());
        computeShader.SetBuffer(0, "cubes", buffer);
        computeShader.SetFloat("resolution", cubes.Count);
        computeShader.Dispatch(0, cubes.Count / 10, 1, 1);

        buffer.GetData(data);

        for (int i = 0; i < cubes.Count; i++)
        {
            GameObject obj = cubes[i];
            Cube cube = data[i];
            obj.transform.position = cube.position;
            obj.GetComponent<Renderer>().material.color = cube.color;
        }
        buffer.Dispose();
    }

    private void Awake()
    {
        data = new Cube[size * size];

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                CreateCubes(i, j);
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnRandomizeGPU();
        }
    }
}
