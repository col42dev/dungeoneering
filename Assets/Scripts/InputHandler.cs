using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class InputHandler : MonoBehaviour {

	private float mouseSensitivity  = 0.2f;
	private Vector3 camerPanLastMousePosition = new Vector3();
	private MapGenMenu  mapGenMenu = null;
	private MapGen  mapGen = null;
	
	Plane floorPlane;

	int lastXpos = -1;
	int lastZpos = -1;

	// Use this for initialization
	void Start () {
	
		floorPlane = new Plane(Vector3.up, new Vector3(0.0f, 0.0f));
	}
	
	// Update is called once per frame
	void Update () {
	
		if ( mapGen == null)
		{
			GameObject mapgenGameObj = GameObject.Find("MapGen");
			if (mapgenGameObj != null) 
			{
				mapGen = mapgenGameObj.GetComponent<MapGen>();
			}
			return;
		} 

		if ( mapGenMenu == null)
		{
			GameObject mapgenMenuGameObj = GameObject.Find("MapGenMenu");
			if (mapgenMenuGameObj != null) 
			{
				mapGenMenu = mapgenMenuGameObj.GetComponent<MapGenMenu>();
			}
			return;
		} 
		
		
		if (GUI.changed)
		{
			return;
		}

		// camera pan
		if (Input.GetMouseButtonDown(0))
		{
			camerPanLastMousePosition = Input.mousePosition;
		}


		switch ( mapGenMenu.GetViewMenuSelectionIndex())
		{
		case 0:
			if (Input.GetMouseButton(0))
			{
				Vector3 delta  = Input.mousePosition - camerPanLastMousePosition;
				transform.Translate(delta.x * mouseSensitivity, delta.y * mouseSensitivity, 0);
			}
			transform.localEulerAngles  = new Vector3(90, 0, 0);
			break;
		case 1:
			if (Input.GetMouseButton(0))
			{
				Vector3 delta  = Input.mousePosition - camerPanLastMousePosition;
				transform.Translate(delta.x * mouseSensitivity, delta.y * mouseSensitivity, delta.y * mouseSensitivity);
			}
			transform.localEulerAngles  = new Vector3(45, 0, 0);
			break;
		}

		camerPanLastMousePosition = Input.mousePosition;


		//Zoom
		transform.Translate(0.0f, 0.0f, Input.GetAxis("Mouse ScrollWheel") * 12);

	
		Ray ray = Camera.main.ScreenPointToRay (Input.mousePosition);
		float hitDist = 0.0f;


		if ( Input.mousePosition.y < (Screen.height - 100) && floorPlane.Raycast( ray, out hitDist)) 
		{
			Vector3 pointerPosition = ray.GetPoint(hitDist);

			if ( mapGenMenu.GetMainMenuSelection() == EMainMenu.kTools) 
			{
				if ( lastXpos != (int)Math.Floor(pointerPosition.x) || lastZpos !=(int)Math.Floor(pointerPosition.z)) 
				{
					lastXpos = (int)Math.Floor( pointerPosition.x);
					lastZpos = (int)Math.Floor( pointerPosition.z);
				}

				if ( mapGenMenu.GetToolsMenuSelection() == EToolsMenu.kPlaceTile) 
				{
					if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)  ) 
					{
						mapGen.PlaceTile( pointerPosition.x, pointerPosition.z,  mapGenMenu.GetBrushHeightMenuSelectionIndex());
					}
				}

				if ( mapGenMenu.GetToolsMenuSelection() == EToolsMenu.kPlaceDoor) 
				{
					if ( Input.GetMouseButtonDown(0))
					{
						mapGen.PlaceDoor( pointerPosition.x, pointerPosition.z);
					}
				}

				if  ( mapGenMenu.GetToolsMenuSelection() == EToolsMenu.kPlacePath) 
				{
					if ( Input.GetMouseButtonDown(0))
					{
						mapGen.AddTerrainPathPoint( pointerPosition);
					}
					if ( Input.GetMouseButtonUp(0))
					{
						mapGen.AddTerrainPath( pointerPosition, mapGenMenu.GetPathsMenuSelection (), mapGenMenu.GetBrushHeightMenuSelectionIndex());
					}
				}


				if  ( mapGenMenu.GetToolsMenuSelection() == EToolsMenu.kPlaceRoom) 
				{
					if ( Input.GetMouseButtonDown(0))
					{
						mapGen.AddRoomCornerPoint( pointerPosition);
					}
					if ( Input.GetMouseButtonUp(0))
					{
						mapGen.AddRoom( pointerPosition, mapGenMenu.GetBrushHeightMenuSelectionIndex());
					}
				}

				if  ( mapGenMenu.GetToolsMenuSelection() == EToolsMenu.kTrigger) 
				{
					if ( Input.GetMouseButtonDown(0))
					{
						mapGen.placeTrigger( pointerPosition, mapGenMenu.GetTriggersMenuSelectionIndex());
					}
				}
			}
		}
	}





		
}
