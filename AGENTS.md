# Proyecto Miscellaneus — Codex Instructions

## Project
- Name: Proyecto Miscellaneus.
- Engine: Unity 6.3 LTS (6000.3.27f1).
- Render pipeline: Universal 2D (URP).
- Language: C#.
- Primary platform: Android.
- Secondary platform: PC (optional).
- Development: single student, beginner in Unity.
- Hardware: Intel i3-4170, 12 GB RAM, Intel HD 4400.
- Budget: no additional paid tools.

## Game
- Genre: dark fantasy, alchemy and real-time combat.
- Perspective: 2D top-down.
- Main character: mushroom wizard.
- Art: low-resolution monochrome pixel art.
- Main loop: prepare potions, duel, unlock recipes, progress.
- Alchemy grammar: BASE -> EFFECT -> MODIFIER.
- MVP: three duels, up to three equipped potions, local save.
- Android APK is mandatory.

## Current verified status
- Unity Editor opens the initial SampleScene.
- Play and Stop have been tested.
- Android APK has been built, installed and opened on
  Samsung SM-J610G with Android 10.
- Android build uses IL2CPP with ARMv7 and ARM64.
- Git repository exists and is connected to GitHub.
- No gameplay features have been implemented yet.

## Coding rules
- Prefer small C# scripts with one main responsibility.
- Use composition and separation of responsibilities.
- Keep input handling separate from player movement logic.
- Use the existing Unity Input System.
- Keep Rigidbody2D top-down movement independent of gravity.
- Avoid unnecessary packages, dependencies and visual effects.
- Do not implement optional features without approval.
- Do not introduce new architecture without justification.

## Safety and scope
- Follow the allowed files and folders specified in each task.
- Do not modify unrelated files.
- Do not delete, rename or reorganize files without approval.
- Do not change Unity version or render pipeline.
- Do not install packages without approval.
- Do not modify Android build settings without approval.
- Do not access personal backup directories.
- Do not create commits, push, reset or rewrite Git history.
- Do not claim tests passed unless actually executed.
- Do not execute destructive commands.
- Ask before taking actions outside the authorized task.

## Task workflow
1. Inspect relevant files.
2. Explain the proposed changes.
3. Modify only explicitly authorized files.
4. Report changed files and relevant technical risks.
5. Explain how to test in Unity.
6. Explain how to test on Android when applicable.
7. Leave Git commit and push to the developer.

## Reporting
Always distinguish:
- Implemented
- Tested in Unity
- Tested on Android
- Pending
- Errors or risks

Explain C# changes clearly so the student can defend them
during the academic presentation.
