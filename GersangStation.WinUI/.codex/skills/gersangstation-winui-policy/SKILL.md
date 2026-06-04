---
name: gersangstation-winui-policy
description: Repository-specific guidance for GersangStation WinUI behavior and policy decisions. Use when changing WebView navigation, StationPage or service responsibilities, launcher or game-process rules, multi-client file layout behavior, privacy-policy assets or links, or release packaging settings in this repository.
---

# GersangStation WinUI Policy

## Overview

Apply this skill for repository-specific app policy that is too detailed for the top-level `AGENTS.md`. Read `AGENTS.md` first for build, test, and validation commands, then load only the reference file that matches the requested change.

## Reference Map

- Read `references/architecture.md` for responsibility boundaries, WebView navigation ownership, and XAML restructuring preferences.
- Read `references/game-policies.md` for game launch state, install-path validation, patch permissions, multi-client layout, and game-window control.
- Read `references/release-and-policy-assets.md` for privacy-policy assets, link manifests, Store update fallback data, and release packaging constraints.

## Maintenance Rules

- Keep repository-wide standing rules in `AGENTS.md`, but store detailed product or implementation policy in this skill.
- Load only the reference file that matches the change unless the task crosses categories.
- Update the relevant reference file in the same change whenever the user adds, retires, or revises one of these policies.
- Merge new policy into the nearest existing category; avoid appending narrow one-off bullets or implementation history.
