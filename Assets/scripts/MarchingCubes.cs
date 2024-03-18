using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PointData
{
    public Vector3 position;
    public float densityValue;
    public PointData(Vector3 _pos, float _densityVal)
    {
        position = _pos;
        densityValue = _densityVal;
    }
}

public class MarchingCubes : MonoBehaviour
{
    public int cubeSize;
    [Range(0.1f, 1f)]
    public float gizmoSize;
    private int width, height;
    [Range(0.1f, 2)]
    public float noiseScale;

    public float activationThreshold;
    public List<PointData> vertices = new List<PointData>();

    private void Start()
    {
        Grid();
        MarchThroughGrid();
    }

    void Grid()
    {
        width = cubeSize / 2;
        height = cubeSize / 2;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    vertices.Add(new PointData(new Vector3(x, y, z), Mathf.PerlinNoise((float)z / width, (float)y / height) * noiseScale));
                }
            }
        }
    }

    void MarchThroughGrid()
    {
        List<PointData> cube = new List<PointData>();
        List<Vector3> edges = new List<Vector3>();
        byte byteCube = 0b00000000;
        for (int i = 0; i < 1; i++)
        {
            cube.Add(vertices[i]);
            cube.Add(vertices[i + 1]);
            cube.Add(vertices[i + (cubeSize / 2 * cubeSize / 2)]);
            cube.Add(vertices[i + (cubeSize / 2 * cubeSize / 2) + 1]);
            cube.Add(vertices[i + width]);
            cube.Add(vertices[i + width + 1]);
            cube.Add(vertices[i + (cubeSize / 2 * cubeSize / 2) + width]);
            cube.Add(vertices[i + (cubeSize / 2 * cubeSize / 2) + width + 1]);
        }

        int index = 0;
        foreach (var item in cube)
        {
            if (item.densityValue > activationThreshold)
            {
                print($"shifting bit | {index}");
                // Activate the corresponding bit
                byteCube |= (byte)(1 << index++);
            }
        }
        print(byteCube);
        print(MarchingCubesTables.triTable[byteCube][0]);
    }

    private void OnDrawGizmosSelected()
    {
        foreach (var item in vertices)
        {
            Gizmos.DrawSphere(item.position, gizmoSize);
            Gizmos.color = new Color(item.densityValue, item.densityValue, item.densityValue);
        }
    }
}
