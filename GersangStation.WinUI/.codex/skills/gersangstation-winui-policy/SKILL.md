---
name: gersangstation-winui-policy
description: Repository-specific guidance for GersangStation WinUI behavior and policy decisions. Use when changing WebView navigation, StationPage or service responsibilities, launcher or game-process rules, multi-client file layout behavior, privacy-policy assets or links, or release packaging settings in this repository.
---

# GersangStation WinUI Policy

## Overview

Apply this skill for repository-specific app policy that is too detailed for the top-level `AGENTS.md`. Read `AGENTS.md` first for build, test, and validation commands, then load only the reference file that matches the requested change.

## Reference Map

- Read `references/architecture.md` for class responsibility boundaries, WebView URL handling, and the preference to avoid unnecessary XAML restructuring.
- Read `references/game-policies.md` for launch-state behavior, client and account limits, install-path validation, and multi-client copy or link rules.
- Read `references/release-and-policy-assets.md` for privacy-policy asset placement and in-app linking, plus release packaging constraints.

## Maintenance Rules

- Keep repository-wide standing rules in `AGENTS.md`, but store detailed product or implementation policy in this skill.
- Update the relevant reference file in the same change whenever the user adds, retires, or revises one of these policies.
