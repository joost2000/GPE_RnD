using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

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

    [Header("Debugging")]
    Triangle[] tris;
    [SerializeField] List<TriangleCollection> triangleChunks = new List<TriangleCollection>();
    List<ChunkBeginEnd> chunkBeginEnds = new List<ChunkBeginEnd>();
    Vector4[] voxelData;
    [SerializeField] List<ChunkVertices> chunkedVoxelData = new List<ChunkVertices>();
    Vector4[] testing;

    ComputeBuffer triangleBuffer;
    ComputeBuffer positionalDataBuffer;
    ComputeBuffer triCountBuffer;
    ComputeBuffer voxelDataBuffer;
    ComputeBuffer indexAndValueBuffer;
    ComputeBuffer indexAndValueCount;

    private void Start()
    {
        StartCoroutine(CreateVoxelDataGPU());
        DisposeAllBuffers();
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

                    chunkedVoxelData.Add(new ChunkVertices { data = _ });

                    GameObject cube = Instantiate(prefab);

                    cube.transform.localScale = Vector3.one * chunkSize;

                    Vector3 center = new Vector3((_[0].x + _[_.Length - 1].x) / 2, (_[0].y + _[_.Length - 1].y) / 2, (_[0].z + _[_.Length - 1].z) / 2);
                    cube.transform.position = center;

                    voxelDataBuffer.Release(); // Release voxelDataBuffer after each iteration

                    //StartCoroutine(SendChunkToGPU(_, chunkSize));

                    yield return new WaitForSecondsRealtime(0.01f);
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
        PlayerScript.OnMarchingCubesEvent += UpdateMesh;
    }

    void OnDisable()
    {
        PlayerScript.OnMarchingCubesEvent -= UpdateMesh;
    }

    int indexFromCoord(int x, int y, int z)
    {
        return z * resolution * resolution + y * resolution + x;
    }

    void UpdateMesh(Vector3 voxel, int updatedIsoValue)
    {
        Vector3[] cubeHit = {
            voxel + Vector3.left,
            voxel + Vector3.right,
            voxel + Vector3.forward,
            voxel + Vector3.back,
            voxel + Vector3.up,
            voxel + Vector3.down,
            voxel + Vector3.left + Vector3.up,
            voxel + Vector3.right + Vector3.up,
            voxel + Vector3.forward + Vector3.up,
            voxel + Vector3.back + Vector3.up,
            voxel + Vector3.left - Vector3.up,
            voxel + Vector3.right - Vector3.up,
            voxel + Vector3.forward - Vector3.up,
            voxel + Vector3.back - Vector3.up,
            voxel
        };

        foreach (var item in cubeHit)
            voxelData[indexFromCoord((int)item.z, (int)item.y, (int)item.x)].w = updatedIsoValue;

        GameObject chunkToUpdate = null;
        int index = indexFromCoord((int)voxel.z, (int)voxel.y, (int)voxel.x);
        voxelData[index].w = updatedIsoValue;

        for (int i = 0; i < chunkBeginEnds.Count; i++)
        {
            if (voxelData[index].z >= chunkBeginEnds[i].xBeginEnd.x && voxelData[index].z < chunkBeginEnds[i].xBeginEnd.y)
            {
                if (voxelData[index].y >= chunkBeginEnds[i].yBeginEnd.x && voxelData[index].y < chunkBeginEnds[i].yBeginEnd.y)
                {
                    if (voxelData[index].x >= chunkBeginEnds[i].zBeginEnd.x && voxelData[index].x < chunkBeginEnds[i].zBeginEnd.y)
                    {
                        CalculateTris(chunkBeginEnds[i].xBeginEnd.x,
                        chunkBeginEnds[i].xBeginEnd.y,
                        chunkBeginEnds[i].yBeginEnd.x,
                        chunkBeginEnds[i].yBeginEnd.y,
                        chunkBeginEnds[i].zBeginEnd.x,
                        chunkBeginEnds[i].zBeginEnd.y);

                        chunkToUpdate = chunkBeginEnds[i].chunk;
                        print(i);
                        break;
                    }
                }
            }
        }

        var vertices = new Vector3[tris.Length * 3];
        var meshTriangles = new int[tris.Length * 3];

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < tris.Length; i++)
        {
            vertices[vertexIndex] = tris[i].p3;
            vertices[vertexIndex + 1] = tris[i].p2;
            vertices[vertexIndex + 2] = tris[i].p1;

            meshTriangles[triangleIndex] = vertexIndex;
            meshTriangles[triangleIndex + 1] = vertexIndex + 1;
            meshTriangles[triangleIndex + 2] = vertexIndex + 2;

            vertexIndex += 3;
            triangleIndex += 3;
        }
        MeshFilter meshToUpdate = chunkToUpdate.GetComponent<MeshFilter>();
        MeshCollider meshColliderToUpdate = chunkToUpdate.GetComponent<MeshCollider>();
        meshToUpdate.mesh.Clear();
        meshToUpdate.mesh.vertices = vertices;
        meshToUpdate.mesh.triangles = meshTriangles;
        meshToUpdate.mesh.RecalculateNormals();
        meshColliderToUpdate.sharedMesh = meshToUpdate.mesh;
    }

    void CreateChunks()
    {

        for (int x = 0; x < (int)amountOfChunks / 4; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < (int)amountOfChunks / 4; z++)
                {
                    var stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();
                    Vector2Int _xBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * x, resolution / ((int)amountOfChunks / 4) * (x + 1));
                    Vector2Int _yBeginEnd = new Vector2Int(resolution / 2 * y, resolution / 2 * (y + 1));
                    Vector2Int _zBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * z, resolution / ((int)amountOfChunks / 4) * (z + 1));
                    if (x == (int)amountOfChunks / 4 - 1)
                        _xBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * x, resolution / ((int)amountOfChunks / 4) * (x + 1) - 1);
                    if (y == 1)
                        _yBeginEnd = new Vector2Int(resolution / 2 * y, resolution / 2 * (y + 1) - 1);
                    if (z == (int)amountOfChunks / 4 - 1)
                        _zBeginEnd = new Vector2Int(resolution / ((int)amountOfChunks / 4) * z, resolution / ((int)amountOfChunks / 4) * (z + 1) - 1);

                    CalculateTris(_xBeginEnd.x, _xBeginEnd.y, _yBeginEnd.x, _yBeginEnd.y, _zBeginEnd.x, _zBeginEnd.y);
                    SetMesh();
                    chunkBeginEnds.Add(new ChunkBeginEnd()
                    {
                        xBeginEnd = _xBeginEnd,
                        yBeginEnd = _yBeginEnd,
                        zBeginEnd = _zBeginEnd,
                        chunk = planetContainer.GetChild(planetContainer.childCount - 1).gameObject
                    });
                    stopwatch.Stop();
                    //Debug.Log("Timer: " + stopwatch.Elapsed);
                    //Debug.Log($"Crated a chunk in: {stopwatch.ElapsedMilliseconds}ms ");
                    stopwatch.Reset();
                }
            }
        }

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

    void CalculateTris(int beginChunkX, int endChunkX, int beginChunkY, int endChunkY, int beginChunkZ, int endChunkZ)
    {
        int totalSize = (int)resolution * (int)resolution * (int)resolution;
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        positionalDataBuffer = new ComputeBuffer(totalSize, sizeof(float) * 4, ComputeBufferType.Default);
        positionalDataBuffer.SetData(voxelData);

        triangleBuffer = new ComputeBuffer(5 * totalSize, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triangleBuffer.SetCounterValue(0);

        computeTriangles.SetBuffer(0, "triangleBuffer", triangleBuffer);
        computeTriangles.SetBuffer(0, "positionalData", positionalDataBuffer);
        computeTriangles.SetInt("size", (int)resolution);
        computeTriangles.SetFloat("isoLevel", isoLevel);
        computeTriangles.SetInt("width", (int)resolution / 3 * 2);
        computeTriangles.SetInt("beginChunkX", beginChunkX);
        computeTriangles.SetInt("endChunkX", endChunkX);
        computeTriangles.SetInt("beginChunkY", beginChunkY);
        computeTriangles.SetInt("endChunkY", endChunkY);
        computeTriangles.SetInt("beginChunkZ", beginChunkZ);
        computeTriangles.SetInt("endChunkZ", endChunkZ);
        computeTriangles.SetInt("height", resolution);
        computeTriangles.SetInt("depth", resolution);

        computeTriangles.Dispatch(0, (int)resolution / 8, (int)resolution / 8, (int)resolution / 8);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

        //triangleChunks.Add(tris);

        testing = new Vector4[totalSize];
        positionalDataBuffer.GetData(testing);

        positionalDataBuffer.Release();
        triangleBuffer.Release();
        triCountBuffer.Release();
    }


    void SetMesh()
    {
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var vertices = new Vector3[tris.Length * 3];
        var meshTriangles = new int[tris.Length * 3];

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

        for (int i = 0; i < tris.Length; i++)
        {
            vertices[vertexIndex] = tris[i].p3;
            vertices[vertexIndex + 1] = tris[i].p2;
            vertices[vertexIndex + 2] = tris[i].p1;

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
        meshCollider.sharedMesh = mesh;
    }

}
