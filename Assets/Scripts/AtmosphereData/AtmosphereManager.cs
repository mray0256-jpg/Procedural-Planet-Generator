using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[ExecuteAlways]
public class AtmosphereManager : MonoBehaviour
{
    public static AtmosphereManager Instance;
    private HashSet<AtmosphereSettings> _planets = new HashSet<AtmosphereSettings>();
    private ComputeBuffer _buffer;
    void Awake() => Instance = this;

    public void Register(AtmosphereSettings p) => _planets.Add(p);
    public void Unregister(AtmosphereSettings p) => _planets.Remove(p);
    int numPlanets;
    bool settingsChanged;


    void Update()
    {
        //if(numPlanets != _planets.Count)
        //{
        //    numPlanets = _planets.Count;
        //    SendToGPU();
        //}
        //check for if planets have been modified (new data to be sent...)
    }

    void SendToGPU()
    {
        if (_planets.Count == 0) return;

        AtmosphereStruct[] outData = new AtmosphereStruct[_planets.Count];
        int i = 0;
        foreach (var p in _planets)
        {
            outData[i] = p.localData;
            i++;
        }

        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtmosphereStruct));
        if (_buffer == null || _buffer.count != outData.Length)
        {
            _buffer?.Release();
            _buffer = new ComputeBuffer(outData.Length, stride);
        }

        _buffer.SetData(outData);
        Shader.SetGlobalBuffer("_PlanetDataBuffer", _buffer);
        Shader.SetGlobalInt("_NumPlanets", outData.Length);
    }

    void Start()
    {
        
    }

    private void OnValidate()
    {

    }

    void OnDisable() => _buffer?.Release();
}
