# Procedural-Planet-Generator
Side project inspired by the video game Outer Wilds.
## Screenshots

![Enemy Battle](assets/screenshots/Fight-1.png)
![Dialogue UI](assets/screenshots/Dialogue-UI.png)
![Boss Fight](assets/screenshots/Bossfight-1.png)
![Hats UI](assets/screenshots/Hats-UI.png)

## Overview
- A project created to simulate planet generation. Currently a WIP with the hopes of being updated every two or so weeks.
- I chose this project primarily because I liked the idea of a modular sort of simulation, in which prior algorithms were only indirectly related to future ones, allowing for less fear of breaking something & nonlinear features.

## Tech Stack
- Engine: Unity
- Language: C#, HLSL

## Project Setup
This repository contains the scripts from the Unity files to be run locally. It does not include exe and build files to reduce bloating.

## Gameplay
Although as of now, the only "gameplay" is changing random stats, a goal in the future is to make a playable character who can explore an entirely procedural solar system. This includes new bodies, e.g. gas giant, Goldilocks planet, asteroid, star, etc. Ideally this solar system would be simulated entirely with integration-based newtonian physics that are akin to the Outer Wilds gameplay loop.

## Technical details & what I learned
- This project has consisted almost entirely of math. I prefer this to my other projects, because of all the skills for development I'm most confident in my math ability. I'm less overwhelmedrho while learning when I have a foundation in something I feel strong in. The first "phase" of the project was creating a sphere. I could've used a generic UV sphere supplied by blender or unity, but I wanted to generate my own to have fine control.

- **Icosphere:**  
  This was a lot of index math. I have pages and pages of subdivided triangles with illegibly scribbled numbers that somehow made sense as I made it. If I redid it, I probably would've spent more time making an ironclad winding pattern so the vertices and faces are generated neatly, but as of now they're somewhat messy. As a result, I had to make redundancy-checker funcitons that are not exactly optimal. Additionally, there is no tessellation and no plans for it; but if I were to improve the sphere, that would be on the list.

  - **How it's done:**  
    When I first wrote the subdividing methods, it was mapped to a single triangle, then to an octohedron, _then_ to an icosohedron. I started with a list of predetermined vectors and their faces corresponding to each shape. To subdivide the triangles, my method of choice was abusing lerping. I wrote a nested loop in code, in which the first loop determine vertical layer, interpolating from bottom to top (most vertices on the face to least), and the inner loop interpolatess from left to right. When looking at a subdivided triangle, the amount of vertices in each layer decreases by 1. Together, I used these properties to create vertices and their faces. To give a sense of the nested loop and index sheninigans that arose, I left the superfluous & debugging methods in the code.

- **Tectonics:**  
  The second phase was making tectonics for the planet. I wanted this so I could make semi-realistic land formations based on the collisions of tectonic plates. These could have been created through a number of strategies, but I chose one I hadn't seen before. First, a common method for generating tectonic plates is choosing a random vertex on the surface of the sphere, then checking a radius around said point. If other vertices don't belong to a plate, they now belong to the same plate that inital vertex does. Now, this is quite basic and only forms circles; most people move on to more advanced fractal algorithms to achieve the blobby shape tectonics on Earth make. I decided that was a convoluted solution. Instead, I changed the radius from a constant into an equation. Using trig functions, I made the outer bounds have that characteristic blobby, deformed shape; then, atop that, I added randomized constants that determine the wavelength of the trig functions. This resulted in shapes I am quite proud of, especially as it's a relatively simply solution that I hadn't seen before.

  After grouping vertices into respective tectonics, I needed to add two things: oceanic vs continetal tectonics, and direction vectors of these tectonics. To achieve this, and to color them, I used a technique I'd learned from an Outer Wilds technical video (linked at the bottom): using color channels to store information. The R, G, and B channels store the X, Y, and Z elements of a unit direction vector (determined by Euler poles, also linked below). The alpha channel holds two pieces of info in one integer: the plate ID of the vertex, and whether it's oceanic or tectonic.

  - **How it's done:**  
    First, the equation used for the tectonic blobs is

    $\ x^2 + y^2 + z^2 + \frac{r}{\alpha}\cos(ax) + \frac{r}{\alpha}\sin(by) + \frac{r}{\alpha}\cos(cz) = r^2 $


    where

    
    $\ r = \frac{4 \cdot \text{Radius}^2}{\text{numTectonics} \cdot d} $
    

    (Figure X).

    Let's talk about r. I needed each tectonic plate to cover roughly $/ /frac{1}{desiredTectonics} $ surface area of the initial sphere. So, I had a question: if a sphere, of radius _R_, has an idential sphere generated on it's edge, how much of the first sphere's surface area does that second sphere encapsulate (Figure Y)? First, we find the points of intersection when represented as a circle. Since the spheres have identical radii, these points are a distance R. Similarly, from center to center is also R. This creates two equilateral triangles (Figure Z). Thus, our angle, $\theta$, is 120 degrees.

    Using spherical coordinates & calculus, we could set
    
    $\ \rho = R,\quad \phi = 2\pi,\quad \theta = \pi / 3 $

    Now we can create an integral and calculate the volume shared between the spheres.

    $\ \int_{0}^{\pi/3} \int_{0}^{2\pi} \int_{0}^{R} \rho^2 \sin(\phi)\, d\rho\, d\phi\, d\theta = \frac{\pi R^3}{3} $

    We can divide the total volume of the sphere by the newlyfound shared volume, yielding a ratio. This can then be applied to our surface area.

    $\ (\frac{\pi R^3}{3})(\frac{3}{4\pi R^3}) = \frac{1}{4} $
    
     $4 \cdot R^2$ would encapsulate the whole of the sphere; instead, we want a portion. If we were to divide by exactly numTectonics, the inherent randomization makes more plates than intended, adding a factor _d_ where $d < 1$ decreases the amount of extra tectonics plates added. I've found that the preferred constant is $\ d = \frac{2}{3} $

    Now that we can divide our sphere into neat portions, we need to create our blobby sphere. To do this, adding randomized trig functions suffices. However, these need to be scaled by the radius so their wavy edges have the same amplitude regardless of radius. I chose to multiply the functions by $\frac{r}{\alpha}$, where r keeps the scale and $\alpha$ is a tweaking constant (currently $\ \alpha = \frac{2}{3} $). Then, the inside of the trig functions is simply a wavelength dependent on where in 3D space the vertex lies. In the end, the function is roughly pythagorean theorem with some pizzazz.

    The next topic is the data of each plate. First, Euler poles. Tectonic plates don't translate linearly; rather, they rotate about an arbitrary axis piercing the Earth. These axes are referred to as Euler poles. To calculate where a plate will rotate, we take a cross product of the vertex's vector to the radius by the vector of the Euler pole. Then, we apply this data to each vertex within the plate.

    Great! This data, along with the plateID and whether it was oceanic or tectonic, was all baked into a color channel. Looking back, this was not as ideal as my optimistic heart said it would be. It turned out fine, but the amount of "this must be the most ridiculously overly-complex code I've ever seen" was truly breathtaking. Colors are stored as bytes, which as I learned painstakingly, do not hold negative values or floats (shocking, I know).

    To remedy this, my solution was to utilize all three digits for information. For the plateIDs, since there would never be greater than 99, I made the first two digits the plate number and the third the plate type (oceanic/contintental). Similarly, the vectors were normalized, then multiplied by 100 (to fill the whole numbers 0 - 99) and given +100 if they were negative. To decode the data, I used the modulus command to find the first two numbers then a conditional for whether the number was >= 100. Now, this perhaps doesn't sound so bad, but my code, which was repeated in various ways, wound up looking as such:

    ```csharp
    float xDir = (float)(planet.colors32[i].r % 100 * (planet.colors32[i].r >= 100 ? -1f : 1f)) / 100f;
    ```

    Horrible, let me tell you.

    Anyways, now that I had all the data needed, I had to visualize it. I made a struct for each boundary, which acted as a single line between two points, then made a list of all boundaries. To fill this list, I learned about hashsets and combined them with dictionaries. I made a method, DetermineNeighbors, which fills a dictionary whose values are vertex indices and whose keys are hashsets of the neighboring vertices' indices. Then, using this dictionary, I compared the plateID belonging to each vertex and it's neighbors' to fill out the boundaries list.

    The boundary struct contains a color value and a magnitude value. These are determined by a cross product of the first vertex's direction and the second's. If they are negative, i.e. oppose each other, they create a divergent boundary! These are colored blue in gizmos. Conversely, convergent boundaries are colored red. Transform boundaries are colored white.

    The last step of making these tectonics was to color the planet. The planet has a material whose shader can change dependent on the phase. The tectonics shader simply shades oceanic tectonics blue, continental green, and creates a border around landmasses. Additionally, to differentiate between plates, the plateID will shade the plate darker the greater it gets. This is achieved through $ \frac{plateID}{numTectonics} $ fed into a lerp function that traverses from white to dark-grey (multiplying a color by black loses any prior hue). Finally, we have realistic enough tectonic plates!


## Future Additions
I hope to update this page every 2 weeks, as I have a number of additional "phases" planned.
- Adding a fractal-based heightmap combined with octave noise to create semi-realistic land formations, and learning compute shaders in the process
- Using splines and gradients to add rivers that accruately travel down a slope to a body of water
- Using boids to add birds that fly around the atmosphere, giving the planet life
- Adding climates (biomes, temperatures, forests, clouds, etc.)
- Adding other planet types, of a simpler caliber, then creating an entirely procedural solar system
- Adding a player and potential gameplay elements

## Future Improvements
There are many improvements that could be made, but the most glaring to me is the lack of object-oriented-ness. My own knowledge of object-oriented programming is lacking, but constantly growing, especially as my college career goes on. Another significant improvement is optimization (tessellation, culling, etc.).

## Sources
