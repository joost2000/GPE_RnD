using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

[Serializable]
struct VoxelData
{
    public int chunkId;
    public Vector4[] voxelData;
}

[Serializable]
public struct Triangle
{
    public Vector3 p1;
    public Vector3 p2;
    public Vector3 p3;
}
[Serializable]
struct ChunkData
{
    public List<Triangle> chunk;
}

[Serializable]
struct ChunkVertices
{
    public Vector4[] data;
}

[Serializable]
struct TriangleCollection
{
    public Triangle[] data;
}

[Serializable]
struct ChunkBeginEnd
{
    public Vector2Int xBeginEnd;
    public Vector2Int yBeginEnd;
    public Vector2Int zBeginEnd;
    public GameObject chunk;
}

[Serializable]
struct testing
{
    public Vector2 xBeginEnd;
    public Vector2 yBeginEnd;
    public Vector2 zBeginEnd;
}

enum AmountOfChunks
{
    Chunk2 = 2,
    Chunk4 = 4,
    Chunk8 = 8,
    Chunk16 = 16,
    Chunk32 = 32,
    Chunk64 = 64,
    Chunk128 = 128
}

enum Resolution
{
    Resolution16 = 16,
    Resolution32 = 32,
    Resolution64 = 64,
    Resolution128 = 128,
    Resolution256 = 256,
    Resolution512 = 512
}

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
public class MarchingCubesGPU : MonoBehaviour
{
    [Header("Variables")]
    public int resolution;
    public float isoLevel;
    [SerializeField] AmountOfChunks amountOfChunks;
    [SerializeField] Material material;
    [SerializeField] bool visualizeGizmos;
    [SerializeField] GameObject prefab;

    [Header("Noise variables")]
    [SerializeField] float noiseFrequency;
    [SerializeField] float heightScale;

    [Header("Gameplay")]
    [SerializeField] GameObject planetPivot;
    [SerializeField] Transform planetContainer;

    [Header("ComputeShader")]
    public ComputeShader computeVoxelData;
    public ComputeShader computeTriangles;
    public ComputeShader cubeMarchingChunks;

    [Header("Debugging")]
    public Triangle[] tris;
    public Vector4[] dataFromJSON;
    [SerializeField] List<TriangleCollection> triangleChunks = new List<TriangleCollection>();
    List<ChunkVertices> chunkedVoxelData = new List<ChunkVertices>();

    ComputeBuffer triangleBuffer;
    ComputeBuffer positionalDataBuffer;
    ComputeBuffer triCountBuffer;
    ComputeBuffer voxelDataBuffer;

    private void Start()
    {
        StartCoroutine(CreateVoxelDataGPU());

        for (int i = 0; i < (int)Mathf.Pow((int)amountOfChunks, 3); i++)
        {
            CreateMeshes(i);
        }
        DisposeAllBuffers();
    }

    void CreateMeshes(int index)
    {
        GameObject gameObj = new GameObject("Chunk");
        MeshFilter filter = gameObj.AddComponent<MeshFilter>();
        MeshRenderer renderer = gameObj.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = material;

        int chunkSize = resolution / (int)amountOfChunks;
        int totalChunkSize = chunkSize * chunkSize * chunkSize;

        dataFromJSON = JsonUtility.FromJson<VoxelData>(System.IO.File.ReadAllText(Application.persistentDataPath + $"/VoxelData/chunk{index}.json")).voxelData;

        ComputeBuffer chunkDataBuffer = new ComputeBuffer(dataFromJSON.Length, sizeof(float) * 4, ComputeBufferType.Default);
        ComputeBuffer triangleBuffer = new ComputeBuffer(dataFromJSON.Length * 5, sizeof(float) * 3, ComputeBufferType.Append);
        ComputeBuffer triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        chunkDataBuffer.SetData(dataFromJSON);

        triangleBuffer.SetCounterValue(0);

        cubeMarchingChunks.SetBuffer(0, "voxelData", chunkDataBuffer);
        cubeMarchingChunks.SetBuffer(0, "triangleData", triangleBuffer);
        cubeMarchingChunks.SetInt("chunkSize", chunkSize);
        cubeMarchingChunks.SetFloat("isoLevel", isoLevel);
        cubeMarchingChunks.Dispatch(0, totalChunkSize / 8, 1, 1);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        tris = new Triangle[numTris];

        triangleBuffer.GetData(tris);

        chunkDataBuffer.Dispose();
        triangleBuffer.Dispose();

        Mesh _mesh = new Mesh();

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

        _mesh.vertices = vertices;
        _mesh.triangles = meshTriangles;
        filter.sharedMesh = _mesh;
    }

    void DisposeAllBuffers()
    {
        triangleBuffer?.Dispose();
        positionalDataBuffer?.Dispose();
        triCountBuffer?.Dispose();
        voxelDataBuffer?.Dispose();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            chunkedVoxelData.RemoveAt(chunkedVoxelData.Count - 1);
        }
    }

    IEnumerator CreateVoxelDataGPU()
    {
        int totalSize = resolution * resolution * resolution;
        int chunkSize = resolution / (int)amountOfChunks;
        int totalChunkSize = chunkSize * chunkSize * chunkSize;
        int totalAmountOfChunks = (int)amountOfChunks * (int)amountOfChunks * (int)amountOfChunks;
        int chunkIndex = 0;

        for (int x = 0; x < (int)amountOfChunks; x++)
        {
            for (int y = 0; y < (int)amountOfChunks; y++)
            {
                for (int z = 0; z < (int)amountOfChunks; z++)
                {
                    voxelDataBuffer = new ComputeBuffer(totalChunkSize, sizeof(float) * 4, ComputeBufferType.Structured);

                    voxelDataBuffer.SetData(new Vector4[totalChunkSize]);
                    computeVoxelData.SetBuffer(0, "voxelData", voxelDataBuffer);
                    computeVoxelData.SetFloat("size", resolution);
                    computeVoxelData.SetFloat("chunkSize", chunkSize);

                    //setting the min and max dists
                    computeVoxelData.SetFloats("xBeginEnd", chunkSize * x, chunkSize * (x + 1));
                    computeVoxelData.SetFloats("yBeginEnd", chunkSize * y, chunkSize * (y + 1));
                    computeVoxelData.SetFloats("zBeginEnd", chunkSize * z, chunkSize * (z + 1));

                    computeVoxelData.Dispatch(0, resolution / 8, resolution / 8, resolution / 8);

                    Vector4[] _ = new Vector4[totalChunkSize];
                    voxelDataBuffer.GetData(_);

                    VoxelData voxels = new VoxelData
                    {
                        chunkId = chunkIndex,
                        voxelData = _
                    };

                    System.IO.File.WriteAllText(Application.persistentDataPath + $"/VoxelData/chunk{chunkIndex}.json", JsonUtility.ToJson(voxels));
                    chunkIndex++;
                    chunkedVoxelData.Add(new ChunkVertices { data = _ });

                    //GameObject cube = Instantiate(prefab);

                    //cube.transform.localScale = Vector3.one * chunkSize;

                    Vector3 center = new Vector3((_[0].x + _[_.Length - 1].x) / 2, (_[0].y + _[_.Length - 1].y) / 2, (_[0].z + _[_.Length - 1].z) / 2);
                    //cube.transform.position = center;

                    voxelDataBuffer.Release(); // Release voxelDataBuffer after each iteration

                    yield return new WaitForSecondsRealtime(0.001f);
                }
            }
        }
        voxelDataBuffer.Dispose();
        yield return null;
    }

    IEnumerator SendChunkToGPU(Vector4[] chunkData, int chunkSize)
    {
        Vector2 xBeginEnd = new Vector2(chunkData[0].x, chunkData[chunkData.Length - 1].x);
        Vector2 yBeginEnd = new Vector2(chunkData[0].y, chunkData[chunkData.Length - 1].y);
        Vector2 zBeginEnd = new Vector2(chunkData[0].z, chunkData[chunkData.Length - 1].z);

        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        voxelDataBuffer = new ComputeBuffer(chunkData.Length, sizeof(float) * 4, ComputeBufferType.Default);
        voxelDataBuffer.SetData(chunkData);

        triangleBuffer = new ComputeBuffer(chunkData.Length * 5, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triangleBuffer.SetCounterValue(0);

        // set the buffers in our compute shader
        computeTriangles.SetBuffer(0, "positionalData", voxelDataBuffer);
        computeTriangles.SetBuffer(0, "triangleBuffer", triangleBuffer);

        // set the variables of our compute shader
        computeTriangles.SetFloat("isoLevel", isoLevel);
        computeTriangles.SetInt("chunkSize", chunkSize);

        //setting the min and max dists
        computeVoxelData.SetFloats("xBeginEnd", xBeginEnd.x, xBeginEnd.y);
        computeVoxelData.SetFloats("yBeginEnd", yBeginEnd.x, yBeginEnd.y);
        computeVoxelData.SetFloats("zBeginEnd", zBeginEnd.x, zBeginEnd.y);

        // Dispatch
        computeTriangles.Dispatch(0, resolution / 8, resolution / 8, resolution / 8);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData(tris);

        triangleChunks.Add(new TriangleCollection { data = tris });

        StartCoroutine(MakeMesh(tris));

        voxelDataBuffer.Release();
        triangleBuffer.Release();
        triCountBuffer.Release();

        yield return null;
    }

    IEnumerator MakeMesh(Triangle[] triangles)
    {
        var mesh = new Mesh();
        var vertices = new Vector3[triangles.Length * 3];
        var meshTriangles = new int[triangles.Length * 3];

        // Create a new GameObject
        GameObject newGameObject = new GameObject("Chunk");

        newGameObject.transform.parent = planetContainer;

        // Add a MeshFilter
        MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();

        // Add a MeshRenderer
        MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();

        MeshCollider meshCollider = newGameObject.AddComponent<MeshCollider>();

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < triangles.Length; i++)
        {
            vertices[vertexIndex] = triangles[i].p3;
            vertices[vertexIndex + 1] = triangles[i].p2;
            vertices[vertexIndex + 2] = triangles[i].p1;

            meshTriangles[triangleIndex] = vertexIndex;
            meshTriangles[triangleIndex + 1] = vertexIndex + 1;
            meshTriangles[triangleIndex + 2] = vertexIndex + 2;

            vertexIndex += 3;
            triangleIndex += 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
        meshRenderer.material = material;
        //meshCollider.sharedMesh = mesh;

        yield return null;
    }

    void OnEnable()
    {
        //PlayerScript.OnMarchingCubesEvent += UpdateMesh;
    }

    void OnDisable()
    {
        //PlayerScript.OnMarchingCubesEvent -= UpdateMesh;
    }

    int indexFromCoord(int x, int y, int z)
    {
        return z * resolution * resolution + y * resolution + x;
    }
    List<Vector4> MakeVoxelDataCPU()
    {
        List<Vector4> data = new List<Vector4>();
        Vector3 center = Vector3.one * (resolution / 2);
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    float value = SphereShape(x, y, z, center);
                    data.Add(new Vector4(x, y, z, value));
                }
            }
        }
        return data;
    }

    private float SphereShape(float x, float y, float z, Vector3 center)
    {
        float distance = Vector3.Distance(new Vector3(x, y, z), center);
        // Add noise to the distance
        float noise = GenerateNoise(new Vector3(x, y, z), noiseFrequency, heightScale);
        return distance -= noise;

    }

    private float GenerateNoise(Vector3 position, float scale, float height)
    {
        int randomOffset = UnityEngine.Random.Range(0, 100);
        float x = position.x * scale;
        float y = position.y * scale;
        float z = position.z * scale;
        return (Mathf.PerlinNoise(x, y) * height) + (Mathf.PerlinNoise(y, z) * height) + (Mathf.PerlinNoise(z, x) * height) / 3;
    }

    private void OnDrawGizmos()
    {
        if (!visualizeGizmos)
            return;
    }
}
