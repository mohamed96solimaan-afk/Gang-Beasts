// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// The window: menu "Gang Beasts > Level Editor" (Ctrl/Cmd+Shift+L).
//   New Level  builds a complete playable level scene and registers it in the game.
//   Build      places geometry, physics props, trigger zones, lights and beasts.
//   Props      harvests props from the shipped levels into prefabs and places them.
//   Kit        extracts / inspects the level kit prefabs.
//   Check      validates the open scene and fixes what it can; menu & build registration.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public class GBLevelEditorWindow : EditorWindow
	{
		static readonly string[] Tabs = { "New Level", "Build", "Props", "Kit", "Check" };
		const string PrefReturnScene = "GBLevelEditor.returnScene";

		enum Shape
		{
			Cube,
			Sphere,
			Capsule,
			Cylinder
		}

		int tab;
		Vector2 scroll;

		// New Level
		GBNewLevelOptions options = new GBNewLevelOptions();
		bool defaultsReady;

		// Build
		Material buildMaterial;
		Vector3 platformSize = new Vector3(6f, 1f, 6f);
		float rampLength = 8f;
		float rampWidth = 4f;
		float rampAngle = 20f;
		Shape propShape = Shape.Cube;
		Vector3 propSize = Vector3.one;
		float propMass = 5f;
		int propTag;
		int propLayer;
		Material propMaterial;
		PhysicMaterial propPhysicMaterial;
		Vector3 zoneSize = new Vector3(10f, 2f, 10f);
		float killDelay;
		AudioClip killSound;
		LightType lightType = LightType.Point;
		Color lightColor = Color.white;
		float lightIntensity = 1.5f;
		float lightRange = 15f;
		float lightSpotAngle = 50f;
		float selectionMass = 5f;
		bool overlays = true;

		// Props
		int sourceIndex;
		List<GBPropCandidate> candidates = new List<GBPropCandidate>();
		string scanScene = string.Empty;
		string returnScene = string.Empty;
		string paletteFilter = string.Empty;
		List<string> palette;

		// Kit
		int referenceIndex = -1;
		int mobIndex = -1;

		// Check
		List<GBIssue> issues;
		string issuesScene = string.Empty;

		// ---- menu ---------------------------------------------------------------------

		[MenuItem("Gang Beasts/Level Editor %#l")]
		public static void Open()
		{
			Window();
		}

		[MenuItem("Gang Beasts/New Level...")]
		static void OpenNewLevel()
		{
			Window().tab = 0;
		}

		[MenuItem("Gang Beasts/Check Current Level")]
		static void OpenCheck()
		{
			GBLevelEditorWindow w = Window();
			w.tab = 4;
			w.RunChecks();
		}

		[MenuItem("Gang Beasts/Bake NavMesh (for mobs)")]
		static void BakeNavMesh()
		{
			int n = GBObjectFactory.BakeNavMesh();
			Debug.Log(GBEditorUtil.LogPrefix + "NavMesh baked; " + n + " object(s) newly marked Navigation Static.");
		}

		static GBLevelEditorWindow Window()
		{
			GBLevelEditorWindow w = GetWindow<GBLevelEditorWindow>();
			w.title = "GB Level Editor";
			w.minSize = new Vector2(420f, 460f);
			w.Show();
			return w;
		}

		// ---- lifecycle ----------------------------------------------------------------

		void OnEnable()
		{
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.onSceneGUIDelegate += OnSceneGUI;
			returnScene = EditorPrefs.GetString(PrefReturnScene, string.Empty);
		}

		void OnDisable()
		{
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			GBPlacementTool.End();
		}

		void OnSceneGUI(SceneView view)
		{
			if (overlays)
			{
				GBPlacementTool.DrawOverlays(view);
			}
		}

		void OnInspectorUpdate()
		{
			if (tab == 2 || GBPlacementTool.IsActive)
			{
				Repaint(); // asset previews arrive asynchronously; placement state changes in the Scene view
			}
		}

		void OnFocus()
		{
			palette = null;
		}

		void InitDefaults()
		{
			if (defaultsReady)
			{
				return;
			}
			defaultsReady = true;
			options.floorMaterial = GBObjectFactory.DefaultFloorMaterial();
			options.killSound = GBObjectFactory.DefaultKillSound();
			buildMaterial = options.floorMaterial;
			killSound = options.killSound;
			string[] levels = GBLevelRegistry.LevelScenePaths();
			referenceIndex = Mathf.Max(0, GBEditorUtil.IndexOf(levels, GBLevelKit.DefaultReferenceScene));
			mobIndex = Mathf.Max(0, GBEditorUtil.IndexOf(levels, GBLevelKit.DefaultMobScene));
			sourceIndex = referenceIndex;
		}

		// ---- GUI ------------------------------------------------------------------------

		void OnGUI()
		{
			InitDefaults();
			tab = GUILayout.Toolbar(tab, Tabs);
			EditorGUILayout.Space();
			if (GBPlacementTool.IsActive)
			{
				EditorGUILayout.HelpBox("Placing: " + GBPlacementTool.Label + ". Click in the Scene view to drop one. Esc (or this button) stops.", MessageType.Info);
				if (GUILayout.Button("Stop placing"))
				{
					GBPlacementTool.End();
				}
			}
			scroll = EditorGUILayout.BeginScrollView(scroll);
			switch (tab)
			{
				case 0:
					NewLevelTab();
					break;
				case 1:
					BuildTab();
					break;
				case 2:
					PropsTab();
					break;
				case 3:
					KitTab();
					break;
				default:
					CheckTab();
					break;
			}
			EditorGUILayout.EndScrollView();
		}

		static void Header(string text)
		{
			EditorGUILayout.Space();
			GUILayout.Label(text, EditorStyles.boldLabel);
		}

		static T ObjectField<T>(string label, T current) where T : UnityEngine.Object
		{
			return EditorGUILayout.ObjectField(label, current, typeof(T), false) as T;
		}

		/// <summary>[Place at view pivot] [Paint with mouse] for one object factory.</summary>
		static void PlaceButtons(string what, System.Func<GameObject> make)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.enabled = !EditorApplication.isPlaying;
			if (GUILayout.Button("Place at view pivot", GUILayout.Width(150f)))
			{
				GBPlacementTool.PlaceAtPivot(what, make);
			}
			if (GUILayout.Button("Paint with mouse", GUILayout.Width(150f)))
			{
				GBPlacementTool.Begin(what, make);
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
		}

		static PrimitiveType ToPrimitive(Shape shape)
		{
			switch (shape)
			{
				case Shape.Sphere:
					return PrimitiveType.Sphere;
				case Shape.Capsule:
					return PrimitiveType.Capsule;
				case Shape.Cylinder:
					return PrimitiveType.Cylinder;
				default:
					return PrimitiveType.Cube;
			}
		}

		// ---- New Level ------------------------------------------------------------------

		void NewLevelTab()
		{
			EditorGUILayout.HelpBox("Creates Assets/Scenes/<name>.unity with everything a Gang Beasts level needs (Scene Manager, follow camera, sky, sun, music, floor, kill zone, the four beasts), then adds it to Build Settings and to the Menu's level list so it appears under 'Level:' in the game.", MessageType.None);

			Header("Level");
			options.levelName = EditorGUILayout.TextField("Name", options.levelName);

			Header("Arena");
			options.floorSize = EditorGUILayout.Slider("Floor size (m)", options.floorSize, 4f, 200f);
			options.floorThickness = EditorGUILayout.Slider("Floor thickness", options.floorThickness, 0.1f, 5f);
			options.floorMaterial = ObjectField<Material>("Floor material", options.floorMaterial);
			options.addWalls = EditorGUILayout.Toggle("Walls around the floor", options.addWalls);
			if (options.addWalls)
			{
				options.wallHeight = EditorGUILayout.Slider("Wall height", options.wallHeight, 0.5f, 20f);
			}

			Header("Kill zone");
			options.addKillZone = EditorGUILayout.Toggle("Kill zone under the level", options.addKillZone);
			if (options.addKillZone)
			{
				options.killZoneDepth = EditorGUILayout.Slider("Depth (y)", options.killZoneDepth, -100f, -1f);
				options.killZoneSize = EditorGUILayout.Slider("Size (m)", options.killZoneSize, 10f, 2000f);
				options.killSound = ObjectField<AudioClip>("Kill sound", options.killSound);
			}

			Header("Beasts");
			options.players = EditorGUILayout.IntSlider("Players", options.players, 1, 4);
			options.spawnRadius = EditorGUILayout.Slider("Spawn radius", options.spawnRadius, 0.5f, 50f);
			options.spawnHeight = EditorGUILayout.Slider("Spawn height", options.spawnHeight, 0f, 10f);
			options.mobs = EditorGUILayout.IntSlider("Mobs (AI)", options.mobs, 0, 12);
			if (options.mobs > 0)
			{
				EditorGUILayout.HelpBox("Mobs walk on the NavMesh. After the level is built, bake it (Check tab > Bake NavMesh).", MessageType.Info);
			}

			Header("Systems");
			options.addSky = EditorGUILayout.Toggle("Marmoset sky", options.addSky);
			options.addMusic = EditorGUILayout.Toggle("Music", options.addMusic);
			if (options.addMusic)
			{
				options.music = ObjectField<AudioClip>("Music clip (empty = kit default)", options.music);
			}
			options.addFpsCounter = EditorGUILayout.Toggle("FPS counter (F)", options.addFpsCounter);
			options.addAudioTimeScale = EditorGUILayout.Toggle("Slow motion keys (- =)", options.addAudioTimeScale);
			options.totalPlayers = EditorGUILayout.IntSlider("Players when run from editor", options.totalPlayers, 1, 4);
			options.startingLives = EditorGUILayout.IntSlider("Lives", options.startingLives, 1, 20);
			EditorGUILayout.HelpBox("'Players when run from editor' only matters when you press Play inside this scene. Started from the menu, the menu's player count wins.", MessageType.None);

			Header("Register");
			options.addToBuildSettings = EditorGUILayout.Toggle("Add to Build Settings", options.addToBuildSettings);
			options.addToMenuList = EditorGUILayout.Toggle("Add to Menu level list", options.addToMenuList);
			options.addToRezzedList = EditorGUILayout.Toggle("Add to Rezzed rotation", options.addToRezzedList);
			options.keepPrefabLinks = EditorGUILayout.Toggle("Keep links to kit prefabs", options.keepPrefabLinks);

			Header("Kit");
			KitStatusLine();

			EditorGUILayout.Space();
			GUI.enabled = !EditorApplication.isPlaying;
			if (GUILayout.Button("Create Level", GUILayout.Height(34f)))
			{
				string path;
				if (GBLevelKit.CreateLevel(options, out path))
				{
					issues = null;
					palette = null;
					EditorUtility.DisplayDialog("Level created", path + "\n\nNext: add geometry and props (Build and Props tabs), then run Check.\n\nTo test: press Play. Controls are bound to controllers; press Tab in the game to bind keyboard keys.", "OK");
					GUIUtility.ExitGUI();
				}
			}
			GUI.enabled = true;
		}

		void KitStatusLine()
		{
			int missing = GBLevelKit.Missing(false).Count;
			int requiredMissing = GBLevelKit.Missing(true).Count;
			EditorGUILayout.BeginHorizontal();
			if (missing == 0)
			{
				EditorGUILayout.LabelField("Kit prefabs: all " + GBLevelKit.Items.Length + " ready");
			}
			else
			{
				EditorGUILayout.LabelField("Kit prefabs: " + missing + " missing" + (requiredMissing > 0 ? " (" + requiredMissing + " required; extracted automatically on Create)" : string.Empty));
			}
			if (GUILayout.Button("Kit tab", GUILayout.Width(70f)))
			{
				tab = 3;
			}
			EditorGUILayout.EndHorizontal();
		}

		// ---- Build ------------------------------------------------------------------------

		void BuildTab()
		{
			Header("Placement");
			GBPlacementTool.snap = EditorGUILayout.FloatField("Grid snap (0 = off)", GBPlacementTool.snap);
			GBPlacementTool.sitOnSurface = EditorGUILayout.Toggle("Rest on surface", GBPlacementTool.sitOnSurface);
			GBPlacementTool.alignToSurface = EditorGUILayout.Toggle("Align to surface normal", GBPlacementTool.alignToSurface);
			GBPlacementTool.randomYaw = EditorGUILayout.Toggle("Random rotation", GBPlacementTool.randomYaw);
			overlays = EditorGUILayout.Toggle("Show zones and spawns", overlays);

			Header("Static geometry");
			buildMaterial = ObjectField<Material>("Material", buildMaterial);
			platformSize = EditorGUILayout.Vector3Field("Platform size", platformSize);
			PlaceButtons("Platform", delegate { return GBObjectFactory.CreatePlatform(platformSize, buildMaterial); });
			rampLength = EditorGUILayout.FloatField("Ramp length", rampLength);
			rampWidth = EditorGUILayout.FloatField("Ramp width", rampWidth);
			rampAngle = EditorGUILayout.Slider("Ramp angle", rampAngle, 0f, 60f);
			PlaceButtons("Ramp", delegate { return GBObjectFactory.CreateRamp(rampLength, rampWidth, rampAngle, buildMaterial); });

			Header("Physics prop");
			propShape = (Shape)EditorGUILayout.EnumPopup("Shape", propShape);
			propSize = EditorGUILayout.Vector3Field("Size", propSize);
			propMass = EditorGUILayout.FloatField("Mass", propMass);
			propTag = EditorGUILayout.Popup("Tag", propTag, GBObjectFactory.PropTags);
			propLayer = EditorGUILayout.Popup("Layer", propLayer, GBObjectFactory.PropLayers);
			propMaterial = ObjectField<Material>("Material", propMaterial);
			propPhysicMaterial = ObjectField<PhysicMaterial>("Physic material", propPhysicMaterial);
			EditorGUILayout.HelpBox("Tag 'Prop': beasts can target and grab it. 'Prop (Harmful)': also hurts on impact. Layer 'Ignore Ball': the beasts' invisible balance ball passes through it (small throwables); 'Default': beasts can stand on it.", MessageType.None);
			PlaceButtons("Physics prop", delegate
			{
				return GBObjectFactory.CreatePhysicsProp(ToPrimitive(propShape), propSize, propMass, GBObjectFactory.PropTags[propTag],
					GBObjectFactory.PropLayers[propLayer], propMaterial, propPhysicMaterial);
			});

			Header("Zones");
			zoneSize = EditorGUILayout.Vector3Field("Zone size", zoneSize);
			killDelay = EditorGUILayout.FloatField("Kill delay (s)", killDelay);
			killSound = ObjectField<AudioClip>("Kill sound", killSound);
			PlaceButtons("Kill Zone", delegate { return GBObjectFactory.CreateKillZone(Vector3.zero, zoneSize, killSound, killDelay); });
			PlaceButtons("Warp Zone (restarts the level)", delegate { return GBObjectFactory.CreateWarpZone(Vector3.zero, zoneSize); });
			PlaceButtons("Dont Track Zone (camera ignores)", delegate { return GBObjectFactory.CreateDontTrackZone(Vector3.zero, zoneSize); });

			Header("Lights");
			lightType = (LightType)EditorGUILayout.EnumPopup("Type", lightType);
			lightColor = EditorGUILayout.ColorField("Color", lightColor);
			lightIntensity = EditorGUILayout.Slider("Intensity", lightIntensity, 0f, 8f);
			lightRange = EditorGUILayout.Slider("Range", lightRange, 1f, 100f);
			if (lightType == LightType.Spot)
			{
				lightSpotAngle = EditorGUILayout.Slider("Spot angle", lightSpotAngle, 1f, 179f);
			}
			PlaceButtons("Light", delegate { return GBObjectFactory.CreateLight(lightType, Vector3.zero, lightColor, lightIntensity, lightRange, lightSpotAngle); });

			Header("Beasts");
			EditorGUILayout.BeginHorizontal();
			GUI.enabled = GBLevelKit.Ready && !EditorApplication.isPlaying;
			for (int i = 0; i < 4; i++)
			{
				int index = i;
				if (GUILayout.Button("Add P" + (i + 1)))
				{
					GBPlacementTool.PlaceAtPivot("Player " + (index + 1), delegate { return GBLevelKit.SpawnPlayer(index, Vector3.zero, Vector3.forward, true); });
				}
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
			if (!GBLevelKit.Ready)
			{
				EditorGUILayout.HelpBox("Extract the kit first (Kit tab) to place beasts.", MessageType.Warning);
			}
			GBKitItem mob = GBLevelKit.Find("Mob");
			GUI.enabled = mob != null && mob.Exists && !EditorApplication.isPlaying;
			PlaceButtons("Mob", delegate { return GBLevelKit.SpawnMob(CountMobs() + 1, Vector3.zero, Vector3.forward, true); });
			GUI.enabled = true;

			Header("Selection");
			GUI.enabled = Selection.gameObjects.Length > 0 && !EditorApplication.isPlaying;
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Apply material"))
			{
				foreach (GameObject go in Selection.gameObjects)
				{
					foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
					{
						Undo.RecordObject(r, "Apply material");
						r.sharedMaterial = buildMaterial;
					}
				}
			}
			if (GUILayout.Button("Make static"))
			{
				foreach (GameObject go in Selection.gameObjects)
				{
					GBObjectFactory.MakeStatic(go);
				}
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			selectionMass = EditorGUILayout.FloatField("Mass", selectionMass);
			if (GUILayout.Button("Add Rigidbody"))
			{
				foreach (GameObject go in Selection.gameObjects)
				{
					Rigidbody rb = go.GetComponent<Rigidbody>();
					if (rb == null)
					{
						rb = Undo.AddComponent<Rigidbody>(go);
					}
					rb.mass = selectionMass;
				}
			}
			EditorGUILayout.EndHorizontal();
			GUI.enabled = true;
		}

		static int CountMobs()
		{
			int n = 0;
			foreach (Player p in GBEditorUtil.RootComponents<Player>())
			{
				if (p.mob)
				{
					n++;
				}
			}
			return n;
		}

		// ---- Props ------------------------------------------------------------------------

		void PropsTab()
		{
			string[] levelPaths = GBLevelRegistry.LevelScenePaths();
			string[] levelNames = GBLevelRegistry.SceneNames(levelPaths);
			string current = EditorApplication.currentScene;

			Header("1. Harvest props from a shipped level");
			EditorGUILayout.HelpBox("The shipped levels keep their props as plain scene objects. Open a level here, tick the objects you want, and they are saved as prefabs under Assets/LevelEditor/Props. The level itself is never changed.", MessageType.None);
			if (levelPaths.Length == 0)
			{
				EditorGUILayout.HelpBox("No scenes under Assets/Scenes.", MessageType.Warning);
				return;
			}
			sourceIndex = Mathf.Clamp(sourceIndex, 0, levelPaths.Length - 1);
			sourceIndex = EditorGUILayout.Popup("Source level", sourceIndex, levelNames);
			EditorGUILayout.BeginHorizontal();
			GUI.enabled = !EditorApplication.isPlaying;
			if (GUILayout.Button("Open level and scan"))
			{
				if (EditorApplication.SaveCurrentSceneIfUserWantsTo())
				{
					if (current != levelPaths[sourceIndex])
					{
						returnScene = current;
						EditorPrefs.SetString(PrefReturnScene, returnScene);
					}
					if (EditorApplication.OpenScene(levelPaths[sourceIndex]))
					{
						scanScene = levelPaths[sourceIndex];
						candidates = GBPropHarvester.Scan();
					}
					GUIUtility.ExitGUI();
				}
			}
			bool canReturn = !string.IsNullOrEmpty(returnScene) && returnScene != current && GBEditorUtil.AssetFileExists(returnScene);
			GUI.enabled = canReturn && !EditorApplication.isPlaying;
			if (GUILayout.Button("Back to " + (canReturn ? GBEditorUtil.SceneName(returnScene) : "my level")))
			{
				if (!EditorApplication.isSceneDirty || EditorUtility.DisplayDialog("Leave " + GBEditorUtil.SceneName(current) + "?",
					"Changes made to the shipped level are not saved (it stays untouched). Continue?", "Leave without saving", "Stay"))
				{
					EditorApplication.OpenScene(returnScene);
					candidates.Clear();
					scanScene = string.Empty;
					palette = null;
					GUIUtility.ExitGUI();
				}
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();

			if (candidates.Count > 0 && scanScene == current)
			{
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("All"))
				{
					GBPropHarvester.SelectAll(candidates, true);
				}
				if (GUILayout.Button("None"))
				{
					GBPropHarvester.SelectAll(candidates, false);
				}
				if (GUILayout.Button("Physics only"))
				{
					GBPropHarvester.SelectPhysicsOnly(candidates);
				}
				EditorGUILayout.EndHorizontal();
				foreach (GBPropCandidate c in candidates)
				{
					if (c.go == null)
					{
						continue;
					}
					EditorGUILayout.BeginHorizontal();
					c.selected = EditorGUILayout.Toggle(c.selected, GUILayout.Width(18f));
					string info = c.name + "   (" + (c.hasRigidbody ? "physics" : "static") + ", " + c.renderers + (c.renderers == 1 ? " renderer" : " renderers")
						+ (c.children > 0 ? ", " + c.children + " children" : string.Empty) + (c.proBuilder ? ", ProBuilder" : string.Empty) + ")";
					if (GUILayout.Button(info, EditorStyles.label))
					{
						Selection.activeGameObject = c.go;
						EditorGUIUtility.PingObject(c.go);
					}
					EditorGUILayout.EndHorizontal();
				}
				int selected = GBPropHarvester.SelectedCount(candidates);
				GUI.enabled = selected > 0 && !EditorApplication.isPlaying;
				if (GUILayout.Button("Save " + selected + " selected as prefabs"))
				{
					string sceneName = GBEditorUtil.SceneName(scanScene);
					int n = GBPropHarvester.Extract(candidates, sceneName);
					palette = null;
					EditorUtility.DisplayDialog("Props saved", n + " prefab(s) saved under " + GBEditorUtil.PropsFolder + "/" + sceneName
						+ ".\n\nUse 'Back to ...' to return to your level, then place them from the palette.", "OK");
				}
				GUI.enabled = true;
			}
			else if (!string.IsNullOrEmpty(scanScene) && scanScene == current)
			{
				EditorGUILayout.HelpBox("Nothing prop-like found in this level.", MessageType.Info);
			}

			Header("2. Palette");
			if (palette == null)
			{
				palette = GBPropHarvester.PalettePrefabPaths();
			}
			EditorGUILayout.BeginHorizontal();
			paletteFilter = EditorGUILayout.TextField("Filter", paletteFilter);
			if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
			{
				palette = GBPropHarvester.PalettePrefabPaths();
			}
			EditorGUILayout.EndHorizontal();
			if (palette.Count == 0)
			{
				EditorGUILayout.HelpBox("No prefabs yet. Harvest some props above, or extract the kit (Kit tab) to get the Mob.", MessageType.Info);
			}
			string filter = paletteFilter.Trim().ToLowerInvariant();
			string lastFolder = null;
			foreach (string path in palette)
			{
				string name = Path.GetFileNameWithoutExtension(path);
				if (filter.Length > 0 && name.ToLowerInvariant().IndexOf(filter) < 0)
				{
					continue;
				}
				string folder = Path.GetDirectoryName(path).Replace('\\', '/');
				if (folder != lastFolder)
				{
					lastFolder = folder;
					GUILayout.Label(folder, EditorStyles.miniLabel);
				}
				GameObject prefab = GBEditorUtil.LoadAsset<GameObject>(path);
				Texture2D preview = prefab != null ? AssetPreview.GetAssetPreview(prefab) : null;
				if (preview == null && prefab != null)
				{
					preview = AssetPreview.GetMiniThumbnail(prefab);
				}
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(preview, GUILayout.Width(40f), GUILayout.Height(40f));
				if (GUILayout.Button(name, EditorStyles.label, GUILayout.Height(40f)))
				{
					Selection.activeObject = prefab;
					EditorGUIUtility.PingObject(prefab);
				}
				string captured = path;
				GUI.enabled = !EditorApplication.isPlaying;
				if (GUILayout.Button("Place", GUILayout.Width(50f)))
				{
					GBPlacementTool.PlaceAtPivot(name, delegate { return GBPropHarvester.Instantiate(captured); });
				}
				if (GUILayout.Button("Paint", GUILayout.Width(50f)))
				{
					GBPlacementTool.Begin(name, delegate { return GBPropHarvester.Instantiate(captured); });
				}
				GUI.enabled = true;
				EditorGUILayout.EndHorizontal();
			}
		}

		// ---- Kit --------------------------------------------------------------------------

		void KitTab()
		{
			EditorGUILayout.HelpBox("The kit is the set of objects every level needs. The shipped game has no prefabs for them, so they are copied once from a reference level into Assets/LevelEditor/Kit as prefabs. The reference levels are opened to copy from and never saved.", MessageType.None);
			string[] levelPaths = GBLevelRegistry.LevelScenePaths();
			string[] levelNames = GBLevelRegistry.SceneNames(levelPaths);
			if (levelPaths.Length == 0)
			{
				EditorGUILayout.HelpBox("No scenes under Assets/Scenes.", MessageType.Warning);
				return;
			}
			referenceIndex = Mathf.Clamp(referenceIndex, 0, levelPaths.Length - 1);
			mobIndex = Mathf.Clamp(mobIndex, 0, levelPaths.Length - 1);
			referenceIndex = EditorGUILayout.Popup("Reference level", referenceIndex, levelNames);
			mobIndex = EditorGUILayout.Popup("Mob source level", mobIndex, levelNames);
			EditorGUILayout.BeginHorizontal();
			GUI.enabled = !EditorApplication.isPlaying;
			if (GUILayout.Button("Extract missing"))
			{
				RunExtract(levelPaths, true);
				GUIUtility.ExitGUI();
			}
			if (GUILayout.Button("Re-extract all"))
			{
				if (EditorUtility.DisplayDialog("Re-extract kit", "Overwrite every kit prefab from the chosen levels? Levels built from them keep working (the prefab links stay).", "Overwrite", "Cancel"))
				{
					RunExtract(levelPaths, false);
					GUIUtility.ExitGUI();
				}
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();

			Header("Kit contents");
			foreach (GBKitItem item in GBLevelKit.Items)
			{
				bool exists = item.Exists;
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(exists ? "OK" : (item.required ? "MISSING" : "missing"), GUILayout.Width(60f));
				GUILayout.Label(item.prefabName, EditorStyles.boldLabel, GUILayout.Width(130f));
				GUILayout.FlexibleSpace();
				GUI.enabled = exists && !EditorApplication.isPlaying;
				if (GUILayout.Button("Place", GUILayout.Width(50f)))
				{
					string captured = item.prefabName;
					GBPlacementTool.PlaceAtPivot(captured, delegate { return GBLevelKit.Spawn(captured, captured, Vector3.zero, Quaternion.identity, true); });
				}
				GUI.enabled = true;
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.LabelField(item.description, EditorStyles.wordWrappedMiniLabel);
			}
		}

		void RunExtract(string[] levelPaths, bool onlyMissing)
		{
			List<string> notFound = GBLevelKit.Extract(levelPaths[referenceIndex], levelPaths[mobIndex], onlyMissing);
			issues = null;
			palette = null;
			if (notFound.Count > 0)
			{
				EditorUtility.DisplayDialog("Kit", "Could not find: " + string.Join(", ", notFound.ToArray())
					+ "\n\nTry another reference level (Ring has players 1-4 and every system; Sandbox has mobs).", "OK");
			}
		}

		// ---- Check ------------------------------------------------------------------------

		void CheckTab()
		{
			string scenePath = EditorApplication.currentScene;
			string levelName = string.IsNullOrEmpty(scenePath) ? string.Empty : GBEditorUtil.SceneName(scenePath);
			if (issues != null && issuesScene != scenePath)
			{
				issues = null;
			}
			EditorGUILayout.LabelField("Scene", string.IsNullOrEmpty(scenePath) ? "(unsaved)" : scenePath);
			if (GUILayout.Button("Run checks", GUILayout.Height(28f)))
			{
				RunChecks();
			}
			if (issues != null)
			{
				foreach (GBIssue issue in issues)
				{
					EditorGUILayout.HelpBox(issue.message, issue.severity);
					if (issue.fix != null)
					{
						EditorGUILayout.BeginHorizontal();
						GUILayout.FlexibleSpace();
						GUI.enabled = !EditorApplication.isPlaying;
						if (GUILayout.Button(issue.fixLabel, GUILayout.Width(200f)))
						{
							issue.fix();
							RunChecks();
							GUIUtility.ExitGUI();
						}
						GUI.enabled = true;
						EditorGUILayout.EndHorizontal();
					}
				}
			}

			Header("Menu and build");
			GUI.enabled = !string.IsNullOrEmpty(scenePath) && !EditorApplication.isPlaying;
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add to Build Settings"))
			{
				GBLevelRegistry.AddToBuildSettings(scenePath);
				issues = null;
			}
			if (GUILayout.Button("Remove from Build Settings"))
			{
				GBLevelRegistry.RemoveFromBuildSettings(scenePath);
				issues = null;
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add to Menu list"))
			{
				GBLevelRegistry.EditMenuLists(levelName, GBLevelRegistry.Add, GBLevelRegistry.Leave);
				issues = null;
				GUIUtility.ExitGUI();
			}
			if (GUILayout.Button("Remove from Menu lists"))
			{
				GBLevelRegistry.EditMenuLists(levelName, GBLevelRegistry.Remove, GBLevelRegistry.Remove);
				issues = null;
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add to Rezzed rotation"))
			{
				GBLevelRegistry.EditMenuLists(levelName, GBLevelRegistry.Leave, GBLevelRegistry.Add);
				issues = null;
				GUIUtility.ExitGUI();
			}
			if (GUILayout.Button("Bake NavMesh"))
			{
				int n = GBObjectFactory.BakeNavMesh();
				Debug.Log(GBEditorUtil.LogPrefix + "NavMesh baked; " + n + " object(s) newly marked Navigation Static.");
			}
			EditorGUILayout.EndHorizontal();
			GUI.enabled = true;
			EditorGUILayout.LabelField("Menu levels", string.Join(", ", GBLevelRegistry.PeekMenuList("levelList")), EditorStyles.wordWrappedLabel);
			EditorGUILayout.LabelField("Rezzed rotation", string.Join(", ", GBLevelRegistry.PeekMenuList("rezzedLevelList")), EditorStyles.wordWrappedLabel);
			EditorGUILayout.LabelField("Build levels", string.Join(", ", GBLevelRegistry.BuildLevelNames()), EditorStyles.wordWrappedLabel);

			Header("Testing");
			EditorGUILayout.HelpBox("Press Play in this scene to test it; its own Scene Manager sets the player count. Controls are bound to controllers by default. Press Tab in the game to open the cInput window and bind keyboard keys (P1 Up/Down/Left/Right, Jump, Left/Right Punch & Grab, Duck, Action, Gesture). R restarts the level, Esc returns to the menu, F shows the frame rate, - and = change the time scale.", MessageType.Info);
		}

		void RunChecks()
		{
			issues = GBLevelValidator.Check();
			issuesScene = EditorApplication.currentScene;
			Repaint();
		}
	}
}
