using System.CodeDom.Compiler;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class CloudManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] bool GeneratingClouds = false;

    [Header("Settings")]
    public int noiseSize = 128;
    public int detailSize = 32;
    public int numCellsPerAxis = 1;
    public int numDetailCellsPerAxis = 10;
    public int numOctaves = 4;
    public ComputeShader noiseCompute;
    public Material atmosphereShader;
    

    [Header("Output")]
    public RenderTexture noiseTex;
    public RenderTexture detailTex;
    private static readonly int noiseTextID = Shader.PropertyToID("_Noise3D");
    private static readonly int detailTextID = Shader.PropertyToID("_DetailNoise3D");
    void GenerateClouds()
    {
        SetupTexture(ref noiseTex, noiseSize);
        SetupTexture(ref detailTex, detailSize);

        DispatchWorleyOctaves(noiseTex, noiseSize, numCellsPerAxis);

        int perlinKernel = noiseCompute.FindKernel("perlinNoise3D");
        noiseCompute.SetInt("_PerlinTextureSize", noiseSize);
        noiseCompute.SetInt("_PerlinBaseFreq", numCellsPerAxis);
        noiseCompute.SetInt("_PerlinNumOctaves", numOctaves);
        noiseCompute.SetTexture(perlinKernel, "_Noise3D", noiseTex);
        int groups = Mathf.CeilToInt(noiseSize / 8.0f);
        noiseCompute.Dispatch(perlinKernel, groups, groups, groups);

        DispatchWorleyOctaves(detailTex, detailSize, numDetailCellsPerAxis);

        Shader.SetGlobalTexture(noiseTextID, noiseTex);
        Shader.SetGlobalTexture(detailTextID, detailTex);
    }

    void DispatchWorleyOctaves(RenderTexture targetTex, int texSize, int startCells)
    {
        int kernel = noiseCompute.FindKernel("worleyNoise3D");
        int groups = Mathf.CeilToInt(texSize / 8.0f);

        for (int i = 0; i < 4; i++)
        {
            //calculate cells per octave
            int currentCells = startCells * (int)Mathf.Pow(2, i);
            noiseCompute.SetInt("_WorleyTextureSize", texSize);
            noiseCompute.SetInt("_WorleyBaseFreq", currentCells);
            noiseCompute.SetInt("_WorleyNumOctaves", numOctaves);
            noiseCompute.SetInt("_TargetChannel", i);
            noiseCompute.SetTexture(kernel, "_Noise3D", targetTex);

            noiseCompute.Dispatch(kernel, groups, groups, groups);
        }
    }

    void SetupTexture(ref RenderTexture tex, int size)
    {
        if (tex != null) tex.Release();
        tex = new RenderTexture(size, size, 0, GraphicsFormat.R16G16B16A16_SFloat);
        tex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        tex.volumeDepth = size;
        tex.enableRandomWrite = true;
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Create();
    }

    void OnValidate()
    {
        if (GeneratingClouds == true)
        {
            GeneratingClouds = false;
            GenerateClouds();
        }
    }

}
