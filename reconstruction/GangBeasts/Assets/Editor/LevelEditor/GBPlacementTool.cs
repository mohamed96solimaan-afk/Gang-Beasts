// Gang Beasts 0.0.3 level editor (Unity 4.3.2).
//
// Click-to-place tool for the Scene view. A "factory" creates the object (a prefab instance, a
// primitive, a zone...), the tool drops it where the mouse points: on the collider under the
// cursor, or on the y = 0 plane when there is nothing. Esc stops placing.
using UnityEditor;
using UnityEngine;

namespace GangBeastsTools
{
	public static class GBPlacementTool
	{
		static bool active;
		static string label = string.Empty;
		static System.Func<GameObject> factory;

		public static float snap = 0.5f;
		public static bool sitOnSurface = true;
		public static bool alignToSurface = false;
		public static bool randomYaw = false;

		public static bool IsActive
		{
			get { return active; }
		}

		public static string Label
		{
			get { return label; }
		}

		/// <summary>Starts click-to-place mode with the given object factory.</summary>
		public static void Begin(string what, System.Func<GameObject> make)
		{
			label = what;
			factory = make;
			active = true;
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.onSceneGUIDelegate += OnSceneGUI;
			if (SceneView.lastActiveSceneView != null)
			{
				SceneView.lastActiveSceneView.Focus();
			}
			SceneView.RepaintAll();
		}

		public static void End()
		{
			active = false;
			factory = null;
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.RepaintAll();
		}

		/// <summary>Creates one object at the Scene view pivot, dropped onto whatever lies below it.</summary>
		public static GameObject PlaceAtPivot(string what, System.Func<GameObject> make)
		{
			Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
			Vector3 normal = Vector3.up;
			RaycastHit hit;
			if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out hit, 400f))
			{
				pos = hit.point;
				normal = hit.normal;
			}
			return Create(make, pos, normal);
		}

		static GameObject Create(System.Func<GameObject> make, Vector3 pos, Vector3 normal)
		{
			GameObject go = make();
			if (go == null)
			{
				return null;
			}
			if (snap > 0f)
			{
				pos = GBEditorUtil.Snap(pos, snap);
			}
			go.transform.position = pos;
			Quaternion rot = go.transform.rotation;
			if (alignToSurface)
			{
				rot = Quaternion.FromToRotation(Vector3.up, normal) * rot;
			}
			if (randomYaw)
			{
				rot = Quaternion.AngleAxis(Random.Range(0f, 360f), alignToSurface ? normal : Vector3.up) * rot;
			}
			go.transform.rotation = rot;
			if (sitOnSurface)
			{
				Bounds b = GBEditorUtil.ObjectBounds(go);
				float lift = pos.y - b.min.y;
				if (Mathf.Abs(lift) > 0.0001f)
				{
					go.transform.position = go.transform.position + Vector3.up * lift;
				}
			}
			Selection.activeGameObject = go;
			return go;
		}

		static void OnSceneGUI(SceneView view)
		{
			if (!active || factory == null)
			{
				SceneView.onSceneGUIDelegate -= OnSceneGUI;
				return;
			}
			Event e = Event.current;
			// Own the default control so a click places instead of selecting.
			HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
			if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
			{
				End();
				e.Use();
				return;
			}
			Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
			Vector3 pos;
			Vector3 normal = Vector3.up;
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 5000f))
			{
				pos = hit.point;
				normal = hit.normal;
			}
			else
			{
				Plane ground = new Plane(Vector3.up, Vector3.zero);
				float distance;
				if (!ground.Raycast(ray, out distance))
				{
					return;
				}
				pos = ray.GetPoint(distance);
			}
			if (snap > 0f)
			{
				pos = GBEditorUtil.Snap(pos, snap);
			}
			Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);
			Handles.DrawWireDisc(pos, normal, 0.5f);
			Handles.DrawLine(pos, pos + normal);
			Handles.Label(pos + Vector3.up * 1.2f, "Click: place " + label + "    Esc: stop");
			if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
			{
				Create(factory, pos, normal);
				e.Use();
			}
			if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
			{
				view.Repaint();
			}
		}

		// ---- overlays ---------------------------------------------------------------

		/// <summary>Draws the trigger zones and player spawns of the open scene (called from the window).</summary>
		public static void DrawOverlays(SceneView view)
		{
			foreach (KillZone z in (KillZone[])Object.FindObjectsOfType(typeof(KillZone)))
			{
				DrawZone(z.gameObject, new Color(1f, 0.25f, 0.25f), "Kill Zone");
			}
			foreach (WarpZone z in (WarpZone[])Object.FindObjectsOfType(typeof(WarpZone)))
			{
				DrawZone(z.gameObject, new Color(1f, 0.8f, 0.2f), "Warp Zone");
			}
			foreach (DontTrackZone z in (DontTrackZone[])Object.FindObjectsOfType(typeof(DontTrackZone)))
			{
				DrawZone(z.gameObject, new Color(0.3f, 0.6f, 1f), "Dont Track Zone");
			}
			foreach (Player p in (Player[])Object.FindObjectsOfType(typeof(Player)))
			{
				if (p.transform.parent != null)
				{
					continue;
				}
				Color c = p.mob ? new Color(0.8f, 0.4f, 1f) : p.actorColor;
				c.a = 1f;
				Handles.color = c;
				Vector3 at = p.transform.position;
				Handles.DrawWireDisc(at, Vector3.up, 0.6f);
				Handles.DrawLine(at, at + p.transform.forward * 1.2f);
				Handles.Label(at + Vector3.up * 2.2f, p.mob ? "Mob" : "P" + (p.player + 1) + " (" + p.gangName + ")");
			}
		}

		static void DrawZone(GameObject go, Color color, string caption)
		{
			BoxCollider box = go.GetComponent<BoxCollider>();
			if (box != null)
			{
				GBEditorUtil.DrawWireBox(go.transform, box.center, box.size, color);
				Handles.color = color;
				Handles.Label(go.transform.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f), caption);
			}
		}
	}
}
