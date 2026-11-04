// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// Shared helpers. Everything in Assets/Editor/LevelEditor is editor-only: it compiles into
// Assembly-CSharp-Editor, so the four reconstructed runtime assemblies (and src/) stay untouched
// and the byte-exact proof still holds. Only Unity 4.3 APIs and C# 3 syntax are used.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public static class GBEditorUtil
	{
		public const string RootFolder = "Assets/LevelEditor";
		public const string KitFolder = "Assets/LevelEditor/Kit";
		public const string PropsFolder = "Assets/LevelEditor/Props";
		public const string ScenesFolder = "Assets/Scenes";
		public const string MenuScenePath = "Assets/Scenes/Menu.unity";
		public const string IntroScenePath = "Assets/Scenes/Intro.unity";
		public const string LogPrefix = "GB Level Editor: ";

		/// <summary>Absolute path of the project folder (the parent of Assets).</summary>
		public static string ProjectRoot
		{
			get { return Directory.GetParent(Application.dataPath).FullName; }
		}

		public static string ToAbsolute(string assetPath)
		{
			return Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
		}

		public static bool AssetFileExists(string assetPath)
		{
			return File.Exists(ToAbsolute(assetPath));
		}

		public static bool AssetFolderExists(string assetPath)
		{
			return Directory.Exists(ToAbsolute(assetPath));
		}

		/// <summary>Creates every missing folder of an "Assets/..." path through the AssetDatabase.</summary>
		public static void EnsureFolder(string assetPath)
		{
			string[] parts = assetPath.Split('/');
			string current = parts[0];
			for (int i = 1; i < parts.Length; i++)
			{
				string next = current + "/" + parts[i];
				if (!Directory.Exists(ToAbsolute(next)))
				{
					AssetDatabase.CreateFolder(current, parts[i]);
				}
				current = next;
			}
		}

		/// <summary>The name Application.LoadLevel uses: the file name without folder or extension.</summary>
		public static string SceneName(string scenePath)
		{
			return Path.GetFileNameWithoutExtension(scenePath);
		}

		/// <summary>Replaces characters that cannot be used in a file name. Spaces are kept.</summary>
		public static string SafeName(string name)
		{
			if (name == null)
			{
				return string.Empty;
			}
			char[] invalid = Path.GetInvalidFileNameChars();
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (char c in name.Trim())
			{
				bool bad = c == '/' || c == '\\' || System.Array.IndexOf(invalid, c) >= 0;
				sb.Append(bad ? '_' : c);
			}
			return sb.ToString();
		}

		/// <summary>Root GameObjects of the open scene, active or not, without editor-internal objects.</summary>
		public static List<GameObject> SceneRoots()
		{
			List<GameObject> roots = new List<GameObject>();
			GameObject[] all = (GameObject[])Resources.FindObjectsOfTypeAll(typeof(GameObject));
			foreach (GameObject go in all)
			{
				if (go == null || go.transform.parent != null)
				{
					continue;
				}
				if (EditorUtility.IsPersistent(go))
				{
					continue; // prefab and model assets
				}
				if ((go.hideFlags & HideFlags.HideInHierarchy) != 0 || (go.hideFlags & HideFlags.DontSave) != 0)
				{
					continue; // scene view cameras, preview objects
				}
				roots.Add(go);
			}
			roots.Sort(delegate(GameObject a, GameObject b)
			{
				return string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
			});
			return roots;
		}

		/// <summary>Components of type T sitting directly on the roots of the open scene.</summary>
		public static List<T> RootComponents<T>() where T : Component
		{
			List<T> result = new List<T>();
			foreach (GameObject root in SceneRoots())
			{
				T c = root.GetComponent<T>();
				if (c != null)
				{
					result.Add(c);
				}
			}
			return result;
		}

		/// <summary>Components of type T anywhere in the open scene, inactive objects included.</summary>
		public static List<T> AllComponents<T>() where T : Component
		{
			List<T> result = new List<T>();
			foreach (GameObject root in SceneRoots())
			{
				result.AddRange(root.GetComponentsInChildren<T>(true));
			}
			return result;
		}

		/// <summary>True when the GameObject carries a component whose class is called typeName.</summary>
		public static bool HasComponentNamed(GameObject go, string typeName)
		{
			foreach (Component c in go.GetComponents<Component>())
			{
				if (c != null && c.GetType().Name == typeName)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>World-space bounds of all renderers (or, failing that, colliders) under a GameObject.</summary>
		public static Bounds ObjectBounds(GameObject go)
		{
			Bounds b = new Bounds(go.transform.position, Vector3.zero);
			bool has = false;
			foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
			{
				if (!has)
				{
					b = r.bounds;
					has = true;
				}
				else
				{
					b.Encapsulate(r.bounds);
				}
			}
			if (!has)
			{
				foreach (Collider c in go.GetComponentsInChildren<Collider>())
				{
					if (!has)
					{
						b = c.bounds;
						has = true;
					}
					else
					{
						b.Encapsulate(c.bounds);
					}
				}
			}
			return b;
		}

		public static Vector3 Snap(Vector3 v, float grid)
		{
			if (grid <= 0f)
			{
				return v;
			}
			return new Vector3(Mathf.Round(v.x / grid) * grid, Mathf.Round(v.y / grid) * grid, Mathf.Round(v.z / grid) * grid);
		}

		/// <summary>Draws a wire box in the Scene view (Unity 4.3 has no Handles.DrawWireCube).</summary>
		public static void DrawWireBox(Transform t, Vector3 center, Vector3 size, Color color)
		{
			Vector3 e = size * 0.5f;
			Vector3[] p = new Vector3[8];
			int i = 0;
			for (int x = -1; x <= 1; x += 2)
			{
				for (int y = -1; y <= 1; y += 2)
				{
					for (int z = -1; z <= 1; z += 2)
					{
						p[i++] = t.TransformPoint(center + new Vector3(e.x * x, e.y * y, e.z * z));
					}
				}
			}
			// Corner index bits: x = 4, y = 2, z = 1. An edge joins two corners that differ in one bit.
			int[] a = { 0, 0, 0, 1, 1, 2, 2, 3, 4, 4, 5, 6 };
			int[] b = { 1, 2, 4, 3, 5, 3, 6, 7, 5, 6, 7, 7 };
			Color old = Handles.color;
			Handles.color = color;
			for (int k = 0; k < 12; k++)
			{
				Handles.DrawLine(p[a[k]], p[b[k]]);
			}
			Handles.color = old;
		}

		public static int IndexOf(string[] options, string value)
		{
			for (int i = 0; i < options.Length; i++)
			{
				if (options[i] == value)
				{
					return i;
				}
			}
			return -1;
		}

		public static T LoadAsset<T>(string assetPath) where T : UnityEngine.Object
		{
			return AssetDatabase.LoadAssetAtPath(assetPath, typeof(T)) as T;
		}

		/// <summary>Finds the first child (any depth) with the given name.</summary>
		public static Transform FindDeep(Transform root, string name)
		{
			foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
			{
				if (t.name == name)
				{
					return t;
				}
			}
			return null;
		}
	}
}
