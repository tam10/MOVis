using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;

public static class Data {

    public static int GetAtomicNumber(string element) => elementToNumber[element];
    public static Vector3 GetColour(int atomicNumber) => numberToAlbedo[atomicNumber];
    public static Vector3 GetColour(string element) => numberToAlbedo[GetAtomicNumber(element)];
    public static float GetRadius(int atomicNumber) => numberToRadius[atomicNumber];
    public static float GetRadius(string element) => numberToRadius[GetAtomicNumber(element)];
    public static float GetMass(int atomicNumber) => numberToMass[atomicNumber];
    public static float GetMass(string element) => numberToMass[GetAtomicNumber(element)];
    
    static Dictionary<string, int> elementToNumber = new Dictionary<string, int> {
        {"X", 0},
        {"H", 1},
        {"He", 2},
        {"Li", 3},
        {"Be", 4},
        {"B", 5},
        {"C", 6},
        {"N", 7},
        {"O", 8},
        {"F", 9},
        {"Ne", 10},
        {"Na", 11},
        {"Mg", 12},
        {"Al", 13},
        {"Si",14},
        {"P", 15},
        {"S", 16},
        {"Cl", 17},
        {"Cd", 48}
    };
    static Dictionary<int, Vector3> numberToAlbedo = new Dictionary<int, Vector3> {
        {0, new Vector3(1,0,1)},
        {1, new Vector3(0.9f,0.9f,0.9f)},
        {2, new Vector3(0,0,0)},
        {3, new Vector3(0,0,0)},
        {4, new Vector3(0,0,0)},
        {5, new Vector3(0,0,0)},
        {6, new Vector3(0.5f,0.5f,0.5f)},
        {7, new Vector3(0,0,1)},
        {8, new Vector3(1,0,0)},
        {9, new Vector3(0,0,0)},
        {10, new Vector3(0,0,0)},
        {11, new Vector3(1,0,0.75f)},
        {12, new Vector3(0,0,0)},
        {13, new Vector3(0,0,0)},
        {14, new Vector3(0,0,0)},
        {15, new Vector3(0,0,0)},
        {16, new Vector3(1,1,0)},
        {17, new Vector3(0,0,0)},
        {48, new Vector3(0.75f,0.5f,0)}
    };
    static Dictionary<int, float> numberToRadius = new Dictionary<int, float> {
        {0, 1f},
        {1, 1.2f},
        {2, 1f},
        {3, 1f},
        {4, 1f},
        {5, 1f},
        {6, 1.7f},
        {7, 1.55f},
        {8, 1.52f},
        {9, 1f},
        {10, 1f},
        {11, 1.6f},
        {12, 1f},
        {13, 1f},
        {14, 1f},
        {15, 1f},
        {16, 1.8f},
        {17, 1f},
        {48, 1.51f}
    };
    static Dictionary<int, float> numberToMass = new Dictionary<int, float> {
        {0, 1f},
        {1, 1f},
        {2, 1f},
        {3, 1f},
        {4, 1f},
        {5, 1f},
        {6, 12f},
        {7, 14f},
        {8, 16f},
        {9, 1f},
        {10, 1f},
        {11, 23f},
        {12, 1f},
        {13, 1f},
        {14, 1f},
        {15, 1f},
        {16, 32f},
        {17, 1f},
        {48, 112f}
    };

    // public void ReadDataFile(string path) {

    //     //elementToAlbedo = new Dictionary<string, Vector3>();
    //     //elementToRadius = new Dictionary<string, float>();
    //     //elementToMass = new Dictionary<string, float>();
    //     numberToAlbedo = new Dictionary<int, Vector3>();
    //     numberToRadius = new Dictionary<int, float>();
    //     numberToMass = new Dictionary<int, float>();

    //     XDocument xDocument = XDocument.Load (path, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
    //     XElement elementsX = xDocument.Element("atoms").Element("elements");

    //     int atomicIndex = 0;

    //     foreach (XElement elementX in elementsX.Elements("element")) {
    //         string element = elementX.Attribute("ID").Value;
    //         try {
    //             float r = float.Parse(elementX.Element("red").Value);
    //             float g = float.Parse(elementX.Element("green").Value);
    //             float b = float.Parse(elementX.Element("blue").Value);
    //             float radius = float.Parse(elementX.Element("radius").Value);
    //             float mass = float.Parse(elementX.Element("mass").Value);
    //             //elementToAlbedo[element] = new Vector3(r,g,b);
    //             //elementToRadius[element] = radius;
    //             //elementToMass[element] = mass;
    //             numberToAlbedo[atomicIndex] = new Vector3(r,g,b);
    //             numberToRadius[atomicIndex] = radius;
    //             numberToMass[atomicIndex] = mass;

    //         } catch {

    //         }
    //         atomicIndex += 1;
    //     }
    // }
}