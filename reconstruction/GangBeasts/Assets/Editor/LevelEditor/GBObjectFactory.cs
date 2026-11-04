// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// Builds the simple things a level is made of: static geometry, physics props, trigger zones
// (KillZone / WarpZone / DontTrackZone from the game's own scripts), lights, and the NavMesh
// bake mobs need. Every object is registered with Undo.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public static class GBObjectFactory
	{
		public const string TagKillZone = "Kill Zone";
		public static readonly string[] PropTags = { "Untagged", "Prop", "Prop (Harmful)" };
		// "Ignore Ball": the beast's invisible balance ball passes through the object (small throwables).
		// "Default": the ball collides, so beasts can stand on the object.
		public static readonly string[] PropLayers = { "Default", "Ignore Ball" };

		public static Material DefaultFloorMaterial()
		{
			Material m = GBEditorUtil.LoadAsset<Material>("Assets/Material/Default Grid.mat");
			if (m == null)
			{
				m = GBEditorUtil.LoadAsset<Material>("Assets/Material/Floor.mat");
			}
			return m;
		}

		public static AudioClip DefaultKillSound()
		{
			return GBEditorUtil.LoadAsset<AudioClip>("Assets/AudioClip/Ring Bell.ogg");
		}

		public static void MakeStatic(GameObject go)
		{
			StaticEditorFlags flags = StaticEditorFlags.LightmapStatic | StaticEditorFlags.OccluderStatic
				| StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic;
			foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
			{
				GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
			}
		}

		static void Register(GameObject go)
		{
			Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
		}

		// ---- static geometry --------------------------------------------------------

		public static GameObject CreateStaticBox(string name, Vector3 center, Vector3 size, Quaternion rotation, Material material)
		{
			GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.transform.position = center;
			go.transform.rotation = rotation;
			go.transform.localScale = size;
			if (material != null)
			{
				go.GetComponent<Renderer>().sharedMaterial = material;
			}
			MakeStatic(go);
			Register(go);
			return go;
		}

		/// <summary>A floor whose top surface is at y = 0.</summary>
		public static GameObject CreateFloor(float size, float thickness, Material material)
		{
			return CreateStaticBox("Floor", new Vector3(0f, -thickness * 0.5f, 0f), new Vector3(size, thickness, size), Quaternion.identity, material);
		}

		public static GameObject CreatePlatform(Vector3 size, Material material)
		{
			return CreateStaticBox("Platform", Vector3.zero, size, Quaternion.identity, material);
		}

		/// <summary>A box tilted around X; angle in degrees.</summary>
		public static GameObject CreateRamp(float length, float width, float angleDegrees, Material material)
		{
			GameObject go = CreateStaticBox("Ramp", Vector3.zero, new Vector3(width, 0.5f, length), Quaternion.Euler(-angleDegrees, 0f, 0f), material);
			return go;
		}

		/// <summary>Four walls around a square floor of the given size, grouped under "Walls".</summary>
		public static GameObject CreateWalls(float floorSize, float height, float thickness, Material material)
		{
			GameObject group = new GameObject("Walls");
			Register(group);
			float half = floorSize * 0.5f + thickness * 0.5f;
			float y = height * 0.5f;
			Vector3[] centers =
			{
				new Vector3(0f, y, half), new Vector3(0f, y, -half), new Vector3(half, y, 0f), new Vector3(-half, y, 0f)
			};
			Vector3[] sizes =
			{
				new Vector3(floorSize + 2f * thickness, height, thickness), new Vector3(floorSize + 2f * thickness, height, thickness),
				new Vector3(thickness, height, floorSize), new Vector3(thickness, height, floorSize)
			};
			for (int i = 0; i < 4; i++)
			{
				GameObject wall = CreateStaticBox("Wall " + (i + 1), centers[i], sizes[i], Quaternion.identity, material);
				wall.transform.parent = group.transform;
			}
			return group;
		}

		// ---- physics props ----------------------------------------------------------

		public static GameObject CreatePhysicsProp(PrimitiveType type, Vector3 size, float mass, string tag, string layer, Material material, PhysicMaterial physicMaterial)
		{
			GameObject go = GameObject.CreatePrimitive(type);
			go.name = type.ToString();
			go.transform.localScale = size;
			Rigidbody rb = go.AddComponent<Rigidbody>();
			rb.mass = mass;
			if (!string.IsNullOrEmpty(tag) && tag != "Untagged")
			{
				go.tag = tag; // "Prop" / "Prop (Harmful)" exist in the Tag Manager
			}
			int layerIndex = LayerMask.NameToLayer(layer);
			if (layerIndex >= 0)
			{
				go.layer = layerIndex;
			}
			if (material != null)
			{
				go.GetComponent<Renderer>().sharedMaterial = material;
			}
			if (physicMaterial != null)
			{
				go.GetComponent<Collider>().sharedMaterial = physicMaterial;
			}
			Register(go);
			return go;
		}

		// ---- zones ------------------------------------------------------------------

		static GameObject CreateTriggerBox(string name, Vector3 center, Vector3 size)
		{
			GameObject go = new GameObject(name);
			go.transform.position = center;
			BoxCollider box = go.AddComponent<BoxCollider>();
			box.isTrigger = true;
			box.size = size;
			Register(go);
			return go;
		}

		/// <summary>Players touching it die (lose a life) after killDelay seconds. Put one under the level.</summary>
		public static GameObject CreateKillZone(Vector3 center, Vector3 size, AudioClip sound, float killDelay)
		{
			GameObject go = CreateTriggerBox("Kill Zone", center, size);
			go.tag = TagKillZone;
			KillZone zone = go.AddComponent<KillZone>();
			zone.killDelay = killDelay;
			zone.killSound = sound;
			return go;
		}

		/// <summary>A player touching it restarts the level (the game's WarpZone script).</summary>
		public static GameObject CreateWarpZone(Vector3 center, Vector3 size)
		{
			GameObject go = CreateTriggerBox("Warp Zone", center, size);
			go.AddComponent<WarpZone>();
			return go;
		}

		/// <summary>Players inside are ignored by the camera framing and by mob targeting.</summary>
		public static GameObject CreateDontTrackZone(Vector3 center, Vector3 size)
		{
			GameObject go = CreateTriggerBox("Dont Track Zone", center, size);
			go.AddComponent<DontTrackZone>();
			return go;
		}

		// ---- lights -----------------------------------------------------------------

		public static GameObject CreateLight(LightType type, Vector3 position, Color color, float intensity, float range, float spotAngle)
		{
			GameObject go = new GameObject(type + " Light");
			go.transform.position = position;
			Light light = go.AddComponent<Light>();
			light.type = type;
			light.color = color;
			light.intensity = intensity;
			light.range = range;
			light.spotAngle = spotAngle;
			if (type == LightType.Spot || type == LightType.Directional)
			{
				go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // point down
			}
			Register(go);
			return go;
		}

		// ---- NavMesh ----------------------------------------------------------------

		static bool IsMoving(GameObject go)
		{
			for (Transform t = go.transform; t != null; t = t.parent)
			{
				if (t.GetComponent<Rigidbody>() != null || t.GetComponent<Player>() != null || t.GetComponent<Camera>() != null)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Marks every non-moving mesh Navigation Static and bakes the NavMesh. Returns how many objects were marked.</summary>
		public static int BakeNavMesh()
		{
			int marked = 0;
			foreach (MeshRenderer r in GBEditorUtil.AllComponents<MeshRenderer>())
			{
				GameObject go = r.gameObject;
				if (!go.activeInHierarchy || IsMoving(go))
				{
					continue;
				}
				StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
				if ((flags & StaticEditorFlags.NavigationStatic) == 0)
				{
					GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.NavigationStatic);
					marked++;
				}
			}
			NavMeshBuilder.BuildNavMesh();
			return marked;
		}
	}
}
