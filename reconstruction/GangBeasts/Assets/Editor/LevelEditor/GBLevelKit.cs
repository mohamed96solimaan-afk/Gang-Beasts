// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// The "level kit": the objects every level needs (the four player beasts, a mob beast, the
// Scene Manager, the follow camera, the Marmoset sky, music, the FPS counter, the sun light).
// None of them is a prefab in the shipped game; they live as plain objects in each scene. The
// kit extracts them once from a reference level (Ring by default, Sandbox for the mob) into
// Assets/LevelEditor/Kit/*.prefab, and CreateLevel assembles a complete playable scene from them.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public class GBKitItem
	{
		public string prefabName;
		public string description;
		public bool required;
		public bool fromMobScene; // taken from the mob source level instead of the reference level
		public System.Func<GameObject, bool> match;

		public string PrefabPath
		{
			get { return GBEditorUtil.KitFolder + "/" + prefabName + ".prefab"; }
		}

		public bool Exists
		{
			get { return GBEditorUtil.AssetFileExists(PrefabPath); }
		}

		public GameObject Load()
		{
			return GBEditorUtil.LoadAsset<GameObject>(PrefabPath);
		}
	}

	/// <summary>Everything the "New Level" tab asks for.</summary>
	public class GBNewLevelOptions
	{
		public string levelName = "My Level";
		public int players = 4;
		public float spawnRadius = 3f;
		public float spawnHeight = 1.1f;
		public int mobs = 0;
		public float floorSize = 20f;
		public float floorThickness = 1f;
		public Material floorMaterial;
		public bool addWalls = false;
		public float wallHeight = 2f;
		public bool addKillZone = true;
		public float killZoneDepth = -6f;
		public float killZoneSize = 200f;
		public AudioClip killSound;
		public bool addSky = true;
		public bool addMusic = true;
		public AudioClip music;
		public bool addFpsCounter = true;
		public bool addAudioTimeScale = true;
		public int totalPlayers = 4;
		public int startingLives = 5;
		public bool addToBuildSettings = true;
		public bool addToMenuList = true;
		public bool addToRezzedList = false;
		public bool keepPrefabLinks = true;
	}

	public static class GBLevelKit
	{
		public const string DefaultReferenceScene = "Assets/Scenes/Ring.unity";
		public const string DefaultMobScene = "Assets/Scenes/Sandbox.unity";
		public static readonly Vector3 CameraOffset = new Vector3(0f, 8f, 10f); // SceneCamera.offset in the shipped levels

		public static readonly GBKitItem[] Items = new GBKitItem[]
		{
			Item("Player 1", "Beast for player 1 (red gang). Full active ragdoll with every gameplay script.", true, false,
				delegate(GameObject g) { return IsHuman(g, 0); }),
			Item("Player 2", "Beast for player 2 (blue gang).", true, false,
				delegate(GameObject g) { return IsHuman(g, 1); }),
			Item("Player 3", "Beast for player 3 (green gang).", true, false,
				delegate(GameObject g) { return IsHuman(g, 2); }),
			Item("Player 4", "Beast for player 4 (yellow gang).", true, false,
				delegate(GameObject g) { return IsHuman(g, 3); }),
			Item("Mob", "AI beast (Player.mob = true, index 4). Chases the nearest player; needs a baked NavMesh.", false, true,
				delegate(GameObject g) { return IsMob(g); }),
			Item("Scene Manager", "Lives, scores, player count and the controller bindings. Tag 'Scene Manager'.", true, false,
				delegate(GameObject g) { return g.GetComponent<SceneManager>() != null; }),
			Item("Main Camera", "SceneCamera follow camera with the image effects the levels use (R restarts, Esc = menu).", true, false,
				delegate(GameObject g) { return g.GetComponent<Camera>() != null && g.GetComponent<SceneCamera>() != null; }),
			Item("Sky", "Marmoset Skyshop sky. Every Marmoset material reads its lighting from this object.", true, false,
				delegate(GameObject g) { return g.GetComponent(typeof(mset.Sky)) != null; }),
			Item("Music", "Looping level music (follows the menu's mute setting).", false, false,
				delegate(GameObject g) { return g.GetComponent<Music>() != null; }),
			Item("AudioTimeScale", "Slow-motion keys (- and =) that also pitch the audio.", false, false,
				delegate(GameObject g) { return g.GetComponent<AudioTimeScale>() != null; }),
			Item("Framerate Counter", "FPS counter toggled with F.", false, false,
				delegate(GameObject g) { return g.GetComponent<HUDFPS>() != null; }),
			Item("Directional Light", "The level's sun.", true, false,
				delegate(GameObject g) { Light l = g.GetComponent<Light>(); return l != null && l.type == LightType.Directional; }),
		};

		static GBKitItem Item(string name, string description, bool required, bool fromMobScene, System.Func<GameObject, bool> match)
		{
			GBKitItem item = new GBKitItem();
			item.prefabName = name;
			item.description = description;
			item.required = required;
			item.fromMobScene = fromMobScene;
			item.match = match;
			return item;
		}

		static bool IsHuman(GameObject g, int index)
		{
			Player p = g.GetComponent<Player>();
			return p != null && !p.mob && p.player == index;
		}

		static bool IsMob(GameObject g)
		{
			Player p = g.GetComponent<Player>();
			return p != null && p.mob;
		}

		public static GBKitItem Find(string prefabName)
		{
			foreach (GBKitItem item in Items)
			{
				if (item.prefabName == prefabName)
				{
					return item;
				}
			}
			return null;
		}

		public static List<GBKitItem> Missing(bool requiredOnly)
		{
			List<GBKitItem> list = new List<GBKitItem>();
			foreach (GBKitItem item in Items)
			{
				if ((!requiredOnly || item.required) && !item.Exists)
				{
					list.Add(item);
				}
			}
			return list;
		}

		public static bool Ready
		{
			get { return Missing(true).Count == 0; }
		}

		// ---- extraction -------------------------------------------------------------

		/// <summary>
		/// Extracts the kit prefabs from the reference level (and the mob from the mob level).
		/// The source scenes are opened and left again without saving, so the shipped levels
		/// are never modified. Returns the names that could not be found.
		/// </summary>
		public static List<string> Extract(string referenceScene, string mobScene, bool onlyMissing)
		{
			List<string> notFound = new List<string>();
			List<string> made = new List<string>();
			if (EditorApplication.isPlaying)
			{
				Debug.LogWarning(GBEditorUtil.LogPrefix + "stop Play mode first.");
				notFound.Add("(play mode)");
				return notFound;
			}
			if (!EditorApplication.SaveCurrentSceneIfUserWantsTo())
			{
				notFound.Add("(cancelled)");
				return notFound;
			}
			string previous = EditorApplication.currentScene;
			GBEditorUtil.EnsureFolder(GBEditorUtil.KitFolder);
			ExtractFrom(referenceScene, false, onlyMissing, made, notFound);
			ExtractFrom(mobScene, true, onlyMissing, made, notFound);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			if (!string.IsNullOrEmpty(previous))
			{
				EditorApplication.OpenScene(previous);
			}
			else
			{
				EditorApplication.NewScene();
			}
			string report = GBEditorUtil.LogPrefix + "extracted " + made.Count + " kit prefab(s) into " + GBEditorUtil.KitFolder;
			if (notFound.Count > 0)
			{
				report += ". Not found: " + string.Join(", ", notFound.ToArray());
			}
			Debug.Log(report);
			return notFound;
		}

		static void ExtractFrom(string scenePath, bool mobItems, bool onlyMissing, List<string> made, List<string> notFound)
		{
			List<GBKitItem> todo = new List<GBKitItem>();
			foreach (GBKitItem item in Items)
			{
				if (item.fromMobScene == mobItems && (!onlyMissing || !item.Exists))
				{
					todo.Add(item);
				}
			}
			if (todo.Count == 0)
			{
				return;
			}
			if (string.IsNullOrEmpty(scenePath) || !EditorApplication.OpenScene(scenePath))
			{
				foreach (GBKitItem item in todo)
				{
					notFound.Add(item.prefabName + " (could not open " + scenePath + ")");
				}
				return;
			}
			List<GameObject> roots = GBEditorUtil.SceneRoots();
			foreach (GBKitItem item in todo)
			{
				GameObject source = FindMatch(roots, item, true);
				if (source == null)
				{
					source = FindMatch(roots, item, false);
				}
				if (source == null)
				{
					notFound.Add(item.prefabName);
					continue;
				}
				SaveAsPrefab(source, item.PrefabPath, item.prefabName);
				made.Add(item.prefabName);
			}
		}

		static GameObject FindMatch(List<GameObject> roots, GBKitItem item, bool activeOnly)
		{
			foreach (GameObject root in roots)
			{
				if (activeOnly && !root.activeSelf)
				{
					continue;
				}
				if (item.match(root))
				{
					return root;
				}
			}
			return null;
		}

		/// <summary>Saves a scene object as a prefab without touching the original (works on a temporary copy).</summary>
		public static GameObject SaveAsPrefab(GameObject source, string path, string name)
		{
			GameObject copy = (GameObject)UnityEngine.Object.Instantiate(source);
			copy.name = name;
			copy.SetActive(true);
			UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
			GameObject prefab;
			if (existing != null)
			{
				prefab = PrefabUtility.ReplacePrefab(copy, existing, ReplacePrefabOptions.ReplaceNameBased);
			}
			else
			{
				prefab = PrefabUtility.CreatePrefab(path, copy);
			}
			UnityEngine.Object.DestroyImmediate(copy);
			return prefab;
		}

		// ---- placing ----------------------------------------------------------------

		/// <summary>Instantiates a kit prefab into the open scene.</summary>
		public static GameObject Spawn(string prefabName, string instanceName, Vector3 position, Quaternion rotation, bool keepPrefabLink)
		{
			GBKitItem item = Find(prefabName);
			GameObject prefab = item != null ? item.Load() : null;
			if (prefab == null)
			{
				Debug.LogError(GBEditorUtil.LogPrefix + "kit prefab '" + prefabName + "' is missing. Extract the kit first (Kit tab).");
				return null;
			}
			GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			go.name = instanceName;
			go.transform.position = position;
			go.transform.rotation = rotation;
			if (!keepPrefabLink)
			{
				PrefabUtility.DisconnectPrefabInstance(go);
			}
			Undo.RegisterCreatedObjectUndo(go, "Add " + instanceName);
			return go;
		}

		/// <summary>Yaw-only rotation that looks from position towards center.</summary>
		public static Quaternion FaceCenter(Vector3 position, Vector3 center)
		{
			Vector3 d = center - position;
			d.y = 0f;
			return d.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(d.normalized);
		}

		/// <summary>Point index of count on a circle; the first four sit on the corners of a square.</summary>
		public static Vector3 CirclePoint(Vector3 center, float radius, float height, int index, int count)
		{
			float angle = (count > 0 ? 360f * index / count : 0f) + 45f;
			float rad = angle * Mathf.Deg2Rad;
			return center + new Vector3(Mathf.Sin(rad) * radius, height, Mathf.Cos(rad) * radius);
		}

		public static GameObject SpawnPlayer(int index, Vector3 position, Vector3 faceTowards, bool keepPrefabLink)
		{
			string name = "Player " + (index + 1);
			return Spawn(name, name, position, FaceCenter(position, faceTowards), keepPrefabLink);
		}

		public static GameObject SpawnMob(int number, Vector3 position, Vector3 faceTowards, bool keepPrefabLink)
		{
			return Spawn("Mob", "Mob " + number, position, FaceCenter(position, faceTowards), keepPrefabLink);
		}

		// ---- new level ----------------------------------------------------------------

		/// <summary>Builds a complete, playable level and saves it as Assets/Scenes/[name].unity.</summary>
		public static bool CreateLevel(GBNewLevelOptions o, out string scenePath)
		{
			scenePath = null;
			if (EditorApplication.isPlaying)
			{
				EditorUtility.DisplayDialog("Level Editor", "Stop Play mode first.", "OK");
				return false;
			}
			string name = GBEditorUtil.SafeName(o.levelName);
			if (name.Length == 0)
			{
				EditorUtility.DisplayDialog("Level Editor", "Enter a level name first.", "OK");
				return false;
			}
			if (name == "Menu" || name == "Intro" || name == "Main Menu")
			{
				EditorUtility.DisplayDialog("Level Editor", "'" + name + "' is reserved by the game. Pick another name.", "OK");
				return false;
			}
			scenePath = GBEditorUtil.ScenesFolder + "/" + name + ".unity";
			if (GBEditorUtil.AssetFileExists(scenePath))
			{
				if (!EditorUtility.DisplayDialog("Level Editor", "A scene called '" + name + "' already exists.\n\nReplace it? The old scene is lost.", "Replace", "Cancel"))
				{
					return false;
				}
			}
			if (!Ready)
			{
				if (!EditorUtility.DisplayDialog("Level Editor",
					"The level kit prefabs have not been extracted yet.\n\nExtract them now from " + GBEditorUtil.SceneName(DefaultReferenceScene)
					+ " (players, camera, sky, lights) and " + GBEditorUtil.SceneName(DefaultMobScene) + " (mob)?", "Extract", "Cancel"))
				{
					return false;
				}
				Extract(DefaultReferenceScene, DefaultMobScene, true);
				if (!Ready)
				{
					EditorUtility.DisplayDialog("Level Editor", "Some required kit prefabs are still missing. See the Console and the Kit tab.", "OK");
					return false;
				}
			}
			if (!EditorApplication.SaveCurrentSceneIfUserWantsTo())
			{
				return false;
			}

			EditorApplication.NewScene();
			foreach (GameObject root in GBEditorUtil.SceneRoots())
			{
				UnityEngine.Object.DestroyImmediate(root); // the default camera of a new scene
			}
			Vector3 center = Vector3.zero;

			// Systems.
			GameObject manager = Spawn("Scene Manager", "Scene Manager", Vector3.zero, Quaternion.identity, o.keepPrefabLinks);
			if (manager != null)
			{
				SceneManager sm = manager.GetComponent<SceneManager>();
				sm.totalPlayers = Mathf.Clamp(o.totalPlayers, 1, 4);
				sm.startingLives = Mathf.Max(1, o.startingLives);
				List<string> levels = new List<string>(GBLevelRegistry.BuildLevelNames());
				if (!levels.Contains(name))
				{
					levels.Add(name);
				}
				sm.levelList = levels.ToArray();
				sm.levelSelection = levels.IndexOf(name);
				EditorUtility.SetDirty(sm);
			}
			Vector3 cameraPosition = center + CameraOffset;
			Spawn("Main Camera", "Main Camera", cameraPosition, Quaternion.LookRotation(center - cameraPosition), o.keepPrefabLinks);
			if (o.addSky)
			{
				Spawn("Sky", "Sky", Vector3.zero, Quaternion.identity, o.keepPrefabLinks);
			}
			if (o.addMusic)
			{
				GameObject music = Spawn("Music", "Music", Vector3.zero, Quaternion.identity, o.keepPrefabLinks);
				if (music != null && o.music != null)
				{
					AudioSource source = music.GetComponent<AudioSource>();
					if (source != null)
					{
						source.clip = o.music;
						EditorUtility.SetDirty(source);
					}
				}
			}
			if (o.addAudioTimeScale)
			{
				Spawn("AudioTimeScale", "AudioTimeScale", Vector3.zero, Quaternion.identity, o.keepPrefabLinks);
			}
			if (o.addFpsCounter)
			{
				Spawn("Framerate Counter", "Framerate Counter", Vector3.zero, Quaternion.identity, o.keepPrefabLinks);
			}
			GBKitItem sun = Find("Directional Light");
			GameObject sunPrefab = sun != null ? sun.Load() : null;
			Spawn("Directional Light", "Directional Light", new Vector3(0f, 10f, 0f), sunPrefab != null ? sunPrefab.transform.rotation : Quaternion.Euler(50f, -30f, 0f), o.keepPrefabLinks);

			// Geometry.
			Material floorMaterial = o.floorMaterial != null ? o.floorMaterial : GBObjectFactory.DefaultFloorMaterial();
			GBObjectFactory.CreateFloor(o.floorSize, o.floorThickness, floorMaterial);
			if (o.addWalls)
			{
				GBObjectFactory.CreateWalls(o.floorSize, o.wallHeight, 0.5f, floorMaterial);
			}
			if (o.addKillZone)
			{
				GBObjectFactory.CreateKillZone(new Vector3(0f, o.killZoneDepth, 0f), new Vector3(o.killZoneSize, 2f, o.killZoneSize), o.killSound, 0f);
			}

			// Beasts.
			int players = Mathf.Clamp(o.players, 1, 4);
			for (int i = 0; i < players; i++)
			{
				SpawnPlayer(i, CirclePoint(center, o.spawnRadius, o.spawnHeight, i, players), center, o.keepPrefabLinks);
			}
			for (int i = 0; i < o.mobs; i++)
			{
				SpawnMob(i + 1, CirclePoint(center, o.spawnRadius * 2f, o.spawnHeight, i, o.mobs), center, o.keepPrefabLinks);
			}

			RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.2f);
			RenderSettings.fog = false;

			if (!EditorApplication.SaveScene(scenePath))
			{
				EditorUtility.DisplayDialog("Level Editor", "Could not save " + scenePath, "OK");
				return false;
			}
			if (o.addToBuildSettings)
			{
				GBLevelRegistry.AddToBuildSettings(scenePath);
			}
			if (o.addToMenuList || o.addToRezzedList)
			{
				GBLevelRegistry.EditMenuLists(name, o.addToMenuList ? GBLevelRegistry.Add : GBLevelRegistry.Leave,
					o.addToRezzedList ? GBLevelRegistry.Add : GBLevelRegistry.Leave);
			}
			// The menu edit above reloads the scene, so look the floor up again instead of keeping a reference.
			GameObject floorAgain = GameObject.Find("Floor");
			if (floorAgain != null)
			{
				Selection.activeGameObject = floorAgain;
			}
			if (SceneView.lastActiveSceneView != null)
			{
				SceneView.lastActiveSceneView.LookAt(center);
			}
			Debug.Log(GBEditorUtil.LogPrefix + "created " + scenePath);
			return true;
		}
	}
}
