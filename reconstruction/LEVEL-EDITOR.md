# Level Editor for Gang Beasts 0.0.3

An editor extension for the reconstructed Unity 4.3.2 project that builds **new, playable levels
from the game's own assets** and wires them into the game's menu.

It lives in `GangBeasts/Assets/Editor/LevelEditor/` and compiles into `Assembly-CSharp-Editor`
only. The four reconstructed runtime assemblies and `src/` are untouched, so the byte-exact proof
still holds. No shipped scene is ever modified: the reference levels are only opened to copy from.

Open it with **Gang Beasts > Level Editor** (Ctrl+Shift+L).

## What a Gang Beasts level is made of

The shipped levels have no prefabs; every level is a flat scene that contains:

| Object | Script | Why it is needed |
|---|---|---|
| `Scene Manager` (tag `Scene Manager`) | `SceneManager` | Lives, scores, player count, controller bindings. Beasts look it up by tag. From the menu, the menu's own copy survives and the level's copy destroys itself. |
| `Main Camera` (tag `MainCamera`) | `SceneCamera` + image effects | Follows the average position of all objects tagged `Player`. Player input is relative to `Camera.main`. R restarts, Esc goes to the menu. |
| 4 beasts (tag `Player`, `Player.player` = 0..3) | `Player`, `Movement`, `Jump`, `PunchAndGrab`, `HealthAndStamina`, `StatusBar` | Active ragdolls. `Player.Start` destroys any beast whose index is >= the menu's player count. |
| Mobs (`Player.mob = true`, index 4) | same + `NavMeshAgent` | Optional AI beasts; need a baked NavMesh. |
| `Kill Zone` (trigger) | `KillZone` | Kills beasts that fall off; without it a round never ends. |
| `Sky` | `mset.Sky` (Marmoset Skyshop) | Lighting data for every Marmoset material. |
| `Directional light`, `Music`, `AudioTimeScale`, `Framerate Counter` | - / `Music` / `AudioTimeScale` / `HUDFPS` | Sun, looping music, slow-motion keys (- =), FPS counter (F). |

To be playable from the menu the scene must be in **Build Settings** and its name must be in
`SceneManager.levelList` on the Menu scene (the list the menu's `Level:` button cycles through).
`Application.LoadLevel(name)` loads it by file name.

## Tabs

### New Level
Builds a complete level in one click: `Assets/Scenes/<name>.unity` with the Scene Manager,
follow camera, sky, sun, music, FPS counter, a floor (optionally walls), a kill zone below, 1-4
beasts around the centre and optional mobs. Then it adds the scene to Build Settings and to the
Menu's level list (and optionally the Rezzed rotation).

If the kit prefabs do not exist yet they are extracted first (see Kit).

### Build
Click-to-place tools. Each row has **Place at view pivot** (one object where the Scene view
orbits) and **Paint with mouse** (click in the Scene view to drop one per click; Esc stops).
Objects rest on whatever is under the cursor, snapped to the grid.

- Static geometry: platforms and ramps (static, NavMesh-ready).
- Physics props: cube / sphere / capsule / cylinder with mass, tag (`Prop`, `Prop (Harmful)`),
  layer (`Default` = beasts can stand on it, `Ignore Ball` = the balance ball passes through),
  material and physic material.
- Zones: Kill Zone, Warp Zone (restarts the level), Dont Track Zone (camera and mobs ignore
  players inside).
- Lights.
- Beasts: add Player 1-4 or a Mob at the pivot.
- Selection helpers: apply material, make static, add Rigidbody.

Zones and spawn points are drawn in the Scene view while the window is open.

### Props
1. **Harvest**: pick a shipped level, *Open level and scan*. Every root object that renders and
   is not a beast/camera/system is listed. Tick what you want and *Save selected as prefabs*;
   they go to `Assets/LevelEditor/Props/<Level>/`. *Back to ...* returns to your level. The
   shipped level is never saved.
2. **Palette**: all harvested props, the game's loose prefabs (`Assets/GameObject`) and the kit
   Mob, with previews and Place / Paint buttons.

### Kit
Extracts the level kit from a reference level (Ring by default; the Mob from Sandbox) into
`Assets/LevelEditor/Kit/*.prefab`, shows which prefabs exist, and places single kit objects.

### Check
Validates the open scene against what the game's scripts expect and offers one-click fixes:
Scene Manager and its tag, MainCamera with `SceneCamera`, one Audio Listener, players 0-3
present once each and standing on something, kill zone present and set as trigger, Marmoset
sky, lights, rigidbodies without colliders, Build Settings, Menu list. Buttons for Build
Settings, the Menu lists and NavMesh baking; shows the current Menu and build lists.

## Testing a level

Press Play in the scene. Its own Scene Manager sets the player count ("Players when run from
editor"). Controls are bound to controllers by default; press **Tab** in the game to open the
cInput window and bind keyboard keys (P1 Up/Down/Left/Right, Jump, Left/Right Punch & Grab,
Duck, Action, Gesture). R restarts, Esc returns to the menu, F shows the frame rate, - and =
change the time scale.

## Files

```
GangBeasts/Assets/Editor/LevelEditor/
  GBLevelEditorWindow.cs   the window and its tabs
  GBLevelKit.cs            kit items, extraction, CreateLevel
  GBLevelRegistry.cs       Build Settings + Menu levelList (SerializedObject edit of Menu.unity)
  GBObjectFactory.cs       floor/platform/ramp, physics props, zones, lights, NavMesh bake
  GBPlacementTool.cs       Scene view click-to-place + overlays
  GBPropHarvester.cs       prop extraction from shipped levels, palette listing
  GBLevelValidator.cs      the checks and fixes
  GBEditorUtil.cs          shared helpers
```

Generated content (`Assets/LevelEditor/Kit`, `Assets/LevelEditor/Props`, new scenes) is yours;
it is not part of the reconstruction. `tools/assemble_project.py` knows nothing of the editor:
after regenerating the project, copy `Assets/Editor` back in.
