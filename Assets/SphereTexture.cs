using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class SphereTexture : MonoBehaviour
{
    public int textureWidth = 256;
    public int textureHeight = 256;

    public Texture2D texture;

    public void GenerateSphereTexture(float thresholdValue)
    {
        if (texture == null || texture.width != textureWidth || texture.height != textureHeight)
        {
            texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
        }

        Color[] pixels = new Color[textureWidth * textureHeight];

        float radiusSquared = Mathf.Pow(Mathf.Min(textureWidth, textureHeight) / 2f, 2);

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float distanceSquared = Mathf.Pow(x - textureWidth / 2f, 2) + Mathf.Pow(y - textureHeight / 2f, 2);
                if (distanceSquared <= radiusSquared * (thresholdValue / 50f))
                {
                    pixels[y * textureWidth + x] = Color.black;
                }
                else
                {
                    pixels[y * textureWidth + x] = Color.white;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
    }

    public Color QueryTexture(int x, int y)
    {
        return texture.GetPixel(x, y);
    }
}
