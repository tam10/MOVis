using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.IO;
using System.Text;

public class ComReader : MonoBehaviour {

	List<string> keywordsList;
	List<string> titleLines;
	//private string commentString = "!";
	
	public delegate void LineParser();
	public LineParser activeParser = () => {};

    public int atomIndex;
    int skipLines;

	public List<string> layers;
	public List<string> elements;
	public List<Vector3> positions;
	public List<int[]> connections;

    string line;
	public bool failed;

	public void Parse(string path) {

		keywordsList = new List<string>();

		elements = new List<string>();
		layers = new List<string>();
		positions = new List<Vector3>();
		connections = new List<int[]>();
		
		failed = false;

		ExpectLink0();

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

	void Pass() {}

	void ExpectLink0() {
		activeParser = ParseLink0;
	}

	void ExpectKeywords() {
		activeParser = ParseKeywords;
	}

	void ExpectTitle() {
		titleLines = new List<string>();
		activeParser = ParseTitle;
	}

	void ExpectChargeMultiplicity() {
		titleLines = new List<string>();
		activeParser = ParseChargeMultiplicity;
	}

	void ExpectAtoms() {
		atomIndex = 0;
		activeParser = ParseAtoms;
	}

	void ExpectConnectivity() {
		//skipLines = 1;
		activeParser = ParseConnectivity;
	}

	void ExpectParameters() {
		skipLines = 2;
		activeParser = ParseParameters;
	}

	void ParseLink0() {
		string line = this.line.Trim();
		if (line.StartsWith ("#")) {
			ExpectKeywords();
			ParseKeywords();
			return;
		}
	}

	void ParseKeywords() {

		string line = this.line.Trim();
		if (line == "" && keywordsList.Count > 0) {
			ExpectTitle();
			return;
		} 

		string[] stringArray = line.Split (new []{ ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
		foreach (string keywordItem in stringArray) {
			keywordsList.Add (keywordItem.ToLower ());
		}
		
	}

	void ParseTitle() {
		
		string line = this.line.Trim();
		if (line == "" && titleLines.Count > 0) {
			ExpectChargeMultiplicity();
			return;
		} 
		titleLines.Add(line);
	}

	void ParseChargeMultiplicity() {
		string line = this.line.Trim();
		if (line == "") {
			throw new System.Exception("Missing Charge/Multiplicity Section!");
		}
		ExpectAtoms();
		
	}

	void ParseAtoms() {

		string line = this.line.Trim();

		if (line == "") {
			ExpectConnectivity();
			return;
		}

		string[] stringArray = line.Split (new []{ ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

		string element = stringArray[0].Split(new [] { '-' }).First().Split(new [] { '(' }).First();
		int offset = (stringArray[1].Length == 1) ? 1 : 0;
		float x = float.Parse(stringArray[1 + offset]);
		float y = float.Parse(stringArray[2 + offset]);
		float z = float.Parse(stringArray[3 + offset]);

		string layer = "H";
		if (stringArray.Length >= 5 + offset) {
			switch (stringArray[4 + offset]) {
				case "L": layer = "L"; break;
				case "M": layer = "L"; break;
				case "H": layer = "H"; break;
			}
		}

		elements.Add(element);
		Vector3 position = new Vector3(x,y,z);
		positions.Add(position);
		layers.Add(layer);
		atomIndex++;

	}

	void ParseConnectivity() {

		string line = this.line.Trim();
		if (line == "") {
			ExpectParameters();
			return;
		}

		string[] splitConn = line.Split (new []{ " " }, System.StringSplitOptions.RemoveEmptyEntries);

		int connectionIndex0;
		if (! int.TryParse(splitConn[0], out connectionIndex0)) {
			failed = true;
			return;
		}
		connectionIndex0 -= 1;

		int numConnections = (splitConn.Length - 1) / 2;

		for (int i = 0; i < numConnections; i++) {
			
			int connectionIndex1;
			if (! int.TryParse(splitConn[i*2+1], out connectionIndex1)) {
				failed = true;
				return;
			}
			connectionIndex1 -= 1;

			float bondFloat;
			if (! float.TryParse(splitConn [i * 2 + 2], out bondFloat)) {
				failed = true;
				return;
			}

			connections.Add(new int[2] {connectionIndex0, connectionIndex1});
		}
	}

	void ParseParameters() {
		string line = this.line.Trim();
		if (line == "") {
			activeParser = Pass;
		}

	}

}