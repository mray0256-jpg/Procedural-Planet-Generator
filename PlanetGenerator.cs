using NUnit.Framework;
using UnityEditor.UI;
using UnityEngine;
using System.Collections.Generic;
using System.Data;
using System;
using System.Linq;

public class PlanetGenerator : MonoBehaviour
{
    static float rootTwoOTwo = Mathf.Sqrt(2) / 2;
    static float goldRat = (Mathf.Sqrt(5) + 1) / 2;
    Mesh planet;
    MeshFilter mf;
    //static Vector3[] octohedron = new Vector3[]
    //{
    //        new Vector3(0, 1, 0),//First are tips
    //        new Vector3(0, -1, 0),

    //        new Vector3(rootTwoOTwo, 0, rootTwoOTwo),//square on xz plane
    //        new Vector3(-rootTwoOTwo, 0, rootTwoOTwo),
    //        new Vector3(rootTwoOTwo, 0, -rootTwoOTwo),
    //        new Vector3(-rootTwoOTwo, 0, -rootTwoOTwo),
    //};

    //int[] octofaces = new int[]
    //{
    //        0, 3, 2,//Top half
    //        0, 5, 3,
    //        0, 4, 5,
    //        0, 2, 4,

    //        1, 2, 3,//bottom half
    //        1, 3, 5,
    //        1, 5, 4,
    //        1, 4, 2
    //};

    static Vector3[] icosohedron = new Vector3[]
    {
            new Vector3(1, goldRat, 0),//xy plane
            new Vector3(1, -goldRat, 0),
            new Vector3(-1, goldRat, 0),
            new Vector3(-1, -goldRat, 0),

            new Vector3(goldRat, 0 , 1),//xz plane
            new Vector3(-goldRat, 0, 1),
            new Vector3(goldRat, 0, -1),
            new Vector3(-goldRat, 0, -1),

            new Vector3(0, 1, goldRat),//square on yz plane
            new Vector3(0, 1, -goldRat),
            new Vector3(0, -1, goldRat),
            new Vector3(0, -1, -goldRat),
    };

    int[] icofaces = new int[]
    {
           0, 2, 8,//pentagon from 0
           0, 9, 2,
           0, 6, 9,
           0, 4, 6,
           0, 8, 4,

           5, 2, 7,//pentagon from 5
           5, 8, 2,
           5, 10, 8,
           5, 3, 10,
           5, 7, 3,

           11, 9, 6,//from 11
           11, 7, 9,
           11, 3, 7,
           11, 1, 3,
           11, 6, 1,

           7, 2, 9,
           4, 8, 10,
            
           1, 6, 4,
           1, 4, 10,
           1, 10, 3

    };



    List<int> faces = new List<int>();
    List<Vector3> vertices = new List<Vector3>();

    private int prevSubs = 0;
    [UnityEngine.Range(0, 25)]
    public int numSubs = 0;

    public float planetRadius = 1.0f;

    [UnityEngine.Range(0, 1)]
    public float radiusLerp = 0;
    float prevLerp = 0;

    Dictionary<int, HashSet<int>> VertexNeighbors = new Dictionary<int, HashSet<int>>();

    //Tectonics
    [UnityEngine.Range(5, 20)]
    public int numTectonics;

    [UnityEngine.Range(0, 1)]
    public float percentOceanic;

    [UnityEngine.Range(1, 99)]
    public int plateUserIndex;

    private int plateNum;
    [UnityEngine.Range(0, 5)]
    public float vectorSize;
    

    public Color32 OceanicColor;
    public Color32 ContinentalColor;

    public bool generateTec = false;

    public Material tectonic;


    void Start()
    {
        mf = GetComponent<MeshFilter>();
        planet = new Mesh();
        defaultVertices();
        defaultFaces();
        updateMesh();
        mf.mesh = planet;
    }

    private void Update()
    {
        if (numSubs != prevSubs)
        {
            prevSubs = numSubs;
            defaultVertices();
            faces.Clear();
            Draw();
            updateMesh();
        }

        if (radiusLerp != prevLerp)
        {
            prevLerp = radiusLerp;
            normalizeRadius(Mathf.Clamp01(radiusLerp));
        }

        if(generateTec == true)
        {
            generateTec = false;
            AssignTectonics();
        }
    }

    void defaultVertices()
    {
        vertices.Clear();
        for (int i = 0; i < icosohedron.Length; i++)
        {
            vertices.Add(icosohedron[i]);
        }
    }

    void defaultFaces()
    {
        faces.Clear();
        for (int i = 0; i < icofaces.Length; i++)
        {
            faces.Add(icofaces[i]);
        }
    }



    void subdivideFace(int numSubs, int[] bounds)//bounds: 0 = top, 1 = right, 2 = left
    {
        Vector3[] face = new Vector3[] { icosohedron[bounds[0]], icosohedron[bounds[1]], icosohedron[bounds[2]] };
        int addingIndex = vertices.Count;//where the first added point is
        int layers = numSubs + 2;

        for (int i = 0; i < layers; i++)//Loops through each LAYER of one of the eight faces and subdivides it.
        {

            float LayerLerp = (float)i / (layers - 1);
            Vector3 layerStart = Vector3.Lerp(face[0], face[1], LayerLerp);
            Vector3 layerEnd = Vector3.Lerp(face[0], face[2], LayerLerp);

            int numVertices = i + 1;
            for (int j = 0; j < numVertices; j++)//creates the points in each layer
            {
                float InterlayerLerp = 1;
                if(numVertices > 1)
                {
                    InterlayerLerp = (float)j / (numVertices - 1);
                }

                Vector3 newVertex = Vector3.Lerp(layerStart, layerEnd, InterlayerLerp);
                //Debug.Log("generated vertex: x(" + newVertex.x + "), y(" + newVertex.y + "), z(" + newVertex.z + ")");
                if (OctoChecker(newVertex)) vertices.Add(newVertex);
            }

            for(int f = 0; f < Mathf.Max(2 * numVertices - 3, 0); f++)//f represents how many faces will be generated.
            {
                bool isUpward = true;
                if(f % 2 != 0) isUpward = false;
                int row = numVertices - 1;
                if (LayerLerp == 1)//BOTTOMMOST LAYER
                {
                    if(f == 0)//FIRST TRIANGLE ON BOTTOM
                    {
                        upwardTriangle(addingIndex, bounds[1], addingIndex + row);
                        addingIndex += 1;
                    }
                    else if(f == ((2 * numVertices) - 4))//LAST TRIANGLE ON BOTTOM
                    {
                        upwardTriangle(addingIndex, addingIndex + row - 1, bounds[2]);
                        addingIndex += 1;
                        //Debug.Log("Found");
                    }
                    else//NORMAL
                    {
                        if (isUpward)
                        {
                            upwardTriangle(addingIndex, addingIndex + row - 1, addingIndex + row);
                            addingIndex += 1;
                        }
                        else downwardTriangle(addingIndex, addingIndex - 1, addingIndex + row - 1);
                    }
                }
                else if(numVertices == 2)//TOPMOST LAYER
                {
                    upwardTriangle(bounds[0], addingIndex, addingIndex + 1);
                }
                else//MIDDLE LAYERS
                {
                    if (isUpward)
                    {
                        upwardTriangle(addingIndex, addingIndex + row, addingIndex + row + 1);
                        addingIndex += 1;
                    }
                    else downwardTriangle(addingIndex, addingIndex - 1, addingIndex + row);
                }
            }
        }
    }

    void upwardTriangle(int top, int right, int left)
    {

        int newTop = VertexChecker(vertices[top]);
        int newRight = VertexChecker(vertices[right]);
        int newLeft = VertexChecker(vertices[left]);

        faces.Add(newTop);//top of new triangle
        faces.Add(newRight);//rightmost point
        faces.Add(newLeft);//leftmost point
    }

    void downwardTriangle(int left, int right, int bottom)
    {
        int newBottom = VertexChecker(vertices[bottom]);
        int newRight = VertexChecker(vertices[right]);
        int newLeft = VertexChecker(vertices[left]);

        faces.Add(newLeft);//top left of new triangle
        faces.Add(newRight);//top right point
        faces.Add(newBottom);//bottom point
    }

    int VertexChecker(Vector3 vertex)//checks if a vertex exists twice. Returns the first instance.
    {
        int checker = 0;
        int checkIndex = -1;
        for (int i = 0; i < vertices.Count; i++)
        {
            if (vertices[i] == vertex)
            {
                checker++;
                if(checker > 1)
                {
                    //Debug.Log("Redundancy found! Index " + checkIndex + " is the same as " + i);
                    return checkIndex;
                }
                checkIndex = i;
            }
        }
        return checkIndex;
    }

    void RedundancyCheck()
    {
        List<int> tempList = new List<int>();
        for(int i = vertices.Count - 1; i >= 0; i--)
        {
            Vector3 currentVector = vertices[i];
            for(int j = 0; j < i; j++)
            {
                if(currentVector == vertices[j])
                {
                    tempList.Add(i);//Creates a list of all points that need to be removed
                }
            }
        }

        for(int i = 0; i < tempList.Count; i++)//parses through and deletes said points, just without bounds dependent on those points
        {
            //Debug.Log("Removed: " + tempList[i]);
            vertices.RemoveAt(tempList[i]);
            for (int k = 0; k < faces.Count; k++)
            {
                if (k < tempList.Count && k > i)
                {
                    if (tempList[k] > tempList[i])
                    {
                        tempList[k] -= 1;
                    }
                }
                if (faces[k] > tempList[i]) faces[k] -= 1;
                else if (faces[k] == tempList[i]) Debug.Log("Big problem!");
            }
        }
    }

    bool OctoChecker(Vector3 vertex)
    {
        for (int i = 0; i < icosohedron.Length; i++)
        {
            if (icosohedron[i] == vertex)
            {
                return false;//that coordinate is already owned by a vertex
            }
        }
        return true;//is valid
    }

    void PrintVertices()
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            Debug.Log("vertex " + i + " : x(" + vertices[i].x + "), y(" + vertices[i].y + "), z(" + vertices[i].z + ")");
        }
    }

    void PrintFaces()
    {
        int counter = 0;
        for (int i = 0; i < faces.Count; i += 3)
        {
            Debug.Log("Face " + counter + " : top(" + faces[i] + "), right(" + faces[i+1] + "), left(" + faces[i+2] + ")");
            counter++;
        }
    }

    void Draw()
    {
        for(int i = 0; i < icofaces.Length; i+=3)
        {
            int[] bounds = new int[] { icofaces[i], icofaces[i + 1], icofaces[i + 2] };
            subdivideFace(numSubs, bounds);
        }
    }
    void normalizeRadius(float lerpVal)
    {
        for(int i = 0; i < vertices.Count; i++)
        {
            float distance = Mathf.Sqrt(vertices[i].x * vertices[i].x + vertices[i].y * vertices[i].y + vertices[i].z * vertices[i].z);
            vertices[i] = Vector3.Lerp(vertices[i], (vertices[i] / distance) * planetRadius, lerpVal);
        }
        updateMesh();
    }
    
    void updateMesh()
    {
        planet.Clear();
        RedundancyCheck();
        planet.SetVertices(vertices);

        //Debug.Log("Vertice size: " + vertices.Count);
        //Debug.Log("Faces: " + (faces.Count/3));
        //PrintFaces();
        DetermineNeighbors();

        planet.SetTriangles(faces, 0);//submesh is the entire mesh
        planet.RecalculateNormals();
    }

   
    //PHASE 2: TECTONICS
     
    List<int> tectonicPlates = new List<int>();

    void AssignTectonics()
    {
        ResetTectonics();

        var colors = planet.colors32;
        if (colors == null || colors.Length != planet.vertexCount)
        {
            colors = new Color32[planet.vertexCount];
        }

        plateNum = 1;
        while(tectonicPlates.Contains(0))//each j is a new tectonic plate. Plate data will be written in later.
        {
            bool isOceanic = UnityEngine.Random.Range(0f, 1f) > 1f - percentOceanic;//this will be replaced with a vector 4, 3 for velocity direciton & 1 for oceanic v continental. For now it doesn't matter. Not until generating mountains...
            //Debug.Log("J Value is: " + j);
            Vector3 EulerPole = Vector3.Normalize(new Vector3(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f)));

            int tectSpawn = UnityEngine.Random.Range(0, tectonicPlates.Count);
           
            for (int k = 0; tectonicPlates[tectSpawn] != 0; k++)
            {
               tectSpawn = UnityEngine.Random.Range(0, tectonicPlates.Count);
               if (k == 8)//If random is taking too long
               {
                   tectSpawn = tectonicPlates.IndexOf(0);
               }
            }
            //now tectSpawn refers to a VALID index of tectonicPlates that can be the center of a new plate.

            float validRadius = (4 * planetRadius * planetRadius) / (numTectonics * 2/3);

            float aConst = UnityEngine.Random.Range(1, 5);
            float bConst = UnityEngine.Random.Range(1, 5);
            float cConst = UnityEngine.Random.Range(1, 5);
            for (int i = 0; i < tectonicPlates.Count; i++)//radius checker from "tectSpawn".
            {
                if (tectonicPlates[i] == 0)//No tectonic plate exists at that point, proceed to check distance
                {
                    float xCoord = (vertices[i].x - vertices[tectSpawn].x);
                    float yCoord = (vertices[i].y - vertices[tectSpawn].y);
                    float zCoord = (vertices[i].z - vertices[tectSpawn].z);
                    if ((validRadius/6)*Mathf.Cos(xCoord * aConst) + (validRadius/6)*Mathf.Sin(yCoord * bConst) + (validRadius/6)*Mathf.Cos(zCoord * cConst) +
                        (xCoord * xCoord) + (yCoord * yCoord) + (zCoord * zCoord) < validRadius)//pythag dist checker
                    {
                        Vector3 direction = Vector3.Normalize(Vector3.Cross(EulerPole, vertices[i])) * 100;
                        colors[i].r = (byte)(Mathf.Abs(direction.x) + (direction.x < 0 ? 100 : 0));
                        colors[i].g = (byte)(Mathf.Abs(direction.y) + (direction.y < 0 ? 100 : 0));
                        colors[i].b = (byte)(Mathf.Abs(direction.z) + (direction.z < 0 ? 100 : 0));
                        colors[i].a = (byte)(plateNum + (isOceanic ? 100 : 0));
                        tectonicPlates[i] = plateNum;//This point now belongs to plate j
                    }
                }
                else continue;//Already belongs to plate. Skip.
            }
            plateNum++;
            if(plateNum > numTectonics * 4)
            {
                Debug.Log("error!!!");
                return;
            }
        }
        //for(int i = 0; i < tectonicPlates.Count; i++)
        //{
        //    Debug.Log(tectonicPlates[i]);
        //}

        planet.colors32 = colors;
        DetermineBounds();
        tectonic.SetInt("_numTectonics", plateNum - 1);
    }

    void ResetTectonics()
    {
        tectonicPlates.Clear();
        for(int i = 0; i < vertices.Count; i++)
        {
            tectonicPlates.Add(0);
        }
        //Debug.Log("Tectonic Plates: " + tectonicPlates.Count + "\nVertices: " + vertices.Count);
    }

    void TectonicRedundancy()//TODO: should find any tectonic plate that exists significantly less than vertices.count/numTectonics and asborb it into another plate.
    {
         
    }
    
    struct boundary//Defines two points, making a line of a boundary and determining what the effect of said boundary should be.
    { 
        public Color32 boundColor;
        public Vector3 boundVertOne;
        public Vector3 boundVertTwo;
        public float collisionMagnitude;
        public int collisionType; //0 == convergent, 1 == divergent
        public boundary(Color32 color, Vector3 One, Vector3 Two, float mag, int type)//constructor
        {
            this.boundColor = color;
            this.boundVertOne = One;
            this.boundVertTwo = Two;
            this.collisionMagnitude = mag;
            this.collisionType = type;
        }
    }

    void DetermineNeighbors()
    {
        VertexNeighbors.Clear();


        for (int i = 0; i < vertices.Count; i++)//loops through each vertex and creates a dictionary returning an array of neighboring indices.
        {
            VertexNeighbors[i] = new HashSet<int>();//A temporary hashset of the neighbors, then returns it as the value in the dictionary.
        }
        for (int t = 0; t < faces.Count; t += 3)
        {
            int a = faces[t];
            int b = faces[t + 1];
            int c = faces[t + 2];

            //Undirected edges: add neighbors both ways
            VertexNeighbors[a].Add(b); VertexNeighbors[a].Add(c);
            VertexNeighbors[b].Add(a); VertexNeighbors[b].Add(c);
            VertexNeighbors[c].Add(a); VertexNeighbors[c].Add(b);
        }
    }

    List<boundary> boundaries = new List<boundary>();

    void DetermineBounds()
    {
        boundaries.Clear();
        for(int i = 0; i < vertices.Count; i++)
        {
            HashSet<int> tempSet = VertexNeighbors[i];
            foreach (int x in tempSet)
            {
                if (tectonicPlates[x] != tectonicPlates[i])
                {
                    float xDir = (float)(planet.colors32[i].r % 100 * (planet.colors32[i].r >= 100 ? -1f : 1f)) / 100f;
                    float yDir = (float)(planet.colors32[i].g % 100 * (planet.colors32[i].g >= 100 ? -1f : 1f)) / 100f;
                    float zDir = (float)(planet.colors32[i].b % 100 * (planet.colors32[i].b >= 100 ? -1f : 1f)) / 100f;

                    float xDir2 = (float)(planet.colors32[x].r % 100 * (planet.colors32[x].r >= 100 ? -1f : 1f)) / 100f;
                    float yDir2 = (float)(planet.colors32[x].g % 100 * (planet.colors32[x].g >= 100 ? -1f : 1f)) / 100f;
                    float zDir2 = (float)(planet.colors32[x].b % 100 * (planet.colors32[x].b >= 100 ? -1f : 1f)) / 100f;

                    float dotProd = Vector3.Dot(new Vector3(xDir, yDir, zDir), new Vector3(xDir2, yDir2, zDir2));

                    Color32 color;
                    int collisionType;
                    int middleColors = (int)(255f - Mathf.Abs(255f * dotProd));

                    if(dotProd < 0)//Going in opposite directions--divergent, blue
                    {
                        collisionType = 1;
                        color = new Color32((byte)middleColors, (byte)middleColors, 255, 60);
                    }
                    else//convergent, red
                    {
                        collisionType = 0;
                        color = new Color32(255, (byte)middleColors, (byte)middleColors, 60);
                    }

                    boundary bound = new boundary(color, vertices[i], vertices[x], dotProd, collisionType);
                    boundaries.Add(bound);
                }
            }
        }
    }

    void OnValidate()
    {
        if (plateNum > 0)
            plateUserIndex = Mathf.Clamp(plateUserIndex, 1, plateNum - 1);
    }


    private void OnDrawGizmos()
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            Gizmos.DrawSphere(vertices[i], 0.02f);
        }
        //if (planet != null &&  planet.colors32 != null && planet.colors32.Length > 0)
        //{
        //    Gizmos.color = Color.hotPink;
        //    for (int i = 0; i < planet.vertices.Count(); i++)
        //    {
        //        if (planet.colors32[i].a % 100 != plateUserIndex) continue;
        //        float xDir = (float)(planet.colors32[i].r % 100 * (planet.colors32[i].r >= 100 ? -1f : 1f)) / 100f;
        //        float yDir = (float)(planet.colors32[i].g % 100 * (planet.colors32[i].g >= 100 ? -1f : 1f)) / 100f;
        //        float zDir = (float)(planet.colors32[i].b % 100 * (planet.colors32[i].b >= 100 ? -1f : 1f)) / 100f;

        //        Gizmos.DrawLine(planet.vertices[i], planet.vertices[i] + Vector3.Normalize(new Vector3(xDir, yDir, zDir)) * vectorSize);
        //    }
        //    if (boundaries != null)
        //    {
        //        for (int i = 0; i < boundaries.Count(); i++)
        //        {
        //            Gizmos.color = boundaries[i].boundColor;
        //            Gizmos.DrawLine(boundaries[i].boundVertOne, boundaries[i].boundVertTwo);
        //        }
        //    }
        //}
    }
}
