using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class CubeReader : MonoBehaviour {

    public float bohrToAngstrom = 0.529177f;
    
    public Vector3 gridOffset;
    public Vector3 gridScale;
    public int[] dimensions;
    public int gridLength;
    int gridIndex;
    public float[] grid;

    public int numAtoms;
    public Vector3[] atomicPositions;
    public int[] atomicNumbers;

    public float minVal;
    public float maxVal;

	public delegate void LineParser();
	public LineParser activeParser = () => {};

    int atomIndex;
    int skipLines;

    bool additionalRecord;

    string line;

    public void Parse(string path) {
        
		atomIndex = 0;
        skipLines = 2;
        activeParser = ParseOffset;
        
        minVal=0;
        maxVal=0;

        dimensions = new int[3];

		if (!File.Exists (path))
			throw new FileNotFoundException ("File " + path + " does not exist.");

		using (FileStream fileStream = File.OpenRead(path)) {
			using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8, true)) {

				while ((line = streamReader.ReadLine()) != null) {

                    if (skipLines == 0) {
                        activeParser();
                    } else if (skipLines > 0) {
                        // Skip linesToSkip lines
                        skipLines--;
                    } else {
                        throw new System.Exception("'linesToSkip' must not be negative in Gaussian Output Reader!");
                    }
				}
			}
		}
    }

    void ParseOffset() {
        string[] offset_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        numAtoms = int.Parse(offset_spec[0]);

        if (numAtoms < 0) {
            numAtoms = -numAtoms;
            additionalRecord = true;
        } else {
            additionalRecord = false;
        }

        atomicPositions = new Vector3[numAtoms];
        atomicNumbers = new int[numAtoms];

        gridOffset[0] = float.Parse(offset_spec[1])*bohrToAngstrom;
        gridOffset[1] = float.Parse(offset_spec[2])*bohrToAngstrom;
        gridOffset[2] = float.Parse(offset_spec[3])*bohrToAngstrom;
        activeParser = ParseDimX;
    }

    void ParseDimX() {
        string[] x_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        dimensions[0] = int.Parse(x_spec[0]);
        gridScale [0] = float.Parse (x_spec [1])*bohrToAngstrom;
        activeParser = ParseDimY;
    }

    void ParseDimY() {
        string[] y_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        dimensions[1] = int.Parse(y_spec[0]);
        gridScale [1] = float.Parse (y_spec [2])*bohrToAngstrom;
        activeParser = ParseDimZ;
    }

    void ParseDimZ() {
        string[] z_spec = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        dimensions[2] = int.Parse(z_spec[0]);
        gridScale [2] = float.Parse (z_spec [3])*bohrToAngstrom;

        atomIndex = 0;
        activeParser = ParseAtoms;
    }

    void ParseAtoms() {

        //AtomID atomID = geometry.atomMap[atomIndex];


        string[] splitLine = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        
        atomicNumbers[atomIndex] = int.Parse(splitLine[0]);

        //Position
        atomicPositions[atomIndex] = new Vector3(
            float.Parse(splitLine[2])*bohrToAngstrom,
            float.Parse(splitLine[3])*bohrToAngstrom,
            float.Parse(splitLine[4])*bohrToAngstrom
        );
        
        atomIndex++;
        if (atomIndex == numAtoms) {
            skipLines = additionalRecord ? 1 : 0;
            gridLength = dimensions[0] * dimensions[1] * dimensions[2];
            grid = new float[gridLength*2];
            gridIndex = 0;
            activeParser = ParseGrid;

            x = 0;
            y = 0;
            z = 0;

        }
        
    }

    void ParseGridFlat() {

        string[] vs = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        if (vs [0].Contains (".")) {
            for (int i = 0; i < vs.Length; i++) {
                float value = float.Parse (vs [i]);
                if (value < minVal) {minVal=value;}
                if (value > maxVal) {maxVal=value;}

                if (value >= 0) {
                    grid [gridIndex++] = value;
                    grid [gridIndex++] = 0;
                } else {
                    grid [gridIndex++] = 0;
                    grid [gridIndex++] = -value;
                }
            }
        }
    }

    int x;
    int y;
    int z;


    void ParseGrid() {

        string[] vs = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);
        if (vs [0].Contains (".")) {
            for (int i = 0; i < vs.Length; i++) {
                float value = float.Parse (vs [i]);
                if (value < minVal) {minVal=value;}
                if (value > maxVal) {maxVal=value;}

                //int index = dimensions[2] * (x * dimensions[1] + y) + z;
                int index = 2 * (dimensions[0] * (z * dimensions[1] + y) + x);

                if (value >= 0) {
                    grid [index] = value;
                    grid [index+1] = 0;
                } else {
                    grid [index] = 0;
                    grid [index+1] = -value;
                }
                
                z++;
                if (z >= dimensions[2]) {
                    z = 0;
                    y++;
                    if (y >= dimensions[1]) {
                        y = 0;
                        x++;
                    }
                }

                gridIndex += 2;

            }
        }
    }

    public List<int[]> GetConnectivity() {
        
        List<int[]> connections = new List<int[]>();

        for (int i=0; i<numAtoms; i++) {
            Vector3 pos0 = atomicPositions[i];
            float radius0 = Data.GetRadius(atomicNumbers[i])*0.5f;
            for (int j=0; j<i; j++) {
                Vector3 pos1 = atomicPositions[j];
                float radius1 = Data.GetRadius(atomicNumbers[j])*0.5f;

                float distance = Vector3.Distance(pos0, pos1);
                if (distance < radius0 + radius1) {
                    connections.Add(new int[2] {i,j});
                }
            }
        }

        return connections;
    }

    public void CleanUp() {

        Debug.LogFormat(
            "Dimensions: {1} {2} {3} ({0})",
            gridIndex,
            dimensions[0],
            dimensions[1],
            dimensions[2]
        );

    }
}
