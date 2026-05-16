using System.Collections.Generic;
using UnityEngine;

public class planetaryChunk : MonoBehaviour
{
    Mesh mesh;
    MeshFilter mf;
    MeshRenderer mr;

    Material defaultMat;

    public void createPlanetChunk(Vector3[] vertices, List<int> faces)
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.MarkDynamic();
        }

        if (vertices.Length > 65535) Debug.Log("too many vertices!");

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(faces, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (mf == null) mf = this.GetComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        if (mr == null) mr = this.GetComponent<MeshRenderer>();
        mr.sharedMaterial = defaultMat;
    }
}
