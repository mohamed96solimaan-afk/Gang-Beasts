// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// The shipped levels hold their props (benches, cones, trucks, punch bags...) as plain scene
// objects, not prefabs. The harvester opens a level, lists everything that looks like a prop,
// and saves the ticked ones as prefabs under Assets/LevelEditor/Props/<Level>/ so they can be
// placed in new levels. The source level is never saved.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public class GBPropCandidate
	{
		public GameObject go;
		public string name;
		public bool selected;
		public bool hasRigidbody;
		public int renderers;
		public int children;
		public bool proBuilder;
	}

	public static class GBPropHarvester
	{
		/// <summary>Root objects of the open scene that render something and are not beasts, cameras or level systems.</summary>
		public static List<GBPropCandidate> Scan()
		{
			List<GBPropCandidate> list = new List<GBPropCandidate>();
			foreach (GameObject root in GBEditorUtil.SceneRoots())
			{
				if (IsSystemObject(root))
				{
					continue;
				}
				Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
				if (renderers.Length == 0)
				{
					continue;
				}
				GBPropCandidate c = new GBPropCandidate();
				c.go = root;
				c.name = root.name;
				c.hasRigidbody = root.GetComponentsInChildren<Rigidbody>(true).Length > 0;
				c.renderers = renderers.Length;
				c.children = root.GetComponentsInChildren<Transform>(true).Length - 1;
				c.proBuilder = GBEditorUtil.HasComponentNamed(root, "pb_Object");
				c.selected = c.hasRigidbody; // physics props are the usual pick; scenery is static level geometry
				list.Add(c);
			}
			return list;
		}

		static bool IsSystemObject(GameObject go)
		{
			return go.GetComponent<Player>() != null
				|| go.GetComponent<Camera>() != null
				|| go.GetComponent<SceneManager>() != null
				|| go.GetComponent(typeof(mset.Sky)) != null
				|| go.GetComponent<Music>() != null
				|| go.GetComponent<HUDFPS>() != null
				|| go.GetComponent<AudioTimeScale>() != null
				|| go.GetComponent<GUIText>() != null
				|| GBEditorUtil.HasComponentNamed(go, "GroupContainer");
		}

		public static void SelectAll(List<GBPropCandidate> candidates, bool selected)
		{
			foreach (GBPropCandidate c in candidates)
			{
				c.selected = selected;
			}
		}

		public static void SelectPhysicsOnly(List<GBPropCandidate> candidates)
		{
			foreach (GBPropCandidate c in candidates)
			{
				c.selected = c.hasRigidbody;
			}
		}

		public static int SelectedCount(List<GBPropCandidate> candidates)
		{
			int n = 0;
			foreach (GBPropCandidate c in candidates)
			{
				if (c.selected)
				{
					n++;
				}
			}
			return n;
		}

		/// <summary>Saves the ticked candidates as prefabs under Assets/LevelEditor/Props/[sourceSceneName]/.</summary>
		public static int Extract(List<GBPropCandidate> candidates, string sourceSceneName)
		{
			string folder = GBEditorUtil.PropsFolder + "/" + GBEditorUtil.SafeName(sourceSceneName);
			GBEditorUtil.EnsureFolder(folder);
			int count = 0;
			foreach (GBPropCandidate c in candidates)
			{
				if (!c.selected || c.go == null)
				{
					continue;
				}
				string name = GBEditorUtil.SafeName(c.name);
				string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".prefab");
				GBLevelKit.SaveAsPrefab(c.go, path, name);
				count++;
			}
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Debug.Log(GBEditorUtil.LogPrefix + "saved " + count + " prop prefab(s) into " + folder);
			return count;
		}

		/// <summary>Prefabs the palette offers: harvested props, the kit mob, the game's loose prefabs, dev-menu prefabs.</summary>
		public static List<string> PalettePrefabPaths()
		{
			List<string> list = new List<string>();
			foreach (string p in AssetDatabase.GetAllAssetPaths())
			{
				if (!p.EndsWith(".prefab"))
				{
					continue;
				}
				if (p.StartsWith(GBEditorUtil.PropsFolder + "/") || p.StartsWith("Assets/GameObject/")
					|| p.StartsWith("Assets/DevMenu/") || p == GBEditorUtil.KitFolder + "/Mob.prefab")
				{
					list.Add(p);
				}
			}
			list.Sort(System.StringComparer.OrdinalIgnoreCase);
			return list;
		}

		/// <summary>Instantiates a palette prefab (kept linked to its prefab).</summary>
		public static GameObject Instantiate(string prefabPath)
		{
			GameObject prefab = GBEditorUtil.LoadAsset<GameObject>(prefabPath);
			if (prefab == null)
			{
				Debug.LogError(GBEditorUtil.LogPrefix + "cannot load " + prefabPath);
				return null;
			}
			GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			go.name = prefab.name;
			Undo.RegisterCreatedObjectUndo(go, "Place " + prefab.name);
			return go;
		}
	}
}
