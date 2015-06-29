using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public enum EMainMenu {

	kFile = 0,
	kEdit,
	kView,
	kTools,

}

public enum EToolsMenu {
	kPlaceTile = 0,
	kPlacePath,
	kPlaceRoom,
	kPlaceDoor,
	kTrigger,
}

public enum EEditMenu {
	kSelectGrid = 0,
	kSelectBrushHeight,
	kNavigation,
}

public class MapGenMenu : MonoBehaviour {
	
	public const string SubTitle_Tiles = "Tiles";
	public const string SubTitle_Ramps = "Ramps";	
	

	public Dictionary<EMainMenu, string> mainToolbarDictionary = new  Dictionary<EMainMenu, string>();
	public Dictionary<EToolsMenu, string> toolsToolbarDictionary = new  Dictionary<EToolsMenu, string>();
	public Dictionary<EEditMenu, string> editToolbarDictionary = new  Dictionary<EEditMenu, string>();

	private string [] pathsToolbarStrings = new string[] { SubTitle_Tiles, SubTitle_Ramps };



	private int mainToolBarIndex = (int)EMainMenu.kTools;
	private int toolsToolBarIndex = 0;
	private int editToolBarIndex = 0;
	private int viewsToolBarIndex = 0;
	private int gridsToolBarIndex = 0;
	private int pathsToolBarIndex = 0;
	private int brushHeightToolBarIndex = 0;
	private int triggerToolBarIndex = 0;

	private MapGen  mapGen = null;
	public Material material = null;


	public EMainMenu GetMainMenuSelection()
	{
		return (EMainMenu)mainToolBarIndex;
	}

	public EToolsMenu GetToolsMenuSelection()
	{
		return (EToolsMenu)toolsToolBarIndex;
	}

	public EEditMenu GetEditMenuSelection()
	{
		return (EEditMenu)editToolBarIndex;
	}

	public int GetViewMenuSelectionIndex()
	{
		return viewsToolBarIndex;
	}

	public string GetPathsMenuSelection()
	{
		return pathsToolbarStrings[pathsToolBarIndex];
	}

	public int GetBrushHeightMenuSelectionIndex()
	{
		return brushHeightToolBarIndex;
	}

	public int GetTriggersMenuSelectionIndex()
	{
		return triggerToolBarIndex;
	}
	
	// Use this for initialization
	void Start () {
	
		mainToolbarDictionary[EMainMenu.kFile] =  "File"; 
		mainToolbarDictionary[EMainMenu.kEdit] =  "Edit"; 
		mainToolbarDictionary[EMainMenu.kView] =  "View"; 
		mainToolbarDictionary[EMainMenu.kTools] =  "Tools"; 

		toolsToolbarDictionary[EToolsMenu.kPlaceTile] =  "Tiles"; 
		toolsToolbarDictionary[EToolsMenu.kPlacePath] =  "Paths"; 
		toolsToolbarDictionary[EToolsMenu.kPlaceRoom] =  "Room"; 
		toolsToolbarDictionary[EToolsMenu.kPlaceDoor] =  "Doors"; 
		toolsToolbarDictionary[EToolsMenu.kTrigger] =  "Trigger"; 

		editToolbarDictionary[EEditMenu.kSelectGrid] =  "Grid"; 
		editToolbarDictionary[EEditMenu.kSelectBrushHeight] =  "Brush Height"; 
		editToolbarDictionary[EEditMenu.kNavigation] =  "Navigation"; 

		OnSelectBrushHeight ();
	}
	
	// Update is called once per frame
	void Update () {

		if ( mapGen == null)
		{
			GameObject mapgenGameObj = GameObject.Find ("MapGen");
			if (mapgenGameObj != null) 
			{
				mapGen = mapgenGameObj.GetComponent<MapGen>();
			}
		}
	}



	void OnGUI() {

		DrawRectangle (new Rect (0, 0, Screen.width, 100), Color.red);  


		string [] foos = new string[mainToolbarDictionary.Count];
		mainToolbarDictionary.Values.CopyTo(foos, 0);

		mainToolBarIndex = GUI.Toolbar (new Rect (15, 15, 750, 25), mainToolBarIndex, foos);

		switch (mainToolBarIndex) 
		{


		case (int)EMainMenu.kTools:
			if (mapGen != null) {

				string [] editstrings = new string[toolsToolbarDictionary.Count];
				toolsToolbarDictionary.Values.CopyTo(editstrings, 0);

				int toolbarInt = toolsToolBarIndex;
				toolsToolBarIndex = GUI.Toolbar (new Rect (15, 45, 250, 25), toolsToolBarIndex, editstrings);

				switch(toolsToolBarIndex)
				{
					case (int)EToolsMenu.kPlacePath:
						toolbarInt = pathsToolBarIndex;
						pathsToolBarIndex = GUI.Toolbar (new Rect (15, 75, 250, 25), pathsToolBarIndex, pathsToolbarStrings);
						break;

					case (int)EToolsMenu.kTrigger:
						string [] triggertypes  = new string[3] {"Party", "Encounter", "Loot"};
						triggerToolBarIndex = GUI.Toolbar (new Rect (15, 75, 300, 25), triggerToolBarIndex, triggertypes);
						break;
				}
			}
			break;

		case (int)EMainMenu.kEdit:
			if (mapGen != null) {
				
				string [] editstrings = new string[editToolbarDictionary.Count];
				editToolbarDictionary.Values.CopyTo(editstrings, 0);
				
				int toolbarInt = editToolBarIndex;
				editToolBarIndex = GUI.Toolbar (new Rect (15, 45, 350, 25), editToolBarIndex, editstrings);
				
				switch(editToolBarIndex)
				{
				case (int)EEditMenu.kSelectGrid:
					if (mapGen != null) {
						string [] toolbarStrings = new string[] {"irregular", "square", "hybrid"};
						
						toolbarInt = gridsToolBarIndex;
						gridsToolBarIndex = GUI.Toolbar (new Rect (15, 75, 250, 25), gridsToolBarIndex, toolbarStrings);
						
						if (toolbarInt != gridsToolBarIndex) {
							mapGen.OnSelect_TileType (toolbarStrings [gridsToolBarIndex]);
						}
					}
					break;

					
				case (int)EEditMenu.kSelectBrushHeight:
					{
						string [] brushHeightToolbarStrings = new string[] {"Zero", "One", "Two", "Three"};
						brushHeightToolBarIndex = GUI.Toolbar (new Rect (15, 75, 250, 25), brushHeightToolBarIndex, brushHeightToolbarStrings);
						
						OnSelectBrushHeight ();
					}
					break;

				case (int)EEditMenu.kNavigation:
					{
						if (mapGen != null) {
							if (GUI.Button (new Rect (15, 75, 60, 25), "Rescan")) {
								mapGen.RescanNavGraph ();
							}
						}
					}
					break;
				}
			}
			break;

		case (int)EMainMenu.kView:
			if (mapGen != null) {
				string [] toolbarStrings = new string[] {"plan", "3D"};
				
				int toolbarInt = viewsToolBarIndex;
				viewsToolBarIndex = GUI.Toolbar (new Rect (15, 45, 250, 25), viewsToolBarIndex, toolbarStrings);
			}
			break;


		case (int)EMainMenu.kFile:
			{
				if (mapGen != null) {
					if (GUI.Button (new Rect (15, 45, 50, 25), "Save")) {
						mapGen.OnSelect_Save ();
					}

					if (GUI.Button (new Rect (75, 45, 50, 25), "Load")) {	
						mapGen.OnSelect_Load ();
					}
				}
			}
			break;
		}


		if (GUI.changed) 
		{
			if (mapGen != null) 
			{
				mapGen.ClearPathPlacement();
			}
		}
	}

	private void OnSelectBrushHeight () {
		editToolbarDictionary [ EEditMenu.kSelectBrushHeight] = "Brush Height: " + brushHeightToolBarIndex;
	}


	void DrawRectangle (Rect position, Color color)
	{    
		// We shouldn't draw until we are told to do so.
		if (Event.current.type != EventType.Repaint)
			return;
		
		// Please assign a material that is using position and color.
		if (material == null) {
			material = Resources.Load("Materials/grey", typeof(Material)) as Material; 
		}
		
		material.SetPass (0);
		
		// Optimization hint: 
		// Consider Graphics.DrawMeshNow
		GL.Color (color);
		GL.Begin (GL.QUADS);
		GL.Vertex3 (position.x, position.y, 0);
		GL.Vertex3 (position.x + position.width, position.y, 0);
		GL.Vertex3 (position.x + position.width, position.y + position.height, 0);
		GL.Vertex3 (position.x, position.y + position.height, 0);
		GL.End ();
	}


}
