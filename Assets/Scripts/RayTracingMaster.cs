using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls what the Camera is looking at, as well as some user input
/// </summary>
/// Credit for sphere ray tracing goes to David Kuri http://blog.three-eyed-games.com/2018/05/03/gpu-ray-tracing-in-unity-part-1/
public class RayTracingMaster : MonoBehaviour {

    public ComputeShader RayTracingShader;
    public Light DirectionalLight;
    public Vector3 groundAlbedo = new Vector3(0.8f, 0.8f, 0.8f);
    public Vector3 groundSpecular = new Vector3(0.8f, 0.8f, 0.8f);

    public CubeReader cubeReader;
    public ComReader comReader;

    Vector3 sceneOffset;
    Vector3 sceneCentre;
    float sphereSize = 0.25f;
    float cylinderSize = 0.1f;

    float translationSpeed = 0.1f;
    float rotationSpeed = 0.25f;

    int numSpheres;
    private ComputeBuffer _sphereBuffer;

    int numBonds;
    int numHydrogenBonds;
    private ComputeBuffer _bondsBuffer;


    private RenderTexture _target;

    private Camera _camera;

    private uint _currentSample = 0;
    private Vector2 _pixelOffset;
    private Material _addMaterial;
    public Shader AddShader;

    public float lightConeAngle = 1f;

    public Texture3D OrbitalTexture;

    float orbitalPower = 3;
    float isoLevel = 0.02f;

    bool takeScreenShotNow;

    // static Vector4[] colours = new Vector4[8] {
    //     new Vector4( 1.5f,-0.5f,-0.5f, 1f),//new Vector3(1,0,0),
    //     new Vector4(-0.5f, 1.5f,-0.5f, 1f),//new Vector3(0,1,0),
    //     new Vector4(-0.5f,-0.5f, 1.5f, 1f),//new Vector3(0,0,1),
    //     new Vector4( 1.5f, 1.5f,-0.5f, 1f),//new Vector3(1,1,0),
    //     new Vector4(-0.5f, 1.5f, 1.5f, 1f),//new Vector3(0,1,1),
    //     new Vector4( 1.5f,-0.5f, 1.5f, 1f),//new Vector3(1,0,1),
    //     new Vector4( 1.5f, 1.5f, 1.5f, 1f),//new Vector3(1,1,1),
    //     new Vector4(-0.5f,-0.5f,-0.5f, 1f)//new Vector3(0,0,0)
    // };
    static Vector4[] colours = new Vector4[8] {
        new Vector4(1f,0f,0f,1f),//new Vector3(1,0,0),
        new Vector4(0f,1f,0f,1f),//new Vector3(0,1,0),
        new Vector4(0f,0f,1f,1f),//new Vector3(0,0,1),
        new Vector4(1f,1f,0f,1f),//new Vector3(1,1,0),
        new Vector4(0f,1f,1f,1f),//new Vector3(0,1,1),
        new Vector4(1f,0f,1f,1f),//new Vector3(1,0,1),
        new Vector4(1f,1f,1f,1f),//new Vector3(1,1,1),
        new Vector4(0f,0f,0f,1f)//new Vector3(0,0,0)
    };
    public Vector4 positivePhaseColour = colours[0];
    public Vector4 negativePhaseColour = colours[1];
    public Vector4 hBondColour = colours[5];
    public float backgroundBrightness = 0.5f;



    bool showOrb;
    bool showIso = false;
    bool showDensity = true;
    bool showGround;
    bool showHBonds;

    const int numSamplesBeforeAntiAlias = 5;

    public static string inputFile = "/Users/tristanmackenzie/Calculations/Orbital_Tests/chrom_171.cub";
    //public static string inputFile = "/Users/tristanmackenzie/Calculations/Orbital_Tests/chrom.gjf";

    bool isPaused;

    public TextMeshProUGUI overlayText;
    public Image textBackground;
    float textTimer;
    float textDuration = 3;
    float textFadeout = 1;
    Color whiteColor = new Color(1,1,1,1);
    Color clearColor = new Color(1,1,1,0);
    Color backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
    Color backgroundClear = new Color(0.25f, 0.25f, 0.25f, 0f);
    Coroutine textCoroutine;
    bool textCoroutineRunning;

    float hBondCutoff = 2.1f;

    List<Sphere> spheres;
    List<Cylinder> bonds;
    List<Cylinder> hBonds;

    private void Awake() {
        _camera = GetComponent<Camera>();
        //UnityEditor.PlayerSettings.use32BitDisplayBuffer = true;
        string extension = System.IO.Path.GetExtension(inputFile);
        switch (extension.ToLower()) {
            case ".cub":
                cubeReader.Parse(inputFile);
                SetUpSceneCUB();
                SetTextureFromGrid();
                break;
            case ".com":
            case ".gjf":
                comReader.Parse(inputFile);
                SetUpSceneCOM();
                SetEmptyTexture();
                break;
            default:
                SetEmptyTexture();
                break;
        }
    }

    private void SetShaderParameters() {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        // Set a random offset for antialiasing
        // Multiply by const 2 / sqrt(2)
        _pixelOffset = new Vector2(Random.value - 0.5f, Random.value - 0.5f);
        RayTracingShader.SetVector("_PixelOffset", _pixelOffset);

        // Lighting
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        
        if (_currentSample > numSamplesBeforeAntiAlias && lightConeAngle > 0) {
            Quaternion r = Quaternion.Euler(
                (Mathf.Exp(-Random.value) * 2 - 1) * lightConeAngle,
                (Mathf.Exp(-Random.value) * 2 - 1) * lightConeAngle,
                (Mathf.Exp(-Random.value) * 2 - 1) * lightConeAngle
            );
            
            l = r * l;
        }
        RayTracingShader.SetVector("_DirectionalLightRandom", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        RayTracingShader.SetVector("_GroundAlbedo", groundAlbedo);
        RayTracingShader.SetVector("_GroundSpecular", groundSpecular);
        RayTracingShader.SetBool("_ShowGround", showGround);

        //Orbitals
        RayTracingShader.SetFloat ("_OrbitalPower", orbitalPower);
        RayTracingShader.SetFloat ("_IsoLevel", isoLevel);
        RayTracingShader.SetBool ("_ShowIso", showIso);
        RayTracingShader.SetBool ("_ShowDensity", showDensity);
        RayTracingShader.SetBool ("_ShowOrb", showOrb && (showDensity || showIso));
        RayTracingShader.SetBool ("_ShowHBonds", showHBonds);

        RayTracingShader.SetVector("_PositivePhaseColour", positivePhaseColour);
        RayTracingShader.SetVector("_NegativePhaseColour", negativePhaseColour);
        RayTracingShader.SetVector("_HBondColour", hBondColour);

        RayTracingShader.SetFloat("_BackgroundBrightness", backgroundBrightness);
    }

    void SetTextureFromGrid() {

        OrbitalTexture = new Texture3D (
            cubeReader.dimensions[0], 
            cubeReader.dimensions[1], 
            cubeReader.dimensions[2],
            TextureFormat.RGFloat, 
            false
        );

        OrbitalTexture.wrapMode = TextureWrapMode.Clamp;
        OrbitalTexture.SetPixelData(cubeReader.grid, 0);

        OrbitalTexture.Apply ();

        Vector3 min = new Vector3(cubeReader.gridOffset.x, cubeReader.gridOffset.y, -cubeReader.gridOffset.z) + sceneOffset;
        Vector3 size = Vector3.Scale(
            cubeReader.gridScale, 
            new Vector3(
                cubeReader.dimensions[0], 
                cubeReader.dimensions[1], 
                -cubeReader.dimensions[2]
            )
        );
        Vector3 max = size + min;
        RayTracingShader.SetTexture(0, "_OrbData", OrbitalTexture);
        RayTracingShader.SetVector ("_OrbBoundsMin", min);
        RayTracingShader.SetVector ("_OrbBoundsMax", max);
        RayTracingShader.SetVector ("_InvOrbBoundsSize", new Vector3(1f / size.x, 1f / size.y, 1f / size.z));
        showOrb = true;

    }

    void SetEmptyTexture() {
        
        OrbitalTexture = new Texture3D (
            1, 
            1, 
            1,
            TextureFormat.RFloat, 
            false
        );

        Vector3 largestRadius = new Vector3(2,2,-2) * sphereSize;

        OrbitalTexture.wrapMode = TextureWrapMode.Clamp;
        OrbitalTexture.Apply ();
        RayTracingShader.SetTexture(0, "_OrbData", OrbitalTexture);
        RayTracingShader.SetVector ("_OrbBoundsMin", new Vector3(comReader.minBound.x, comReader.minBound.y, -comReader.minBound.z) + sceneOffset - largestRadius);
        RayTracingShader.SetVector ("_OrbBoundsMax", new Vector3(comReader.maxBound.x, comReader.maxBound.y, -comReader.maxBound.z) + sceneOffset + largestRadius);
        Vector3 size = comReader.maxBound - comReader.minBound + 2 * largestRadius;
        RayTracingShader.SetVector ("_InvOrbBoundsSize", new Vector3(1f / size.x, 1f / size.y, 1f / size.z));
        showOrb = false;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination) {
        // Make sure we have a current render target
        InitRenderTexture(Screen.width, Screen.height);
        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        
        if (transform.hasChanged) {
            _currentSample = 0;
            transform.hasChanged = false;
        }
        
        if (windowSize.x != Screen.width || windowSize.y != Screen.height) {
            _currentSample = 0;
            windowSize.x = Screen.width;
            windowSize.y = Screen.height;
        }

        if (DirectionalLight.transform.hasChanged) {
            Vector3 l = DirectionalLight.transform.forward;
            _currentSample = 0;
            //RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
            DirectionalLight.transform.hasChanged = false;
        }
        
        if (! isPaused) {
            if (_currentSample > numSamplesBeforeAntiAlias) {
                if (_addMaterial == null) {
                    _addMaterial = new Material(AddShader);
                }
                _addMaterial.SetFloat("_Alpha", 1f / (1f + _currentSample - numSamplesBeforeAntiAlias));

                // Blit the result texture to the screen
                Graphics.Blit(_target, destination, _addMaterial);
                //Graphics.Blit(_target, destination);
            } else {
                Graphics.Blit(_target, destination);
            }
            
            // Update sample index
            _currentSample++;
        } else {
            Graphics.Blit(_target, destination);
        }
        //Graphics.Blit(destination, renderCopy);

        if (takeScreenShotNow) {
            RenderTexture currentRT = RenderTexture.active;
            TakeScreenShot(destination);
            RenderTexture.active = currentRT;
            takeScreenShotNow = false;
        }

    }

    private void InitRenderTexture(int screenWidth, int screenHeight) {
        if (_target == null || _target.width != screenWidth || _target.height != screenHeight) {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();
            // Get a render target for Ray Tracing
            _target = new RenderTexture(screenWidth, screenHeight, 0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();

            // Reset current sample index
            _currentSample = 0;
        }
    }

    private void OnEnable() {
        _currentSample = 0;
    }

    private void OnDisable() {
        if (_sphereBuffer != null) {
            _sphereBuffer.Release();
        }
        if (_bondsBuffer != null) {
            _bondsBuffer.Release();
        }
    }

    void OnApplicationFocus(bool hasFocus) {
        isPaused = !hasFocus;
    }

    void OnApplicationPause(bool pauseStatus) {
        isPaused = pauseStatus;
    }

    private void SetUpSceneCUB() {

        int[][] connections = cubeReader.GetConnectivity().ToArray();

        SetSceneCentre(cubeReader.atomicPositions);

        GetSpheres(cubeReader.atomicPositions, cubeReader.atomicNumbers);

        // Assign to compute buffer
        numSpheres = spheres.Count;
        _sphereBuffer = new ComputeBuffer(numSpheres, 32);
        _sphereBuffer.SetData(spheres);

        //Spheres
        RayTracingShader.SetInt("numSpheres", numSpheres);
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);

        GetBonds(connections, cubeReader.atomicPositions, cubeReader.atomicNumbers);
        numBonds = bonds.Count;
        RayTracingShader.SetInt("numBonds", numBonds);

        GetHBonds(connections, cubeReader.atomicPositions, cubeReader.atomicNumbers);
        numHydrogenBonds = hBonds.Count;
        RayTracingShader.SetInt("numHBonds", numHydrogenBonds);
        
        //Cylinders
        _bondsBuffer = new ComputeBuffer(numBonds + numHydrogenBonds, 48);
        bonds.AddRange(hBonds);
        _bondsBuffer.SetData(bonds);
        RayTracingShader.SetBuffer(0, "_Bonds", _bondsBuffer);
    }

    private void SetUpSceneCOM() {

        Vector3[] atomicPositions = comReader.positions.ToArray();
        int numAtoms = atomicPositions.Length;
        string[] layers = comReader.layers.ToArray();
        int[][] connections = comReader.connections.ToArray();

        int[] atomicNumbers = GetAtomicNumbers(comReader.elements.ToArray());
        SetSceneCentre(atomicPositions);

        GetSpheres(atomicPositions, atomicNumbers, layers);

        // Assign to compute buffer
        numSpheres = spheres.Count;
        _sphereBuffer = new ComputeBuffer(numSpheres, 32);
        _sphereBuffer.SetData(spheres);

        //Spheres
        RayTracingShader.SetInt("numSpheres", numSpheres);
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);

        GetBonds(connections, atomicPositions, atomicNumbers, layers);
        numBonds = bonds.Count;
        RayTracingShader.SetInt("numBonds", numBonds);

        GetHBonds(connections, atomicPositions, atomicNumbers);
        numHydrogenBonds = hBonds.Count;
        RayTracingShader.SetInt("numHBonds", numHydrogenBonds);
        
        //Cylinders
        _bondsBuffer = new ComputeBuffer(numBonds + numHydrogenBonds, 48);
        bonds.AddRange(hBonds);
        _bondsBuffer.SetData(bonds);
        RayTracingShader.SetBuffer(0, "_Bonds", _bondsBuffer);
        
    }

    public int[] GetAtomicNumbers(string[] elements) {
        int[] atomicNumbers = new int[elements.Length];
        for (int i = 0; i < elements.Length; i++) {
            atomicNumbers[i] = Data.GetAtomicNumber(elements[i]);
        }
        return atomicNumbers;
    }

    public void SetSceneCentre(
        Vector3[] atomicPositions
    ) {
        
        Vector3 minP = atomicPositions[0];
        Vector3 maxP = atomicPositions[0];
        Vector3 centre = Vector3.zero;
        float lowestY = float.MaxValue;
        // Determine scene centre
        for (int i = 0; i < atomicPositions.Length; i++) {
            Vector3 position = atomicPositions[i];
            
            //Flip z
            position.z = -position.z;

            centre += position;
            if (position.y < lowestY) {
                lowestY = position.y;
            }

            minP = Vector3.Min(minP, position);
            maxP = Vector3.Max(maxP, position);
            //Debug.Log(position);
        }

        centre /= atomicPositions.Length;
        lowestY -= centre.y;

        sceneCentre = new Vector3(0, 2-lowestY, 0);

        transform.position = sceneCentre - Vector3.forward * 20;

        sceneOffset = sceneCentre - centre;
    }

    void GetSpheres(
        Vector3[] atomicPositions,
        int[] atomicNumbers,
        string[] layers=null
    ) {
        spheres = new List<Sphere>();
        for (int i = 0; i < atomicPositions.Length; i++) {

            Sphere sphere = new Sphere();
            int atomicNumber = atomicNumbers[i];

            // Radius and position
            float radius = Data.GetRadius(atomicNumber) * sphereSize;

            if (layers != null) {
                string layer = layers[i];
                if (layer != "H") {
                    radius *= 0.25f;
                }
            }
            
            sphere.radius = radius;

            sphere.position = atomicPositions[i];

            //Flip z
            sphere.position.z = -sphere.position.z;
            sphere.position += sceneOffset;
            
            // Albedo and specular color
            Vector3 color = Data.GetColour(atomicNumber);
            sphere.specular = color;

            sphere.mass = Data.GetMass(atomicNumber);

            // Add the sphere to the list
            spheres.Add(sphere);
        }
    }

    void GetBonds(
        int[][] connections,
        Vector3[] atomicPositions,
        int[] atomicNumbers,
        string[] layers=null
    ) {
        bonds = new List<Cylinder>();
        
        for (int i = 0; i < connections.Length; i++) {
            Cylinder cylinder = new Cylinder();

            int index0 = connections[i][0];
            int index1 = connections[i][1];

            int atomicNumber0 = atomicNumbers[index0];
            int atomicNumber1 = atomicNumbers[index1];
            

            // Radius and position
            float radius = (Data.GetRadius(atomicNumber0) + Data.GetRadius(atomicNumber1)) * cylinderSize * 0.5f;

            if (layers != null) {
                string layer0 = layers[index0];
                string layer1 = layers[index1];
                if (layer0 != "H") {radius *= 0.5f;}
                if (layer1 != "H") {radius *= 0.5f;}
            }
            cylinder.radius = radius;
            cylinder.position = (atomicPositions[index0] + atomicPositions[index1]) * 0.5f;
            
            //Flip z
            cylinder.position.z = -cylinder.position.z;
            cylinder.position += sceneOffset;

            // Dimensions
            Vector3 v01 = (atomicPositions[index0] - atomicPositions[index1]);
            //Flip z
            v01.z = -v01.z;
            cylinder.length = v01.magnitude;
            cylinder.direction = (cylinder.length == 0) ? Vector3.forward : v01 / cylinder.length;

            // Albedo and specular color
            Vector3 color0 = Data.GetColour(atomicNumber0);
            Vector3 color1 = Data.GetColour(atomicNumber1);
            cylinder.specular = (color0 + color1) / 2;

            bonds.Add(cylinder);
        }
    }

    void GetHBonds(
        int[][] connections,
        Vector3[] atomicPositions,
        int[] atomicNumbers
    ) {
        List<int[]> hBondsConnections = GetHydrogenBonds(connections, atomicPositions, atomicNumbers);
        
        hBonds = new List<Cylinder>();

        // Add bonds
        for (int i = 0; i < hBondsConnections.Count; i++) {
            Cylinder cylinder = new Cylinder();

            int index0 = hBondsConnections[i][0];
            int index1 = hBondsConnections[i][1];

            // Radius and position
            cylinder.radius = cylinderSize;
            cylinder.position = (atomicPositions[index0] + atomicPositions[index1]) * 0.5f;
            
            //Flip z
            cylinder.position.z = -cylinder.position.z;
            cylinder.position += sceneOffset;

            // Dimensions
            Vector3 v01 = (atomicPositions[index0] - atomicPositions[index1]);
            //Flip z
            v01.z = -v01.z;
            cylinder.length = v01.magnitude * 0.8f;
            cylinder.direction = (cylinder.length == 0) ? Vector3.forward : v01 / cylinder.length;

            // Albedo and specular color
            Vector3 color = new Vector3(0.8f, 0.8f, 0.4f);
            cylinder.specular = color;

            hBonds.Add(cylinder);
        }
    }

    public List<int[]> GetHydrogenBonds(
        int[][] connections,
        Vector3[] atomicPositions,
        int[] atomicNumbers
    ) {
        int numAtoms = atomicPositions.Length;
        List<int[]> hydrogenBonds = new List<int[]>();

        float hBondCutoffSq = hBondCutoff * hBondCutoff;

        bool IsH(int atomicNumber) {
            return atomicNumber == 1;
        }

        bool IsHAcceptor(int atomicNumber) {
            return atomicNumber == 7 || atomicNumber == 8;
        }

        for (int i=0; i<connections.Length; i++) {
            int[] connection = connections[i];
            int index0 = connection[0];
            int index1 = connection[1];
            
            int atomicNumber0 = atomicNumbers[index0];
            int atomicNumber1 = atomicNumbers[index1];

            // Determine if this is H and eligible for H bond
            int hIndex;
            int neighbourIndex;
            if (IsH(atomicNumber0) && IsHAcceptor(atomicNumber1)) {
                hIndex = index0;
                neighbourIndex = index1;
            } else if (IsH(atomicNumber1) && IsHAcceptor(atomicNumber0)) {
                hIndex = index1;
                neighbourIndex = index0;
            } else {
                // Not an H-O or H-N bond
                continue;
            }

            Vector3 pos0 = atomicPositions[hIndex];

            for (int accIndex=0; accIndex<numAtoms; accIndex++) {
                
                int atomicNumberAcc = atomicNumbers[accIndex];

                // Check these are not already neighbours
                if (IsHAcceptor(atomicNumberAcc) && accIndex != neighbourIndex) {
                    Vector3 pos1 = atomicPositions[accIndex];

                    float distanceSq = Vector3.SqrMagnitude(pos0 - pos1);
                    if (distanceSq < hBondCutoffSq) {
                        hydrogenBonds.Add(new int[2] {hIndex,accIndex});
                    }
                }

            }
        }

        return hydrogenBonds;
    }

    private void Update()   {


        bool shiftDown = (
            Input.GetKey(KeyCode.LeftShift) || 
            Input.GetKey(KeyCode.RightShift)
        );

        bool ctrlDown = (
            Input.GetKey(KeyCode.LeftControl) || 
            Input.GetKey(KeyCode.RightControl)
        );

        if (Input.GetMouseButtonDown(0)) {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                StopCoroutine("RotateLight");
                StartCoroutine("RotateLight");
            } else {
                StopCoroutine("RotateCamera");
                StartCoroutine("RotateCamera");
            }
        }

        if (Input.GetMouseButtonDown(1)) {
            StopCoroutine("TranslateCamera");
            StartCoroutine("TranslateCamera");
        }

        if (Input.GetKeyDown(KeyCode.Space)) {
            StopCoroutine("RotateLight");
            StopCoroutine("RotateCamera");
            StopCoroutine("TranslateCamera");

        }


        if (shiftDown) {
            CheckChangeValue(ref isoLevel, KeyCode.Equals, KeyCode.Minus, isoLevel, 0.0001f, 0.4f, "Iso Level {0:#.0000}");
            CheckChangeValue(ref backgroundBrightness, KeyCode.RightBracket, KeyCode.LeftBracket, 0.5f, 0, 1f, "Background Brightness {0:#.0}");
        } else {
            CheckChangeValue(ref orbitalPower, KeyCode.Equals, KeyCode.Minus, 2, 0, 20f, "Orbital Intensity {0:#.0}");
            CheckChangeValue(ref lightConeAngle, KeyCode.RightBracket, KeyCode.LeftBracket, 0.5f, 0, 2f, "Lighting Angle {0:#.0}");
        }

        if (Input.GetKeyDown(KeyCode.I)) {
            showIso = !showIso;
            _currentSample = 0;
            SetText(showIso?"Showing iso surface":"Hiding iso surface");
        }
        if (Input.GetKeyDown(KeyCode.D)) {
            showDensity = !showDensity;
            _currentSample = 0;
            SetText(showDensity?"Showing density":"Hiding density");
        }
        if (Input.GetKeyDown(KeyCode.G)) {
            showGround = !showGround;
            _currentSample = 0;
            SetText(showGround?"Showing ground plane":"Hiding ground plane");
        }
        if (Input.GetKeyDown(KeyCode.H)) {
            showHBonds = !showHBonds;
            _currentSample = 0;
            SetText(showHBonds?"Showing hydrogen bonds":"Hiding hydrogen bonds");
        }
        if (Input.GetKeyDown(KeyCode.B)) {
            SetSceneBest();
            SetText("Rotating to best position");
        }

        if (Input.GetKeyDown(KeyCode.E)) {
            if (shiftDown) {
                TakeHighResScreenShot(30);
            } else {
                takeScreenShotNow = true;
            }
        }

        // Phase Colours
        CheckAssignPhaseColour(KeyCode.Alpha1, shiftDown, ctrlDown, colours[0]);
        CheckAssignPhaseColour(KeyCode.Alpha2, shiftDown, ctrlDown, colours[1]);
        CheckAssignPhaseColour(KeyCode.Alpha3, shiftDown, ctrlDown, colours[2]);
        CheckAssignPhaseColour(KeyCode.Alpha4, shiftDown, ctrlDown, colours[3]);
        CheckAssignPhaseColour(KeyCode.Alpha5, shiftDown, ctrlDown, colours[4]);
        CheckAssignPhaseColour(KeyCode.Alpha6, shiftDown, ctrlDown, colours[5]);
        CheckAssignPhaseColour(KeyCode.Alpha7, shiftDown, ctrlDown, colours[6]);
        CheckAssignPhaseColour(KeyCode.Alpha8, shiftDown, ctrlDown, colours[7]);

        if (Input.GetKey(KeyCode.Escape)){
            Application.Quit();
        }

        transform.Translate(Vector3.forward * Input.mouseScrollDelta.y);
    }

    private void CheckChangeValue(ref float value, KeyCode increaseKey, KeyCode decreaseKey, float rate, float minValue, float maxValue, string messageFormat) {
        if (Input.GetKey(increaseKey)) {
            value += rate * Time.deltaTime;
            if (value > maxValue) {
                value = maxValue;
            } else {
                _currentSample = 0;
            }
        } else if (Input.GetKey(decreaseKey)) {        
            value -= rate * Time.deltaTime;
            if (value < minValue) {
                value = minValue;
            } else {
                _currentSample = 0;
            }
        } else {
            return;
        }
        SetText(string.Format(messageFormat, value));
    }

    private void CheckAssignPhaseColour(KeyCode key, bool shiftDown, bool ctrlDown, Vector4 colour) {
        if (Input.GetKeyDown(key)) {
            if (shiftDown) {
                negativePhaseColour = colour;
            } else if (ctrlDown) {
                hBondColour = colour;
            } else {
                positivePhaseColour = colour;
            }
            _currentSample = 0;
        }
    }

    Vector3 mousePosition;
    Vector2 windowSize;

    public IEnumerator RotateCamera() {
        mousePosition = Input.mousePosition;
        Vector3 deltaMousePosition = Vector3.zero;
        while (Input.GetMouseButton(0)) {
            
            deltaMousePosition = mousePosition - Input.mousePosition;
            transform.RotateAround(sceneCentre, transform.up, -deltaMousePosition.x*rotationSpeed);
            transform.RotateAround(sceneCentre, transform.right, deltaMousePosition.y*rotationSpeed);
            mousePosition = Input.mousePosition;
            yield return null;
        }

        while (deltaMousePosition != Vector3.zero) {
            transform.RotateAround(sceneCentre, transform.up, -deltaMousePosition.x*rotationSpeed);
            transform.RotateAround(sceneCentre, transform.right, deltaMousePosition.y*rotationSpeed);
            yield return null;
        }
    }

    public IEnumerator RotateLight() {
        mousePosition = Input.mousePosition;
        Vector3 deltaMousePosition = Vector3.zero;
        while (Input.GetMouseButton(0)) {
            
            deltaMousePosition = mousePosition - Input.mousePosition;
            DirectionalLight.transform.RotateAround(sceneCentre, DirectionalLight.transform.up, -deltaMousePosition.x);
            DirectionalLight.transform.RotateAround(sceneCentre, DirectionalLight.transform.right, deltaMousePosition.y);
            mousePosition = Input.mousePosition;
            yield return null;
        }

        if (deltaMousePosition != Vector3.zero) {
            DirectionalLight.transform.RotateAround(sceneCentre, DirectionalLight.transform.up, -deltaMousePosition.x);
            DirectionalLight.transform.RotateAround(sceneCentre, DirectionalLight.transform.right, deltaMousePosition.y);
            yield return null;
        }
    }

    public IEnumerator TranslateCamera() {
        mousePosition = Input.mousePosition;
        Vector3 deltaMousePosition = Vector3.zero;
        while (Input.GetMouseButton(1)) {
            
            deltaMousePosition = mousePosition - Input.mousePosition;
            transform.Translate(deltaMousePosition * translationSpeed, Space.Self);
            mousePosition = Input.mousePosition;
            yield return null;
        }

        if (deltaMousePosition != Vector3.zero) {
            transform.Translate(deltaMousePosition * translationSpeed, Space.Self);
            yield return null;
        }
    }

    public void SetSceneBest() {

        // Compute the inertia tensor, then:
        // align the forward vector with the largest moment of inertia
        // align the up vector with the second largest moment of inertia

        if (numSpheres == 0) {return;}

        float[] masses = spheres.Select(x => x.mass).ToArray();
        Vector3[] positions = spheres.Select(x => x.position).ToArray();

        Matrix4x4 inertiaTensor = Mathematics.GetInertiaTensor(positions, masses);
        
        Vector3 eigenvalues = Mathematics.GetEigenValues(inertiaTensor);
        Matrix4x4 eigenvectors = Mathematics.GetEigenVectors(inertiaTensor, eigenvalues);
        Vector3 normEig = eigenvalues.normalized;

        //Put Atom 0 in the left
        Vector3 newForward = eigenvectors.GetRow(0);
        Vector3 newUp = eigenvectors.GetRow(1);
        Vector3 newRight = eigenvectors.GetRow(2);
        Vector3 position0 = spheres.First().position;
        if (Vector3.Dot(newRight, position0) > 0) {
            newForward *= -1;
        }

        Quaternion rotation = Quaternion.LookRotation(
            newForward,
            newUp
        );

        transform.rotation = rotation;
        transform.position = sceneCentre - transform.forward * 20;

        // Move the light to be just above the camera
        DirectionalLight.transform.rotation = transform.rotation;
        DirectionalLight.transform.RotateAround(sceneCentre, DirectionalLight.transform.right, 20);
    }



    void SetText(string text) {
        overlayText.text = text;
        textTimer = textDuration + textFadeout;
        if (textCoroutine == null || !textCoroutineRunning) {
            textCoroutine = StartCoroutine(ShowText());
        }
    }

    IEnumerator ShowText() {
        textCoroutineRunning = true;
        overlayText.color = whiteColor;
        while (textTimer > 0) {
            textTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(textTimer / textFadeout);
            overlayText.color = Color.Lerp(clearColor, whiteColor, t);
            textBackground.color = Color.Lerp(backgroundClear, backgroundColor, t);
            yield return null;
        }
        overlayText.color = clearColor;
        textBackground.color = backgroundClear;
        textCoroutineRunning = false;
    }

    void TakeScreenShot(RenderTexture renderTexture) {

        string directory = System.IO.Path.GetDirectoryName(inputFile);
        string filename = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";

        int w = renderTexture.width;
        int h = renderTexture.height;
        var screenShot = new Texture2D(w, h, TextureFormat.ARGB32, false, true);
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        screenShot.ReadPixels(new Rect (0, 0, w, h), 0, 0);
        screenShot.Apply();
        RenderTexture.active = currentRT;

        byte[] bytes = screenShot.EncodeToPNG();

        System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, filename), bytes);
    }

    void TakeHighResScreenShot(int numSamples) {

        string directory = System.IO.Path.GetDirectoryName(inputFile);
        string filename = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";

        int w = Screen.width * 2;
        int h = Screen.height * 2;

        RenderTexture renderTexture = new RenderTexture(w, h, 0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();

        var screenShot = new Texture2D(w, h, TextureFormat.ARGB32, false, true);
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;

        InitRenderTexture(w, h);
        
        // Ensure AA material isn't null
        if (_addMaterial == null) {
            _addMaterial = new Material(AddShader);
        }

        for (int sample=0; sample<numSamples; sample++) {
                
            RayTracingShader.SetTexture(0, "Result", _target);
            int threadGroupsX = Mathf.CeilToInt(w / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(h / 8.0f);
            RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

            _addMaterial.SetFloat("_Alpha", 1f / (1f + sample));

            Graphics.Blit(_target, renderTexture, _addMaterial);

            screenShot.ReadPixels(new Rect (0, 0, w, h), 0, 0);
            screenShot.Apply();
        }
        
        RenderTexture.active = currentRT;

        byte[] bytes = screenShot.EncodeToPNG();

        System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, filename), bytes);
    }
}


public struct Sphere {
    public Vector3 position;
    public float radius;
    public Vector3 specular;
    public float mass;
};

public struct Cylinder {
    public Vector3 position;
    public float radius;
    public Vector3 direction;
    public float length;
    public Vector4 specular;
};
