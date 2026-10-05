# Changelog

## 0.1.1

- Fix truncated Preferences labels by placing checkboxes before their text and using the full available row width.

## 0.1.0

- Package the editor extensions for installation from a Git URL through Unity Package Manager.
- Add Animation window CopyState and ApplyState gear commands.
- Add Timeline gear commands for scene pose capture across multiple bound animation roots.
- Synchronize Timeline-linked Animation row/key selection with scene GameObject selection.
- Share snapshot and internal editor API helpers under ExtensionsForAnimationWondows.
- Add independent feature toggles in Unity Preferences.
- Record Undo, prefab overrides and scene changes when applying a pose.
- Correct Transform quaternion channel handling and exposed-reference property handling.
