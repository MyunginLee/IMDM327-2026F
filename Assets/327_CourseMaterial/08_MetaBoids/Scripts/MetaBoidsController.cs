using UnityEngine;

namespace IMDM327.MetaBoids
{
    /*
     * START HERE
     * C# creates the data and sends settings. The two shaders move and draw it.
     * Follow the numbered sections from setup to movement to drawing.
     * Learn more: https://learn.unity.com/tutorial/urp-recipe-compute-shaders
     */
    public class MetaBoidsController : MonoBehaviour
    {
        public enum ViewMode { Boids, Density, Metaballs }
        const int MaxBoids = 2000;
        const int ThreadsPerGroup = 64; // Same number as numthreads in MetaBoids.compute.

        /*
         * 1. INSPECTOR SETTINGS
         * Boids settings change movement. Metaballs settings change appearance.
         */
        [Header("Boids")]
        [Range(16, MaxBoids)] public int boidCount = MaxBoids;
        [Range(0f, 3f)] public float separation = 1.4f;
        [Range(0f, 3f)] public float alignment = 0.8f;
        [Range(0f, 3f)] public float cohesion = 0.9f;
        [Range(0.05f, 0.4f)] public float neighborRadius = 0.18f;
        [Range(0.01f, 0.1f)] public float separationRadius = 0.045f;
        [Range(0.03f, 0.4f)] public float maxSpeed = 0.16f;

        [Header("Metaballs")]
        public ViewMode mode = ViewMode.Metaballs;
        [Range(0.015f, 0.08f)] public float radius = 0.036f;
        [Range(0.2f, 2f)] public float threshold = 0.9f;
        [Range(0.005f, 0.15f)] public float edgeWidth = 0.035f;
        public bool showCenters;
        public bool showGrid;

        [Header("Playback")]
        public bool paused;
        [Range(0.25f, 2f)] public float simulationSpeed = 1f;
        public int seed = 327;

        [Header("References")]
        public ComputeShader simulation;
        public Material displayMaterial;
        public Renderer display;
        public Camera viewCamera;

        /*
         * GPU DATA
         * One boid uses four numbers: position X/Y, then velocity X/Y.
         * Keep two buffers: read the old state, write the new state.
         */
        const int BoidStride = sizeof(float) * 4;
        ComputeBuffer currentBuffer;
        int activeCount;
        Vector2 fieldSize;

        ComputeBuffer nextBuffer;
        Material material;
        int kernel;
        Vector4 leftPinch, rightPinch; // Optional hand forces; zero in the basic scene.

        public void SetPinchForces(Vector4 left, Vector4 right)
        {
            leftPinch = left;
            rightPinch = right;
        }

        /*
         * 2. CREATE THE STARTING DATA
         * Start prepares the material and two buffers when Play begins.
         * The seed repeats the same starting positions and velocities.
         */
        void Start()
        {
            FitDisplayToCamera();
            material = new Material(displayMaterial);
            kernel = simulation.FindKernel("Simulate");
            display.sharedMaterial = material;
            ResetSimulation();
        }

        void ResetSimulation()
        {
            currentBuffer?.Release();
            nextBuffer?.Release();
            activeCount = boidCount;
            currentBuffer = new ComputeBuffer(activeCount, BoidStride);
            nextBuffer = new ComputeBuffer(activeCount, BoidStride);
            Vector4[] data = new Vector4[activeCount];
            var random = new System.Random(seed); // Does not change Unity's global random state.
            for (int i = 0; i < data.Length; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float x = 0.05f + (float)random.NextDouble() * 0.9f;
                float y = 0.05f + (float)random.NextDouble() * 0.9f;
                float velocityX = Mathf.Cos(angle) * maxSpeed * 0.7f;
                float velocityY = Mathf.Sin(angle) * maxSpeed * 0.7f;
                data[i] = new Vector4(x, y, velocityX, velocityY);
            }
            currentBuffer.SetData(data);
            nextBuffer.SetData(data);
            Draw();
        }

        /*
         * 3. RUN THE SIMULATION
         * Update applies Inspector changes to the display each frame.
         * FixedUpdate moves the boids at Unity's fixed time interval.
         */
        void Update()
        {
            FitDisplayToCamera();
            if (boidCount != activeCount) ResetSimulation();
            Draw();
        }

        void FixedUpdate()
        {
            if (!paused) SimulateStep(Time.fixedDeltaTime * simulationSpeed);
        }

        void SimulateStep(float deltaTime)
        {
            // A. Send the current Inspector values to the movement shader.
            simulation.SetInt("_BoidCount", activeCount);
            simulation.SetVector("_FieldSize", fieldSize);
            simulation.SetFloat("_DeltaTime", deltaTime);
            simulation.SetFloat("_MaxSpeed", maxSpeed);
            simulation.SetFloat("_NeighborRadius", neighborRadius);
            simulation.SetFloat("_SeparationRadius", separationRadius);
            simulation.SetVector("_Weights", new Vector4(separation, alignment, cohesion, 0f));
            simulation.SetVector("_LeftPinch", leftPinch);
            simulation.SetVector("_RightPinch", rightPinch);

            // B. Run one thread per boid. 2,000 boids need 32 groups of 64.
            simulation.SetBuffer(kernel, "_ReadBoids", currentBuffer);
            simulation.SetBuffer(kernel, "_WriteBoids", nextBuffer);
            int groups = Mathf.CeilToInt(activeCount / (float)ThreadsPerGroup);
            simulation.Dispatch(kernel, groups, 1, 1);

            // C. Swap: the newly written data becomes the next frame's input.
            ComputeBuffer old = currentBuffer;
            currentBuffer = nextBuffer;
            nextBuffer = old;
        }

        /*
         * 4. DRAW THE METABALLS
         * Give the display shader the latest buffer and surface settings.
         * This draws the boids; it does not change their positions.
         */
        void Draw()
        {
            material.SetBuffer("_Boids", currentBuffer);
            material.SetInt("_BoidCount", activeCount);
            material.SetInt("_Mode", (int)mode);
            material.SetInt("_ShowCenters", showCenters ? 1 : 0);
            material.SetInt("_ShowGrid", showGrid ? 1 : 0);
            material.SetVector("_FieldSize", fieldSize);
            material.SetFloat("_Radius", radius);
            material.SetFloat("_Threshold", threshold);
            material.SetFloat("_EdgeWidth", edgeWidth);
        }

        /*
         * 5. CAMERA AND CLEANUP
         * Match the quad to the window. Free GPU memory when the object is destroyed.
         */
        void FitDisplayToCamera()
        {
            viewCamera.rect = new Rect(0f, 0f, 1f, 1f);
            float height = viewCamera.orthographicSize * 2f;
            float aspect = viewCamera.aspect;
            display.transform.localScale = new Vector3(height * aspect, height, 1f);
            fieldSize = new Vector2(aspect, 1f);
        }

        void OnDestroy()
        {
            currentBuffer?.Release();
            nextBuffer?.Release();
            Destroy(material);
        }
    }
}
