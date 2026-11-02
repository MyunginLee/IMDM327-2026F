# MetaBoids

Open `Scenes/08_MetaBoids.unity`, select **Controller**, and press **Play**. Change the settings in the Inspector. The field fills the Game view. Boid Count uses the value saved in the Inspector and supports up to **2,000**. All titles and controls stay out of the Game view.

This example uses Unity 6000.3, URP, and a GPU that supports compute shaders and Shader Model 4.5. The basic scene needs no extra packages or Renderer Features. The pinch scene also reuses the existing EDM afterimage Renderer Feature.

## Two-hand pinch and sound

Open `Scenes/09_MetaBoids_Pinch.unity` and press Play with a webcam connected. Both hands work independently: bring the thumb and index fingertip together to pull boids toward that hand; open the pinch to release. Two pinches apply two forces, so you can gather the surface or pull it in two directions.

This scene reuses `Scripts/MediaPipeInput.cs`, `MediaPipeBodyTracker.cs`, and `BoidFMSynth.cs` from the course. Pinch detection uses fingertips 4 and 8. Attraction follows the tracked wrist, matching the earlier `InteractiveBody` example. Coordinates are mirrored to match the camera preview. If tracking reports a missing hand, its force and sound gate turn off.

- **MediaPipe**: choose Camera Index, toggle Show Camera or Mirror X, and adjust the tracker's Pinch Threshold in the Inspector.
- **MetaBoids Pinch Input**: Pull Strength controls both independent attractors.
- **Pinch Audio**: Master Gain, Smoothing Seconds, Pitch Range, Cutoff Range, and Resonance control the sound. Disable the audio component to mute it.
- **Main Camera > Screen Afterimage**: toggle Afterimage or change Afterimage Seconds. The default is 0.35 seconds. Larger values leave longer screen trails.

The screen trails reuse `06_EDMBoids/Scripts/EDMAfterimage.cs`, `Shaders/Afterimage.shader`, and `EDMLightPipeline.asset`. `ScreenAfterimage.cs` temporarily selects that renderer and restores the previous pipeline when disabled. It keeps the fixed MetaBoids camera. No orbit or EDM color grading is added.

Depth Priming is disabled in the shared renderer because this simple unlit quad has no separate depth pass.

Each frame keeps the brighter of the current image and the fading history: `max(current, history * exp(-deltaTime / seconds))`. The result becomes the next history. After one decay time, an uncovered bright pixel retains about 37% of its brightness. This is separate from the boid movement and from the individual TrailRenderers in the EDM scene.

The audio component creates two instances of the existing `BoidFMSynth`, one per hand. Pinching opens a voice; hand height changes pitch, horizontal position controls stereo pan, and faster movement increases FM modulation and opens the low-pass filter. Parameter smoothing rounds the attack and release. The chain is **FM synthesis → 45 Hz high-pass → resonant low-pass → short echo**. The low-pass resonance, glide, and small echo are sound-design choices for a soft, elastic texture.

The new scene starts with 2,000 boids and a smaller influence radius for the denser field. The original scene keeps its saved settings. Its optional pinch forces stay zero unless a hand component supplies them.

`Interaction/MetaBoidsPinchInput.cs` only connects existing tracking to the GPU. `Interaction/MetaBoidsPinchAudio.cs` only makes sound. These scripts are outside the core assembly so the basic simulation does not depend on MediaPipe or audio classes. No Editor folder or custom on-screen controls are added.

References: [MediaPipe hand landmark indices](https://ai.google.dev/edge/api/mediapipe/python/mp/tasks/vision/drawing_styles/hand_landmarker/HandLandmark), [Unity audio filters](https://docs.unity.com/en-us/engine/6000.3/manual/audio/reference/filters), and [OnAudioFilterRead](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnAudioFilterRead.html). The webcam models are the existing local files in `Assets/StreamingAssets/MediaPipe`.

The Controller assumes the saved camera, material, display, and compute shader references are assigned. The basic scene uses the Inspector; the second scene adds the separate hand and audio components.

## Start with three files

Read the numbered English comment blocks in this order. Each block explains one job.

| Read | File | What it does |
| --- | --- | --- |
| 1 | `Scripts/MetaBoidsController.cs` | Creates data, sends Inspector values, and starts GPU work. |
| 2 | `Shaders/MetaBoids.compute` | Moves each boid using nearby boids. |
| 3 | `Shaders/MetaBoids.shader` | Turns moving positions into a soft surface. |

`Shaders/MetaBoidsStyle.hlsl` contains the colors, shine, grid, and direction lines. You can skip it on the first read.

## 1. C# sends the data

A boid is four numbers:

```text
Vector4(x, y, velocityX, velocityY)
        position      velocity
```

In HLSL, `.xy` reads the position and `.zw` reads the velocity. Positions use normalized coordinates: `(0, 0)` is the bottom-left corner and `(1, 1)` is the top-right corner.

The Controller has three main steps:

```text
Start -> ResetSimulation
    Make starting positions and velocities. Send them to two GPU buffers.

FixedUpdate -> SimulateStep
    Send movement settings. Run the compute shader. Swap the buffers.

Update -> Draw
    Send the latest buffer and surface settings to the display shader.
```

A **buffer** is an array stored on the GPU. We use two so every boid reads the same old frame while writing a new one. After the movement calculation, the two buffers swap jobs.

A **kernel** is the compute shader function Unity runs. Here it is named `Simulate`. A **thread** runs that function for one boid. Threads are launched in groups of 64, so 2,000 boids use 32 groups. The last 48 threads exit without reading the array.

## 2. The compute shader moves the boids

For each boid, the shader reads its old position, checks its neighbors, and combines three rules:

| Rule | Simple meaning | Inspector weight |
| --- | --- | --- |
| Separation | Move away from very close neighbors. | Separation |
| Alignment | Match the neighbors' average velocity. | Alignment |
| Cohesion | Move toward the neighbors' average position. | Cohesion |

Then it updates velocity and position:

```text
velocity = velocity + acceleration * timeStep
position = position + velocity * timeStep
```

The code also limits speed and turns away from walls. The wall force is an extra helper, separate from the three flocking rules.

The shader compares squared distances using `dot(offset, offset)`. This avoids a square root for every neighbor check. Small `max(...)` values prevent division by zero.

## 3. The display shader makes metaballs

Each boid leaves a soft influence around its center. At every pixel, the shader adds those influences:

```hlsl
float influence = exp(-dot(offset, offset) / (2.0 * radius * radius));
field += influence;
```

Close to a center, the influence is strong. Far away, it is weak. The surface appears where the sum is high enough:

```hlsl
float mask = smoothstep(threshold - edge, threshold + edge, field);
```

Two nearby centers can make a bridge because their influences add together. This is the same Gaussian function used in `07_Metaballs/Shaders/Metaballs2D.shader`.

**Radius** controls the influence spread. **Threshold** selects the surface level. **Edge Width** softens the boundary. `radius` is a Gaussian standard deviation, not a fixed visible circle radius. The shine is optional 2D shading; it does not create a 3D mesh.

Both shaders multiply horizontal distances by the window's aspect ratio. This keeps distances and circles consistent when the field fills a wide or tall window. Distances and velocities use field-height units, while positions stay normalized.

## Try these small experiments

These are suggested learning exercises. For the first experiments, you can temporarily set Boid Count to 48 to see individuals more clearly. The maximum is 2,000.

1. Pause and switch **Mode** between Boids, Density, and Metaballs. The same positions produce three different views.
2. Stay paused and change **Radius** or **Threshold**. The surface changes while the centers stay still. Turn on **Show Centers** to check.
3. Set the three movement weights to zero. Raise one weight at a time and uncheck **Paused** to compare its effect. The wall force still applies.
4. Open `MetaBoids.compute` and find the five numbered blocks. Trace one boid from reading its old data to writing its new data.

Inspector controls include **Paused**, **Show Centers**, and **Show Grid** checkboxes. Changing Boid Count recreates the buffers and resets the simulation. Stop and start Play mode to restart with the saved settings.

Play-mode Inspector changes are discarded when Play mode ends, as usual in Unity. Set the saved starting values outside Play mode.

## Videos and reading

The order below is a suggested learning path. The Unity videos and tutorial go beyond this small example; focus first on buffers, the kernel, and Dispatch.

| Resource | Use it for |
| --- | --- |
| [Unity video: Compute shaders, Part 1: Particle fun](https://www.youtube.com/watch?v=omZap7XHxKc) | See how C# sends particle data to a compute shader. |
| [Unity video: Compute shaders, Part 2: Flocking](https://www.youtube.com/watch?v=1rOinLsyXAA) | See flocking logic running on the GPU. |
| [Unity Learn: URP recipe: Compute shaders](https://learn.unity.com/tutorial/urp-recipe-compute-shaders) | Follow the written steps for a buffer, a kernel, and a Dispatch call. Unity labels this tutorial intermediate. |
| [Craig Reynolds: Boids](https://www.red3d.com/cwr/boids/) | Read the original explanation and diagrams of the three flocking rules. |
| [Jamie Wong: Metaballs and Marching Squares](https://jamie-wong.com/2014/08/19/metaballs-and-marching-squares/) | Read “The Math” and explore the animations to understand added fields and thresholds. This article uses an inverse-square field and later marching squares; our example uses a Gaussian and draws pixels directly. |

For API details: [Unity Compute shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ComputeShader.html), [ComputeShader.Dispatch](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ComputeShader.Dispatch.html), and [ComputeBuffer](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ComputeBuffer.html).

## Connection to earlier course examples

`Scripts/FlockingSynth.cs` and `Scripts/PianoBoids.cs` use attraction, close-range repulsion, and acceleration-to-velocity-to-position updates. They do not use the same three Reynolds rules. This lesson keeps their data-update idea, replaces the forces with three flocking rules, and moves that calculation to the GPU. Audio and individual boid GameObjects are omitted.

## Limits

Movement checks every pair of boids: its cost grows with `N * N`. Rendering samples every boid for every pixel. Lower Boid Count or Game-view resolution if needed. This lesson deliberately omits spatial hashing, viewing angles, obstacle avoidance, and 3D raymarching to keep the main ideas readable.
