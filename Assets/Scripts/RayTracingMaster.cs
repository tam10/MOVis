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

    public Texture SkyboxTexture;
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
    private Material _addMaterial;
    public Shader AddShader;

    public float lightConeAngle = 1f;

    public Texture3D OrbitalTexture;

    float orbitalPower = 3;
    float isoLevel = 0.02f;
    bool showIso;
    bool showGround;
    bool showHBonds;

    const int numSamplesBeforeAntiAlias = 10;

    public static string cubeFile = "/Users/tristanmackenzie/Calculations/Orbital_Tests/chrom_171.cub";

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

    private void Awake() {
        _camera = GetComponent<Camera>();
        string extension = System.IO.Path.GetExtension(cubeFile);
        switch (extension.ToLower()) {
            case ".cub":
                cubeReader.Parse(cubeFile);
                SetUpSceneCUB();
                SetTextureFromGrid();
                break;
            case ".com":
            case ".gjf":
                comReader.Parse(cubeFile);
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
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

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
        RayTracingShader.SetBool ("_ShowHBonds", showHBonds);

    }

    void SetTextureFromGrid() {

        OrbitalTexture = new Texture3D (
            cubeReader.dimensions[0], 
            cubeReader.dimensions[1], 
            cubeReader.dimensions[2],
            TextureFormat.RFloat, 
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
        RayTracingShader.SetVector ("_OrbBoundsSize", size);
        RayTracingShader.SetBool ("_ShowOrb", true);

    }

    void SetEmptyTexture() {
        
        OrbitalTexture = new Texture3D (
            1, 
            1, 
            1,
            TextureFormat.RFloat, 
            false
        );

        OrbitalTexture.wrapMode = TextureWrapMode.Clamp;
        OrbitalTexture.Apply ();
        RayTracingShader.SetTexture(0, "_OrbData", OrbitalTexture);
        RayTracingShader.SetBool ("_ShowOrb", false);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination) {
        // Make sure we have a current render target
        InitRenderTexture();
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
                _addMaterial.SetFloat("_Sample", _currentSample-11);

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

    }

    private void InitRenderTexture() {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height) {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();
            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
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

        List<Sphere> spheres = GetSpheres(cubeReader.atomicPositions, cubeReader.atomicNumbers);

        // Assign to compute buffer
        numSpheres = spheres.Count;
        _sphereBuffer = new ComputeBuffer(numSpheres, 40);
        _sphereBuffer.SetData(spheres);

        //Spheres
        RayTracingShader.SetInt("numSpheres", numSpheres);
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);

        List<Cylinder> bonds = GetBonds(connections, cubeReader.atomicPositions, cubeReader.atomicNumbers);
        numBonds = bonds.Count;
        RayTracingShader.SetInt("numBonds", numBonds);

        List<Cylinder> hBonds = GetHBonds(connections, cubeReader.atomicPositions, cubeReader.atomicNumbers);
        numHydrogenBonds = hBonds.Count;
        RayTracingShader.SetInt("numHBonds", numHydrogenBonds);
        
        //Cylinders
        _bondsBuffer = new ComputeBuffer(numBonds + numHydrogenBonds, 80);
        bonds.AddRange(hBonds);
        _bondsBuffer.SetData(bonds);
        RayTracingShader.SetBuffer(0, "_Bonds", _bondsBuffer);

        //Sky
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
    }

    private void SetUpSceneCOM() {

        Vector3[] atomicPositions = comReader.positions.ToArray();
        int numAtoms = atomicPositions.Length;
        string[] layers = comReader.layers.ToArray();
        int[][] connections = comReader.connections.ToArray();

        int[] atomicNumbers = GetAtomicNumbers(comReader.elements.ToArray());
        SetSceneCentre(atomicPositions);

        List<Sphere> spheres = GetSpheres(atomicPositions, atomicNumbers, layers);

        // Assign to compute buffer
        numSpheres = spheres.Count;
        _sphereBuffer = new ComputeBuffer(numSpheres, 40);
        _sphereBuffer.SetData(spheres);

        //Spheres
        RayTracingShader.SetInt("numSpheres", numSpheres);
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);

        List<Cylinder> bonds = GetBonds(connections, atomicPositions, atomicNumbers, layers);
        numBonds = bonds.Count;
        RayTracingShader.SetInt("numBonds", numBonds);

        List<Cylinder> hBonds = GetHBonds(connections, atomicPositions, atomicNumbers);
        numHydrogenBonds = hBonds.Count;
        RayTracingShader.SetInt("numHBonds", numHydrogenBonds);
        
        //Cylinders
        _bondsBuffer = new ComputeBuffer(numBonds + numHydrogenBonds, 80);
        bonds.AddRange(hBonds);
        _bondsBuffer.SetData(bonds);
        RayTracingShader.SetBuffer(0, "_Bonds", _bondsBuffer);
        
        //Sky
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
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

    public List<Sphere> GetSpheres(
        Vector3[] atomicPositions,
        int[] atomicNumbers,
        string[] layers=null
    ) {
        List<Sphere> spheres = new List<Sphere>();
        for (int i = 0; i < atomicPositions.Length; i++) {

            Sphere sphere = new Sphere();
            int atomicNumber = atomicNumbers[i];

            // Radius and position
            sphere.radius = Data.GetRadius(atomicNumber) * sphereSize;

            if (layers != null) {
                string layer = layers[i];
                if (layer != "H") {
                    sphere.radius *= 0.25f;
                }
            }

            sphere.position = atomicPositions[i];

            //Flip z
            sphere.position.z = -sphere.position.z;
            sphere.position += sceneOffset;
            
            // Albedo and specular color
            Vector3 color = Data.GetColour(atomicNumber);
            sphere.albedo = Vector3.zero;
            sphere.specular = color;

            // Add the sphere to the list
            spheres.Add(sphere);
        }
        return spheres;
    }

    public List<Cylinder> GetBonds(
        int[][] connections,
        Vector3[] atomicPositions,
        int[] atomicNumbers,
        string[] layers=null
    ) {
        List<Cylinder> bonds = new List<Cylinder>();
        
        for (int i = 0; i < connections.Length; i++) {
            Cylinder cylinder = new Cylinder();

            int index0 = connections[i][0];
            int index1 = connections[i][1];

            int atomicNumber0 = atomicNumbers[index0];
            int atomicNumber1 = atomicNumbers[index1];
            

            // Radius and position
            cylinder.radius = (Data.GetRadius(atomicNumber0) + Data.GetRadius(atomicNumber1)) * cylinderSize * 0.5f;

            if (layers != null) {
                string layer0 = layers[index0];
                string layer1 = layers[index1];
                if (layer0 != "H") {cylinder.radius *= 0.5f;}
                if (layer1 != "H") {cylinder.radius *= 0.5f;}
            }
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
            cylinder.albedo0 = Vector3.zero;
            cylinder.specular0 = color0;
            
            Vector3 color1 = Data.GetColour(atomicNumber1);
            cylinder.albedo1 = Vector3.zero;
            cylinder.specular1 = color1;

            bonds.Add(cylinder);
        }
        return bonds;
    }

    public List<Cylinder> GetHBonds(
        int[][] connections,
        Vector3[] atomicPositions,
        int[] atomicNumbers
    ) {
        List<int[]> hBondsConnections = GetHydrogenBonds(connections, atomicPositions, atomicNumbers);
        
        List<Cylinder> hbonds = new List<Cylinder>();

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
            cylinder.albedo0 = Vector3.zero;
            cylinder.specular0 = color;
            cylinder.albedo1 = Vector3.zero;
            cylinder.specular1 = color;

            hbonds.Add(cylinder);
        }

        return hbonds;
    }

    public List<int[]> GetHydrogenBonds(
        int[][] connections,
        Vector3[] atomicPositions,
        int[] atomicNumbers
    ) {
        int numAtoms = atomicPositions.Length;
        List<int[]> hydrogenBonds = new List<int[]>();

        for (int i=0; i<connections.Length; i++) {
            int[] connection = connections[i];
            int index0 = connection[0];
            int index1 = connection[1];
            
            int atomicNumber0 = atomicNumbers[index0];
            int atomicNumber1 = atomicNumbers[index1];

            // Determine if this is H and eligible for H bond
            int hIndex;
            int neighbourIndex;
            if (atomicNumber0 == 1) {
                // index0 is H
                if (atomicNumber1 == 7 || atomicNumber1 == 8) {
                    // index1 is N or O
                    hIndex = index0;
                    neighbourIndex = index1;
                } else {
                    continue;
                }
            } else if (atomicNumber1 == 1) {
                // index1 is H
                if (atomicNumber0 == 7 || atomicNumber0 == 8) {
                    // index0 is N or O
                    hIndex = index1;
                    neighbourIndex = index0;
                } else {
                    continue;
                }
            } else {
                // No H
                continue;
            }

            Vector3 pos0 = atomicPositions[hIndex];

            for (int NOIndex=0; NOIndex<numAtoms; NOIndex++) {
                
                int atomicNumberNO = atomicNumbers[NOIndex];

                if ((atomicNumberNO == 7 || atomicNumberNO == 8) && NOIndex != neighbourIndex) {
                    Vector3 pos1 = atomicPositions[NOIndex];

                    float distance = Vector3.Distance(pos0, pos1);
                    if (distance < 2.1f) {
                        hydrogenBonds.Add(new int[2] {hIndex,NOIndex});
                    }
                }

            }
        }

        return hydrogenBonds;
    }

    private void Update()   {

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

        if (Input.GetKey(KeyCode.Equals)) {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                isoLevel += isoLevel * Time.deltaTime;
                if (isoLevel > 0.4f) {
                    isoLevel=0.4f;
                } else {
                    _currentSample = 0;
                }
                SetText($"Iso Level: {isoLevel:#.0000}");
            } else {
                orbitalPower += 2 * Time.deltaTime;
                if (orbitalPower > 50) {
                    orbitalPower=50;
                } else {
                    _currentSample = 0;
                }
                SetText($"Orbital Intensity: {orbitalPower:#.0}");
            }
        }
        if (Input.GetKey(KeyCode.Minus)) {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                isoLevel -= isoLevel * Time.deltaTime;
                if (isoLevel < 0.0001f) {
                    isoLevel=0.0001f;
                } else {
                    _currentSample = 0;
                }
                SetText($"Iso Level: {isoLevel:#.0000}");
            } else {
                orbitalPower -= 2 * Time.deltaTime;
                if (orbitalPower < 0) {
                    orbitalPower=0;
                } else {
                    _currentSample = 0;
                }
                SetText($"Orbital Intensity: {orbitalPower:#.0}");
            }
        }

        if (Input.GetKey(KeyCode.LeftBracket)) {
            lightConeAngle -= 0.5f * Time.deltaTime;
            if (lightConeAngle < 0) {
                lightConeAngle = 0;
            } else {
                _currentSample = 0;
            }
            SetText($"Lighting Angle: {lightConeAngle:#.0}");
        }
        if (Input.GetKey(KeyCode.RightBracket)) {
            lightConeAngle += 0.5f * Time.deltaTime;
            if (lightConeAngle > 2) {
                lightConeAngle = 2;
            } else {
                _currentSample = 0;
            }
            SetText($"Lighting Angle: {lightConeAngle:#.0}");
        }

        if (Input.GetKeyDown(KeyCode.I)) {
            showIso = !showIso;
            _currentSample = 0;
            SetText(showIso?"Showing iso surface":"Showing density");
        }
        if (Input.GetKeyDown(KeyCode.G)) {
            showGround = !showGround;
            _currentSample = 0;
            SetText(showGround?"Showing ground plane":"Hiding ground plane");
        }
        if (Input.GetKeyDown(KeyCode.H)) {
            showHBonds = !showHBonds;
            _currentSample = 0;
            SetText(showHBonds?"Showing hydrogen bonds":"Hiding hydrogen bondss");
        }

        if (Input.GetKey(KeyCode.Escape)){
            Application.Quit();
        }

        transform.Translate(Vector3.forward * Input.mouseScrollDelta.y);
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
}


public struct Sphere {
    public Vector3 position;
    public float radius;
    public Vector3 albedo;
    public Vector3 specular;
};

public struct Cylinder {
    public Vector3 position;
    public Vector3 direction;
    public float radius;
    public float length;
    public Vector3 albedo0;
    public Vector3 specular0;
    public Vector3 albedo1;
    public Vector3 specular1;
};
