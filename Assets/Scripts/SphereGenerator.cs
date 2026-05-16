using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEditor;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using static SphereGenerator;

public class SphereGenerator : MonoBehaviour
{
    //NOTES

    //TODO list::
    //finish triangle Packing << 
    //finish mesh SO and regeneration. Also, consider whether meshes generate asymmetrically. << Simple SO queue/list/array or smth

    //could be nice::
    //chunks are contiguous using some sort of angle checker by dividing circular region into 16 pie-shaped slices
    //delete faceDict and replace with array. Use 20 * 4 ^ n to determine location within array.
    //general optimizations and looking into seams for GPU



    public enum HeightmapType
    {
        Spherical,
        DefaultHF,
        DefaultLF
    }


    public class triangle
    {
        //three vertices composing the triangle
        public int a;
        public int b;
        public int c;

        public int idx;
        public byte depth;//should not tessellate past ~10 subdivisions, no need for a full int

        //for quadtree data structure
        public int descendant1;
        public int descendant2;
        public int descendant3;
        public int descendant4;

        //adjacencies are necessary for coupled ring search
        public int adjacency1;
        public int adjacency2;
        public int adjacency3;

        //for quadtree, rendering, and adjacency discovery
        public int parent;
        public bool visible;

        public triangle(int idx, byte depth, int a, int b, int c, int parent = -1)
        {
            this.idx = idx;
            this.parent = parent;
            this.depth = depth;
            this.a = a;
            this.b = b;
            this.c = c;
            this.visible = true;
            this.descendant1 = -1;
            this.descendant2 = -1;
            this.descendant3 = -1;
            this.descendant4 = -1;
            this.adjacency1 = -1;
            this.adjacency2 = -1;
            this.adjacency3 = -1;
        }
    }
    private Vector3 addVertexToUnitSphere(Vector3 v)
    {
        float length = (float)Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        return v/length;
    }

    static float goldRat = (Mathf.Sqrt(5) + 1) / 2;

    Dictionary<int, triangle> faceDict; //dict of each triangle with key corresponding to triangle idx
    Dictionary<int, Vector3> vertexDict; //dict of each vertex with value corresponding to idx
    HashSet<int> tessellationEdges; //hashset of triangle indices where each triangle has a shallow neighbor, meaning they're either on the observing border or need to be packed
    Vector3[] vertices;
    List<int> faces;
    int latestIndex = 11;
    int latestVertexIndex = 0;

    [Range(0, 6)]
    public int uniformDepth = 1;

    [Range(6, 12)]
    public int maxDepth = 8;

    [Range(1, 3)]
    public int chunkDepth = 2;
    public float planetRadius = 1;
    public bool update = false;
    public bool tessellateFace0 = false;

    GameObject[] chunkMeshes;
    public GameObject chunkPreFab;
    public Camera cam;

    public Vector3 playerLoc;
    //public float playerDistToSurface;

    public void GenerateBaseIcosohedron()
    {
        vertexDict = new Dictionary<int, Vector3>()
        {
            { latestVertexIndex++, new Vector3(-1, goldRat, 0) },//xy plane
            { latestVertexIndex++, new Vector3(1, goldRat, 0) },
            { latestVertexIndex++, new Vector3(-1, -goldRat, 0) },
            { latestVertexIndex++, new Vector3(1, -goldRat, 0) },

            { latestVertexIndex++, new Vector3(0, -1, goldRat) },//square on yz plane
            { latestVertexIndex++, new Vector3(0, 1, goldRat) },
            { latestVertexIndex++, new Vector3(0, -1, -goldRat) },
            { latestVertexIndex++, new Vector3(0, 1, -goldRat) },

            { latestVertexIndex++, new Vector3(goldRat, 0 , -1) },//xz plane
            { latestVertexIndex++, new Vector3(goldRat, 0, 1) },
            { latestVertexIndex++, new Vector3(-goldRat, 0, -1) },
            { latestVertexIndex++, new Vector3(-goldRat, 0, 1) }
        };

        for(int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = applyHeightmap(vertices[i]);
        }

        faceDict = new Dictionary<int, triangle>()
        {
            {0, new triangle(0, 0, 0, 11, 5)},
            {1, new triangle(1, 0, 0, 5, 1)},
            {2, new triangle(2, 0, 0, 1, 7)},
            {3, new triangle(3, 0, 0, 7, 10)},
            {4, new triangle(4, 0, 0, 10, 11)},

            {5, new triangle(5, 0, 1, 5, 9)},
            {6, new triangle(6, 0, 5, 11, 4)},
            {7, new triangle(7, 0, 11, 10, 2)},
            {8, new triangle(8, 0, 10, 7, 6)},
            {9, new triangle(9, 0, 7, 1, 8)},

            {10, new triangle(10, 0, 3, 9, 4)},
            {11, new triangle(11, 0, 3, 4, 2)},
            {12, new triangle(12, 0, 3, 2, 6)},
            {13, new triangle(13, 0, 3, 6, 8)},
            {14, new triangle(14, 0, 3, 8, 9)},
            
            {15, new triangle(15, 0, 4, 9, 5)},
            {16, new triangle(16, 0, 2, 4, 11)},
            {17, new triangle(17, 0, 6, 2, 10)},
            {18, new triangle(18, 0, 8, 6, 7)},
            {19, new triangle(19, 0, 9, 8, 1)},
        };

        // Face 0 adjacencies;
        faceDict[0].adjacency1 = 4;
        faceDict[0].adjacency2 = 6;
        faceDict[0].adjacency3 = 1;

        // Face 1 adjacencies; 
        faceDict[1].adjacency1 = 0;
        faceDict[1].adjacency2 = 5;
        faceDict[1].adjacency3 = 2;

        // Face 2 adjacencies; 
        faceDict[2].adjacency1 = 1;
        faceDict[2].adjacency2 = 9;
        faceDict[2].adjacency3 = 3;

        // Face 3 adjacencies;  
        faceDict[3].adjacency1 = 2;
        faceDict[3].adjacency2 = 8;
        faceDict[3].adjacency3 = 4;

        // Face 4 adjacencies; 
        faceDict[4].adjacency1 = 3;
        faceDict[4].adjacency2 = 7;
        faceDict[4].adjacency3 = 0;

        // Face 5 adjacencies; 
        faceDict[5].adjacency1 = 1;
        faceDict[5].adjacency2 = 15;
        faceDict[5].adjacency3 = 19;

        // Face 6 adjacencies; 
        faceDict[6].adjacency1 = 0;
        faceDict[6].adjacency2 = 16;
        faceDict[6].adjacency3 = 15;

        // Face 7 adjacencies; 
        faceDict[7].adjacency1 = 4;
        faceDict[7].adjacency2 = 17;
        faceDict[7].adjacency3 = 16;

        // Face 8 adjacencies; 
        faceDict[8].adjacency1 = 3;
        faceDict[8].adjacency2 = 18;
        faceDict[8].adjacency3 = 17;

        // Face 9 adjacencies; 
        faceDict[9].adjacency1 = 2;
        faceDict[9].adjacency2 = 19;
        faceDict[9].adjacency3 = 18;

        // Face 10 adjacencies; 
        faceDict[10].adjacency1 = 14;
        faceDict[10].adjacency2 = 15;
        faceDict[10].adjacency3 = 11;

        // Face 11 adjacencies; 
        faceDict[11].adjacency1 = 10;
        faceDict[11].adjacency2 = 16;
        faceDict[11].adjacency3 = 12;

        // Face 12 adjacencies; 
        faceDict[12].adjacency1 = 11;
        faceDict[12].adjacency2 = 17;
        faceDict[12].adjacency3 = 13;

        // Face 13 adjacencies; 
        faceDict[13].adjacency1 = 12;
        faceDict[13].adjacency2 = 18;
        faceDict[13].adjacency3 = 14;

        // Face 14 adjacencies; 
        faceDict[14].adjacency1 = 13;
        faceDict[14].adjacency2 = 19;
        faceDict[14].adjacency3 = 10;

        // Face 15 adjacencies; 
        faceDict[15].adjacency1 = 10;
        faceDict[15].adjacency2 = 5;
        faceDict[15].adjacency3 = 6;
        
        // Face 16 adjacencies; 
        faceDict[16].adjacency1 = 11;
        faceDict[16].adjacency2 = 6;
        faceDict[16].adjacency3 = 7;
        
        // Face 17 adjacencies; 
        faceDict[17].adjacency1 = 12;
        faceDict[17].adjacency2 = 7;
        faceDict[17].adjacency3 = 8;

        // Face 18 adjacencies; 
        faceDict[18].adjacency1 = 13;
        faceDict[18].adjacency2 = 8;
        faceDict[18].adjacency3 = 9;

        // Face 19 adjacencies; 
        faceDict[19].adjacency1 = 14;
        faceDict[19].adjacency2 = 9;
        faceDict[19].adjacency3 = 5;

        latestIndex = 20;
    }

    public void GenerateSphere()
    {
        if(chunkMeshes == null)
        {
            chunkMeshes = new GameObject[16];
            for(int i = 0; i < 16; i++)
            {
                chunkMeshes[i] = Instantiate(chunkPreFab);
            }
        }

        GenerateBaseIcosohedron();
        // Recurse the icosphere.
        for (int i = 0; i < uniformDepth; i++)
        {
            uniformlyRecurseIcosphere();
        }
        //purgeAncestry();
        createChunks(chunkDepth);

    }

    private void purgeAncestry()
    {
        // Temporary list to avoid concurrent modification
        List<int> facesToPurge = new List<int>();

        foreach (triangle t in faceDict.Values)
        {
            if (!t.visible)
            {
                facesToPurge.Add(t.idx);
            }
            //else Debug.Log(t.idx);
        }

        foreach (int index in facesToPurge)
        {
            //Debug.Log(index);
            faceDict.Remove(index);
        }
    }
    private void uniformlyRecurseIcosphere()
    {
        // Create a temporary dictionary of all visible faces.  We will recurse this temporary collection so we don't
        // cross-contaminate our master collection while we are in the process iterating through it.
        Dictionary<int, triangle> temporaryFaces = new Dictionary<int, triangle>();

        // Pull out the visible faces.
        foreach (triangle t in faceDict.Values)
        {
            if (t.visible)
            {
                temporaryFaces.Add(t.idx, t);
            }
        }

        // Each of these Triangles is a reference.  Thus, updating it in our temporary collection
        // will also update it in our master collection.
        foreach (triangle t in temporaryFaces.Values)
        {
            recurseSingleFace(t.idx);
        }
    }

    void UpdateFace0()
    {
        //Debug.Log("tessellating face!");
        //triangle leaf = descendToRecursiveLevel(faceDict[0], 10);
        // Debug.Log("tessellating face: " + leaf.idx);
        //recurseSingleFace(leaf.idx);
        // PrepareMesh();
        // updateMesh();
        playerLoc = cam.transform.position - this.transform.position;
        tessellate();
        //PrepareMesh();
       // updateMesh();
    }

    public void recursePosterity(int idx, int currentGeneration, int targetGeneration)
    {
        if(currentGeneration < targetGeneration)
        {
            if(faceDict[idx].descendant1 == -1) recurseSingleFace(idx);

            recursePosterity(faceDict[faceDict[idx].descendant1].idx, currentGeneration + 1, targetGeneration);
            recursePosterity(faceDict[faceDict[idx].descendant2].idx, currentGeneration + 1, targetGeneration);
            recursePosterity(faceDict[faceDict[idx].descendant3].idx, currentGeneration + 1, targetGeneration);
            recursePosterity(faceDict[faceDict[idx].descendant4].idx, currentGeneration + 1, targetGeneration);
        }
    }

    private void purgePosterity(int idx, int targetDepth)//TODO: FILL OUT
    {
        if (faceDict[idx].depth > targetDepth)
        {
            purgePosterity(faceDict[faceDict[idx].parent].idx, targetDepth);
            return;
        }

    }


    public void recurseSingleFace(int idx, HeightmapType type = HeightmapType.Spherical)//takes triangle idx and recurses it
    {
        //Debug.Log("recursive idx:" + idx);
        triangle faceToRecurse = faceDict[idx];

        byte newDepth = (byte)(faceToRecurse.depth + 1);

        //generates new vertices at midpoints of faceToRecurse's edges
        Vector3 newA = applyHeightmap(Vector3.Lerp(vertexDict[faceToRecurse.a], vertexDict[faceToRecurse.b], 0.5f), type);
        Vector3 newB = applyHeightmap(Vector3.Lerp(vertexDict[faceToRecurse.b], vertexDict[faceToRecurse.c], 0.5f), type);
        Vector3 newC = applyHeightmap(Vector3.Lerp(vertexDict[faceToRecurse.c], vertexDict[faceToRecurse.a], 0.5f), type);

        //check values for existence
        //this solutions prevents the need for a bidirectional vertex dict
        int a = determineWhetherVertexExists(faceToRecurse, newA);
        a = a == -1 ? latestVertexIndex++ : a;

        int b = determineWhetherVertexExists(faceToRecurse, newB);
        b = b == -1 ? latestVertexIndex++ : b;

        int c = determineWhetherVertexExists(faceToRecurse, newC);
        c = c == -1 ? latestVertexIndex++ : c;

        //construct triangles out of new vertices
        triangle t1 = new triangle(latestIndex, newDepth, faceToRecurse.a, a, c, faceToRecurse.idx);
        if (faceDict.TryAdd(t1.idx, t1)) latestIndex++;
        else Debug.Log("indexing error in recurse single face!");

        triangle t2 = new triangle(latestIndex, newDepth, a, faceToRecurse.b, b, faceToRecurse.idx);
        if (faceDict.TryAdd(t2.idx, t2)) latestIndex++;
        else Debug.Log("indexing error in recurse single face!");

        triangle t3 = new triangle(latestIndex, newDepth, c, b, faceToRecurse.c, faceToRecurse.idx);
        if (faceDict.TryAdd(t3.idx, t3)) latestIndex++;
        else Debug.Log("indexing error in recurse single face!");

        triangle t4 = new triangle(latestIndex, newDepth, b, c, a, faceToRecurse.idx);
        if (faceDict.TryAdd(t4.idx, t4)) latestIndex++;
        else Debug.Log("indexing error in recurse single face!");

        //assign descendants
        faceToRecurse.descendant1 = t1.idx;
        faceToRecurse.descendant2 = t2.idx;
        faceToRecurse.descendant3 = t3.idx;
        faceToRecurse.descendant4 = t4.idx;

        //update adjacencies
        updateDescendantAdjacencies(faceToRecurse);

        //set parent to invisible
        faceToRecurse.visible = false;
    }

    //TODO: Verify whether this is correct
    private int determineWhetherVertexExists(triangle parent, Vector3 vertex)
    {
        int query = -1;

        for(int i = 0; i < 3; i++)
        {
            int adj = retrieveAdjacency(parent, i);
            triangle a = faceDict[adj];
            if (a.descendant4 == -1) continue;
            triangle testForExistence = faceDict[a.descendant4];
            if (vertex == vertexDict[testForExistence.a]) return testForExistence.a;
            if (vertex == vertexDict[testForExistence.b]) return testForExistence.b;
            if (vertex == vertexDict[testForExistence.c]) return testForExistence.c;
        }
        return query;
    }

    private int retrieveAdjacency(triangle t, int adj)
    {
        int adjIdx = -1;
        if (adj == 1) adjIdx = t.adjacency1;
        else if (adj == 2) adjIdx = t.adjacency2;
        else if (adj == 3) adjIdx = t.adjacency3;

        return adjIdx;
    }

    private int retrieveVertex(triangle t, int vertex)
    {
        int vec = -1;
        if (vertex == 0) vec = t.a;
        else if (vertex == 1) vec = t.b;
        else if (vertex == 2) vec = t.c;
        return vec;
    }

    public Vector3 applyHeightmap(Vector3 vertex, HeightmapType type = HeightmapType.Spherical)//Takes a given vertex and based on instance of another class determines the height of this new point
    {
        //HeightmapType might also be something applied when an instance of "sphereGenerator" is created and applies to the entire process

        //calculate multiplier to initial radius

        //calculate location on unit sphere * (r + m)

        //return new location

        vertex = addVertexToUnitSphere(vertex) * planetRadius;
        return vertex;
    }   

    private void updateDescendantAdjacencies(triangle t)
    {
        if (t.descendant1 == -1) return;

        updateDescendantAdjacency(faceDict[t.descendant1], true, 1);
        updateDescendantAdjacency(faceDict[t.descendant2], true, 2);
        updateDescendantAdjacency(faceDict[t.descendant3], true, 3);

        faceDict[t.descendant4].adjacency1 = t.descendant3;
        faceDict[t.descendant4].adjacency2 = t.descendant1;
        faceDict[t.descendant4].adjacency3 = t.descendant2;
    }

    public void updateDescendantAdjacency(triangle t, bool recurse, int descendant)//Updates a single descendants adjacency
    {//will discover and fill the slots in triangle t "adjacency1, adjacenct2, adjacency3"
        bool isEdge = false;
        triangle parent = faceDict[t.parent];

        //these are the two edges that aren't D4 of the same parent
        triangle parentalNeighborA;
        triangle parentalNeighborB;

        triangle adjacencyA;
        triangle adjacencyB;

        // Obtain relevant parental adjacencies & set D4
        if (descendant == 1) 
        {
            parentalNeighborA = faceDict[parent.adjacency1];
            parentalNeighborB = faceDict[parent.adjacency3];
            t.adjacency2 = parent.descendant4;
        }
        else if (descendant == 2)
        {
            parentalNeighborA = faceDict[parent.adjacency1];
            parentalNeighborB = faceDict[parent.adjacency2];
            t.adjacency3 = parent.descendant4;
        }
        else//descendant == 3
        {
            parentalNeighborA = faceDict[parent.adjacency2];
            parentalNeighborB = faceDict[parent.adjacency3];
            t.adjacency1 = parent.descendant4;
        }

        // See how deeply parental neighbor A recurses.
        triangle deepestDescendantOfParentalNeighborA = descendToRecursiveLevel(parentalNeighborA, t.depth);

        // Either we will reach a matching depth, or we will reach a more shallow depth.
        if (deepestDescendantOfParentalNeighborA.depth < t.depth)
        {
            // If we have reached a more shallow depth, this triangle is t's neighbor.
            //adjacencyA = scanForSharedVertices(t, deepestDescendantOfParentalNeighborA);
            adjacencyA = deepestDescendantOfParentalNeighborA;
            isEdge = true;
            //isEdge will add the value to our tessellationEdges hashset
            //no need to update adjacency, as it will not point to a smaller depth triangle
        }
        else
        {
            //triangle t is same or shallower
            triangle p = faceDict[deepestDescendantOfParentalNeighborA.parent];
            adjacencyA = scanForSharedVertices(t, p);
            if (recurse)//Could be improved
            {
                updateDescendantAdjacency(faceDict[p.descendant1], false, 1);
                updateDescendantAdjacency(faceDict[p.descendant2], false, 2);
                updateDescendantAdjacency(faceDict[p.descendant3], false, 3);
            }
        }

        // See how deeply parental neighbor B recurses. 
        triangle deepestDescendantOfParentalNeighborB = descendToRecursiveLevel(parentalNeighborB, t.depth);

        if (deepestDescendantOfParentalNeighborB.depth < t.depth)
        {
            //adjacencyB = scanForSharedVertices(t, deepestDescendantOfParentalNeighborB);
            adjacencyB = deepestDescendantOfParentalNeighborB;
            isEdge = true;
        }
        else
        {
            triangle p = faceDict[deepestDescendantOfParentalNeighborB.parent];
            adjacencyB = scanForSharedVertices(t, p);

            if (recurse)
            {
                updateDescendantAdjacency(faceDict[p.descendant1], false, 1);
                updateDescendantAdjacency(faceDict[p.descendant2], false, 2);
                updateDescendantAdjacency(faceDict[p.descendant3], false, 3);
            }
        }

        //update t
        if (descendant == 1)
        {
            t.adjacency1 = adjacencyA.idx;
            t.adjacency3 = adjacencyB.idx;
        }
        else if (descendant == 2)
        {
            t.adjacency1 = adjacencyA.idx;
            t.adjacency2 = adjacencyB.idx;
        }
        else if (descendant == 3)
        {
            t.adjacency2 = adjacencyA.idx;
            t.adjacency3 = adjacencyB.idx;
        }

        //safely determines whether or not the point is an egde. Dynamically updates when this function is recursively called.
        if (isEdge) tessellationEdges.Add(t.idx);
        else tessellationEdges.Remove(t.idx);
    }

    // Probe from Triangle t to the specified recursive depth.
    // If the level of depth does not exist, return the deepest available depth.
    private triangle descendToRecursiveLevel(triangle t, int targetDepth)
    {
        // If we are at the target depth, return this triangle.
        if (t.depth == targetDepth)
        {
            return t;
        }

        // If this triangle has no descendants, return this triangle.
        if (t.descendant1 == -1 || t.descendant2 == -1 || t.descendant3 == -1 || t.descendant4 == -1)
        {
            return t;
        }

        // Otherwise, scan for descendants.
        int currentDepth = t.depth;

        // Get the deepest level available of each descendant.
        triangle deepestDescendant1 = descendToRecursiveLevel(faceDict[t.descendant1], targetDepth);
        triangle deepestDescendant2 = descendToRecursiveLevel(faceDict[t.descendant2], targetDepth);
        triangle deepestDescendant3 = descendToRecursiveLevel(faceDict[t.descendant3], targetDepth);
        triangle deepestDescendant4 = descendToRecursiveLevel(faceDict[t.descendant4], targetDepth);

        // Find the deepest descendant of the deepest descendants.
        triangle deepest = deepestDescendant1;
        deepest = (deepestDescendant2.depth > deepest.depth) ? deepestDescendant2 : deepest;
        deepest = (deepestDescendant3.depth > deepest.depth) ? deepestDescendant3 : deepest;
        deepest = (deepestDescendant4.depth > deepest.depth) ? deepestDescendant4 : deepest;

        return deepest;
    }
    private triangle scanForSharedVertices(triangle scanningFrom, triangle scanDescendantsOf)
    {
        // For each codescendant,
        triangle adjacency = null;

        for(int i = 0; i < 3; i++)
        {
            triangle potentialAdjacency = faceDict[scanDescendantsOf.descendant1];
            if (i == 1) potentialAdjacency = faceDict[scanDescendantsOf.descendant2];
            else if (i == 2) potentialAdjacency = faceDict[scanDescendantsOf.descendant3];

            // For each vertex of this codescendant,
            int sharedVertices = 0;
            for(int j = 0; j < 3; j++)
            {
                Vector3 vertex = vertexDict[potentialAdjacency.c];
                if(j == 0) vertex = vertexDict[potentialAdjacency.a];
                if(j == 1) vertex = vertexDict[potentialAdjacency.b];
                // If the vertex matches one of the vertices of the triangle we are scanning from, log it
                if (vertex.Equals(scanningFrom.a) || vertex.Equals(scanningFrom.b) || vertex.Equals(scanningFrom.c))
                {
                    sharedVertices++;
                }
                // If we have found two vertex matches, this is our adjacency; break
                if (sharedVertices >= 2)
                {
                    adjacency = potentialAdjacency;
                    break;
                }
            }
        }
        if(adjacency == null) Debug.Log("scan for shared vertices error!");
        return adjacency;
    }



    float timeToUpdate = 1;
    float time = 0;

    private void Update()
    {
        time += Time.deltaTime;
        if(time > timeToUpdate)
        {
            UpdateFace0();
            time = 0;
        }
    }

    #region ChunkMethods
    public Dictionary<int, int> pruneChunks(int observerRecursiveDepth, Dictionary<int, int> chunks)//TODO
    {
        //prune chunks based on player distance. Should ensure chunks always hold roughly the same amount of total triangles.



        return chunks;
    }

    public void divideChunks(HashSet<int> chunks, int numMeshes, int center)//TODO
    {
        //uses SO instances of chunk gameobjects to store more mesh data 
        //foreach chunk -> store correct verts and faces -> update mesh
        //each chunk is only used for MESH RENDERING and COLLIDER PHYSICS, there is NO generation logic
        //should NOT update chunk collision data (for now).
        int numBaseChunks = chunks.Count;
        int baseChunksPerMesh = Mathf.CeilToInt(numBaseChunks / numMeshes);
        int[][] chunkData = new int[numMeshes][];

        int meshNum = 0;
        while(chunks.Count != 0)
        {//TODO: Test, plan, and improve this function
         //idea: determine angle with respect to center triangle, theta, and divide angular regions into 16 to keep packed
         //however angle determination could be difficult
            chunkData[meshNum] = new int[baseChunksPerMesh];
            for(int i = 0; i < baseChunksPerMesh; i++)
            {
                int item = chunks.First();
                chunkData[meshNum][i] = item;
                chunks.Remove(item);
            }
            meshNum++;
        }
        
        for (int i = 0; i < meshNum; i++)
        {
            buildChunk(chunkData[i], i);
        }
    }

    private void buildChunk(int[] faces, int meshNum)
    {
        List<int> chunkTriangles = new List<int>();

        //O(n) -- prepares every face
        foreach(int face in faces)
        {
            buildPosterity(face, chunkTriangles);
        }
        //now chunkTriangles contains every triangle necessary to construct mesh

        // Initialize collections. 
        Dictionary<Vector3, int> tempVertexDict = new Dictionary<Vector3, int>();
        Vector3[] chunkVertices;
        List<int> chunkFaces = new List<int>();

        // Initialize Counters
        int currentVertex = 0;

        // For each triangle in the Icosphere
        //O(n)
        foreach (int face in chunkTriangles)
        {
            triangle t = faceDict[face];

            if (!t.visible)
            {
                continue;
            }

            Vector3[] vertices = vertexModifier(t);
            foreach(Vector3 vertex in vertices)
            {
                if (tempVertexDict.TryAdd(vertex, currentVertex)) currentVertex++;
                chunkFaces.Add(tempVertexDict[vertex]);
            }
        }

        chunkVertices = new Vector3[tempVertexDict.Count];

        //O(n)
        foreach (Vector3 vertex in tempVertexDict.Keys)
        {
            vertices[tempVertexDict[vertex]] = vertex;
        }

        //now chunkVertices and chunkFaces are built and can be sent off to their mesh
        planetaryChunk PC = chunkMeshes[meshNum].GetComponent<planetaryChunk>();
        PC.createPlanetChunk(chunkVertices, chunkFaces);
    }

    void buildPosterity(int face, List<int> posterityVertices)
    {
        if (face == -1) return;
        triangle t = faceDict[face];

        if (t.visible) posterityVertices.Add(face);
        else
        {
            buildPosterity(t.descendant1, posterityVertices);
            buildPosterity(t.descendant2, posterityVertices);
            buildPosterity(t.descendant3, posterityVertices);
            buildPosterity(t.descendant4, posterityVertices);
        }
    }
    //TODO: Not important, but minor optimization that adds frustum culling from space and absolutely nothing else
    private void fillChunk(HashSet<int> chunks, int currentChunk, int chunkIdx, int baseChunksPerMesh, Dictionary<int, int> chunkToIdx, Dictionary<int, int> idxToChunk)
    {
        triangle t = faceDict[chunkIdx];




    }

    #region seamModifierMethods
    Vector3[] vertexModifier(triangle t)
    {
        Vector3[] newVerts = new Vector3[3];
        HashSet<int> shallowAdjacencies = new HashSet<int>();

        for (int i = 0; i < 3; i++)
        {
            //fills new verts with old triangle indices
            newVerts[i] = vertexDict[retrieveVertex(t, i)];

            //checks whether immediate adjacencies are shallow
            triangle sibling = faceDict[retrieveAdjacency(t, i)];
            if (sibling.depth < t.depth) shallowAdjacencies.Add(sibling.idx);

            for (int j = 0; j < 3; j++)
            {
                //checks whether cousin adjacencies are shallow
                triangle cousin = faceDict[retrieveAdjacency(sibling, j)];
                if (cousin.depth < t.depth) shallowAdjacencies.Add(cousin.idx);
            }
        }

        //all shallow candidates have been recorded. Now to update each vertex with the applicable adjacency.
        foreach(int shallowAdjacency in shallowAdjacencies)
        {
            for (int i = 0; i < 3; i++)
            {
                int vert = retrieveVertex(t, i);
                Vector3 tempVec = updateVertex(t, faceDict[shallowAdjacency], vert);
                if (tempVec == Vector3.negativeInfinity) continue;
                newVerts[i] = tempVec;
            }
        }

        return newVerts;
    }

    Vector3 updateVertex(triangle t, triangle shallowAdjacency, int vert)
    {
        Vector3 newVec = vertexDict[vert];

        Vector3 v1 = Vector3.zero;
        Vector3 v2 = Vector3.negativeInfinity;

        int points = 0;
        triangle p = faceDict[t.parent];
        
        while(points < 2)
        {
            points = 0;
            int a = p.a;
            int b = p.b;
            int c = p.c;
            for (int i = 0; i < 3; i++)
            {
                int v = retrieveVertex(shallowAdjacency, i);
                if (hasVertex(p, v)) points++;
                if (points == 1) v1 = vertexDict[v];
                else if (points == 2)
                {
                    v2 = vertexDict[v];
                    break;
                }
            }
            if(p.parent == -1 && points < 2)
            {
                Debug.Log("vertex updating issue! (expected on triangles w/ greater than 2 adjacencies)");
                return v2;
            }
            //climb the quad tree
            p = faceDict[p.parent];
        }

        float interpolant = determinePointInterpolant(v1, v2, newVec);
        return Vector3.Lerp(v1, v1, interpolant);
    }

    bool hasVertex(triangle t, int vert)
    {
        return (t.a == vert || t.b == vert || t.c == vert);
    }
    private float determinePointInterpolant(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        return Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab);
    }

    #endregion

    private void createChunks(int chunkDepth)
    {
        //A.) determine view radius
        //first, calculate centralmost triangle of depth baseChunk
        int center = determineCenterTriangle(chunkDepth);

        //determine dist from center and radius of cone
        float distFromCenterSqr = Vector3.Dot(playerLoc, playerLoc);
        float hypotenuseSqr = distFromCenterSqr + planetRadius * planetRadius;//a^2 + b^2 = c^2

        //determine whether hypotenuse ray intersects the surface
        float theta = planetRadius * planetRadius / (playerLoc.x * playerLoc.x + playerLoc.y * playerLoc.y + playerLoc.z * playerLoc.z);

        HashSet<int> currentBaseChunks = angularPingPong(center, theta);
        //B.) construct all triangles and their meshes
        divideChunks(currentBaseChunks, chunkMeshes.Length, center);
    }

    #endregion

    #region TessellationMethods
    private void tessellate()
    {
        //Called when the player's center triangle changes by a certain threshold
        //TODO: Either change packing to just be re-running tessellate() every time OR, if superior packing method, change tessellate(); to also perform outer ring searches
        int center = determineCenterTriangle();
        byte goalDepth = playerDistToSurface();
        byte currentDepth = faceDict[center].depth;
        int differential = goalDepth - currentDepth;
        float[] approximateSqrRingRadii = new float[maxDepth];
        for (int i = 0; i < differential; i++)
        {
            unpackDetail(center, (byte)(currentDepth + 1 + i), approximateSqrRingRadii);
            center = determineNearestDescendant(center);
        }
        Debug.Log("Center now has depth: " + faceDict[center].depth);
        //if(pack) packDetail(center, approximateSqrRingRadii);//automatically accounts for differential
    }

    void packDetail(int center, float[] approximateSqrRingRadii)//This function will dynamically subtract all faces less than the center & prune incorrect edges
    {
        float approximationFactor = 1.2f;
        foreach(int idx in tessellationEdges)
        {
            triangle t = faceDict[idx];
            if(approximateDistSqrToCenter(idx, center) > approximateSqrRingRadii[t.depth] * approximationFactor)//determines whether a triangle should be pruned
            {
                pruneEdge(idx);
            }
        }
    }

    void pruneEdge(int idx)
    {
        triangle t = faceDict[idx];
        triangle p = faceDict[t.parent];

        int[] toRemove =
        {
            p.descendant1,
            p.descendant2,
            p.descendant3,
            p.descendant4
        };

        for (int i= 0; i < toRemove.Length; i++)
        {
            faceDict.Remove(toRemove[i]);
        }

        p.descendant1 = -1;
        p.descendant2 = -1;
        p.descendant3 = -1;
        p.descendant4 = -1;

        p.visible = true;

        updateDescendantAdjacencies(faceDict[p.adjacency1]);
        updateDescendantAdjacencies(faceDict[p.adjacency2]);
        updateDescendantAdjacencies(faceDict[p.adjacency3]);

    }

    void unpackDetail(int center, byte depth, float[] approximateSqrRingRadii)//This function will dynamically tessellate once to the region around the center
    {
        HashSet<int> trianglesToTessellate = ringSearch(center, depth, approximateSqrRingRadii);
        foreach(int idx in trianglesToTessellate)
        {
            triangle t = faceDict[idx];
            if (t.descendant1 != -1) continue;
            recurseSingleFace(idx);
        }
    }

    public HashSet<int> ringSearch(int center, byte depth, float[] approximateSqrRingRadii)
    {
        // Initialize Collections
        HashSet<int> adjacencies = new HashSet<int>();

        // Add the origin to the collection of all adjacencies and make it ring 1
        adjacencies.Add(center);

        // Begin ring search
        radialPingPong(adjacencies, 2 * depth + 1, center, approximateSqrRingRadii);
        return adjacencies;
    }
    
    private void radialPingPong(HashSet<int> adjacencies, int targetRadius, int center, float[] approximateSqrRingRadii)
    {
        HashSet<int> outermostRing = new HashSet<int>(adjacencies);
        int currentRadius = 0;
        while (currentRadius < targetRadius)
        {
            HashSet<int> buffer = new HashSet<int>();//this stores our ring for next frame

            foreach (int idx in outermostRing)
            {
                triangle t = faceDict[idx];
                int adj1 = t.adjacency1;
                int adj2 = t.adjacency2;
                int adj3 = t.adjacency3;

                if (faceDict[adj1].visible)
                {
                    if (adjacencies.Add(adj1))
                    {
                        //if adjacencies doesn't contain this new adjacency, then it must be outside the currently valid radius
                        //therefore it belongs to next iteration's ring
                        buffer.Add(adj1);
                    }
                }
                if (faceDict[adj2].visible)
                {
                    if (adjacencies.Add(adj2))
                    {
                        buffer.Add(adj2);
                    }
                }
                if (faceDict[adj3].visible)
                {
                    if (adjacencies.Add(adj3))
                    {
                        buffer.Add(adj3);
                    }
                }

            }
            //ping-pong
            outermostRing.Clear();
            outermostRing = buffer;
            //iterate
            currentRadius++;
        }
        int outer = outermostRing.First();
        float ringDist = approximateDistSqrToCenter(outer, center);
        approximateSqrRingRadii[Mathf.Clamp(faceDict[outer].depth - 1, 0, maxDepth - 1)] = ringDist;
    }

    private HashSet<int> angularPingPong(int center, float angleTheta)
    {
        HashSet<int> adjacencies = new HashSet<int>();
        HashSet<int> outermostRing = new HashSet<int>(adjacencies);

        float angleRho = 0;
        while (angleTheta >= angleRho)
        {
            HashSet<int> buffer = new HashSet<int>();//this stores our ring for next frame

            foreach (int idx in outermostRing)
            {
                triangle t = faceDict[idx];
                int adj1 = t.adjacency1;
                int adj2 = t.adjacency2;
                int adj3 = t.adjacency3;

                if (faceDict[adj1].visible)
                {
                    if (adjacencies.Add(adj1))
                    {
                        //if adjacencies doesn't contain this new adjacency, then it must be outside the currently valid radius
                        //therefore it belongs to next iteration's ring
                        buffer.Add(adj1);
                    }
                }
                if (faceDict[adj2].visible)
                {
                    if (adjacencies.Add(adj2))
                    {
                        buffer.Add(adj2);
                    }
                }
                if (faceDict[adj3].visible)
                {
                    if (adjacencies.Add(adj3))
                    {
                        buffer.Add(adj3);
                    }
                }

            }
            //ping-pong
            outermostRing.Clear();
            outermostRing = buffer;
            //iterate
            angleRho = determineRho(outermostRing.First());
        }

        return adjacencies;
    }

    private float determineRho(int faceIdx)
    {
        triangle t = faceDict[faceIdx];
        Vector3 chunkCenter = triangleCenter(t);
        float rho = Vector3.Dot(playerLoc, chunkCenter);
        return rho;
    }

    private int determineCenterTriangle(int depth = -1)
    {//O(log(n) + 20) function that finds triangle closest to player
        float minDist = Mathf.Infinity;
        int nearestTriangle = -1;
        for(int i = 0; i < 20; i++)
        {
            triangle t = faceDict[i];
            float distSqr = Vector3.Dot(triangleCenter(t) - playerLoc, triangleCenter(t) - playerLoc);
            if (distSqr < minDist)
            {
                minDist = distSqr;
                nearestTriangle = i;
            }
        }
        int center = determineNearestDescendant(nearestTriangle, depth);
        return center;
    }

    private int determineNearestDescendant(int idx, int depth = -1)
    {
        triangle t = faceDict[idx];
        if (t.descendant1 == -1 || (int)t.depth == depth) return t.idx;
        int[] descendants = new int[]
        {
            t.descendant1, t.descendant2, t.descendant3, t.descendant4
        };

        float minDist = Mathf.Infinity;
        int nearestTriangle = idx;
        for (int i = 0; i < 4; i++)
        {
            triangle d = faceDict[descendants[i]];
            float distSqr = Vector3.Dot(triangleCenter(d) - playerLoc, triangleCenter(d) - playerLoc);
            if (distSqr < minDist)
            {
                minDist = distSqr;
                nearestTriangle = descendants[i];
            }
        }
        return determineNearestDescendant(nearestTriangle);
    }

    private Vector3 triangleCenter(triangle t)
    {
        return (vertexDict[t.a] + vertexDict[t.b] + vertexDict[t.c]) / 3;
    }

    //this function compared the number of necessary adjacencies to travel to the center
    //to the initial triangles depth and checks whether it is in the expected range
    private int pruneBy(int idx, int center)
    {
        int numAdjacencies = numAdjacenciesToCenter(idx, center);
        byte expectedDepth = determineDepthByAdjacencies(numAdjacencies, faceDict[center].depth);

        int differential = faceDict[idx].depth - expectedDepth;
        return differential;
    }

    private byte determineDepthByAdjacencies(int numAdjacencies, byte centerDepth)
    {
        int depth = centerDepth;
        int scale = (int)Mathf.Pow(2, depth);
        int maxAdjacencies = (depth * 2 + 1) * scale;
        while (numAdjacencies > maxAdjacencies)
        {
            depth--;
            if(depth < 0)
            {
                Debug.Log("depth error in pruning!");
                return 0;
            }
            scale /= 3;
            maxAdjacencies += (2 * depth + 1) * scale;
        }
        return (byte)depth;
    }

    private float approximateDistSqrToCenter(int idx, int centerIdx)
    {
        triangle t = faceDict[idx];
        triangle center = faceDict[centerIdx];

        Vector3 distVector = triangleCenter(t) - triangleCenter(center);
        return Vector3.Dot(distVector, distVector);
    }

    //since this function is only used for packing,
    //triangles in need of packing will ALWAYS have a dist to the center that is
    //too large
    private int numAdjacenciesToCenter(int idx, int centerIdx)
    {
        triangle t = faceDict[idx];
        triangle center = faceDict[centerIdx];
        Vector3 centerPos = triangleCenter(center);
        int numAdjacencies = 0;

        while(t.idx != centerIdx)
        {
            float minDistanceSqr = Mathf.Infinity;

            Vector3 adj1 = triangleCenter(faceDict[t.adjacency1]);
            Vector3 adj2 = triangleCenter(faceDict[t.adjacency2]);
            Vector3 adj3 = triangleCenter(faceDict[t.adjacency3]);

            if(Vector3.Dot(adj1, centerPos) < minDistanceSqr)
            {
                minDistanceSqr = Vector3.Dot(adj1, centerPos);
                t = faceDict[t.adjacency1];
            }
            if (Vector3.Dot(adj2, centerPos) < minDistanceSqr)
            {
                minDistanceSqr = Vector3.Dot(adj2, centerPos);
                t = faceDict[t.adjacency2];
            }
            if (Vector3.Dot(adj3, centerPos) < minDistanceSqr)
            {
                minDistanceSqr = Vector3.Dot(adj3, centerPos);
                t = faceDict[t.adjacency3];
            }

            numAdjacencies++;
        }
        return numAdjacencies;
    }

    private byte playerDistToSurface()
    {
        byte recursive = 0;
        float playerDistToCenterSqr = Vector3.Dot(playerLoc, playerLoc);
        if (playerDistToCenterSqr > planetRadius * planetRadius * 4) return recursive;
        float playerDistToSurface = (playerLoc - applyHeightmap(playerLoc)).magnitude;

        float ratio = Mathf.Clamp01(1 - playerDistToSurface / (planetRadius));//LOD system based on planet radius
        float modifiedRatio = ratio * ratio * maxDepth;//Scales the ratio by some amount. Currenly squares it.

        recursive = (byte)Math.Floor(modifiedRatio);
        return recursive;
    }

    #endregion

    private void OnValidate()
    {
        if(update == true)
        {
            update = false;
            GenerateSphere();
        }
        if(tessellateFace0 == true)
        {
            tessellateFace0 = false;
            UpdateFace0();
        }
    }






}
