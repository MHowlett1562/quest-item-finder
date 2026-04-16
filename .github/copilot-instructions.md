# Project overview

This repository contains a Unity mixed reality prototype for Meta Quest 3.

The app lets a user save the last known location of an item in their environment and later recall that location with AR guidance.
This is a bare-bones MVP, not a production app.

# Primary goal

Help the user:
1. save an item name
2. save a spatial anchor for that item
3. save a timestamp and optional snapshot/reference data
4. later recall the item and guide the user back to the saved anchor

# Target platform

- Unity project for Meta Quest 3
- Mixed reality / passthrough use case
- Android build target
- C# only

# Coding style

- Prefer simple, readable Unity C# over clever abstractions.
- Prefer small MonoBehaviour scripts with one clear responsibility.
- Prefer composition over inheritance.
- Avoid overengineering.
- Avoid unnecessary design patterns.
- Keep classes and methods short when practical.
- Use explicit names over abbreviations.
- Use [SerializeField] private fields instead of public fields unless public access is required.
- Add comments only when the intent is not obvious from the code.
- Preserve inspector-assigned references.
- Do not rename serialized fields unless necessary.
- Do not rewrite unrelated code.

# Architecture preferences

- Separate data classes from MonoBehaviours.
- Keep save/load logic separate from UI logic.
- Keep anchor placement/recall logic separate from menu logic.
- Prefer plain serializable data classes for saved data.
- Prefer one manager per small feature area only when it clearly simplifies the prototype.
- Do not introduce dependency injection frameworks or large service architectures.

# Unity-specific guidance

- Assume this is a Unity project and use Unity-friendly patterns.
- Do not invent Unity, Meta XR, or Quest APIs.
- If uncertain about a Unity or Meta XR API, say so clearly instead of guessing.
- Prefer minimal patches to existing scripts over full rewrites.
- Keep Play Mode iteration fast.
- Avoid changes that would require broad scene or prefab rewiring unless explicitly requested.

# Debugging guidance

When fixing bugs:
- explain the root cause briefly
- make the smallest reasonable code change
- preserve current behavior unless the bug requires behavior change
- add Debug.Log statements only when they help diagnose a real issue
- remove temporary debug code when done

# Output preferences

When asked to generate code:
- provide complete compilable Unity C# scripts when creating new files
- provide minimal diffs or targeted replacements when editing existing files
- state any assumptions clearly
- do not add placeholder systems that are not needed yet

# MVP scope

In scope:
- save item name
- save last known anchor/location
- save timestamp
- simple UI for save/find
- beacon/arrow/distance guidance
- local persistence

Out of scope for now:
- advanced AI object recognition
- cloud sync
- multiplayer/shared anchors
- large refactors
- polished production UX
- live tracker integrations