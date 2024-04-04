using System;
using UnityEngine;

[Serializable]
struct Triangle
{
    public Vector3 p1;
    public Vector3 p2;
    public Vector3 p3;
}
[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
public class MarchingCubesGPU : MonoBehaviour
{
    [Header("Variables")]
    public int resolution = 32;
    public float isoLevel;

    [Header("ComputeShader")]
    public ComputeShader computeShader;
    public ComputeShader computeTriangles;

    [Header("Debugging")]
    Triangle[] tris;
    Vector4[] voxelData;
    Vector4[] testing;

    ComputeBuffer triangleBuffer;
    ComputeBuffer voxelDataBuffer;
    ComputeBuffer positionalDataBuffer;
    ComputeBuffer triCountBuffer;

    private void Start()
    {
        MakeVoxelData();
        CalculateTris();
        SetMesh();
    }

    void SetMesh()
    {
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var vertices = new Vector3[tris.Length * 3];
        var meshTriangles = new int[tris.Length * 3];

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < tris.Length; i++)
        {
            vertices[vertexIndex] = tris[i].p1;
            vertices[vertexIndex + 1] = tris[i].p2;
            vertices[vertexIndex + 2] = tris[i].p3;

            meshTriangles[triangleIndex] = vertexIndex;
            meshTriangles[triangleIndex + 1] = vertexIndex + 1;
            meshTriangles[triangleIndex + 2] = vertexIndex + 2;

            vertexIndex += 3;
            triangleIndex += 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();
        gameObject.GetComponent<MeshFilter>().mesh = mesh;
    }

    void MakeVoxelData()
    {
        voxelDataBuffer = new ComputeBuffer(resolution * resolution * resolution, sizeof(float) * 4, ComputeBufferType.Default);

        voxelDataBuffer.SetData(new Vector4[resolution * resolution * resolution]);

        computeShader.SetBuffer(0, "voxelData", voxelDataBuffer);
        computeShader.SetInt("size", resolution);
        computeShader.Dispatch(0, resolution / 8, resolution / 8, resolution / 8);

        voxelData = new Vector4[resolution * resolution * resolution];

        voxelDataBuffer.GetData(voxelData);

        voxelDataBuffer.Release();
    }

    void CalculateTris()
    {
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        positionalDataBuffer = new ComputeBuffer(resolution * resolution * resolution, sizeof(float) * 4, ComputeBufferType.Default);
        positionalDataBuffer.SetData(voxelData);

        triangleBuffer = new ComputeBuffer(5 * resolution * resolution * resolution, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triangleBuffer.SetCounterValue(0);

        computeTriangles.SetBuffer(0, "triangleBuffer", triangleBuffer);
        computeTriangles.SetBuffer(0, "positionalData", positionalDataBuffer);
        computeTriangles.SetInt("size", resolution);
        computeTriangles.SetFloat("isoLevel", isoLevel);
        computeTriangles.Dispatch(0, resolution / 8, resolution / 8, resolution / 8);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

        testing = new Vector4[resolution * resolution * resolution];
        positionalDataBuffer.GetData(testing);

        positionalDataBuffer.Release();
        triangleBuffer.Release();
        triCountBuffer.Release();
    }

    private void OnDrawGizmos()
    {
        if (resolution > 16)
        {
            return;
        }
        foreach (var item in voxelData)
        {
            if (item.w < isoLevel)
            {
                Gizmos.DrawSphere(item, 0.1f);
            }
        }
    }
}
