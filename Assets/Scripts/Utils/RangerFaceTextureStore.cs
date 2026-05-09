using System.IO;
using UnityEngine;

public static class RangerFaceTextureStore
{
    public const int TextureWidth = 32;
    public const int TextureHeight = 16;

    private const string ResourceTexturePath = "Arts/Ranger/Texture/Ranger_Face";
    private const string CustomTextureDirectoryName = "Ranger";
    private const string CustomTextureFileName = "Ranger_Face.png";
    private const string FaceMaterialName = "Ranger Face_Mat";

    private static Texture2D _cachedRuntimeTexture;

    public static string CustomTexturePath => Path.Combine(Application.persistentDataPath, CustomTextureDirectoryName, CustomTextureFileName);

    public static Texture2D CreateEditableTexture()
    {
        Texture2D editableTexture = CreateEmptyTexture();
        Texture2D sourceTexture = LoadRuntimeTexture();
        if (sourceTexture != null)
            CopyPixels(sourceTexture, editableTexture);

        return editableTexture;
    }

    public static void SaveCustomTexture(Texture2D sourceTexture)
    {
        string savePath = CustomTexturePath;
        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
        File.WriteAllBytes(savePath, sourceTexture.EncodeToPNG());
        ReplaceCachedRuntimeTexture(sourceTexture);
    }

    public static void ApplyTo(GameObject root)
    {
        if (root == null)
            return;

        Texture2D faceTexture = LoadRuntimeTexture();
        if (faceTexture == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            Material[] materials = targetRenderer.materials;
            foreach (Material material in materials)
            {
                if (material == null || !material.name.StartsWith(FaceMaterialName))
                    continue;

                ApplyTextureToMaterial(material, faceTexture);
            }
        }
    }

    public static void ApplyToLoadedRangers()
    {
        RangerController[] rangers = Object.FindObjectsByType<RangerController>(FindObjectsInactive.Include);
        foreach (RangerController ranger in rangers)
            ApplyTo(ranger.gameObject);
    }

    private static Texture2D LoadRuntimeTexture()
    {
        if (_cachedRuntimeTexture != null)
            return _cachedRuntimeTexture;

        string customTexturePath = CustomTexturePath;
        if (File.Exists(customTexturePath))
        {
            Texture2D customTexture = CreateEmptyTexture();
            if (customTexture.LoadImage(File.ReadAllBytes(customTexturePath)))
            {
                ConfigureTexture(customTexture);
                _cachedRuntimeTexture = customTexture;
                return _cachedRuntimeTexture;
            }

            Object.Destroy(customTexture);
        }

        _cachedRuntimeTexture = Resources.Load<Texture2D>(ResourceTexturePath);
        return _cachedRuntimeTexture;
    }

    private static Texture2D CreateEmptyTexture()
    {
        Texture2D texture = new(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        ConfigureTexture(texture);
        return texture;
    }

    private static void ConfigureTexture(Texture2D texture)
    {
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
    }

    private static void CopyPixels(Texture2D sourceTexture, Texture2D destinationTexture)
    {
        Texture2D readableSourceTexture = sourceTexture.isReadable ? sourceTexture : CreateReadableCopy(sourceTexture);
        Color[] pixels = new Color[TextureWidth * TextureHeight];
        for (int y = 0; y < TextureHeight; y++)
        {
            int sourceY = Mathf.Clamp(y, 0, readableSourceTexture.height - 1);
            for (int x = 0; x < TextureWidth; x++)
            {
                int sourceX = Mathf.Clamp(x, 0, readableSourceTexture.width - 1);
                pixels[y * TextureWidth + x] = readableSourceTexture.GetPixel(sourceX, sourceY);
            }
        }

        destinationTexture.SetPixels(pixels);
        destinationTexture.Apply();

        if (readableSourceTexture != sourceTexture)
            Object.Destroy(readableSourceTexture);
    }

    private static Texture2D CreateReadableCopy(Texture sourceTexture)
    {
        RenderTexture previousRenderTexture = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
        Graphics.Blit(sourceTexture, renderTexture);

        RenderTexture.active = renderTexture;
        Texture2D readableTexture = new(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
        readableTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
        readableTexture.Apply();
        ConfigureTexture(readableTexture);

        RenderTexture.active = previousRenderTexture;
        RenderTexture.ReleaseTemporary(renderTexture);
        return readableTexture;
    }

    private static void ReplaceCachedRuntimeTexture(Texture2D sourceTexture)
    {
        if (_cachedRuntimeTexture != null && _cachedRuntimeTexture.name != "Ranger_Face")
            Object.Destroy(_cachedRuntimeTexture);

        _cachedRuntimeTexture = CreateEmptyTexture();
        CopyPixels(sourceTexture, _cachedRuntimeTexture);
    }

    private static void ApplyTextureToMaterial(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        if (material.HasProperty("_EmissionMap"))
            material.SetTexture("_EmissionMap", texture);
    }
}
