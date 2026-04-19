using UnityEngine;

[System.Serializable]
public struct AtmosphereStruct
{
    public Vector3 _planetCenter;
    public float _planetRadius;
    public float _atmosphereRadius;
    public float _heightScalar;
    public float _scatteringConstant;
    public int numOpticals;
    public int numScatters;
}
