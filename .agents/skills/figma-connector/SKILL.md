---
name: figma-connector
description: Connects Quantum Updater design tokens, SVG components, and layout specs with Figma REST API and Tokens Studio workflows.
---

# Figma Connector Skill

## 1. Overview
The Figma Connector bridges Quantum Updater's frontend implementation with Figma design files, enabling token syncing, component inspection, and automated vector export.

## 2. Environment Configuration
To synchronize directly with a Figma workspace, configure the following environment parameters:
- `FIGMA_ACCESS_TOKEN`: Personal Access Token from Figma Account Settings.
- `FIGMA_FILE_KEY`: Target Figma file identifier from the project URL (`https://figma.com/design/:file_key/:title`).

## 3. Supported Workflows
1. **Design Tokens Extraction**:
   - Maps Figma Variables (Color, Numbers, Booleans) to CSS Custom Properties (`--c-midnight-teal`, `--c-ocean-blue`, `--c-emerald-mint`, `--c-golden-yellow`, `--c-coral-peach`, `--c-coral-pink`).
   - Exports tokens as standard W3C Design Tokens JSON.
2. **Vector SVG Component Syncing**:
   - Queries `GET https://api.figma.com/v1/images/:file_key?ids=:node_id&format=svg` to pull production-ready SVG assets.
   - Enforces 24x24 viewBox and stroke-based vector definitions.
3. **Stitch <-> Figma Bridge**:
   - Screens generated via Stitch MCP can be cross-referenced or ported into Figma using JSON design specs.
