// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// Makes a level reachable from the game: the scene must be in Build Settings (so
// Application.LoadLevel can find it) and its name must be in SceneManager.levelList on the
// Menu scene's "Scene Manager" object (that is the list the menu's "Level:" button cycles).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public static class GBLevelRegistry
	{
		public const int Leave = 0;
		public const int Add = 1;
		public const int Remove = 2;

		// ---- scenes -----------------------------------------------------------------

		public static string[] AllScenePaths()
		{
			List<string> list = new List<string>();
			foreach (string p in AssetDatabase.GetAllAssetPaths())
			{
				if (p.StartsWith(GBEditorUtil.ScenesFolder + "/") && p.EndsWith(".unity"))
				{
					list.Add(p);
				}
			}
			list.Sort(System.StringComparer.OrdinalIgnoreCase);
			return list.ToArray();
		}

		/// <summary>Intro and Menu are not levels.</summary>
		public static bool IsSystemScene(string scenePath)
		{
			string n = GBEditorUtil.SceneName(scenePath);
			return n == "Menu" || n == "Intro";
		}

		public static string[] LevelScenePaths()
		{
			List<string> list = new List<string>();
			foreach (string p in AllScenePaths())
			{
				if (!IsSystemScene(p))
				{
					list.Add(p);
				}
			}
			return list.ToArray();
		}

		public static string[] SceneNames(string[] scenePaths)
		{
			string[] names = new string[scenePaths.Length];
			for (int i = 0; i < scenePaths.Length; i++)
			{
				names[i] = GBEditorUtil.SceneName(scenePaths[i]);
			}
			return names;
		}

		// ---- Build Settings ---------------------------------------------------------

		public static bool IsInBuildSettings(string scenePath)
		{
			foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
			{
				if (s.path == scenePath)
				{
					return s.enabled;
				}
			}
			return false;
		}

		public static void AddToBuildSettings(string scenePath)
		{
			List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
			for (int i = 0; i < scenes.Count; i++)
			{
				if (scenes[i].path == scenePath)
				{
					scenes[i] = new EditorBuildSettingsScene(scenePath, true);
					EditorBuildSettings.scenes = scenes.ToArray();
					return;
				}
			}
			scenes.Add(new EditorBuildSettingsScene(scenePath, true));
			EditorBuildSettings.scenes = scenes.ToArray();
		}

		public static void RemoveFromBuildSettings(string scenePath)
		{
			List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
			scenes.RemoveAll(delegate(EditorBuildSettingsScene s) { return s.path == scenePath; });
			EditorBuildSettings.scenes = scenes.ToArray();
		}

		/// <summary>Enabled level names in build order (Intro and Menu excluded).</summary>
		public static string[] BuildLevelNames()
		{
			List<string> names = new List<string>();
			foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
			{
				if (s.enabled && !IsSystemScene(s.path))
				{
					names.Add(GBEditorUtil.SceneName(s.path));
				}
			}
			return names.ToArray();
		}

		// ---- Menu level list --------------------------------------------------------

		/// <summary>
		/// Reads a string[] field of the Menu's SceneManager straight from Menu.unity. The project
		/// is text-serialized, so this shows the list without switching scenes. Read-only.
		/// </summary>
		public static string[] PeekMenuList(string fieldName)
		{
			string file = GBEditorUtil.ToAbsolute(GBEditorUtil.MenuScenePath);
			List<string> result = new List<string>();
			if (!File.Exists(file))
			{
				return result.ToArray();
			}
			string[] lines = File.ReadAllLines(file);
			for (int i = 0; i < lines.Length; i++)
			{
				string t = lines[i].Trim();
				if (t == fieldName + ":")
				{
					for (int j = i + 1; j < lines.Length; j++)
					{
						string u = lines[j].Trim();
						if (!u.StartsWith("- "))
						{
							break;
						}
						result.Add(u.Substring(2).Trim());
					}
					return result.ToArray();
				}
				if (t.StartsWith(fieldName + ": ["))
				{
					return result.ToArray(); // "levelList: []"
				}
			}
			return result.ToArray();
		}

		public static bool IsInMenuList(string levelName)
		{
			return GBEditorUtil.IndexOf(PeekMenuList("levelList"), levelName) >= 0;
		}

		public static bool IsInRezzedList(string levelName)
		{
			return GBEditorUtil.IndexOf(PeekMenuList("rezzedLevelList"), levelName) >= 0;
		}

		/// <summary>
		/// Opens Menu.unity, adds/removes the level in SceneManager.levelList and rezzedLevelList
		/// (modes: Leave, Add, Remove), saves the Menu scene and returns to the scene that was open.
		/// </summary>
		public static bool EditMenuLists(string levelName, int levelListMode, int rezzedMode)
		{
			if (EditorApplication.isPlaying)
			{
				Debug.LogWarning(GBEditorUtil.LogPrefix + "stop Play mode first.");
				return false;
			}
			if (!GBEditorUtil.AssetFileExists(GBEditorUtil.MenuScenePath))
			{
				Debug.LogError(GBEditorUtil.LogPrefix + GBEditorUtil.MenuScenePath + " not found.");
				return false;
			}
			string previous = EditorApplication.currentScene;
			bool menuWasOpen = previous == GBEditorUtil.MenuScenePath;
			if (!menuWasOpen)
			{
				if (!EditorApplication.SaveCurrentSceneIfUserWantsTo())
				{
					return false;
				}
				if (!EditorApplication.OpenScene(GBEditorUtil.MenuScenePath))
				{
					Debug.LogError(GBEditorUtil.LogPrefix + "could not open " + GBEditorUtil.MenuScenePath);
					return false;
				}
			}
			int changes = 0;
			foreach (SceneManager sm in GBEditorUtil.AllComponents<SceneManager>())
			{
				SerializedObject so = new SerializedObject(sm);
				if (ApplyMode(so.FindProperty("levelList"), levelName, levelListMode))
				{
					changes++;
				}
				if (ApplyMode(so.FindProperty("rezzedLevelList"), levelName, rezzedMode))
				{
					changes++;
				}
				so.ApplyModifiedProperties();
			}
			if (changes > 0)
			{
				EditorApplication.SaveScene(GBEditorUtil.MenuScenePath);
				Debug.Log(GBEditorUtil.LogPrefix + "Menu scene updated for level '" + levelName + "'.");
			}
			if (!menuWasOpen)
			{
				if (!string.IsNullOrEmpty(previous))
				{
					EditorApplication.OpenScene(previous);
				}
				else
				{
					EditorApplication.NewScene();
				}
			}
			return true;
		}

		static bool ApplyMode(SerializedProperty list, string name, int mode)
		{
			if (list == null || mode == Leave)
			{
				return false;
			}
			int index = -1;
			for (int i = 0; i < list.arraySize; i++)
			{
				if (list.GetArrayElementAtIndex(i).stringValue == name)
				{
					index = i;
					break;
				}
			}
			if (mode == Add && index < 0)
			{
				list.arraySize = list.arraySize + 1;
				list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = name;
				return true;
			}
			if (mode == Remove && index >= 0)
			{
				list.DeleteArrayElementAtIndex(index);
				return true;
			}
			return false;
		}
	}
}
