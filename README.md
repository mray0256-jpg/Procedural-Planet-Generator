# Procedural-Planet-Generator
Into graphics programming project inspired by the video game Outer Wilds.

<img width="480" height="380" alt="image" src="https://github.com/user-attachments/assets/aefffacc-fcda-45a0-b317-c3a0cd389a4b" /><br>
*Procedural planet with physically-based atmosphere and clouds*

## Overview
- A project created to simulate planet generation. Currently a WIP with the hopes of being updated every month or two.
- I chose this project primarily because I liked the idea of a modular sort of simulation, in which prior algorithms were only indirectly related to future ones, allowing for less fear of breaking something & nonlinear features.

## Tech Stack
- Engine: Unity
- Language: C#, HLSL

## Project Setup
This project contains all of the files to be run locally.

## Technical Highlights

- **Procedural planet generation using an icosphere mesh**

- **GPU-based terrain generation via compute shaders**, using diffusion limited aggregation to produce believable mountain shapes at runtime

- **Tectonic plate simulation system**, generating plate boundaries and Euler poles to organically determine large-scale terrain features such as mountain

- **Physically-based atmospheric scattering model**, including Rayleigh scattering and optical depth integration using ray marching through a spherical atmosphere

- **Volumetric noise-based clouds**, including fractal brownian motion, eroded perlin-worley noise, raymarching, and dual-lobe scattering
  
- **Modular procedural systems architecture**, enabling independent tuning of terrain, atmosphere, and cloud generation parameters in the editor at runtime

## Technical details & what I learned
-  Note before you begin reading: I have been working on this for about four months. Alongside progress on the project, you will see me progress as a graphics programmer and Unity developer. I first learned how to code basic python about a year ago, and I first started Unity seven months ago. The first few sections are unpolished and amatuer, but I think they show growth so I've left them largely untouched.

- This project has consisted almost entirely of math. I prefer this to my other projects, because of all the skills for development I'm most confident in my math ability. I've decided to split the project into phases due to it's modular nature. The first "phase" of the project was creating a sphere. I could've used a generic UV sphere supplied by blender or unity, but I wanted to generate my own to have fine control.
  
- **Icosphere:**  
  This was a lot of index math. I have pages and pages of subdivided triangles with illegibly scribbled numbers that somehow made sense as I made it. If I redid it, I probably would've spent more time making an ironclad winding pattern so the vertices and faces are generated neatly, but as of now they're somewhat messy. As a result, I had to make redundancy-checker funcitons that are not exactly optimal. Additionally, there is no tessellation and no plans for it; but if I were to improve the sphere, that would be on the list.

<img width="430" height="260" alt="Screenshot 2025-12-23 111323" src="https://github.com/user-attachments/assets/948ef4d8-d4c1-4f6c-bb24-118f8b0642ed" /><br>
  *Figure 1: Subdivided Icosahedron*

  - **How it's done:**  
    When I first wrote the subdividing methods, it was mapped to a single triangle, then to an octohedron, _then_ to an icosahedron. I started with a list of predetermined vectors and their faces corresponding to a base shape. To subdivide its triangles, my method of choice was lerp abuse. I wrote a nested loop in code, in which the first loop determines the vertical layer, interpolating from bottom to top, and the inner loop interpolates from left to right. When looking at a subdivided triangle, the amount of vertices in each layer decreases by 1. Together, I used these properties to create vertices and their faces. After this process was finished, I added a simple method that would loop through each point and normalize their radius to the center. To do this, the current distance of a vertex from the center was divided by the goal radius, then that scalar ratio was multiplied to the vector position.

    Equation for number of vertices

    $\ numVerts = 12+30(n)+20(\frac{n(n-1)}{2})$

    with the approximation

    $\ numVerts ≈ 10(n)^2 $

    Then, using the vertices we can generate faces
    
    $\ numFaces = 20(n+1)+40(\frac{n(n+1)}{2}) $

    with the approximation

    $\ numFaces ≈ 20(n)^2 $

    where n is the number of subdivisions and both approximations are accurate to $\log_{10}(n)$ digits.

<img width="502" height="282" alt="Screenshot 2025-12-23 111339" src="https://github.com/user-attachments/assets/1410552d-5ebd-4ad2-8671-26d51ba5620b" /><br>
  *Figure 2: Subdivided Icosahedron with Normalized Radius & Vertex Visuals*

- **Phase 2: Tectonics**  
  The second phase was making tectonics for the planet. I wanted this so I could make semi-realistic land formations based on the collisions of tectonic plates. These could have been created through a number of strategies, but I chose one I hadn't seen before. To start, a common method for generating tectonic plates is choosing a random vertex on the surface of the sphere, then checking a radius around said point. If other vertices don't belong to a plate, they now belong to the same plate that inital vertex does. Now, this is quite basic and only forms circles; most people move on to more advanced fractal algorithms to achieve the blobby shape tectonics on Earth make. I decided that was a convoluted solution. Instead, I changed the radius from a constant into an equation. Using trig functions, I made the outer bounds have that characteristic blobby, deformed shape; then, atop that, I added randomized constants that determine the wavelength of the trig functions. This resulted in shapes I am quite proud of, especially as it's a relatively simply solution that I hadn't seen before.

  After grouping vertices into respective tectonics, I needed to add two things: oceanic vs continetal tectonics, and direction vectors of these tectonics. To achieve this, and to color them, I used a technique I'd learned from an Outer Wilds technical video (linked at the bottom): using color channels to store information. The R, G, and B channels store the X, Y, and Z elements of a unit direction vector (determined by Euler poles, also linked below). The alpha channel holds two pieces of info in one integer: the plate ID of the vertex, and whether it's oceanic or tectonic.

  - **How it's done:**  
    First, the equation used for the tectonic blobs is

    $\ x^2 + y^2 + z^2 + \frac{r}{\alpha}\cos(ax) + \frac{r}{\alpha}\sin(by) + \frac{r}{\alpha}\cos(cz) = r $

    where
    
    $\ r = \frac{4 \cdot \text{Radius}^2}{\text{numTectonics} \cdot d} $
    
    <img width="595" height="323" alt="Screenshot 2025-12-23 112142" src="https://github.com/user-attachments/assets/a76dda5f-3ae5-4707-b884-6368e13e1b8c" /><br>
*Figure 3: Tectonic Radius*

    Let's talk about r. I needed each tectonic plate to cover roughly $\frac{1}{desiredTectonics}$ surface area of the initial sphere. So, I had a question: if a sphere, of radius _R_, has an idential sphere generated on it's edge, how much of the first sphere's surface area does that second sphere encapsulate? First, we find the points of intersection when represented as a circle. Since the spheres have identical radii, these points are a distance R. Similarly, from center to center is also R. This creates two equilateral triangles. Thus, our angle, $\theta$, is 60 degrees.

    Using spherical coordinates & calculus, we could set
    
    $\ \rho = R,\quad \phi = \frac{\pi}{3},\quad \theta = 2\pi $

    Now we can create an integral and calculate the volume shared between the spheres.

    $\ \int_{0}^{2\pi} \int_{0}^{\frac{\pi}{3}} \int_{0}^{R} \rho^2 \sin(\phi)\ d\rho\ d\phi\ d\theta = \frac{\pi R^3}{3} $

    We can divide the newlyfound shared volume by the total volume of a sphere, yielding a ratio. This can then be applied to our surface area.

    <img width="594" height="325" alt="Screenshot 2025-12-23 111440" src="https://github.com/user-attachments/assets/cdaf0870-2ff9-49a5-944f-7d22ae4f4fb0" /><br>
    <img width="471" height="265" alt="Screenshot 2025-12-23 111403" src="https://github.com/user-attachments/assets/5c4b7431-0f82-4fed-8de8-fb49c7bc2e12" /><br>
*Figures 4 & 5: Area / Volume Enclosed*

    $\ (\frac{\pi R^3}{3})(\frac{3}{4\pi R^3}) = \frac{1}{4} $
    
    $4 \cdot R^2$ would encapsulate the whole of the sphere; instead, we want a portion. If we were to divide by exactly numTectonics, the inherent randomization winds up making _more_ plates than intended. To counteract, we add a factor _d_ where $d < 1$ decreases the amount of extra tectonics plates added. I've found that the preferred constant is $\ d = \frac{2}{3} $.

    <img width="493" height="332" alt="Screenshot 2025-12-23 111416" src="https://github.com/user-attachments/assets/9ac6ae40-97a0-4bc9-820b-ef23bd1cc4aa" /><br>
*Figure 6: Graph of Encapsulation Ratio*

    Now that we can divide our sphere into neat portions, we need to create our blobby sphere. To do this, adding randomized trig functions suffices; however, these need to be scaled by the radius so their wavy edges have the same amplitude regardless of said radius. I chose to multiply the functions by $\frac{r}{\alpha}$, where r keeps the scale and $\alpha$ is a tweaking constant (currently $\ \alpha = 6 $). Then, the inside of the trig functions is simply a wavelength dependent on where in 3D space the vertex lies. In the end, the function is roughly pythagorean theorem with some pizzazz.

    The next topic is the data of each plate. Tectonic plates don't translate linearly; rather, they rotate about an arbitrary axis piercing the Earth. These axes are referred to as Euler poles. To calculate where a plate will rotate, we take a cross product of the vertex's vector to the radius by the vector of the Euler pole. Then, we apply this data to each vertex within the plate.

    Great! This data, along with the plateID and whether it was oceanic or tectonic, was all baked into a color channel. Looking back, this was not as ideal as my optimistic heart said it would be. It turned out fine, but the amount of "this must be the most ridiculously overly-complex code I've ever seen" was truly breathtaking. Colors are stored as bytes, which as I learned painstakingly, do not hold negative values or floats (shocking, I know).

    To remedy this, my solution was to utilize all three digits for information. For the plateIDs, since there would never be greater than 99, I made the first two digits the plate number and the third the plate type (oceanic/contintental). Similarly, the vectors were normalized, then multiplied by 100 (to fill the whole numbers 0 - 99) and given +100 if they were negative. To decode the data, I used the modulus command to find the first two numbers then a conditional for whether the number was >= 100. Now, this perhaps doesn't sound so bad, but my code, which was repeated in various ways, wound up looking as such:

    ```csharp
    float xDir = (float)(planet.colors32[i].r % 100 * (planet.colors32[i].r >= 100 ? -1f : 1f)) / 100f;
    ```

    Horrible, let me tell you.

    Anyways, now that I had all the data needed, I had to visualize it. I made a struct for each boundary, which acted as a single line between two points, then made a list of all boundaries. To fill this list, I learned about hashsets and combined them with dictionaries. I made a method, DetermineNeighbors, which fills a dictionary whose values are vertex indices and whose keys are hashsets of the neighboring vertices' indices. Then, using this dictionary, I compared the plateID belonging to each vertex and it's neighbors' to fill out the boundaries list.

    The boundary struct contains a color value and a magnitude value. These are determined by a dot product of the first vertex's direction and the second's. If they are negative, i.e. oppose each other, they create a divergent boundary! These are colored blue in gizmos. Conversely, convergent boundaries are colored red. Transform boundaries are colored white.
    
    <img width="394" height="272" alt="Screenshot 2025-12-23 111051" src="https://github.com/user-attachments/assets/ad0a2871-26b8-4473-893a-2f8540df2dee" /><br>
*Figure 6: Tectonic Plates*
  
    The last step of making these tectonics was to color the planet. The planet has a material whose shader can change dependent on the phase. The tectonics shader simply shades oceanic tectonics blue, continental green, and creates a border around landmasses. Additionally, to differentiate between plates, each plate is shaded darker as their IDs increases. This is achieved through $\\frac{plateID}{numTectonics}$ fed into a lerp function that traverses from white to dark-grey (multiplying a color by black loses any prior hue). Finally, we have realistic tectonic plates!

<img width="540" height="300" alt="Screenshot 2025-12-23 111144" src="https://github.com/user-attachments/assets/6824590d-3b06-40f6-b4ff-5114a8cc66f7" /><br>
*Figure 7: Tectonic Plates with Visual Aids*

- **Phase 3: Mountains & Terrain**
- 
  The next, and perhaps the most challenging, step of the planet generation was terrain. As you'll see, this has taken me far more time than the previous two sections. Before I begin, however, I'd like to update some of the previous code and preface this phase with some remarks.

  First, in order to add terrain, I wanted the planet to support far more vertices than currently tractable. My sphere generator from phase one was not cutting it; it was barely churning along at a horrible rate of O(n^2)! This was because points along the borders of the initial icosphere would overlap during the subdivision function. At the time, I put in a temporary solution to find duplicate points and only keep the first point. Before writing my mountain generator, I fixed this. Well, sort of. Eventually, I'll probably spruce things up and house certain mesh related data on the GPU, but for now I simply used a dictionary. Keys are vertex vectors, so duplicates can't be added. The return value is an index, which will always correspond to the first instance of the given vector/vertex. This was necessary because in my original subdivision method I calculate things via indices that may or may not be duplicates, and I didn't feel like rewriting it. Now the script runs in O(n), and generates thousands of vertices with no problem.

![SubdivideIcosphere](https://github.com/user-attachments/assets/26e7f4c6-1138-44ed-ae93-aafd33077642)
*Figure 8: Subdividing an Icosphere*

  Additionally, I had to fix the equations for calculating the collisions of tectonic plates. This seemed like an obvious problem, but wound up being quite a thorn. The issue with the last version was that it took two *vertices* and compared their direction vectors, when it should have compared their movement with the normal vector of the plate bound. If it had compared *faces*, that solution would have been correct. My first attempt at fixing this simply took the normal of the line seperating boundary points, but, as you'll notice in the images above, the boundaries are jagged. This resulted in magnitudes drastically changing from line to line.

  The second attempt involved looking at the boundary points' neighbors. They always share two distinct neighbors. The vast majority of the time, one or both of these neighbors will also share tectonic plates with one of the boundary points. If a boundary and neighbor point belong to the same tectonic plate, I can find the normal of *that* line and it will be the correct normal of the tectonic plate itself. Using a corrected normal, I compared it with the difference of our original boundary vectors by taking a dot product. This results in accurate collision magnitudes. The tectonics generator is still in need of some good ol' optimization, but for now I'm satisfied.

   Be warned, this phase isn't yet finished. At the end I'll walk through the changes and additions I intend on making, but I decided now is a good time to record my progress. Also, my usual work is pretty math-centric, but this time I decided I should challenge myself on the coding side. I wrote the terrain generator almost entirely on the GPU using Unity's compute shaders and Microsoft's HLSL.

 - **3.1: DLA**

   The inital chapter of this phase was writing a massive DLA algorithm that could generate mountains. Diffusion-Limited Aggregation, or DLA, is an algorithm intended to replicate the dendritic fractal shape that resembles coral, lightning, veins, zinc synthesis, and (fortunately for us) mountain ridges. At a high level, this pattern is then taken and blurred until it is a heightmap that sufficiently mirrors a mountain. Traditionally, to run this algorithm a sequential process is used. A heightmap, commonly a texture, is given seeds as desired. These are hand placed "frozen" particles. Then, a new mobile particle is spawned at an arbitrary location. It wanders randomly until a neighboring point happens to be one of these frozen particles. Then, the new particle freezes, sticking to the frozen particle, and the process repeats.

   By simulating the random walking, mesmerizing leichtenburg-like figures are generated. However: this approach comes with a few major problems, namely that it is horribly inefficient. By generating one particle at a time and giving it tremendous amounts of space to walk, it could take *thousands* of iterations for even one particle to freeze. I found a fantastic resource, linked below, walking through this mountain technique. Their solution was to use a process of upscaling and blurring, but it still took place entirely on the CPU. Perhaps it was fast, but I challenged myself to make it *fast*. I decided to tackle a dilemma they deemed impossible: coding the algorithm on the GPU.

<img width="400" height="400" alt="image" src="https://github.com/user-attachments/assets/52f53cc3-c746-4c94-8764-c3bab76eb9ab" /><br>
*Figure 9: DLA Algorithm*

- **How it's done:**  

    I started by creating a struct to store the data of each particle.
 
    ```hlsl
    struct particle
    {
       int idx; //coincident vertex index
       int isMobile; //to freeze a particle (HLSL does not have booleans)
       int frozenIndex; //points to parent
       int step;
       int eldestChild; //points to this particles FIRST child
       int youngerSibling; //a pointer for this particle next youngest sibling
       float magnitude; //used for stickiness & max height
       float particleRNG; //rn between 0-1 a particle is created with
    };
    ```

  The user can create as many particles as they want; more correlates with larger, sprawling mountains. They can also dictate how many seeds they would like to begin with. Once all of these ingredients are prepared, they're sent to the GPU to become a rather scrumptious mountain. The GPU is wonderful because it can run thousands of kernels, or functions, in parallel. The GPU is simultaneously the bane of my existance, because it is so limited in its ability. In the hardware, there exists a bus that can transport signals to and fro. Naturally, as the bus becomes overcrowded, its propensity to create a bottleneck becomes apparent. The intuition is then to segregate data, using the bus as conservatively as possible. However, this route also introduces complexity. As is predestined with every coder, I gleefully marched down the complexity route, blissfully ignorant of its poisons.

  Before I finish that age old tale, let's discuss compute shaders in Unity. 
  
   *"Compute shaders can be unbelievably fast, but they also have this distressing habit of finding new and creative ways of crashing my computer, so it's a bit of a love-hate relationship." - Sebastian Lague*
  
    <img width="448" height="274" alt="Screenshot 2026-01-09 204621" src="https://github.com/user-attachments/assets/385d0cb1-3a38-4bb8-9ffd-2e4e17cb6bd7" /><br>
    <img width="454" height="300" alt="Screenshot 2026-01-16 105044" src="https://github.com/user-attachments/assets/14255d45-49c6-4941-abba-86abd97453ff" /><br>
    <img width="466" height="261" alt="Screenshot 2026-01-18 144034" src="https://github.com/user-attachments/assets/5c1eeafd-e91f-4e2c-b6b3-f79dcb247927" /><br>
*Figures 10, 11 and 12: Bugs!*

  Unity uses C# for CPU scripts and HLSL for GPU scripts. To send data to a Compute Shader, you use what's called a compute buffer. They house arrays of whatever you'd like. For example, I send the particles via a ComputeBuffer. HLSL recieves this, and on the GPU side it can be stored as a few different types of buffers. The common ones are RWStructuredBuffer (read-write) and StructuredBuffer (read only). These buffers are great when you know their exact dimensions and which indices to read.
  
  I met an issue, though. I want to freeze some particles and reuse others. Say I kept all particles in one giant RWStructuredBuffer, and there were 100,000 particles. When there's one particle left the other 99,999 would still have to run! That's not exactly ideal. Luckily for me, there exists another type of buffer: an append buffer. These can either be consumed from or appended to, and they have a special counter that tracks valid members. Their CPU analog would be a list. Now that we have dynamic data structures on the GPU, we can simply use the counter inside to run the GPU cores efficiently. At least, that's the idea. This is where my little complexity problem was introduced. 
  
  To begin a function on the GPU, it must be dispatched on C#. The C# dispatch call has to tell the GPU precisely how many groups (e.g., iterations) should be run. Now, if I have a dynamic buffer, I no longer know how many members there are, so I don't know how many iterations should be run. (For context, each "iteration" consumes a particle from the append buffer). Unity kindly supplies us with a special type of dispatching, though, called DispatchIndirect. This type of function calling does not require the group count from the CPU, and instead supplies the group count from a buffer. Once buffers are set, their data is stored within the GPU's VRAM, which makes it O(1) to call.

  To use dispatch indirect, I first used another function called CopyCount. This takes the append buffer's hidden counter and stores that value inside a different buffer, as a usable element. Now, when dispatch indirect is called, if the group count parameter is the buffer from copycount, it should store an accurate number of iterations. In the end... this didn't work. The complexity route bit me here; because I had introduced so many foreign pieces integral to the algorithm, debugging became significantly harder. Worse still, debugging on the GPU required a lot more creativity than print and try-catch statements everywhere. Despite my best efforts, I never fully figured out why it happened, but I gleaned where. Some way or another DispatchIndirect and CopyCount never worked as intended. It was almost certainly user error of some kind, as this was my first time using compute shaders, but I couldn't find much help online, and forum posts suggested DispatchIndirect had been broken or buggy in the past. Note: if something I've described is erroneous, please reach out! This repository is meant to document learning, not mastery.

  To fix the bus issues, I replaced every usage of DispatchIndirect with my own special counter. I used CopyCount, sending the count to a RW buffer called "_Args", which stored one element: a uint describing the number of particles intended to be manipulated. In every kernel that used consume and append buffers, I added a simple bounds checker with _Args and would return if a particle was decidedly null (HLSL has no built in null check). This describes most of the bus-related issues, but there were two more. Let's talk about race conditions. If you have an array, say particles[], and you read data from the array and replace that data in the same function, what happens on the GPU but wouldn't on a CPU? That's right: garbage! Since every particle is acted on simultaneously, if one is modified as a different thread reads that same data, it will read the *next iterations* data. If that data is then used in a calculation, something might be a little bit off. If that repeats 100,000 times, the problem will exponentially exacerbate! In my case, it resulted in particles overriding each other and data becoming corrupted.

  To amend this, we seperate particles[] into readParticles[] and writeParticles[]. If we never modify read particles, every thread will check the same data. Great! However, on the next iteration, they're reading old data. At the end of each cycle, readParticles[] must change into an exact copy of writeParticles[], so that frame's data correctly examines what was written last frame. Now, before we find a solution to this, lets discuss a very similar problem. I said in the previous paragraph my data was stored in an append buffer. That means when all of the present frame's data has been consumed, that buffer will have a counter value of 0. Conversely, the append buffer collecting data will have a counter value of numParticles. At the end of the frame, I can perform what's called ping-ponging. I can simply copy the append data back into the consume buffer, and vice versa.
  
    ```csharp
    //ping-ponging example
    ComputeBuffer temp = aliveParticlesA;
    aliveParticlesA = aliveParticlesB;
    aliveParticlesB = temp;

    //now to set the data
    aliveParticlesB.SetCounterValue(0);//ensures our append buffer is empty
    
    //buffers must be recalled for GPU. Just because the CPU pointer changed doesn't mean the VRAM pointer did
    DLAShader.SetBuffer(performID, "_aliveParticles", aliveParticlesA);
    DLAShader.SetBuffer(performID, "_nextFrameParticles", aliveParticlesB); 
    ```
  
  Now, the consume buffer contains numParticles particles to run and the append buffer is empty, and therefore ready to collect. Fantastic! If we look back at what a regular buffer might look like, in the case of readParticles[], and try to do this, we discover complications. Truly, our woes know no bounds. When we ping-pong, both arrays swap. This is convenient because it occurs in C# as shown above, and makes dispatching and controlling data precise and easy. For our arrays however, we want to fully copy writeParticles[] *into* readParticles[], not swap the two. Since their data exists on VRAM, we can't access this in C# without overcrowding our bus route and diminishing efficiency. So, we write a new GPU kernel to do this for us, and dispatch it with the known, fixed size.

    ```csharp
    //these arrays contain all indices of the mesh/submesh the algorithm is running on
    //some vertices have particles, some don't. These are used to store our heightmap relative to the vertices
    DLAShader.SetBuffer(copyID, "_writeIndexToParticle", writeIndexToParticle);
    DLAShader.SetBuffer(copyID, "_readIndexToParticle", readIndexToParticle);

    DLAShader.Dispatch(copyID, Mathf.CeilToInt(vertices.Count / 64.0f), 1, 1);
    ```

    ```hlsl
    #pragma kernel deepCopy

    [numthreads(64, 1, 1)]
    void deepCopy(uint3 id : SV_DispatchThreadID)
    {
        if (id.x >= (uint) numVertices) return;
        particle p = _writeIndexToParticle[id.x];
        _readIndexToParticle[id.x] = p;
    }
    ```

    We finally have all of the underlying infrastructure to begin the actual algorithm. Converting DLA onto the GPU is difficult due its inherent sequential data. We can attempt to ameliorate this by generating *lots* of particles and running one iteration for all of them at once. Recall the race conditions we discussed earlier. The same thing happens here, just in a different form. This time, our problem is that two particles can exist in the same location. Two particles can also *freeze* in the same location.

    This can be solved by using what are called atomic operations. These are operations that sync up with every other GPU core, effectively reintroducing sequential logic. Thankfully, for the first time, we don't need this. Since randomness doesn't mar the outcome of the algorithm, its 100% ok to lose a few particles. Atomics are useful, and we'll touch on them in a bit.

   We can begin writing the scripts in C# and HLSL to run a single iteration of this algorithm. It utilizes the append and consume buffers *and* the StructureBuffers we touched on earlier. It goes as follows.

  ```csharp
  void iterateDLA()
  {
      //aliveParticlesB is our append buffer; it is initially empty
      aliveParticlesB.SetCounterValue(0);

      //particleCountBuffer stores the particles in our consume buffer, and is used to prevent indexing out of bounds
      ComputeBuffer.CopyCount(aliveParticlesA, particleCountBuffer, 0);

      //we'll get to why these are here...
      DLAShader.SetFloat("timer", Time.time);
      DLAShader.SetInt("currentStep", DLAStep);
  
      //prepare buffers
      DLAShader.SetBuffer(performID, "_Args", particleCountBuffer);
      DLAShader.SetBuffer(performID, "_writeIndexToParticle", writeIndexToParticle);
      DLAShader.SetBuffer(performID, "_readIndexToParticle", readIndexToParticle);
      DLAShader.SetBuffer(performID, "_aliveParticles", aliveParticlesA);
      DLAShader.SetBuffer(performID, "_nextFrameParticles", aliveParticlesB);

      //run iteration
      DLAShader.Dispatch(performID, Mathf.CeilToInt(_GPUParticles.Count / 64f), 1, 1);
  
      //copy data
      DLAShader.Dispatch(copyID, Mathf.CeilToInt(vertices.Count / 64f), 1, 1);//copies writeIndex into readIndex

      //ping-pong
      ComputeBuffer temp = aliveParticlesA;
      aliveParticlesA = aliveParticlesB;
      aliveParticlesB = temp;
  }
  ```

  ```hlsl
  #pragma kernel performDLA
  
  [numthreads(64,1,1)]
  void performDLA(uint3 id : SV_DispatchThreadID)
  {
     if (id.x >= _Args[0]) return; //prevents indexing out of bounds
  
     particle p = _aliveParticles.Consume();
     if (p.isMobile != 1)
     {
         _deadParticles.Append(p);
         return; //frozen particles shouldn't move
     }
     
     int delIdx = floor(rng(p.idx, 2, p.particleRNG) * 6);//chooses random direction to move in

     int nextIdx = _neighbors[p.idx * 6 + delidx];
     //moves particle index to one of its neighbors
     //multiplies by 6 because each vertex is surrounded by a hexagon
     //however 12 vertices on the planet have 5 neighbors, being the 12 initial icosahedron points
     //so those invalid indices are -1
  
     if (nextIdx != -1)
     { 
         delIdx = p.idx;//reuse variable to store old position in case bouncing
         p.idx = nextIdx;
     }
     for (int i = 0; i < 6; i++)
     {
         int neighborIdx = _neighbors[p.idx * 6 + i];
         if (neighborIdx == -1)
             continue;
         float height = _readIndexToParticle[neighborIdx].magnitude;

         //only frozen points will have a magnitude
         //initially, this is only the seeds
         //value of magnitude is the strength of seed's tectonic collision
  
         if (height > 0)//heights has a magnitude! Collided!
         {
             p.frozenIndex = neighborIdx;
             p.magnitude = height;
             break;
         }
     }
     
     p.isMobile = (int) (p.magnitude < rng(p.idx, 3, p.particleRNG));
     //higher magnitude yields higher chance of freezing
     //lower magnitude introduces chance of bouncing
     //in DLA, this is known as stickiness
     //causes fuzzier and smaller branches if bouncier
     //this means magnitude correlates to both height and sprawl of a mountain
  
     if (p.isMobile == 1 && p.magnitude != 0)//i.e., bounced
     {
         p.idx = delIdx; //moves the particle to its previous position
         p.magnitude = 0;
         p.frozenIndex = -1;
         _nextFrameParticles.Append(p);
         return;
     }
     else if (p.magnitude == 0 && p.isMobile == 1)//i.e., still moving as normal
     {
         _nextFrameParticles.Append(p);
         return;
     }
     else if (p.isMobile != 1 && p.magnitude == 0)
     {
         //particle should never get here
         //give it an invalid magnitude, will cause graphical errors and alert user of error
         p.magnitude = -1;
         _deadParticles.Append(p);
     }
     else //froze!
     {
         _deadParticles.Append(p);
         _writeIndexToParticle[p.idx] = p;
         return;
     }

     //a particle should never get here
     //give it an invalid magnitude, will cause graphical errors and alert user of error
     p.magnitude = -1;
     _deadParticles.Append(p);
     return;
  } 
  ```

    These are both quite a handful. Before I move on to other kernels, take a look at the RNG function called a few times. HLSL does not supply its humble coders with an undeterministic rng function. This means we need to create our own hash based function, and this *also* means it will be deterministic. For now, I am using a hash function heavily based on two articles I read, linked below. In the future, I might add a chapter where I create my own. If I do that, I think it would be neat to make the random functions noise based, so I can reproduce a planet's result with a specific seed. Then, I could populate a reproducable universe not dissimilar to No Man's Sky.

   ```hlsl
    // PCG 32-bit hash (Reed / Jarzynski & Olano)
    uint pcg_hash(uint v)
    {
        uint state = v * 747796405u + 2891336453u;
        uint word  = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
        return (word >> 22u) ^ word;
    }
    
    float rng(uint input, uint call, float RN)
    {
        uint t = (uint)(timer * 12345.678f);
        uint rn = (uint)(saturate(RN) * 16777215.0f); // 2^24 - 1
    
        uint seed = 0u;
        seed ^= input;
        seed ^= call * 0x9E3779B9u;
        seed ^= (uint)currentStep * 0x85EBCA6Bu;
        seed ^= t * 0xC2B2AE35u;
        seed ^= rn * 0x27D4EB2Du;
    
        // Hash twice for extra diffusion
        uint h = pcg_hash(seed);
        h = pcg_hash(h ^ (input + 0xB5297A4Du));
    
        // Convert to 0-1
        return (float)h * (1.0f / 4294967296.0f);
    }

   ```

  The performDLA kernel is one of the five kernels used during the DLA process. The other four are smaller, following similar patterns. The next challenge was to assign every particle a step, or how high into the mountain range it was. Each particle in a mountain stores the same magnitude, as that represents the height and sprawl of that mountain; but that is data for the mountain as a whole, not for each particle in the mountain.

    As with all problems we've discussed thus far, it was upsettingly more complicated than I first figured. Initially, I recorded which iteration of the DLA algorithm a particle froze at. While not a terrible idea, if two mountains generated simultaneously, and one froze faster than the other, the first mountain's base wouldn't converge into the ground, instead hovering awkwardly.

  By my thid solution, I succeeded--it just took a whole lotta persistance. I knew I wanted to record a simple question for each particle: how many steps away from a base particle (ground level) am I?

  I essentially came up with a "linked list" using index pointers. The idea is that each base particle has an exact step of 1. Then, every other particle (which must have children) will have their step value equal to childStep + 1. If they had multiple children, take the maximum. The idea is to run the script a number of times until the step information has propogated from the base particles to the seeds.

  If you go back to the particle struct I proposed earlier, you'll notice eldestChild and youngerSibling. They were part of a failed attempt (no longer present in the actual code). The idea was for each particle to have pointers to their first child and next youngest siblings, if applicable. Then, as a string ends with \0 and a linked list ends with null, my mock list would end with -1. If I ever read a -1, I knew to terminate that chain of children. It was done this way because a particle could have anywhere from 1 - 5 children, and this was the simplest way to group them (to me at least).

  Unfortunately, excited as I was about this solution, the GPU struck again. If two particles froze to a parent at the same time, they had a chance of labeling *each other* as their younger siblings. This created an infinite loop. It could have been solved with atomics, but I came up with a better solution.

  I replaced both previous pointers with a pointer to a particle's parent, and effectively reversed the process I came up with. Instead of a particle's step determined by it's children, a particle's parent was determined by the particle. That might sound ridiculous at first, but think about it. Instead of a convoluted chain of pointers, I only needed one. I read data from any given particle, and if they have a parent I write data *to the parent*. While much better, I still had to use atomics, but they were easy to iplement and didn't have any lingering problems.

  ```hlsl
  #pragma kernel propogateSteps
  
  [numthreads(64, 1, 1)]
  void propogateSteps(uint3 id : SV_DispatchThreadID)
  {    
      if (id.x >= _Args[0])
          return;
   
      particle q = _aliveParticles.Consume();
      particle p = _readIndexToParticle[q.idx];
      //_readIndexToParticle is more up to date
      //and will finish the algorithm faster
      //however _aliveParticles can perfectly
      //summon all old particles, who keep the same index data
      
      int parent = p.frozenIndex;
   
      if (parent != -1 && _readIndexToParticle[parent].step < p.step + 1)
      {
          InterlockedMax(_writeIndexToParticle[parent].step, p.step + 1);
          //interlockedmax is the atomic function, which keeps the max value
   
          finishedElevating[0] = 0;
          //finishedElevating is sent back to the CPU every 20 iterations
          //to prevent superfluous algorithm calls
      }
      _deadParticles.Append(p);
      //_writeIndexToParticle[p.idx] = p;
  }
  ```

    Now that each value stores a step with it, we can use this step as the parameter to a function, determining each particle's base height.

    $\ 1 - \frac{1}{1 + s} $

    where s is the particle's step. This returns a value, 0 to 1, that can be multiplied to the particles preexisting magnitude. This data can then be stored in _writeIndexToParticle, at the index corresponding to the particle's vertex, and used to scale the planet's radius.

    <img width="640" height="264" alt="Screenshot 2026-01-21 205349" src="https://github.com/user-attachments/assets/36459cd3-e497-4731-b68a-a9af1faed41e" /><br>
*Figure 13: Two Functions Considered for Step Multiplier*

    Now, one final step is needed before the heightmap can be applied. If we scaled now, the planet would have *exact* fractals on its surface. To look like a mountain, we need to somehow blur the base of the fractal, but retain it's detailed peaks and ridges.

    <img width="596" height="344" alt="Screenshot 2026-01-21 210235" src="https://github.com/user-attachments/assets/a4238951-c9fd-4bf7-bdd0-3d8fc4c21980" /><br>
*Figure 14: Unprocessed DLA Output*

    The blurring was relatively simple. You can mimic a gaussian blur pretty well by modifying a point based on its neighbors, i.e. taking a weighted average. The more blurs, the more the effect will propogate across the planet. However, the more blurs, the more detail is lost as well. To give a mountain it's deserved majesty, both are needed. This was achieved by making another RWStructuredBuffer that existed for the sole purpose of storing height data relative to vertices. Every time blurDLA would repeat, this new buffer (_heightMap) would add the outputs atop the old data. At the end, the value was divided by the number of blurs. Of course, if more tweaking were desired, the outputs could be weighted with a function. For now, I am quite happy with their output.

```hlsl
#pragma kernel blurDLA

[numthreads(64, 1, 1)]
void blurDLA(uint3 id : SV_DispatchThreadID)
{
   if (id.x >= (uint) numVertices) return;
   float height = _readIndexToParticle[id.x].magnitude;
   float numHeights = 1;
   float totalHeight = height;
   for (int i = 0; i < 6; i++)
   {
       int index = id.x * 6 + i;
       if (_neighbors[index] == -1) continue; //in case of a pentagon
       
       totalHeight += _readIndexToParticle[_neighbors[index]].magnitude * weight;
       numHeights += weight;//more accurate than += 1
   }
   height = totalHeight / numHeights;
   
   _writeIndexToParticle[id.x].magnitude = height;
}

#pragma kernel scaleRadii

[numthreads(64, 1, 1)]
void scaleRadii(uint3 id : SV_DispatchThreadID)
{
   if (id.x >= (uint) numVertices) return;
   float scalar = _readIndexToParticle[id.x].magnitude;

   _heightMap[id.x] += scalar;//could apply a function here...
   
   if (currentStep == numBlurs)
   {
       float3 vertex = _vertexData[id.x];
       scalar = (float) _heightMap[id.x] / (numBlurs == 0 ? 1 : numBlurs);
       //despite reading and writing to heightmap in the same function
       //it isn't a race condition
       //since data of other particles is irrelevant

       vertex *= 1.0f + scalar * mountainRatio;
       _vertexData[id.x] = vertex;
   }
}
```

  The final step to completing the DLA algorithm was a scaling kernel that would apply the finished heightmap to a vertices array. Then, the planet's mesh is set to that array. Easy!

![MountainGeneration](https://github.com/user-attachments/assets/b79b9285-1813-49a6-975c-c88b925b7140)
*Figure 15: Mountain Generation*

  Now that the algorithm is done, we can mess around with it to our heart's content. Before we delve into the modifications, lets discuss the power of this DLA algorithm. Currently, the algorithm runs on the entire planet mesh, instantly generating mountains with the only variable between them being their height and sprawl. Instead, I could apply it to various submeshes. This will be section 3.2--turning random tectonic collisions into instances of a terrain class. Each will have unique generation parameters for the initial heightmap and broad terrain, and then simulated rainfall and temperature will determine the biome and climate. They will be divided into chunks in a voronoi-like pattern.

<img width="435" height="255" alt="Screenshot 2026-01-21 204851" src="https://github.com/user-attachments/assets/067b643e-aefa-4520-a219-cc78d2869428" /><br>
*Mountain Generated due to Convergent Tectonic Boundary*

<img width="440" height="261" alt="Screenshot 2026-01-21 205120" src="https://github.com/user-attachments/assets/6861658f-ed39-42bd-9a26-88169ae04133" /><br>
*Figure 16: Clamped DLA Mountains Represent Mesa Plateaus*

<img width="425" height="281" alt="Screenshot 2026-01-21 190004" src="https://github.com/user-attachments/assets/fb3c2a52-21f3-4381-973f-465b9e24b9db" /><br>
*Figure 17: Rift Generated by Subtracting DLA Output*


- **Phase 4: Atmosphere**
- The last section I wrote, the DLA algorithm, I finished about three months ago. During those months, I took a break to work on a game-jam and do some less math-heavy projects. It wasn't long, however, before I was ready to come back. Originally, I had grand plans to begin my return by massively overhauling the architecure of this project. As I'm sure is no surprise to anyone who's coded before, my ambitions morphed into a vision far less future-proof. Instead of fixing older issues, I began to create new ones.

  Thus began the month-long journey of learning physically-based shaders. Ray-marching, texture tradeoffs, calculus, pedantically named functions, volumetrics, noise, and heaps of research papers. Boy, did I love it.

  The atmosphere shader was inspired by Sean O'Neil's paper in GPU Gems Vol. 2. The process appeared remarkably simple. First, the equations. Note that the clouds use very similar equations, so I will not rewrite them.

  Henyey-Greenstein: $F(\theta, g) = \frac{1}{4\pi}\cdot \frac{1-g^2}{(1 + g^2-2g \cdot cos(\theta))^\frac{3}{2}}$

  The Henyey-Greenstein equation represents the directions light might scatter. Imagine looking in the direction of the sun, but you see a cloud along the way. The cloud will have a silvery outline; that effect is calculated by this function. Here, $\theta$ represents the view direction (in code, we pass this in as cosTheta because a dot product has a magnitude |a||b|cos(theta). Since a and b, viewDir and sunDir, are unit vectors we can avoid an extra calculation), and g represents how strongly the light scatters forward (see below). Clouds, being white or grey, scatter more colors forward with a g value of ~0.8 (called Mie Scattering). The atmosphere scatters light in all directions equally, with a g value of 0 (called Rayleigh Scattering).

<img width="397" height="127" alt="images" src="https://github.com/user-attachments/assets/1dbbe1db-550d-4539-b076-f32f442ed58c" /><br>
  *Figure 18: Rayleigh Scattering (g = 0) and Mie Scattering (g ~ 0.8) represented by Henyey-Greenstein function*

  Density: $den=e^{\frac{-h}{H_0}}$

  This equation answers a simple question: how dense is the atmosphere at a given height above the planet? This translates to how much light and color will be let through given points in the atmosphere.

  Optical Depth: $OD=4\pi\cdot K(\lambda)\cdot \int_{P_b}^{P_a}(den) ds$

  Now we've reached the ray marching functions. In math, an integral represents an infinite sum. In code, we unfortunately cannot do that. Graphics programming is all about the art of approximation. The typical response to an integral, if the math cannot be hand-calculated, is to create a for-loop and turn the integral into a finite summation. A compromise is then presented to the programmer: increase the iterations, boosting visual fidelity but compromising on speed, or decrease the iterations and accept a lesser result.

  The optical depth represents a sum of densities along some ray. Imagine we have a point along the atmosphere. The optical depth asks: how much light from the sun reaches this point? Furthermore, the function can be recycled to answer the question: how much of *that* light reaches the viewer? The way light is treated is akin to a probability: what are the chances a ray of light, scattered through aerosols or clouds or O2 particles, might hit the viewers eyes? 

  In-Scattering: $InScattering = I_s(\lambda)\cdot K(\lambda)\cdot F(\theta,g)\cdot \int_{P_b}^{P_a}(den \cdot e^{(-OD_{Sun} \cdot OD_{ViewRay})})ds$

Finally, the in-scattering equation puts it all together. It is essentially a scaled summation of optical depths. If each optical depth calculates light at one point, the in scattering equations attempts to calculate *all* points along a given ray. In code, we have a loop that runs numScatters time; each time it calls the OpticalDepth function which runs numOpticals times. If we choose large numbers, the scene might look nice, but it will get out of hand very quickly. One of the challenges of this atmosphere shader is the sheer quantity of parameters. There are *34* of them! The goal is to balance visuals with computation. Tweaking took a good long time, but it was worth it.

<img width="500" height="117" alt="16_atmospheric_02" src="https://github.com/user-attachments/assets/4f41f664-6363-48d8-b364-edd2ed14c1d7" /><br>
  *Figure 19: Scattering Diagram*

Now that we have our equations, we should port them to code. Immediately, we run into a problem. There is nothing in the scene to actually raymarch through. To keep it procedural, we create a fullscreen shader and apply it to a sphere that exists purely mathematically. This shader runs in what's called a fragment shader. These take the triangles on our screen, break them up into pixels (fragments), and render them. We can use some fancy linear algebra to determine our world space camera position and then calculate the ray direction of every pixel. We can also access the depth buffer and linearize it so we don't accidentally render the clouds behind the planet. This is the data the computer gives us, but it's not yet what we need. For the atmosphere and clouds, we need two more things from these rays: A), when does the ray hit the atmosphere? And B), how far through the atmosphere does it traverse? These are necessary to compute the correct densities in the correct locations. Without it, the computer wouldn't know where to render anything.

  To calculate these, we use what's called a ray-sphere intersection. They are quite fun to calculate on one's own. It's the kind of challenge that is simple enough not to be daunting but also not so easy it's boring. If we had a float3 representing our center, and a ray, we could use trigonometry to find the distance between the center and the nearest point on the ray. 

<img width="400" height="300" alt="raysphere4" src="https://github.com/user-attachments/assets/b1175c19-ebb3-4303-bf2f-ed9513f845a8" /><br>
  *Figure 20: Ray-Sphere Intersection Diagram*

  First, we use our rayOrigin to find a vector to the center of the sphere. Then, we take a dot product between sphereVec and rayVec. This tells us exactly how long the rayVec must be for the most promixity to the sphere center. We will call this closest point p. Before continuing, we always want to work in distances squared. Sqrt functions are expensive. We actually determine whether p is in the sphere through the quadratic formula's discriminant: b^2 - 4ac. If the discriminant is > 0, e.g. has two real roots, we know the ray passes through two points on the sphere. 
  
  We use point p's distance from the center of the circle to create a new right triangle. We know the length between p and the center, and we know the hypotenuse must be the radius of the sphere, because we are trying to find the viewRay's entry point into the sphere. Now all we do is find the distance from p to the edge of the sphere. Now we can calculate the distance to the front and back edge of the sphere, because they will always have the same distance from point p. Using this, we return our distanceTo and distanceThrough values, completing the function.

  Once this has all been placed into code, we have a basic atmosphere with tunable colors!

<img width="515" height="320" alt="AtmosphereScattering" src="https://github.com/user-attachments/assets/c541d221-a942-4dc3-b44e-43b4ae96bb0b" /><br>
  *Figure 21: Changing Scattering Parameters to Change Atmosphere Color*

  As for improvements, there is always optimization. The optical depth can be a cheap 2D texture lookup instead of an expensive exp() function. As for function, the atmosphere currently doesn't interact with the ground or the clouds, which it should.

<img width="640" height="473" alt="image" src="https://github.com/user-attachments/assets/80f2a35a-1b9d-41b8-82b5-ec44f42e929e" /><br>
  *Figure 22: Atmosphere from planet surface*

<img width="502" height="362" alt="image" src="https://github.com/user-attachments/assets/864c0d9a-d32e-40ce-a6f8-c300e3b80d51" /><br>
  *Figure 23: Alien atmosphere!*

Now that the atmosphere is finished, we can begin working on our clouds. They use a very similar process; you need optical depth, in-scattering, raymarching, and a raysphere intersection (the raysphere intersection can actually be optimized out by sharing the atmosphere's and then tweaking the values). However, they have one major difference: the density of clouds is *not* a nice and consistent exp() function. For this, we bring in noise and remap functions.

The first noise algorithm we make is called Perlin Noise. It is unbelievably useful. When you watch a movie or play a game and see some fancy visual effects, 80% of the time it's a variation of perlin noise. Fog? Clouds? Water? Fire? Dissolving? In the 80's, Ken Perlin sought to make a form of visual noise that was organic in appearance, but generated entirely procedurally with just math. For 3D noise, the algorithm takes a grid of cubes.

  <img width="480" height="240" alt="PerlinNoiseGradientGrid svg" src="https://github.com/user-attachments/assets/4310dadc-56c2-4677-9e93-ad27ae59e4a2" /><br>
*Figure 24: Depiction of Perlin Noise Generation*

The position of each corner is put into a hash function, and the value is used to create a random unit vector. Every corner is assigned one. When the actual texture itself is generated, there might be 10 or 50 texels, or texture pixels, between each corner of this grid. At each texel, we find the vector from the texel location to each of the four corners. Then, we compare that vector to the random unit vector via a dot product.

  <img width="225" height="225" alt="download" src="https://github.com/user-attachments/assets/85f2fa5e-1758-4a0e-9009-5598b1da02ce" /><br>
*Figure 25: 2D Perlin Noise*

This generates a nice gradient with large blobs that are useful for organic shapes and patterns. The other noise used for these clouds was worley noise. It uses a similar process: start with a 3D grid, and in each cell place a random point. It could be located anywhere within the cell's bounds. When the texture is generated, each texel corresponds to the distance of the nearest random point. Worley noise creates blobby, cell-like shapes. They can be used for caustics, terrain generation, or, luckily for us, clouds. 

  <img width="250" height="250" alt="2d-cnoise-2x2" src="https://github.com/user-attachments/assets/20b407f1-2c5d-45f1-ba12-80fbf7da30d5" /><br>
*Figure 26: Inverted Worley Noise*

Noise can be layered, too. Here, I've stacked 7 layers of Worley noise on top of each other, increasing the frequency but decreasing the amplitude of each. This technique is known as fractal brownian motion.

  <img width="293" height="289" alt="image" src="https://github.com/user-attachments/assets/9efcb6f5-0ad9-4322-b6dc-1e06ae264bec" /><br>
*Figure 27: Worley FBM*

To generate the clouds, we use a strange bastard of these two noises: perlin-worley. A remap function can be used, which takes a value of a range (min,max) and uses that ratio to place it in a new (min,max). This function is handy because it will leave high-density regions at the core of the cloud largely unnaffected, but mid or low density regions can have substantial effects. This means our new function keeps the organic shapes of perlin, but inscribes the billowiness of worley onto the high-density regions of it.

  <img width="200" height="197" alt="image" src="https://github.com/user-attachments/assets/bb230a9c-aeaa-4386-a7a2-ca5fb361fd3e" /><br>
*Figure 28: Perlin-Worley*

This noise was combined with some other effects and used as a replacement for the density function shown above. The result has the unmistakable appearance of a cloud. Of course, there's plenty of room for improvement. The clouds only have a stratus layer; it would be beneficial to also have a cirrus band (long wispy clouds very, very high up). Additionally, the erosion of the edges of the clouds is subpar at the moment. It could be improved by carving wispy shapes at the base of a cloud and billowy shapes at the top of a cloud. Of course, there's also optimization. The main resource I used for these, Guerilla Games' Horizon series, uses temporal upscaling. I don't have plans for adding that, but I do intend to improve the scattering by introducing a variable step size to the ray march. This will hopefully allow for less ray marches while increasing the visuals up-close.

Now that the atmosphere is largely complete, we can put everything together! The last real step is changing parameters or combining the noise in unique ways to achieve different effects.
  
<img width="1191" height="990" alt="AtmosphereDemonstration" src="https://github.com/user-attachments/assets/c4916a23-b126-43c6-9f88-a60dc51453d3" /><br>
*Figure 29: Clouds, atmosphere, and planet running at 270 FPS on Unity editor*

<img width="347" height="326" alt="image" src="https://github.com/user-attachments/assets/c0eff448-2428-40d4-8b98-34b97abb693e" /><br>
*Figure 30: Higher resolution clouds without planet*

<img width="409" height="361" alt="image" src="https://github.com/user-attachments/assets/e3055c1b-3368-495c-acc4-1ff51f1cb556" /><br>
*Figure 31: More High-res clouds!*

<img width="366" height="390" alt="image" src="https://github.com/user-attachments/assets/63ae95a1-df7a-47bc-a2ab-167e2d92b56e" /><br>
*Figure 32: Cartoonish clouds*

<img width="325" height="340" alt="image" src="https://github.com/user-attachments/assets/eb4cee8c-965a-4b1f-81c6-d68f9e3fa17c" /><br>
*Figure 33: Thin, wispy clouds*

## Future Additions
I hope to update this page every 2 weeks, as I have a number of additional "phases" planned.
- Adding octave noise atop the DLA algorithm to create semi-realistic land formations
- Adding climates (biomes, temperatures, forests, clouds, etc.)
- Using splines and gradients to add rivers that accruately travel down a slope to a body of water
- Using boids to add birds that fly around the atmosphere, giving the planet life
- Adding other planet types, of a simpler caliber, then creating an entirely procedural solar system
- Adding a player and potential gameplay elements

## Future Improvements
There are quite a few things in need of another look. 

The tectonic plates are a bit of a mess. I am rather considering deleting some of what I wrote on this page, but I've left it to show improvement and thought process. My "blobby sphere" technique is... lets be honest, terrible. My computer is not pleased with that amount of trig passes. Additionally, my equation for the scaling is completely wrong. I'm not sure what happened, but I either miscalculated or it was lost in translation at some point. Anyways, I am rewriting that function to be either noisy spheres or gaussian random particles that I can reuse for caldera, volcano, and crater basins. 

Since having written the DLA generator, I stumbled across a YouTube video showing another mountain generation method. It's inspired by Josh Clay, who created and posted a ridged noise generator on shadertoy. The unfortunate thing is that their method outclasses the DLA method much of the time. I am still deciding on what to do with this information.

Thanks for reading! If you noticed any mistakes or would like to contact me, please email me at mray0256@gmail.com. Have a nice day!

## Sources & Inspirations
- Outer Wilds Technical Presentation: [Hosted by Unity](https://www.youtube.com/watch?v=Ww12q6HsmJA)
- Tectonics: [Fractal Philosophy on YouTube](https://www.youtube.com/watch?v=7xL0udlhnqI)
- Procedural Planets: [Sebastian Lague on YouTube](https://www.youtube.com/watch?v=lctXaT9pxA0&t=202s)
- Golden Ratio: [Wolfram Math](https://mathworld.wolfram.com/RegularIcosahedron.html)
- Mountains: [Josh's Channel on YouTube](https://www.youtube.com/watch?v=gsJHzBTPG0Y)
- Planets: [Devote on Youtube](https://www.youtube.com/watch?v=CeJz8tsgCPw)
- Making of Outer Wilds: [Documentary by /noclip on YouTube](https://www.youtube.com/watch?v=LbY0mBXKKT0)
- GPU-based DLA: [Article by Mykola Haltiuk](https://medium.com/@goader_/diffusion-limited-aggregation-in-a-highly-parallel-fashion-using-cuda-954ee66137e2)
- RNG: [Article by Nathan Reed](https://www.reedbeta.com/blog/hash-functions-for-gpu-rendering/), [Article by Mark Jarzynski & Marc Oblano](https://jcgt.org/published/0009/03/02/paper.pdf)
- Clouds: [Presentation by Andrew Schneider](https://gdcvault.com/play/1028023/The-Real-Time-Volumetric-Superstorms) & [SimonDev on YouTube](https://www.youtube.com/watch?v=Qj_tK_mdRcA&t=77s)
- Atmosphere: [Paper by Sean O'Neil](https://developer.nvidia.com/gpugems/gpugems2/part-ii-shading-lighting-and-shadows/chapter-16-accurate-atmospheric-scattering) & [Sebastian Lague on YouTube](https://www.youtube.com/watch?v=DxfEbulyFcY)
- Perlin-Worley Noise: [Shader by piyushslayer on Shadertoy](https://www.shadertoy.com/view/3dVXDc)
- Candidate for Terrain Generation: [runevision on YouTube](https://www.youtube.com/watch?v=r4V21_uUK8Y)
