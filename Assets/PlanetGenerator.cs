using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Rendering;

public class PlanetGenerator : MonoBehaviour
{
    static float goldRat = (Mathf.Sqrt(5) + 1) / 2;
    Mesh planet;
    MeshFilter mf;

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

    public bool printVertices;//TODO: Delete this

    List<int> faces = new List<int>();
    List<Vector3> vertices = new List<Vector3>();
    Dictionary<Vector3, int> VertexDictionary = new Dictionary<Vector3, int>();
    List<Vector3> trueVertices = new List<Vector3>();

    private int prevSubs = 0;
    private int vectorDecimals = 5;

    [UnityEngine.Range(0, 250)]
    public int numSubs = 0;

    float planetRadius = 18.0f;

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

    //Mountains

    public bool generateMountains = false;

    public ComputeShader DLAShader;
    int performID;
    int elevateID;
    int blurID;
    int scaleID;
    int stepsID;
    int copyID;

    struct GPUParticle
    {
        public int idx;
        public int isMobile;
        public int frozenIndex;
        public int step;
        public float magnitude;
        public float RNG;

        public GPUParticle(int ind, int mob, float RN, float mag = 0, int stp = 1, int frzIdx = -1)//constructor
        {
            this.idx = ind;
            this.isMobile = mob;
            this.frozenIndex = frzIdx;
            this.step = stp;
            this.magnitude = mag;
            this.RNG = RN;
        }
    }

    List<GPUParticle> _GPUParticles;
    GPUParticle[] _IndexToParticles;

    [UnityEngine.Range(0, 0.8f)]
    public float percentMountains;

    //Fractal Brownian Motion

    public ComputeShader NoiseShader;
    int noiseID;

    //Terrain

    struct terrain
    {
        public bool isOceanic;
        public int terrainMesh;//submesh
        public Vector3Int terrainType; //sorted: perlin noise, DLA, special
        public List<Vector3> terrainVertices;
    }

    //Atmosphere

    public Vector3 wavelengths;
    private Vector3 compVec;
    public float scatteringConstant;
    private float compScat;
    void Start()
    {
        //scaleIco(planetRadius);
        mf = GetComponent<MeshFilter>();
        planet = new Mesh();
        defaultVertices();
        defaultFaces();
        updateMesh();
        mf.mesh = planet;
        planet.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        performID = DLAShader.FindKernel("performDLA");
        stepsID = DLAShader.FindKernel("propogateSteps");
        elevateID = DLAShader.FindKernel("elevate");
        blurID = DLAShader.FindKernel("blurDLA");
        scaleID = DLAShader.FindKernel("scaleRadii");
        copyID = DLAShader.FindKernel("deepCopy");
        noiseID = NoiseShader.FindKernel("simplexNoise");
    }

    //void scaleIco(float scale)
    //{
    //    for(int i = 0; i < icosohedron.Count(); i++)
    //    {
    //        icosohedron[i] *= scale;
    //    }
    //}

    private void Update()
    {
       
    }

    void defaultVertices()
    {
        if(VertexDictionary != null) VertexDictionary.Clear();
        trueVertices.Clear();
        vertices.Clear();
        for (int i = 0; i < icosohedron.Length; i++)
        {
            vertices.Add(roundVector(icosohedron[i], vectorDecimals));
            VertexDictionary.Add(roundVector(icosohedron[i], vectorDecimals), i);
            trueVertices.Add(roundVector(icosohedron[i], vectorDecimals));
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

                Vector3 newVertex = roundVector(Vector3.Lerp(layerStart, layerEnd, InterlayerLerp), vectorDecimals);
                //Debug.Log("generated vertex: x(" + newVertex.x + "), y(" + newVertex.y + "), z(" + newVertex.z + ")");
                if (OctoChecker(newVertex)) vertices.Add(newVertex);
                if(VertexDictionary.TryAdd(newVertex, VertexDictionary.Count))
                {
                    trueVertices.Add(newVertex);
                }
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
    
    Vector3 roundVector(Vector3 initial, int decimalPlaces)
    {
        float multiplier = Mathf.Pow(10, decimalPlaces);

        initial.x = Mathf.Round(initial.x * multiplier) / multiplier;
        initial.y = Mathf.Round(initial.y * multiplier) / multiplier;
        initial.z = Mathf.Round(initial.z * multiplier) / multiplier;
        return initial;
    }

    void upwardTriangle(int top, int right, int left)
    {

        int newTop = VertexDictionary[vertices[top]];
        int newRight = VertexDictionary[vertices[right]];
        int newLeft = VertexDictionary[vertices[left]];

        faces.Add(newTop);//top of new triangle
        faces.Add(newRight);//rightmost point
        faces.Add(newLeft);//leftmost point
    }

    void downwardTriangle(int left, int right, int bottom)
    {
        int newBottom = VertexDictionary[vertices[bottom]];
        int newRight = VertexDictionary[vertices[right]];
        int newLeft = VertexDictionary[vertices[left]];

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
                    Debug.Log("Dictionary says..." + VertexDictionary[vertices[checkIndex]] + " and... " + VertexDictionary[vertices[i]] + " are unique!");
                    Debug.Log("Redundancy found! Index " + checkIndex + " is the same as " + i);
                    Debug.Log("Actual points are: " + vertices[checkIndex].ToString("F7") + " and " + vertices[i].ToString("F7"));
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
    
    void Draw()
    {
        for(int i = 0; i < icofaces.Length; i+=3)
        {
            int[] bounds = new int[] { icofaces[i], icofaces[i + 1], icofaces[i + 2] };
            subdivideFace(numSubs, bounds);
        }
    }
    public void normalizeRadius(float lerpVal)
    {
        for(int i = 0; i < vertices.Count; i++)
        {
            float distance = Mathf.Sqrt(vertices[i].x * vertices[i].x + vertices[i].y * vertices[i].y + vertices[i].z * vertices[i].z);
            vertices[i] = Vector3.Lerp(vertices[i], (vertices[i] / distance) * planetRadius, lerpVal);
        }
        updateMesh();
    }
    
    void updateVertices()
    {
        planet.SetVertices(vertices);
        planet.RecalculateNormals();
    }

    void updateMesh()
    {
        planet.Clear();
        //RedundancyCheck();
        if(vertices.Count != trueVertices.Count) vertices = new List<Vector3>(trueVertices);
        planet.SetVertices(vertices);

        DetermineNeighbors();

        planet.SetTriangles(faces, 0);//submesh is the entire mesh
        planet.RecalculateNormals();
    }

   
    //PHASE 2: TECTONICS

    List<int> tectonicPlates = new List<int>();

    public void AssignTectonics()
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
        public int boundOneIndex;
        public boundary(Color32 color, Vector3 One, Vector3 Two, float mag, int ind)//constructor
        {
            this.boundColor = color;
            this.boundVertOne = One;
            this.boundVertTwo = Two;
            this.collisionMagnitude = mag;
            this.boundOneIndex = ind;
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
    
    float[] determineEulerMagnitudes()
    {
        float[] EulerMags = new float[plateNum];
        for(int i = 0; i < plateNum; i++)
        {
            EulerMags[i] = UnityEngine.Random.Range(1f, 2f);
        }
        return EulerMags;
    }

    void DetermineBounds()
    {
        float[] Euler = determineEulerMagnitudes();
        boundaries.Clear();
        for(int i = 0; i < vertices.Count; i++)
        {
            float aPower = Euler[tectonicPlates[i]];
            HashSet<int> tempSet = VertexNeighbors[i];
            foreach (int x in tempSet)
            {
                if (tectonicPlates[x] != tectonicPlates[i])
                {
                    float bPower = Euler[tectonicPlates[x]];

                    float xDir = (float)(planet.colors32[i].r % 100 * (planet.colors32[i].r >= 100 ? -1f : 1f)) / 100f;
                    float yDir = (float)(planet.colors32[i].g % 100 * (planet.colors32[i].g >= 100 ? -1f : 1f)) / 100f;
                    float zDir = (float)(planet.colors32[i].b % 100 * (planet.colors32[i].b >= 100 ? -1f : 1f)) / 100f;

                    float xDir2 = (float)(planet.colors32[x].r % 100 * (planet.colors32[x].r >= 100 ? -1f : 1f)) / 100f;
                    float yDir2 = (float)(planet.colors32[x].g % 100 * (planet.colors32[x].g >= 100 ? -1f : 1f)) / 100f;
                    float zDir2 = (float)(planet.colors32[x].b % 100 * (planet.colors32[x].b >= 100 ? -1f : 1f)) / 100f;

                    //TODO: Create a straight line out of the 60 degree angles of triangles
                    Vector3 subVec = Vector3.Normalize((new Vector3(xDir2, yDir2, zDir2) * bPower) - (new Vector3(xDir, yDir, zDir) * aPower));
                    HashSet<int> neighborSet = VertexNeighbors[x];
                    int thirdVertex = -1;
                    int firstVertex = i;
                    Vector3 normal;
                    float dotProd;
                    foreach(int n in tempSet)
                    {
                        if(neighborSet.Contains(n) && tectonicPlates[n] == tectonicPlates[i])//finds third point in a face
                        {
                            thirdVertex = n;
                            break;
                        }
                        else if (neighborSet.Contains(n) && tectonicPlates[n] == tectonicPlates[x])
                        {
                            thirdVertex = n;
                            firstVertex = x;
                            break;
                        }
                    }
                    if (thirdVertex != -1)
                    {
                        normal = Vector3.Normalize(Vector3.Cross(vertices[thirdVertex] - vertices[firstVertex], vertices[firstVertex]));
                        dotProd = Vector3.Dot(subVec, normal);
                        //dotProd *= (firstVertex == x) ? -1 : 1;
                    }
                    else { dotProd = Vector3.Dot(new Vector3(xDir, yDir, zDir), new Vector3(xDir2, yDir2, zDir2)); Debug.Log("Coner"); }
                    Color32 color;
                    int middleColors = (int)(255f - Mathf.Abs(255f * dotProd));

                    if(dotProd < 0)//Going in opposite directions--divergent, blue
                    {
                        color = new Color32((byte)middleColors, (byte)middleColors, 255, 60);
                    }
                    else//convergent, red
                    {
                        color = new Color32(255, (byte)middleColors, (byte)middleColors, 60);
                    }

                    boundary bound = new boundary(color, vertices[i], vertices[x], dotProd, i);
                    boundaries.Add(bound);
                }
            }
        }
    }

    //PHASE 3: MOUNTAINS

    float DLAThreshold = 0.6f;
    float DLAWeight = 0.6f;
    int DLAStep = 1;
    int DLAMax = 5000;
    int DLAblurs = 20;
    int particleStride = 24;
    float mountainRatio = 0.2f;
    int[] neighborsArray;

    //compute buffers

    ComputeBuffer aliveParticlesA; //Consume
    ComputeBuffer aliveParticlesB; //Append
    ComputeBuffer neighborsBuffer;
    ComputeBuffer frozenParticles;
    ComputeBuffer particleCountBuffer;
    ComputeBuffer vertexBuffer;
    ComputeBuffer writeIndexToParticle;
    ComputeBuffer readIndexToParticle;
    ComputeBuffer finishedElevation;
    ComputeBuffer processedDLA;

    void SafeRelease(ref ComputeBuffer buffer) { if (buffer != null) buffer.Release(); buffer = null; }

    void resetBuffers()
    {
        SafeRelease(ref aliveParticlesA);
        SafeRelease(ref aliveParticlesB);
        SafeRelease(ref neighborsBuffer);
        SafeRelease(ref frozenParticles);
        SafeRelease(ref particleCountBuffer);
        SafeRelease(ref vertexBuffer);
        SafeRelease(ref writeIndexToParticle);
        SafeRelease(ref readIndexToParticle);
        SafeRelease(ref finishedElevation);
        SafeRelease(ref processedDLA);
    }

    IEnumerator runDLA()
    {
        //Debug.Log("Began DLA. Initial vertices: " + vertices.Count);
        //prepare data
        resetBuffers();
        DLAShader.SetInt("numVertices", vertices.Count);
        DLAShader.SetFloat("weight", DLAWeight);

        _IndexToParticles = new GPUParticle[vertices.Count];

        GenerateParticles((int)Mathf.Floor(vertices.Count * percentMountains));
        GenerateNeighbors();

        //prepare buffers
        aliveParticlesA = new ComputeBuffer(_GPUParticles.Count, particleStride, ComputeBufferType.Append);
        aliveParticlesA.SetData(_GPUParticles);
        aliveParticlesA.SetCounterValue((uint)_GPUParticles.Count());

        aliveParticlesB = new ComputeBuffer(_GPUParticles.Count, particleStride, ComputeBufferType.Append);

        neighborsBuffer = new ComputeBuffer(neighborsArray.Count(), 4);
        neighborsBuffer.SetData(neighborsArray);

        frozenParticles = new ComputeBuffer(_GPUParticles.Count, particleStride, ComputeBufferType.Append);//data is written in compute shader
        frozenParticles.SetCounterValue(0);

        particleCountBuffer = new ComputeBuffer(1, sizeof(int));
        particleCountBuffer.SetData(new int[] { _GPUParticles.Count});

        vertexBuffer = new ComputeBuffer(vertices.Count, 12);
        vertexBuffer.SetData(vertices);

        writeIndexToParticle = new ComputeBuffer(vertices.Count, particleStride);
        writeIndexToParticle.SetData(_IndexToParticles);

        readIndexToParticle = new ComputeBuffer(vertices.Count, particleStride);
        readIndexToParticle.SetData(_IndexToParticles);

        processedDLA = new ComputeBuffer(vertices.Count, 4);
        processedDLA.SetData(new int[vertices.Count]);

        finishedElevation = new ComputeBuffer(1, sizeof(int));
        int[] elevationData = new int[1];

        //set buffers
        DLAShader.SetBuffer(performID, "_neighbors", neighborsBuffer);
        DLAShader.SetBuffer(performID, "_deadParticles", frozenParticles);
        DLAShader.SetBuffer(copyID, "_writeIndexToParticle", writeIndexToParticle);
        DLAShader.SetBuffer(copyID, "_readIndexToParticle", readIndexToParticle);
        
        for (DLAStep = 1; DLAStep < DLAMax; DLAStep++)
        {
            iterateDLA();
            if (DLAStep % 50 == 0)
            {
                var isFrozen = AsyncGPUReadback.Request(particleCountBuffer);
                yield return new WaitUntil(() => isFrozen.done);
                if (isFrozen.hasError) { Debug.Log("count error!"); break; }
                else if (isFrozen.GetData<int>()[0] == 0) { Debug.Log("DLA done in " + DLAStep + " iterations."); break; }
                //else Debug.Log(isFrozen.GetData<int>()[0]);
            }
        }
        if (DLAStep == DLAMax) Debug.Log("Maximum DLA iterations!!!");

        //propogate steps

        ComputeBuffer temp = aliveParticlesA;
        aliveParticlesA = frozenParticles;
        frozenParticles = temp;


        //var getParticles = AsyncGPUReadback.Request(aliveParticlesA); //this block is useful for debugging compute shaders.
        //yield return new WaitUntil(() => getParticles.done);
        //if (getParticles.hasError) { Debug.Log("particles error!"); }
        //else
        //{
        //    var p = getParticles.GetData<GPUParticle>();
        //    foreach (GPUParticle i in p)
        //    {
        //        Debug.Log("Index: " + i.idx + " Parent index: " + i.frozenIndex + " Steps: " + i.step + " Magnitude: " + i.magnitude);
        //    }
        //}

        int propogateStep;
        int maxPropogate = 500;
        for (propogateStep = 1; propogateStep < maxPropogate; propogateStep++)
        {
            aliveParticlesB.SetCounterValue(0);

            DLAShader.SetBuffer(stepsID, "_aliveParticles", aliveParticlesA);
            DLAShader.SetBuffer(stepsID, "_deadParticles", aliveParticlesB);

            DLAShader.SetBuffer(stepsID, "_writeIndexToParticle", writeIndexToParticle);
            DLAShader.SetBuffer(stepsID, "_readIndexToParticle", readIndexToParticle);

            elevationData[0] = 1;
            finishedElevation.SetData(elevationData);
            DLAShader.SetBuffer(stepsID, "finishedElevating", finishedElevation);

            //performs propogateSteps
            ComputeBuffer.CopyCount(aliveParticlesA, particleCountBuffer, 0);
            DLAShader.SetBuffer(stepsID, "_Args", particleCountBuffer);

            DLAShader.Dispatch(stepsID, Mathf.CeilToInt(_GPUParticles.Count / 64f), 1, 1);

            if (propogateStep % 20 == 0)//tests if done
            {
                //Debug.Log(propogateStep + " propogation iterations...");
                var isElevated = AsyncGPUReadback.Request(finishedElevation);
                yield return new WaitUntil(() => isElevated.done);
                if (isElevated.hasError) { Debug.Log("propogation error!"); break; }
                else if (isElevated.GetData<int>()[0] == 1) { Debug.Log("Step propogation done in " + propogateStep + " iterations."); break; }
            }

            //ping-pongs
            DLAShader.Dispatch(copyID, Mathf.CeilToInt(vertices.Count / 64.0f), 1, 1);

            ComputeBuffer temp2 = aliveParticlesA;
            aliveParticlesA = aliveParticlesB;
            aliveParticlesB = temp2;
        }
        if(propogateStep != maxPropogate)
        {
            Debug.Log("max propogations!");
            ComputeBuffer temp3 = aliveParticlesA;
            aliveParticlesA = aliveParticlesB;
            aliveParticlesB = temp3;
        }

        Debug.Log("elevating!");
        DLAShader.Dispatch(copyID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);
        DLAShader.SetBuffer(elevateID, "_Args", particleCountBuffer);

        DLAShader.SetBuffer(elevateID, "_writeIndexToParticle", writeIndexToParticle);
        DLAShader.SetBuffer(elevateID, "_readIndexToParticle", readIndexToParticle);
        DLAShader.SetBuffer(elevateID, "_aliveParticles", aliveParticlesA);

        DLAShader.Dispatch(elevateID, Mathf.CeilToInt(_GPUParticles.Count / 64f), 1, 1);

        DLAShader.Dispatch(copyID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);

        DLAShader.SetBuffer(scaleID, "_readIndexToParticle", readIndexToParticle);
        DLAShader.SetInt("numBlurs", DLAblurs);
        DLAShader.SetBuffer(scaleID, "_vertexData", vertexBuffer);
        DLAShader.SetFloat("mountainRatio", mountainRatio);
        DLAShader.SetBuffer(scaleID, "_heightMap", processedDLA);//final heightmap!
        scaleDLA(0);

        DLAShader.SetBuffer(blurID, "_neighbors", neighborsBuffer);
        for (int i = 0; i < DLAblurs; i++)
        {
            blurDLA();
            scaleDLA(i + 1);
        }
        //scale radii
       
        
        
        var vertexChange = AsyncGPUReadback.Request(vertexBuffer);
        yield return new WaitUntil(() => vertexChange.done);
        if (vertexChange.hasError) { Debug.LogError("vertex error!"); yield break; }
        vertices = vertexChange.GetData<Vector3>().ToList();
        //for(int i = 0; i < vertices.Count; i++)
        //{ 
        //    if (float.IsNaN(vertices[i].x))
        //    Debug.Log(vertices[i]);
        //}
        updateVertices();
        resetBuffers();
    }
    void iterateDLA()
    {
        aliveParticlesB.SetCounterValue(0);
        ComputeBuffer.CopyCount(aliveParticlesA, particleCountBuffer, 0);
        DLAShader.SetFloat("timer", Time.time);
        DLAShader.SetInt("currentStep", DLAStep);
        //run iteration
        DLAShader.SetBuffer(performID, "_Args", particleCountBuffer);
        DLAShader.SetBuffer(performID, "_writeIndexToParticle", writeIndexToParticle);
        DLAShader.SetBuffer(performID, "_readIndexToParticle", readIndexToParticle);
        DLAShader.SetBuffer(performID, "_aliveParticles", aliveParticlesA);
        DLAShader.SetBuffer(performID, "_nextFrameParticles", aliveParticlesB);

        DLAShader.Dispatch(performID, Mathf.CeilToInt(_GPUParticles.Count / 64f), 1, 1);
        //ping-pong
        DLAShader.Dispatch(copyID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);//copies writeIndex into readIndex

        ComputeBuffer temp = aliveParticlesA;
        aliveParticlesA = aliveParticlesB;
        aliveParticlesB = temp;
    }

    void blurDLA()
    {
        DLAShader.SetBuffer(blurID, "_readIndexToParticle", readIndexToParticle);
        DLAShader.SetBuffer(blurID, "_writeIndexToParticle", writeIndexToParticle);
        DLAShader.Dispatch(blurID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);

        DLAShader.Dispatch(copyID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);
    }

    void scaleDLA(int iteration)
    {
        DLAShader.SetInt("currentStep", iteration);
        DLAShader.Dispatch(scaleID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);
    }

    void GenerateParticles(int numParticles)
    {
        if(_GPUParticles != null) _GPUParticles.Clear();
        else _GPUParticles = new List<GPUParticle>();

        int mountGenerator = 8;
        while(mountGenerator > 0)
        {
            int randIdx = (int)UnityEngine.Random.Range(0, boundaries.Count);

            if (boundaries[randIdx].collisionMagnitude > DLAThreshold)
            {
                //Debug.Log(boundaries[randIdx].boundOneIndex + " is a seed index.");
                float rando = UnityEngine.Random.Range(0f, 1f);
                GPUParticle p = new GPUParticle(boundaries[randIdx].boundOneIndex, 0, rando, boundaries[randIdx].collisionMagnitude);
                _IndexToParticles[boundaries[randIdx].boundOneIndex] = p;
                _GPUParticles.Add(p);
                numParticles--;
                mountGenerator--;
            }
        }
        //Debug.Log("Particles left: " + numParticles);
        while(numParticles > 0)
        {
            int index = (int)UnityEngine.Random.Range(0, vertices.Count);
            //Debug.Log(index);
            float rando = UnityEngine.Random.Range(0f, 1f);
            GPUParticle p = new GPUParticle(index, 1, rando);
            _GPUParticles.Add(p);
            numParticles--;
        }
    }

    void GenerateNeighbors()
    {
        neighborsArray = new int[vertices.Count * 6];
        System.Array.Fill(neighborsArray, -1);

        for(int i = 0; i < vertices.Count; i++)
        {
            int j = 0;
            HashSet<int> tempSet = VertexNeighbors[i];
            foreach(int item in tempSet)
            {
                if (j >= 6) break;
                neighborsArray[i * 6 + j] = item;
                j++;
            }
        }
    }


    //PHASE 3.2 -- FRACTAL BROWNIAN MOTION

    ComputeBuffer test;
    public bool generateNoise = false;

    IEnumerator GenerateNoise()
    {
        test = new ComputeBuffer(vertices.Count, 12);
        test.SetData(vertices);
        NoiseShader.SetBuffer(noiseID, "_vertices", test);
        NoiseShader.Dispatch(noiseID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);


        var vertexChange = AsyncGPUReadback.Request(test);
        yield return new WaitUntil(() => vertexChange.done);
        if (vertexChange.hasError) { Debug.LogError("vertex error!"); yield break; }
        vertices = vertexChange.GetData<Vector3>().ToList();

        updateVertices();
        SafeRelease(ref test);
    }


    private void OnDestroy()
    {
        resetBuffers();
    }


    void OnValidate()
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

        if (generateTec == true)
        {
            generateTec = false;
            AssignTectonics();
        }

        if (plateNum > 0)
            plateUserIndex = Mathf.Clamp(plateUserIndex, 1, plateNum - 1);

        if (generateMountains == true)
        {
            generateMountains = false;
            StartCoroutine(runDLA());
        }

        if(printVertices == true)
        {
            printVertices = false;
            Debug.Log("vertices: " + vertices.Count);
            Debug.Log("true vertices: " + trueVertices.Count);
            Debug.Log("dictionary: " + VertexDictionary.Count);
            Debug.Log("faces: " + faces.Count/3);
            //for(int i = 0; i < vertices.Count; i++)
            //{
            //    VertexChecker(vertices[i]);
            //}
            //for(int i = 0; i < 50; i++)
            //{
            //    Debug.Log(neighborsArray[i]);
            //}
        }
        if(generateNoise == true)
        {
            generateNoise = false;
            StartCoroutine(GenerateNoise());
        }

        if (wavelengths != compVec || compScat != scatteringConstant)
        {
            Vector3 values = new Vector3(Mathf.Pow(400 / wavelengths.x, 4) * scatteringConstant, Mathf.Pow(400 / wavelengths.y, 4) * scatteringConstant, Mathf.Pow(400 / wavelengths.z, 4) * scatteringConstant);
            Shader.SetGlobalVector("scattering", values);
        }
    }
    //if nothing else works, I can force DLA to work by introducing one new particle every iteration until _GPUParticles is empty (Starting with, say, half).
    



    private void OnDrawGizmos()
    {
        for (int i = 0; i < 12; i++)
        {
            Gizmos.DrawSphere(vertices[i], 0.02f);
        }
        //if (planet != null && planet.colors32 != null && planet.colors32.Length > 0)
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
