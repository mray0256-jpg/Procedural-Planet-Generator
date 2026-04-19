using UnityEngine;

[CreateAssetMenu(fileName = "AtmosphereTemplate", menuName = "Scriptable Objects/AtmosphereTemplate")]
public class AtmosphereTemplate : ScriptableObject
{
    public AtmosphereStruct atmo;
    public void UpdateFromJSON(string jsonString)//will override each atmosphere preset with the saved atmosphere data
    {
        JsonUtility.FromJsonOverwrite(jsonString, this.atmo);
    }
}
