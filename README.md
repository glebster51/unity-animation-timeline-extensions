# Unity Animation & Timeline Window Extensions

**Copy animation preview poses, apply them to the scene, and select GameObjects from Timeline-linked Animation window keys.** An editor-only Unity Package Manager (UPM) package for Unity 6.

## Features

| Window | Extension | What it does |
| --- | --- | --- |
| Animation | `CopyState` / `ApplyState` in the gear menu | Keep the currently evaluated animation pose after leaving Preview. |
| Timeline | `CopyState` / `ApplyState` in the gear menu | Capture scene properties across bound animation tracks and multiple Animators, including evaluated blending and track root offsets. |
| Animation opened from Timeline | Scene selection from rows and keys | Selecting an animated property row or a key selects its corresponding GameObject in the scene hierarchy. |

No runtime components are required. Extensions install automatically after the editor imports the package.

## Install with Unity Package Manager

In Unity, open **Window → Package Management → Package Manager**, choose **Install package from Git URL** (the `+` / Add menu), and paste:

```text
https://github.com/glebster51/unity-animation-timeline-extensions.git#v0.1.1
```

The version tag keeps your project on a reproducible release. To follow the latest changes instead, use the same URL without `#v0.1.1`.

Alternatively, add this entry to your project's `Packages/manifest.json` dependencies:

```json
"com.glebster51.extensions-for-animation-windows": "https://github.com/glebster51/unity-animation-timeline-extensions.git#v0.1.1"
```

Unity needs Git installed for Git-based packages. See the [official Unity Git URL installation guide](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-ui-giturl.html).

## Keep an Animation window preview pose

1. Open a scene object's animation in the **Animation** window.
2. Enable **Preview** and move to the pose you want to keep.
3. Open the window's **gear menu → CopyState**.
4. Disable Preview. Unity restores the original scene values.
5. Open **gear menu → ApplyState** to apply the copied values to the original scene objects.
6. Save the scene when you want to persist the result. Use Undo to revert it.

This copies the evaluated scene properties, not keyframes. ApplyState does not insert keys or change an AnimationClip or AnimatorController asset.

## Keep a Timeline pose across multiple Animators

1. Open a Timeline bound to a scene `PlayableDirector`.
2. Enable Timeline Preview and move its playhead to the desired frame.
3. Choose **Timeline gear menu → CopyState**.
4. Turn off Preview, then choose **Timeline gear menu → ApplyState**.

The snapshot collects supported properties from unmuted animation tracks, child/override tracks, regular clips and infinite clips. It reads the already evaluated scene, so overlapping clips, track offsets and masks are reflected in the captured values. It also includes the bound root transforms and mapped Humanoid bone transforms.

ApplyState requires the same inspected Timeline and Director as CopyState. Animation and Timeline have independent in-memory clipboards. Exit all animation preview modes before applying; the command is disabled while preview or Play Mode is active.

## Select a scene object from Timeline animation keys

Double-click a Timeline animation clip to open its linked **Animation** window. Click a property row or select its keys. The extension resolves the clip's bound animation root, then selects the GameObjects addressed by those curve bindings.

Multiple selected keys can select multiple objects. The extension preserves the Timeline link, preview state, playback time and window focus. The summary row does not select the entire animated hierarchy. Disable **Filter by Selection** if you want automatic scene selection: that filter intentionally takes precedence.

## Preferences

Open **Edit → Preferences → Animation Window Extensions**. Each feature can be enabled or disabled independently. Preferences are per user and shared between projects.

## Compatibility and limitations

- Target environment: **Unity 6000.6.1f1** and **Timeline 6.6.0**. The package declares Timeline as a dependency.
- The editor integration uses some internal Unity window APIs. Other editor or Timeline versions are not guaranteed to work. If an internal Timeline API fails, its overlay is removed and a warning is logged.
- Timeline's gear is an IMGUI overlay that preserves the regular preferences, time format, duration, marker and frame rate entries. Developer-only debug menu entries are not reproduced.
- Snapshots capture supported serialized animation properties and Transform channels on scene objects. They are not full scene backups; procedural state, custom Playable tracks, unmapped Humanoid data, constraints and non-serialized component internals are outside their scope.
- Clipboard contents are lost on script/domain reload, package update or editor restart. They refer to the original objects; this is not a pose-retargeting tool.
- ApplyState records Undo, prefab instance overrides and scene dirtiness. It does not automatically save your scene.
- Remove older standalone copies of these extensions before installing the package to avoid duplicate callbacks or menu entries.

## Package structure

```text
package.json
Editor/
  ExtensionsForAnimationWondows.Editor.asmdef
  UnityEditorBridge.cs
  StateSnapshot.cs
  AnimationStateClipboard.cs
  AnimationWindowStateMenu.cs
  TimelineStateClipboard.cs
  TimelineStateMenu.cs
  TimelineAnimationSceneSelection.cs
  ExtensionPreferences.cs
README.md
CHANGELOG.md
LICENSE.md
```

All scripts use the shared namespace **`ExtensionsForAnimationWondows`**. Its spelling is intentional for continuity with the original tools. The assembly is restricted to the editor and contains no project-specific scene references.

## Common Unity editor problems addressed

- "How do I copy an animation preview pose and keep it after exiting Preview?"
- "How do I apply a Timeline pose to several Animators in the scene?"
- "Why doesn't the Animation window select a GameObject when I click a Timeline animation key or property row?"

## Reporting a problem

Please [open an issue](https://github.com/glebster51/unity-animation-timeline-extensions/issues) with your Unity version, Timeline version, affected feature, Console output and reproduction steps. Mention whether the clip is regular or infinite and whether its track is an override track. A minimal reproduction is especially useful for internal editor API changes.

## Кратко по-русски

Пакет добавляет `CopyState` / `ApplyState` в меню шестерёнки окон Animation и Timeline. Скопируйте нужную позу в Preview, выключите Preview и примените её к исходным объектам сцены. В Animation, открытом из Timeline, выбор строк свойств и ключей выделяет соответствующие объекты сцены. Все три возможности можно отключить отдельно в Preferences → Animation Window Extensions.

## License

[MIT](LICENSE.md). This is an independent community extension; it is not an official Unity package.
