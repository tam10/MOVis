using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System;
using TMPro;
using System.IO;
using UnityEngine.SceneManagement;

public class FileSelector : MonoBehaviour {

	public GameObject listItemPrefab;

	public Scrollbar verticalScrollbar;
	public RectTransform contentHolder;

	[Header("Enabled File Color Block")]
	public ColorBlock enabledFileColorBlock;

	[Header("Disabled File Color Block")]
	public ColorBlock disabledFileColorBlock;

	[Header("Directory Color Block")]
	public ColorBlock directoryColorBlock;


	public string fullPath;
	public TextMeshProUGUI titleText;
	public TMP_InputField fileNameInput;

	public string confirmedText;

	private string[] filetypes;

	public bool userResponded;
	public bool cancelled;

	string home;
	string currentDirectory;

	public void Awake() {

		PlatformID platformID = Environment.OSVersion.Platform;
		bool isWindows = (platformID != PlatformID.Unix && platformID != PlatformID.MacOSX);
    	home = isWindows 
			? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) 
			: Environment.GetEnvironmentVariable("HOME");
		currentDirectory = home;

		fullPath = home;

		userResponded = false;
		cancelled = false;
		
		Initialise("Select .cub/com/gjf file:", new List<string> {"cub", "com", "gjf"});

		Populate();
	}

	public void Initialise(string promptText, List<string> fileTypes=null) {


		SetFileTypes(fileTypes ?? new List<string>());
		SetPromptText(promptText);

		Populate();
	}

	void Populate() {

		Clear();

		DirectoryInfo directory = new DirectoryInfo(currentDirectory);

		string[] allFiles = directory
			.GetFiles()
			.Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
			.Select(f => f.Name)
			.ToArray();
		string[] allDirectories = directory
			.GetDirectories()
			.Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
			.Select(f => f.Name)
			.ToArray();
		AddItem("..", false, true);

		for (int i = 0; i < allDirectories.Length; i++) {
			
			string directoryName = allDirectories[i];
			AddItem(
				Path.GetFileName(directoryName) + Path.DirectorySeparatorChar, 
				false, 
				true
			);
		}

		for (int i = 0; i < allFiles.Length; i++) {
			
			string filename = allFiles[i];
			AddItem(
				Path.GetFileName(filename), 
				true, 
				CheckExtension(filename)
			);
		}

		verticalScrollbar.value = 1f;
	}

	public void AddItem(string textValue, bool isFile, bool isEnabled, string visibleText="") {
		
		GameObject item = GameObject.Instantiate<GameObject>(listItemPrefab, contentHolder, false);
		RectTransform itemRect = item.GetComponent<RectTransform>();


		item.name = textValue;
		TextMeshProUGUI text = item.GetComponentInChildren<TextMeshProUGUI>();
		text.text = string.IsNullOrWhiteSpace(visibleText) ? textValue : visibleText;

		Button button = item.GetComponent<Button>();
		if (isEnabled) {
			button.interactable = true;
			
			if (isFile) {
				button.onClick.AddListener(delegate {SelectFile(textValue);});
				button.colors = enabledFileColorBlock;

			} else {
				button.onClick.AddListener(delegate {ChangeDirectory(textValue);});
				button.colors = directoryColorBlock;
			}
			
		} else {
			button.interactable = false;
			button.colors = disabledFileColorBlock;
		}
	}

	public void Clear() {
		foreach (Transform child in contentHolder) {
			GameObject.Destroy(child.gameObject);
		}
	}

	public void ChangeDirectory(string newDirectory) {
		currentDirectory = Path.GetFullPath(Path.Combine(currentDirectory, newDirectory));
		fullPath = currentDirectory;
		fileNameInput.text = string.Empty;
		Populate();
	}

	public void SelectFile(string filename) {
		fullPath = Path.GetFullPath(Path.Combine(currentDirectory, filename));
		if (CheckFile(filename)) {

		}
		fileNameInput.text = filename;
	}

	bool CheckExtension(string filename) {
		if (filetypes.Length == 0) {return true;}
		string extension = Path.GetExtension(filename).TrimStart('.');
		return filetypes.Any(x => x == extension);
	}

	public void InputTextChanged() {
		fullPath = Path.GetFullPath(Path.Combine(currentDirectory, fileNameInput.text));
	}

	public void SetFileTypes(List<string> filetypes) {
		this.filetypes = new string[filetypes.Count];
		for (int i = 0; i < filetypes.Count; i++) {
			this.filetypes[i] = filetypes[i].TrimStart('.');
		}
	}

	public void SetPromptText(string text) {
		titleText.SetText(text);
	}

	public void Cancel() {
		confirmedText = "";
		userResponded = true;
		cancelled = true;
	}
	
	public void Confirm() {

		if (CheckFile(fullPath)) {
			userResponded = true;
			cancelled = false;

			RayTracingMaster.cubeFile = fullPath;
			SceneManager.LoadScene("Visualiser");
		}
	}

	bool CheckFile(string text) {

		confirmedText = "";

		if (text == "") {
			return false;
		}

		if (! File.Exists(text)) {
			return false;
		}

		if (filetypes.Length > 0) {
			if (! CheckExtension(text)) {
				return false;
			}
		}

		confirmedText = text;
		return true;
	}

}
