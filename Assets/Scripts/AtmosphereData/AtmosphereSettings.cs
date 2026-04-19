using UnityEngine;

[ExecuteAlways]
public class AtmosphereSettings : MonoBehaviour
{
    public AtmosphereTemplate settings;
    [HideInInspector] public AtmosphereStruct localData;
    void OnEnable()
    {
        if (AtmosphereManager.Instance != null) AtmosphereManager.Instance.Register(this);
        localData = settings.atmo;
    }

    void OnDisable()
    {
        if (AtmosphereManager.Instance != null) AtmosphereManager.Instance.Unregister(this);
    }
    void Update()
    {
        if (settings != null)//this is where information about planet instance updates!
        {
            localData._planetCenter = transform.position;
        }
    }
}
