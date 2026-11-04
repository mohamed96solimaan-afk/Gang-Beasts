// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// Checks that the open scene has everything the game needs to run it as a level, and offers
// one-click fixes. The rules come from the game's own scripts: SceneManager (tag "Scene Manager",
// lives/scores), SceneCamera (follows objects tagged Player), Player.Start (destroys beasts whose
// index is >= totalPlayers), KillZone (kills beasts that fall off), mset.Sky (Marmoset lighting).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public class GBIssue
	{
		public MessageType severity;
		public string message;
		public string fixLabel;
		public System.Action fix;
	}

	public static class GBLevelValidator
	{
		public static List<GBIssue> Check()
		{
			List<GBIssue> issues = new List<GBIssue>();
			string scenePath = EditorApplication.currentScene;
			string levelName = string.IsNullOrEmpty(scenePath) ? string.Empty : GBEditorUtil.SceneName(scenePath);

			if (string.IsNullOrEmpty(scenePath))
			{
				Add(issues, MessageType.Error, "The scene has not been saved yet. Save it into Assets/Scenes first (File > Save Scene).", null, null);
			}
			else if (!scenePath.StartsWith(GBEditorUtil.ScenesFolder + "/"))
			{
				Add(issues, MessageType.Warning, "The scene is outside Assets/Scenes. It still works when it is in Build Settings, but the shipped levels all live there.", null, null);
			}

			CheckSceneManager(issues);
			CheckCamera(issues);
			CheckPlayers(issues);
			CheckZonesAndLighting(issues);
			CheckPhysics(issues);
			if (!string.IsNullOrEmpty(scenePath))
			{
				CheckRegistration(issues, scenePath, levelName);
			}
			if (issues.Count == 0)
			{
				Add(issues, MessageType.Info, "All checks passed. Press Play to try the level, or build the game and pick it from the menu.", null, null);
			}
			return issues;
		}

		static void Add(List<GBIssue> issues, MessageType severity, string message, string fixLabel, System.Action fix)
		{
			GBIssue issue = new GBIssue();
			issue.severity = severity;
			issue.message = message;
			issue.fixLabel = fixLabel;
			issue.fix = fix;
			issues.Add(issue);
		}

		static Vector3 Pivot()
		{
			return SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
		}

		static void CheckSceneManager(List<GBIssue> issues)
		{
			List<SceneManager> managers = GBEditorUtil.AllComponents<SceneManager>();
			if (managers.Count == 0)
			{
				Add(issues, MessageType.Error, "No Scene Manager. Beasts look it up by tag at start and throw errors without it.",
					"Add Scene Manager", delegate { GBLevelKit.Spawn("Scene Manager", "Scene Manager", Vector3.zero, Quaternion.identity, true); });
				return;
			}
			foreach (SceneManager m in managers)
			{
				if (m.gameObject.tag != "Scene Manager")
				{
					SceneManager captured = m;
					Add(issues, MessageType.Error, "'" + m.name + "' has a SceneManager script but not the tag 'Scene Manager', so the beasts cannot find it.",
						"Set tag", delegate { captured.gameObject.tag = "Scene Manager"; EditorUtility.SetDirty(captured.gameObject); });
				}
			}
			if (managers.Count > 1)
			{
				Add(issues, MessageType.Warning, managers.Count + " Scene Managers in the scene. The game keeps only one (the extra copies destroy themselves), so remove the duplicates.", null, null);
			}
		}

		static void CheckCamera(List<GBIssue> issues)
		{
			List<Camera> mains = new List<Camera>();
			foreach (Camera c in GBEditorUtil.AllComponents<Camera>())
			{
				if (c.gameObject.tag == "MainCamera")
				{
					mains.Add(c);
				}
			}
			if (mains.Count == 0)
			{
				Add(issues, MessageType.Error, "No camera tagged MainCamera. Player input is relative to Camera.main, so the beasts cannot move without one.",
					"Add follow camera", delegate
					{
						Vector3 at = Pivot() + GBLevelKit.CameraOffset;
						GBLevelKit.Spawn("Main Camera", "Main Camera", at, Quaternion.LookRotation(Pivot() - at), true);
					});
			}
			else
			{
				foreach (Camera c in mains)
				{
					if (c.GetComponent<SceneCamera>() == null)
					{
						Camera captured = c;
						Add(issues, MessageType.Warning, "'" + c.name + "' has no SceneCamera script, so it will not follow the beasts.",
							"Add SceneCamera", delegate { captured.gameObject.AddComponent<SceneCamera>(); });
					}
				}
				if (mains.Count > 1)
				{
					Add(issues, MessageType.Warning, mains.Count + " cameras are tagged MainCamera. Keep one.", null, null);
				}
			}
			int listeners = GBEditorUtil.AllComponents<AudioListener>().Count;
			if (listeners != 1)
			{
				Add(issues, MessageType.Warning, listeners + " Audio Listeners in the scene (Unity wants exactly one; the kit camera has one).", null, null);
			}
		}

		static void CheckPlayers(List<GBIssue> issues)
		{
			List<Player> humans = new List<Player>();
			List<Player> mobs = new List<Player>();
			foreach (Player p in GBEditorUtil.RootComponents<Player>())
			{
				if (p.mob)
				{
					mobs.Add(p);
				}
				else
				{
					humans.Add(p);
				}
			}
			if (humans.Count == 0)
			{
				Add(issues, MessageType.Error, "No player beasts in the scene.", "Add players 1-4", delegate
				{
					for (int i = 0; i < 4; i++)
					{
						GBLevelKit.SpawnPlayer(i, GBLevelKit.CirclePoint(Pivot(), 3f, 1.1f, i, 4), Pivot(), true);
					}
				});
			}
			else
			{
				for (int index = 0; index < 4; index++)
				{
					int count = 0;
					foreach (Player p in humans)
					{
						if (p.player == index)
						{
							count++;
						}
					}
					if (count == 0)
					{
						int captured = index;
						Add(issues, MessageType.Warning, "Player " + (index + 1) + " is missing. With the menu set to " + (index + 1) + "+ players a round can never end, because that player never appears and never loses a life.",
							"Add Player " + (index + 1), delegate { GBLevelKit.SpawnPlayer(captured, Pivot() + Vector3.up * 1.1f, Pivot() + Vector3.forward, true); });
					}
					else if (count > 1)
					{
						Add(issues, MessageType.Error, count + " beasts have the player index " + index + " (Player " + (index + 1) + "). Each index must appear once; they share one set of controls and one life counter.", null, null);
					}
				}
				foreach (Player p in humans)
				{
					if (p.player < 0 || p.player > 3)
					{
						Add(issues, MessageType.Error, "'" + p.name + "' has player index " + p.player + ". Human beasts use 0-3 (index 4 is the mob convention).", null, null);
					}
					RaycastHit hit;
					if (!Physics.Raycast(p.transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 100f))
					{
						Add(issues, MessageType.Warning, "'" + p.name + "' has no floor below it; it will fall at start.", null, null);
					}
				}
			}
			if (mobs.Count > 0)
			{
				Add(issues, MessageType.Info, mobs.Count + " mob beast(s). Mobs move on the NavMesh, so bake it after changing the level geometry.",
					"Bake NavMesh", delegate { GBObjectFactory.BakeNavMesh(); });
			}
		}

		static void CheckZonesAndLighting(List<GBIssue> issues)
		{
			List<KillZone> kills = GBEditorUtil.AllComponents<KillZone>();
			if (kills.Count == 0)
			{
				Add(issues, MessageType.Warning, "No Kill Zone. A beast that falls off the level keeps falling forever and the round never ends.",
					"Add Kill Zone below the level", delegate
					{
						GBObjectFactory.CreateKillZone(new Vector3(Pivot().x, -6f, Pivot().z), new Vector3(200f, 2f, 200f), GBObjectFactory.DefaultKillSound(), 0f);
					});
			}
			foreach (KillZone k in kills)
			{
				Collider c = k.GetComponent<Collider>();
				if (c == null)
				{
					Add(issues, MessageType.Error, "Kill Zone '" + k.name + "' has no collider.", null, null);
				}
				else if (!c.isTrigger)
				{
					Collider captured = c;
					Add(issues, MessageType.Error, "Kill Zone '" + k.name + "' is a solid collider. It must be a trigger (KillZone uses OnTriggerStay).",
						"Make trigger", delegate { captured.isTrigger = true; EditorUtility.SetDirty(captured); });
				}
			}
			if (GBEditorUtil.AllComponents<mset.Sky>().Count == 0)
			{
				Add(issues, MessageType.Warning, "No Marmoset Sky object. Marmoset materials (most of the game's art) take their lighting from it and look wrong without it.",
					"Add Sky", delegate { GBLevelKit.Spawn("Sky", "Sky", Vector3.zero, Quaternion.identity, true); });
			}
			if (GBEditorUtil.AllComponents<Light>().Count == 0)
			{
				Add(issues, MessageType.Warning, "No lights in the scene.", "Add sun light", delegate
				{
					GBKitItem sun = GBLevelKit.Find("Directional Light");
					GameObject prefab = sun != null ? sun.Load() : null;
					GBLevelKit.Spawn("Directional Light", "Directional Light", Pivot() + Vector3.up * 10f, prefab != null ? prefab.transform.rotation : Quaternion.Euler(50f, -30f, 0f), true);
				});
			}
			if (GBEditorUtil.AllComponents<Music>().Count == 0)
			{
				Add(issues, MessageType.Info, "No Music object (optional).", "Add Music", delegate { GBLevelKit.Spawn("Music", "Music", Vector3.zero, Quaternion.identity, true); });
			}
		}

		static void CheckPhysics(List<GBIssue> issues)
		{
			foreach (Rigidbody rb in GBEditorUtil.AllComponents<Rigidbody>())
			{
				if (rb.transform.root.GetComponent<Player>() != null)
				{
					continue; // beasts are checked by their own scripts
				}
				if (rb.GetComponentsInChildren<Collider>(true).Length == 0)
				{
					Add(issues, MessageType.Warning, "'" + rb.name + "' has a Rigidbody but no collider: it will fall through the level.", null, null);
				}
			}
		}

		static void CheckRegistration(List<GBIssue> issues, string scenePath, string levelName)
		{
			if (!GBLevelRegistry.IsInBuildSettings(scenePath))
			{
				Add(issues, MessageType.Error, "The scene is not in Build Settings (or is disabled there). Application.LoadLevel cannot load it from the menu or a build.",
					"Add to Build Settings", delegate { GBLevelRegistry.AddToBuildSettings(scenePath); });
			}
			if (!GBLevelRegistry.IsInMenuList(levelName))
			{
				Add(issues, MessageType.Warning, "'" + levelName + "' is not in the Menu's level list, so the 'Level:' button in the menu never offers it.",
					"Add to Menu list", delegate { GBLevelRegistry.EditMenuLists(levelName, GBLevelRegistry.Add, GBLevelRegistry.Leave); });
			}
		}
	}
}
