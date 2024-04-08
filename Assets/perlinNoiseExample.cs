using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class perlinNoiseExample : MonoBehaviour
{
    [SerializeField] List<Vector2> perlinNoiseValues = new List<Vector2>();
    [Range(0f, 5f)]
    [SerializeField] float noiseScale;
    [Range(0f, 5f)]
    [SerializeField] float heightScale;
    [Range(0, 500)]
    [SerializeField] int iterarions;
    [Range(0, 100)]
    [SerializeField] int noiseOffset;

    private void Update()
    {
        setPerlinNoiseValues();
    }

    void setPerlinNoiseValues()
    {
        List<Vector2> tempList = new List<Vector2>();
        for (float i = 0; i < iterarions; i++)
        {
            float noise = Mathf.PerlinNoise(noiseOffset + i / iterarions * noiseScale, noiseOffset + i / iterarions * noiseScale) * heightScale;
            Vector2 vectorWithNoise = new Vector2(i / (iterarions / 4), noise);
            tempList.Add(vectorWithNoise);
        }
        perlinNoiseValues = tempList;
    }

    private void OnDrawGizmos()
    {
        foreach (var item in perlinNoiseValues)
        {
            Gizmos.DrawSphere(item, .1f);
        }
    }
}
