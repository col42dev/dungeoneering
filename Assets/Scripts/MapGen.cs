using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Delaunay;
using Assets.Map;
using System.Reflection;


// 
public class GameTrigger
{
	public Vector3 point;
	public int type; //0:party, 1: encounter, 2: loot

	public bool ShouldSerializegfx() { return false; }
	public GameObject gfx ;

}

public class MapGen : MonoBehaviour {

	//Prefabs
	public GameObject UIPathPrefab;
	public GameObject AssetLibraryPrefab;	


	//AssetLib
	private GameObject assetLibrary;


	//collision
	const bool kbRenderCollisionMesh = false;
	
	//Paths
	private List<Vector3> pathPositions = new List<Vector3>();
	private GameObject pathRenderGFX; 

	//Room
	private List<Vector3> roomPositions = new List<Vector3>();
	private GameObject [] roomRenderGFX = new GameObject[4];

	// Voronoi
	List<Vector2> points = new List<Vector2>();
	List<uint> colors = new List<uint>();
	private int _pointCount = 500;
	public const float Width = 50;
	public const float Height = 50;
	const int NUM_LLOYD_RELAXATIONS = 2;
	Voronoi voronoi = null;
	public Graph Graph { get; private set; }

	//nav
	bool bRescanNavGraph = false;	


	// Trigger
	List<GameTrigger> gameTriggers = new List<GameTrigger>();

	// Use this for initialization
	void Start () 
	{
		assetLibrary = Object.Instantiate (AssetLibraryPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
		MakeNewMap ("irregular");
	}

	// Update is called once per frame
	void Update() 
	{
		DebugVoronoiGFX ();
		
		if (bRescanNavGraph) 
		{
			bRescanNavGraph = false;
			AstarPath.active.Scan();
		}
		
		PathGFX();

		RoomGFX();
	}
	

	public void OnSelect_TileType( string tiletype )
	{
		foreach(Transform child in this.transform)
		{
			Destroy(child.gameObject);
		}
		
		MakeNewMap (tiletype);
	}
	
	public void OnSelect_RelaxTileSpacing(  )
	{
		foreach(Transform child in this.transform)
		{
			Destroy(child.gameObject);
		}
		
		List<Assets.Map.Center>  oldcenters = Graph.centers;
		
		RelaxPoints(2);
		
		BuildVoronoi();
		
		Graph = new Graph( points, voronoi, (int)Width, (int)Height );
		
		for (int centerIdx = 0; centerIdx < Graph.centers.Count; centerIdx ++)
		{
			Graph.centers[centerIdx].elevation = oldcenters[centerIdx].elevation;
		}

		BuildVoronoiGFX ();
	}

	public void RescanNavGraph(  )
	{
		bRescanNavGraph = true;	
	}


	public void OnSelect_Save(  )
	{
		Debug.Log ("OnSelect_Save");

		SaveGameTriggers ();


		//Collision
		List<GameObject> combinedGameObjectList = new List<GameObject>(); 

		for (int layer = 8; layer <= 10; layer ++) {

			GameObject obj = null;

			switch(layer)
			{
				case 8: // ground
					obj = Object.Instantiate (assetLibrary.GetComponent<AssetLibrary>().collisionPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
					break;
				case 9: // obstacles
					obj = Object.Instantiate (assetLibrary.GetComponent<AssetLibrary>().collisionPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
					break;
				case 10: // Use ground tile prefab for now
					obj = Object.Instantiate (assetLibrary.GetComponent<AssetLibrary>().VizPrefabGroundTile, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
					break;
			}
		
			obj.layer = layer; 

			//Assign a default material since wavefront .obj format requires one.
			obj.AddComponent<MeshRenderer>();

			if ( layer != 10)
			{
				obj.GetComponent<MeshRenderer>().enabled = false; 
				Material mat = Resources.Load ("Materials/obstacles", typeof(Material)) as Material; 
				Material [] mats = new Material[1];
				mats [0] = mat;
				obj.GetComponent<Renderer> ().materials = mats;
			}


			Transform[] transforms = gameObject.GetComponentsInChildren<Transform>();
			List<CombineInstance> combinedInstanceList = new List<CombineInstance>(); 


			int t = 0;
			while (t < transforms.Length) 
			{
				MeshFilter mf = transforms[t].gameObject.GetComponent<MeshFilter>();

				if (mf != null)
				{
					if ( mf.gameObject.layer == layer)
					{
						CombineInstance thisCombine = new CombineInstance();
						thisCombine.mesh 		= mf.sharedMesh;
						thisCombine.transform 	= mf.transform.localToWorldMatrix;

						combinedInstanceList.Add(thisCombine);

						// assign materials
						if ( layer == 10)
						{
							obj.transform.GetComponent<Renderer> ().materials = mf.gameObject.transform.GetComponent<Renderer>().materials;
						}
					}
				}

				t ++;
			}


			obj.GetComponent<MeshFilter> ().mesh = new Mesh ();
			obj.GetComponent<MeshFilter> ().mesh.CombineMeshes( combinedInstanceList.ToArray(), true, true);
			obj.GetComponent<MeshFilter> ().mesh.name = "levelMesh" + layer;

			if ( layer == 10)
			{
				//obj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 1, 2};
			}

			obj.GetComponent<MeshFilter> ().mesh.RecalculateBounds ();
			obj.GetComponent<MeshFilter> ().mesh.RecalculateNormals ();


			// Add MeshCollider
			switch(layer)
			{
				case 8: // ground
				case 9: // obstacle
					obj.AddComponent<MeshCollider> ();
					obj.GetComponent<MeshCollider> ().sharedMesh = obj.GetComponent<MeshFilter> ().mesh;
					obj.GetComponent<MeshCollider> ().sharedMesh.RecalculateBounds();
					obj.GetComponent<MeshCollider> ().sharedMesh.RecalculateNormals();
					break;
			}

			combinedGameObjectList.Add(obj);
		}

		ObjExporter.DoExport (combinedGameObjectList[0], true, "ground");
		ObjExporter.DoExport (combinedGameObjectList[1], true, "obstacles");
		ObjExporter.DoExport (combinedGameObjectList[2], true, "viz");
	
		Object.Destroy (combinedGameObjectList [0]);
		Object.Destroy (combinedGameObjectList [1]);
		Object.Destroy (combinedGameObjectList [2]);

		RescanNavGraph();
	}



	public void OnSelect_Load(  )
	{
		Debug.Log ("OnSelect_Load"+ Time.realtimeSinceStartup);

		LoadGameTriggers (); // 


		GameObject objectManager = GameObject.Find ("ObjectManager");
		
		ObjReader objReader = objectManager.GetComponent<ObjReader> ();

		for (int layer = 8; layer <= 10; layer ++) 
		{
			Debug.Log ("OnSelect_Load layer: "+ layer +  " " + + Time.realtimeSinceStartup);


			string objFileName = "";



			switch (layer)
			{
				case 8:
					objFileName = Application.dataPath + "/../Exports/MapGen.ground.obj";
					break;
				case 9:
					objFileName = Application.dataPath + "/../Exports/MapGen.obstacles.obj";
					break;
				case 10:
					objFileName = Application.dataPath + "/../Exports/MapGen.viz.obj";
					break;
			}

			
			GameObject[] objs = objReader.ConvertFile(objFileName, false, null, null);

			foreach (GameObject obj in objs) 
			{
				obj.layer = layer;
			}

		

			// Debug render collsion mesh
			switch (layer)
			{
				case 8:
				case 9:
					if ( kbRenderCollisionMesh == false)
					{
						foreach (GameObject obj in objs) 
						{
							obj.GetComponent<MeshRenderer>().enabled = false;;
						}
					}
					break;
			}
	

			// Update MeshCollider
			switch (layer)
			{
			case 8:
			case 9:
				foreach (GameObject obj in objs) 
				{
					obj.AddComponent<MeshCollider>();
					obj.GetComponent<MeshCollider>().sharedMesh = obj.GetComponent<MeshFilter> ().mesh;
					obj.GetComponent<MeshCollider>().sharedMesh.RecalculateBounds ();
					obj.GetComponent<MeshCollider>().sharedMesh.RecalculateNormals ();
				}
				break;
			}

	
			// Assign materials
			switch (layer)
			{

			case 10:
				foreach (GameObject obj in objs) 
				{
					obj.GetComponent<MeshRenderer>().enabled = true; 
					Material mat = Resources.Load ("Materials/DungeonMat1", typeof(Material)) as Material; 
					Material [] mats = new Material[1];
					mats [0] = mat;
					obj.GetComponent<Renderer> ().materials = mats;
				}
				break;
			}
			//obj.transform.SetParent (this.transform);
		}


		Debug.Log ("OnSelect_Load done: "+ + Time.realtimeSinceStartup);

		RescanNavGraph();
	}

	public void placeTrigger( Vector3 triggerPoint, int triggerTypeIndex )
	{
		GameTrigger lTrigger = new GameTrigger ();


		lTrigger.point = triggerPoint;
		lTrigger.type = triggerTypeIndex;

		GameObject go = GameObject.CreatePrimitive (PrimitiveType.Sphere);
		go.transform.position = triggerPoint;
		go.transform.localScale = new Vector3 (1.0f, 1.0f, 1.0f);

		switch(lTrigger.type)
		{
		case 0:
			go.GetComponent<Renderer>().material.color = new Color(0.5f,1,1); 
			break;
		case 1:
			go.GetComponent<Renderer>().material.color = new Color(1,0.5f,1); 
			break;
		case 2:
			go.GetComponent<Renderer>().material.color = new Color(1,1,0.5f); 
			break;
		}

			
		lTrigger.gfx = go;

		gameTriggers.Add (lTrigger);
	}

	public void SaveGameTriggers()
	{
		
		string json = JsonConvert.SerializeObject( gameTriggers.ToArray(), Formatting.Indented, new JsonSerializerSettings { 
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore
		});
		
		Debug.Log ("writing file");
		var sr = File.CreateText("Exports/MapGen.triggers.json");
		sr.WriteLine (json);
		sr.Close();
	}

	public void LoadGameTriggers()
	{

		//gameTriggers = new List<GameTrigger>();
		gameTriggers.Clear ();

		
		Debug.Log ("reading file");
		string json = File.ReadAllText("Exports/MapGen.triggers.json");
		
		GameTrigger [] gameTriggersArray = JsonConvert.DeserializeObject< GameTrigger [] >( json);

		for (int i = 0; i < gameTriggersArray.Length; i ++) {
			Debug.Log( gameTriggersArray[i].point.x + ", " + gameTriggersArray[i].point.y);

			placeTrigger( gameTriggersArray[i].point, gameTriggersArray[i].type);
		}
	}
	
	private void MakeNewMap(string type)
	{
		BuildPoints (type);

		BuildVoronoi ();
		
		Graph = new Graph( points, voronoi, (int)Width, (int)Height );


		foreach ( Assets.Map.Center selectedCenter in Graph.centers)
		{
			selectedCenter.elevation = 1;
		}
		
		BuildVoronoiGFX ();
	}

	private void BuildPoints(string type)
	{
		points = new List<Vector2>();
		colors = new List<uint>();

		if (type == "irregular") 
		{
			for (int i = 0; i < _pointCount; i++)
			{
				colors.Add(0);
				points.Add( new Vector2( UnityEngine.Random.Range(0, Width), UnityEngine.Random.Range(0, Height)) );
			}
			RelaxPoints (NUM_LLOYD_RELAXATIONS);

		}
		else if (type == "hybrid") 
		{
		
			for (int i = 0; i < 250; i++)
			{
				colors.Add(0);

				Vector2 vPos = (Random.insideUnitCircle * 20) + new Vector2(Width/2, Height/2);

				points.Add( vPos);
			}



			for (int i = 0; i < 2; i++)
			{
				points = Graph.RelaxPoints(points, Width, Height).ToList();
			}

			// remove outliers
			bool thisRemoved = false;
			do {
				thisRemoved = false;
				foreach ( Vector2 point in points)
				{
					if ( Vector2.Distance( point,  new Vector2(Width/2, Height/2)) > 20 )
					{
						points.Remove(point);
						thisRemoved = true;
						break;
					}
				}
			} while (thisRemoved == true);

			// add outlies in grid format
			for (int i = 0; i < Width; i+=2)
			{
				for (int j = 0; j < Height; j+=2) 
				{
					if ( Vector2.Distance( new Vector2(i, j),  new Vector2(Width/2, Height/2)) > 20)
					{
						colors.Add(0);
						points.Add( new Vector2 (i, j));
					}
				}
			}



		
		}
		else if (type == "square") {
			for (float i = 0; i < Width; i+=2.0f) {
				for (float j = 0; j < Height; j+=2.0f) {
					colors.Add (0);
					points.Add (new Vector2 (i, j));
				}
			}
		}
	}

	private void RelaxPoints( int times)
	{
		for (int i = 0; i < times; i++)
			points = Graph.RelaxPoints(points, Width, Height).ToList();
	}

	private void BuildVoronoi()
	{
		voronoi = new Voronoi(points, colors, new Rect(0, 0, Width, Height));
	}
	
	private void DebugVoronoiGFX()
	{
		foreach (var c in Graph.centers) 
		{
			Vector3 [] corners3d = c.corners.Select (p => new Vector3 (p.point.x , 0.0f, p.point.y)).ToArray ();

			for ( int crnIndex = 0; crnIndex < corners3d.Length; crnIndex ++)
			{
				int startCrnIndex = crnIndex;
				int endCrnIndex = (crnIndex + 1) % corners3d.Length;
				
				Debug.DrawLine( corners3d[startCrnIndex], corners3d[endCrnIndex],  Color.red, 0.0f, true);
			}
		}
	}

	private void PathGFX()
	{

		if ( pathPositions.Count == 1 )
		{
			GameObject targetGameObj = GameObject.Find ("Target");
			if (targetGameObj != null) 
			{
				LineRenderer lr = pathRenderGFX.GetComponent<LineRenderer>();

				lr.SetWidth(0.25f, 0.25f);

				Vector3 startPosition = pathPositions[0];
				Assets.Map.Center startTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (startPosition.x, startPosition.z));
				if (startTileCenter!=null)
				{
					startPosition.y = startTileCenter.elevation +0.5f;

					Vector3 endPosition = targetGameObj.transform.position;
					Assets.Map.Center endTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (endPosition.x, endPosition.z));
					if (endTileCenter!=null)
					{
						endPosition.y = endTileCenter.elevation + 0.5f;

						lr.SetPosition(0, startPosition);
						lr.SetPosition(1, endPosition);
					}
				}
			}
		}
	}


	private void RoomGFX()
	{
		
		if ( roomPositions.Count == 1 )
		{
			GameObject targetGameObj = GameObject.Find ("Target");
			if (targetGameObj != null) 
			{
				for (int side = 0; side < 4; side ++)
				{
					LineRenderer lr = roomRenderGFX[side].GetComponent<LineRenderer>();
					
					lr.SetWidth(0.25f, 0.25f);
					
					Vector3 startPosition  = new Vector3(0, 0, 0);

					switch( side)
					{
					case 0:
						startPosition = roomPositions[0];
						break;
					case 1:
						startPosition = targetGameObj.transform.position;
						break;
					case 2:
						startPosition = roomPositions[0];
						startPosition.z = targetGameObj.transform.position.z;

						break;
					case 3:
						startPosition = roomPositions[0];
						startPosition.x = targetGameObj.transform.position.x;
						break;
					}
					Assets.Map.Center startTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (startPosition.x, startPosition.z));
					if (startTileCenter!=null)
					{
						startPosition.y = startTileCenter.elevation +0.5f;
						
						Vector3 endPosition = new Vector3(0, 0, 0);

						switch( side)
						{
						case 0:
							endPosition = roomPositions[0];
							endPosition.x = targetGameObj.transform.position.x;
							break;
						case 1:
							endPosition = roomPositions[0];
							endPosition.z = targetGameObj.transform.position.z;
							break;
						case 2:
							endPosition = roomPositions[0];
							break;
						case 3:
							endPosition = targetGameObj.transform.position;
							break;
						}

						Assets.Map.Center endTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (endPosition.x, endPosition.z));
						if (endTileCenter!=null)
						{
							endPosition.y = endTileCenter.elevation + 0.5f;
							
							lr.SetPosition(0, startPosition);
							lr.SetPosition(1, endPosition);
						}
					}
				}
			}
		}
	}


	public void AddRoom(Vector3 cornerPoint, int brushHeight) 
	{
		//Place path
		if (roomPositions.Count == 1) {
			
			
			roomPositions.Add (cornerPoint);
			
			Assets.Map.Center startTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (roomPositions [0].x, roomPositions [0].z));
			Assets.Map.Center endTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (roomPositions [1].x, roomPositions [1].z));
			
			if (startTileCenter != endTileCenter) 
			{
				Vector3 path = roomPositions [1] - roomPositions [0];
				
				Assets.Map.Center tileCenter = startTileCenter;


				foreach ( Assets.Map.Center selectedCenter in Graph.centers)
				{
					float minX = Mathf.Min(roomPositions[0].x, roomPositions[1].x);
					float maxX = Mathf.Max(roomPositions[0].x, roomPositions[1].x);

					if ( selectedCenter.point.x > minX &&  selectedCenter.point.x < maxX)
					{
						float minZ = Mathf.Min(roomPositions[0].z, roomPositions[1].z);
						float maxZ = Mathf.Max(roomPositions[0].z, roomPositions[1].z);

						if ( selectedCenter.point.y > minZ &&  selectedCenter.point.y < maxZ)
						{
							PlaceTile (selectedCenter.point.x, selectedCenter.point.y, brushHeight);
						}
					}
				}
			}
			
			roomPositions.Clear ();
			

			Object.Destroy (roomRenderGFX[0]);
			Object.Destroy (roomRenderGFX[1]);
			Object.Destroy (roomRenderGFX[2]);
			Object.Destroy (roomRenderGFX[3]);
		}
	}

	public void AddTerrainPath(Vector3 pathPoint, string pathType, int brushHeight) 
	{
		//Place path
		if (pathPositions.Count == 1) {


			pathPositions.Add (pathPoint);
			
			Assets.Map.Center startTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (pathPositions [0].x, pathPositions [0].z));
			Assets.Map.Center endTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (pathPositions [1].x, pathPositions [1].z));
			
			if (startTileCenter != endTileCenter) {
				Vector3 path = pathPositions [1] - pathPositions [0];
				
				Assets.Map.Center tileCenter = startTileCenter;
				
				for (Vector3 pathSegment = Vector3.zero; Vector3.Magnitude(pathSegment) <= Vector3.Magnitude(path); pathSegment += path/50) {
					
					Vector3 segment = pathPositions [0] + pathSegment;
					
					Assets.Map.Center thisTileCenter = Graph.centers.FirstOrDefault (p => p.PointInside (segment.x, segment.z));

					if (thisTileCenter != null && tileCenter != null)
					{
						if (thisTileCenter != tileCenter) {

							switch ( pathType)
							{
							case MapGenMenu.SubTitle_Ramps:
								if (thisTileCenter.elevation < tileCenter.elevation)
								{
									AddRamp (tileCenter, thisTileCenter);
								} else {
									AddRamp (thisTileCenter, tileCenter);
								}
								break;
							case MapGenMenu.SubTitle_Tiles:
								PlaceTile (tileCenter.point.x, tileCenter.point.y, brushHeight);
								break;
							}

							tileCenter = thisTileCenter;
						}
					}
				}
			}

			pathPositions.Clear ();

			Object.Destroy (pathRenderGFX);
		}
	}


	public void AddTerrainPathPoint(Vector3 pathPoint) 
	{
		if ( pathPositions.Count == 0)
		{
			pathPositions.Add (pathPoint);

			pathRenderGFX = Object.Instantiate (UIPathPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;

		}
	}


	public void AddRoomCornerPoint(Vector3 cornerPoint) 
	{
		if ( roomPositions.Count == 0)
		{
			roomPositions.Add (cornerPoint);
			
			roomRenderGFX[0] = Object.Instantiate (UIPathPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
			roomRenderGFX[1] = Object.Instantiate (UIPathPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
			roomRenderGFX[2] = Object.Instantiate (UIPathPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
			roomRenderGFX[3] = Object.Instantiate (UIPathPrefab, new Vector3 (0, 0, 0), Quaternion.identity) as GameObject;
			
		}
	}

	public void ClearPathPlacement() 
	{
		pathPositions.Clear();
		Object.Destroy(pathRenderGFX);
	}

	// Ramp between selectedCenter and target center (c)
	public void AddRamp(Assets.Map.Center selectedCenter, Assets.Map.Center c) 
	{
		if (c.elevation < selectedCenter.elevation) 
		{	
			foreach ( Assets.Map.Edge edge in  c.borders)
			{
				if (edge.d0 == selectedCenter || edge.d1 == selectedCenter) // todo: floating point equaulity may break with non-integer floats.
				{ // this is the edge which connects to the selected site

					if (edge.gameObjectRamp.Count == 0)
					{
						edge.gameObjectWalls.ForEach( _gfx => Object.Destroy(_gfx));
						edge.gameObjectWalls.Clear();

						edge.gameObjectWallsViz.ForEach( _gfx => Object.Destroy(_gfx));
						edge.gameObjectWallsViz.Clear();

						edge.gameObjectRamp.ForEach( _gfx => Object.Destroy(_gfx));
						edge.gameObjectRamp.Clear();

						Vector2 halfEdgeVector = edge.v0.point - edge.v1.point;
						halfEdgeVector = halfEdgeVector / 2;

						Vector2 centerVector =  c.point - selectedCenter.point;
						centerVector = centerVector / 2;


						GameObject obj = Object.Instantiate( assetLibrary.GetComponent<AssetLibrary>().collisionPrefab, new Vector3 (c.point.x, 0, c.point.y), Quaternion.identity) as GameObject;

						obj.layer = 8; //ground

						obj.name = "rampCollision";

						//obj.GetComponent<Renderer>().enabled = false; // hide collison mesh 

						obj.GetComponent<MeshFilter>().mesh.vertices = new Vector3[]
						{
							new Vector3 (edge.v1.point.x - c.point.x, 					selectedCenter.elevation, 		edge.v1.point.y - c.point.y),
							new Vector3 (edge.v1.point.x - c.point.x + centerVector.x,  c.elevation, 					edge.v1.point.y - c.point.y + centerVector.y),
							new Vector3 (edge.v0.point.x - c.point.x, 					selectedCenter.elevation, 		edge.v0.point.y - c.point.y),
							new Vector3 (edge.v0.point.x - c.point.x + centerVector.x,  c.elevation, 					edge.v0.point.y - c.point.y + centerVector.y)
						};

						obj.GetComponent<MeshFilter>().mesh.uv = new [] { new Vector2 (1, 1), new Vector2 (1, 0), new Vector2(0, 1), new Vector2(0, 0)};

						// tri ordering is based on vectors between center and adjacent center
						if ( c.point.y == selectedCenter.point.y) //TODO: float equality
						{
							if ( c.point.x < selectedCenter.point.x) 
							{
								obj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 1, 2, 2, 1, 3};
							}
							else
							{
								obj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 2, 1, 1, 2, 3};
							}
						}
						else if ( c.point.y > selectedCenter.point.y)
						{
							obj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 2, 1, 1, 2, 3};
						}
						else if ( c.point.y < selectedCenter.point.y)
						{
							obj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 1, 2, 2, 1, 3};
						}

						obj.GetComponent<MeshFilter>().mesh.RecalculateNormals ();

						//obj.AddComponent<MeshCollider>();
						obj.GetComponent<MeshCollider>().sharedMesh = obj.GetComponent<MeshFilter>().mesh; 

						obj.transform.SetParent (this.transform);
						
						edge.gameObjectRamp.Add(obj);


						// Ramp Viz Object
						Vector3 newPos = (new Vector3(edge.v0.point.x, 0, edge.v0.point.y) + new Vector3(edge.v1.point.x, 0, edge.v1.point.y)) / 2;
						newPos.y = c.elevation;

						
						GameObject vizObj  = Object.Instantiate( assetLibrary.GetComponent<AssetLibrary>().VizPrefabStair, newPos, Quaternion.identity) as GameObject;
						vizObj.name = "VizPrefabStair";

						// flag MeshFilter game objects as viz layer
						MeshFilter[] childmf = vizObj.GetComponentsInChildren<MeshFilter>();
						int ct = 0;
						while (ct < childmf.Length) 
						{
							MeshFilter cmf = childmf[ct];
							
							if (cmf != null)
							{
								cmf.gameObject.layer = 10;
							}
							
							ct ++;
						}

						// rotate to angle of edge face
						float yAngle = Vector3.Angle(  new Vector3(0, 0, 1), new Vector3(c.point.x, 0, c.point.y) - new Vector3(selectedCenter.point.x, 0, selectedCenter.point.y));
						
						if (selectedCenter.point.x > c.point.x)
						{
							vizObj.transform.Rotate(0, -yAngle, 0);
						}
						else
						{
							vizObj.transform.Rotate(0, yAngle, 0);
						}
						
						Vector3 ls = vizObj.transform.localScale;
						ls.x = Mathf.Sqrt( Vector2.SqrMagnitude(edge.v0.point - edge.v1.point)) / 2.0f;
						ls.y = selectedCenter.elevation - c.elevation;
						vizObj.transform.localScale = ls;
						
						vizObj.transform.SetParent( this.transform);
						
						edge.gameObjectRamp.Add (vizObj);

						bRescanNavGraph = true;

						return;
					}
				}
			}
		}

	}

	public void PlaceDoor(float x, float y) 
	{
		Debug.Log("PlaceDoor");

		Assets.Map.Center selectedCenter = Graph.centers.FirstOrDefault(p => p.PointInside(x, y));
		if (selectedCenter == null) 
		{
			Debug.Log("PlaceDoor: selectedCenter is null");
			return;
		}

		// Generate List of edges which are valid for door placement.
		List<Assets.Map.Edge> validDoorEdges = new List<Assets.Map.Edge> ();
		for ( int edgeIndex = 0; edgeIndex < selectedCenter.borders.Count; edgeIndex ++)
		{
			Assets.Map.Edge selectedEdge  = selectedCenter.borders[edgeIndex];
			if ( selectedEdge.v0 != null && selectedEdge.v1 != null )
			{
				if ( Vector3.Magnitude(selectedEdge.v0.point - selectedEdge.v1.point) > 0 )
				{
					validDoorEdges.Add(selectedEdge);
				}
			}
		}

		// remove existing door Viz game objects around selected center & store index of removed door Viz.
		int selectedEdgeIndex = 0;
		bool bRemovedDoor = false;

		for ( int edgeIndex = 0; edgeIndex < validDoorEdges.Count; edgeIndex ++)
		{
			Assets.Map.Edge selectedEdge  = validDoorEdges[edgeIndex];
			if (selectedEdge.gameObjectDoorViz.Count > 0)
			{
				selectedEdge.gameObjectDoorViz.ForEach( _gfx => Object.Destroy(_gfx));
				selectedEdge.gameObjectDoorViz.Clear();

				selectedEdgeIndex = edgeIndex;
				bRemovedDoor = true;
			}
		}

		// door placement will be at next edge index.
		if (bRemovedDoor == true) 
		{
			selectedEdgeIndex ++;
		}

		// this condition is used to define a state where no doors will be attached to the selected tile
		if (selectedEdgeIndex >= validDoorEdges.Count) 
		{
			return;
		}


		Assets.Map.Edge edge = validDoorEdges[selectedEdgeIndex];
		Assets.Map.Center adjCenter; // may be at either d0 or d1
		
		if (edge.d0 == selectedCenter) // todo: floating point equaulity may break with non-integer floats.
		{
			adjCenter = edge.d1;
		}
		else{
			adjCenter = edge.d0;
		}

		int thisElevation = (int)selectedCenter.elevation ;
		if ( edge.v0 != null && edge.v1 != null) //graph border tiles may be null
		{
			//VIZ
			Vector3 newPos = (new Vector3(edge.v0.point.x, 0, edge.v0.point.y) + new Vector3(edge.v1.point.x, 0, edge.v1.point.y)) / 2;
			
			newPos.y = thisElevation;
			
			GameObject vizObj  = Object.Instantiate( assetLibrary.GetComponent<AssetLibrary>().VizPrefabArch, newPos, Quaternion.identity) as GameObject;
			
			vizObj.name = "VizPrefabArch";
			
			// flag MeshFilter game objects as viz layer
			MeshFilter[] childmf = vizObj.GetComponentsInChildren<MeshFilter>();
			int ct = 0;
			while (ct < childmf.Length) 
			{
				MeshFilter cmf = childmf[ct];
				
				if (cmf != null)
				{
					cmf.gameObject.layer = 10;
				}
				
				ct ++;
			}
			
			//rotate to angle of edge face
			float yAngle = Vector3.Angle(  new Vector3(0, 0, 1), new Vector3(selectedCenter.point.x, 0, selectedCenter.point.y) - new Vector3(adjCenter.point.x, 0, adjCenter.point.y));
			
			if (adjCenter.point.x > selectedCenter.point.x)
			{
				vizObj.transform.Rotate(0, -yAngle, 0);
			}
			else
			{
				vizObj.transform.Rotate(0, yAngle, 0);
			}
			
			Vector3 ls = vizObj.transform.localScale;
			ls.x = Mathf.Sqrt( Vector2.SqrMagnitude(edge.v0.point - edge.v1.point));
			vizObj.transform.localScale = ls;
			
			vizObj.transform.SetParent( this.transform);
			
			edge.gameObjectDoorViz.Add (vizObj);
		}
	}


	public void PlaceTile(float x, float y, int brushHeight) 
	{
		Assets.Map.Center selectedCenter = Graph.centers.FirstOrDefault(p => p.PointInside(x, y));
		if (selectedCenter != null) 
		{
			selectedCenter.elevation = brushHeight;

			// rebuild
			BuildVoronoiTileGFX (selectedCenter);


			bRescanNavGraph = true;
		}
	}

	private void BuildVoronoiGFX()
	{
		foreach (Center c in Graph.centers) 
		{
			BuildVoronoiTileGFX (c);
		}
		
	}
	
	private void BuildVoronoiTileGFX(Center c)
	{

		// Tiles
		c.gameObjectTile.ForEach( _gfx => Object.Destroy(_gfx));

		if (c.elevation > 0) {
			Vector3 [] corners3d = c.corners.Select (p => new Vector3 (p.point.x, 0.0f, p.point.y)).ToArray ();
		
			for (int crnIndex = 0; crnIndex < corners3d.Length; crnIndex ++) 
			{
				// Collision object
				{
					GameObject collisionObj = Object.Instantiate (assetLibrary.GetComponent<AssetLibrary>().collisionPrefab, new Vector3 (c.point.x, 0, c.point.y), Quaternion.identity) as GameObject;
				
					collisionObj.layer = 8; //ground



					collisionObj.name = "groundCollision";
				
					int startCrnIndex = crnIndex;
					int endCrnIndex = (crnIndex + 1) % corners3d.Length;
				
					Vector3[] vertices = new Vector3[]
					{
						new Vector3 (0, 										c.elevation, 0),
						new Vector3 (corners3d [startCrnIndex].x - c.point.x, 	c.elevation, corners3d [startCrnIndex].z - c.point.y),
						new Vector3 (corners3d [endCrnIndex].x - c.point.x, 	c.elevation, corners3d [endCrnIndex].z - c.point.y)
					};


					collisionObj.GetComponent<MeshFilter> ().mesh.name = "tileTri";
					collisionObj.GetComponent<MeshFilter> ().mesh.vertices = vertices;
					collisionObj.GetComponent<MeshFilter> ().mesh.uv = new [] { new Vector2 (0, 0), new Vector2 (0, 1), new Vector2 (1, 1)};
					collisionObj.GetComponent<MeshFilter> ().mesh.triangles = new [] {0, 1, 2, 0, 2, 0};
					collisionObj.GetComponent<MeshFilter> ().mesh.RecalculateNormals();

					if (kbRenderCollisionMesh==true)
					{
						collisionObj.AddComponent<MeshRenderer>();
					}



					collisionObj.transform.SetParent(this.transform);
					//collisionObj.AddComponent<MeshCollider>();
					collisionObj.GetComponent<MeshCollider>().sharedMesh = collisionObj.GetComponent<MeshFilter>().mesh; 

					c.gameObjectTile.Add (collisionObj);
				}

				//Viz object
				{
					GameObject vizObj = Object.Instantiate (assetLibrary.GetComponent<AssetLibrary>().VizPrefabGroundTile, new Vector3 (c.point.x, 0, c.point.y), Quaternion.identity) as GameObject;
					
					vizObj.layer = 10; //viz
					vizObj.name = "groundViz";

					int startCrnIndex = crnIndex;
					int endCrnIndex = (crnIndex + 1) % corners3d.Length;
					
					Vector3[] vertices = new Vector3[]
					{
						new Vector3 (0, 										c.elevation, 0),
						new Vector3 (corners3d [startCrnIndex].x - c.point.x, 	c.elevation, corners3d [startCrnIndex].z - c.point.y),
						new Vector3 (corners3d [endCrnIndex].x - c.point.x, 	c.elevation, corners3d [endCrnIndex].z - c.point.y)
					};

					vizObj.GetComponent<MeshFilter> ().mesh.name = "tileTri";
					vizObj.GetComponent<MeshFilter> ().mesh.vertices = vertices;
					vizObj.GetComponent<MeshFilter> ().mesh.uv = new [] { new Vector2 (0.366f, 0.460f), new Vector2 (0.366f, 0.7f), new Vector2 (0.568f, 0.7f)};
					vizObj.GetComponent<MeshFilter> ().mesh.triangles = new [] {0, 1, 2, 0, 2, 0};
					vizObj.GetComponent<MeshFilter> ().mesh.RecalculateNormals();
					
					Material mat = Resources.Load ("Materials/DungeonMat1", typeof(Material)) as Material; // i.e. .png, .jpg, etc
					Material [] mats = new Material[1];
					mats [0] = mat;
					vizObj.transform.GetComponent<Renderer>().materials = mats;
					
					vizObj.transform.SetParent(this.transform);
					
					c.gameObjectTile.Add (vizObj);
				}
			}
		}
		
	
		// WALLS
		foreach ( Assets.Map.Edge edge in  c.borders)
		{

			edge.gameObjectWalls.ForEach( _gfx => Object.Destroy(_gfx));
			edge.gameObjectWalls.Clear();

			edge.gameObjectWallsViz.ForEach( _gfx => Object.Destroy(_gfx));
			edge.gameObjectWallsViz.Clear();

			// Remove edge ramps.
			edge.gameObjectRamp.ForEach( _gfx => Object.Destroy(_gfx));
			edge.gameObjectRamp.Clear();
	
			Assets.Map.Center adjCenter; // may be at either d0 or d1

			if (edge.d0 == c) // todo: floating point equaulity may break with non-integer floats.
			{
				adjCenter = edge.d1;
			}
			else{
				adjCenter = edge.d0;
			}

			// place walls for all surrounding tiles which are of lower elevation
			// this ensures that walls are only generated once for adjacent tiles.
			if ( c.elevation != adjCenter.elevation )
			{
				int lowerElevation = (int)Mathf.Min((int)adjCenter.elevation, (int)c.elevation);
				int higherElevation = (int)Mathf.Max((int)adjCenter.elevation, (int)c.elevation);

				for (int thisElevation = lowerElevation ; thisElevation < higherElevation; thisElevation ++) 
				{
					if ( edge.v0 != null && edge.v1 != null) //graph border tiles may be null
					{
						if ( Vector3.Magnitude(edge.v0.point - edge.v1.point) > 0)
						{
							//VIZ
							Vector3 newPos = (new Vector3(edge.v0.point.x, 0, edge.v0.point.y) + new Vector3(edge.v1.point.x, 0, edge.v1.point.y)) / 2;

							newPos.y = thisElevation;

							GameObject vizObj  = Object.Instantiate( assetLibrary.GetComponent<AssetLibrary>().VizPrefabWall, newPos, Quaternion.identity) as GameObject;

							vizObj.name = "VizPrefabWall";

							// flag MeshFilter game objects as viz layer
							MeshFilter[] childmf = vizObj.GetComponentsInChildren<MeshFilter>();
							int ct = 0;
							while (ct < childmf.Length) 
							{
								MeshFilter cmf = childmf[ct];
								
								if (cmf != null)
								{
									cmf.gameObject.layer = 10;
								}
								
								ct ++;
							}

							//rotate to angle of edge face
							float yAngle = Vector3.Angle(  new Vector3(0, 0, 1), new Vector3(c.point.x, 0, c.point.y) - new Vector3(adjCenter.point.x, 0, adjCenter.point.y));

							if (adjCenter.point.x > c.point.x)
							{
								vizObj.transform.Rotate(0, -yAngle, 0);
							}
							else
							{
								vizObj.transform.Rotate(0, yAngle, 0);
							}

							Vector3 ls = vizObj.transform.localScale;
							ls.x = Mathf.Sqrt( Vector2.SqrMagnitude(edge.v0.point - edge.v1.point));
							vizObj.transform.localScale = ls;

							vizObj.transform.SetParent( this.transform);
							
							edge.gameObjectWallsViz.Add (vizObj);

							// COLLISION
							GameObject collisionObj  = Object.Instantiate( assetLibrary.GetComponent<AssetLibrary>().collisionPrefab, new Vector3(c.point.x, 0, c.point.y), Quaternion.identity) as GameObject;
							
							collisionObj.layer = 9; //obstacle

							collisionObj.name = "wallCollision";

							if (kbRenderCollisionMesh==true)
							{
								collisionObj.AddComponent<MeshRenderer>();
							}
			
							collisionObj.GetComponent<MeshFilter>().mesh.vertices = new Vector3[]
							{
								new Vector3(edge.v1.point.x - c.point.x, thisElevation + 1, edge.v1.point.y - c.point.y),
								new Vector3(edge.v1.point.x - c.point.x, thisElevation + 0, edge.v1.point.y - c.point.y),
								new Vector3(edge.v0.point.x - c.point.x, thisElevation + 1, edge.v0.point.y - c.point.y),
								new Vector3(edge.v0.point.x - c.point.x, thisElevation + 0, edge.v0.point.y - c.point.y),
							};

							collisionObj.GetComponent<MeshFilter>().mesh.uv = new [] { new Vector2 (1, 1), new Vector2 (1, 0), new Vector2(0, 1), new Vector2(0, 0)};

							// tri ordering is based on vectors between center and adjacent center
							if ( c.point.y == adjCenter.point.y) //TODO: float equality
							{
								if ( c.point.x > adjCenter.point.x) 
								{
									if ( c.elevation > adjCenter.elevation )
									{
										collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 1, 2, 2, 1, 3};
									}
									else
									{
										collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 2, 1, 1, 2, 3};
									}
								}
								else
								{
									if ( c.elevation > adjCenter.elevation )
									{
										collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 2, 1, 1, 2, 3};
									}
									else
									{
										collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 1, 2, 2, 1, 3};
									}
								}
							}
							else if ( c.point.y < adjCenter.point.y)
							{
								if ( c.elevation > adjCenter.elevation )
								{
									collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 2, 1, 1, 2, 3};
								}
								else
								{
									collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 1, 2, 2, 1, 3};
								}
							}
							else if ( c.point.y > adjCenter.point.y)
							{
								if ( c.elevation > adjCenter.elevation )
								{
									collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 1, 2, 2, 1, 3};
								}
								else
								{
									collisionObj.GetComponent<MeshFilter>().mesh.triangles = new [] {0, 2, 1, 1, 2, 3};
								}
							}

							collisionObj.GetComponent<MeshFilter>().mesh.RecalculateNormals();
							collisionObj.GetComponent<MeshCollider>().sharedMesh = collisionObj.GetComponent<MeshFilter>().mesh; 
							collisionObj.transform.SetParent( this.transform);

							edge.gameObjectWalls.Add( collisionObj);
						}
					}
					
				}
			}
			
		}
		
		
		
	}


















}
