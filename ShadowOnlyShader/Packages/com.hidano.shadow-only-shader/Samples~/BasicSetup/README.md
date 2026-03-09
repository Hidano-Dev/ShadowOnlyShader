# Shadow Only Shader - Basic Setup Sample

## Contents

- **Scenes/ShadowOnlySample.unity** - Setup-completed sample scene
- **Settings/URP-ShadowOnlySample-Renderer.asset** - URP Renderer with ShadowOnlyRendererFeature

## Scene Hierarchy

```
Main Camera          - Camera (position: 0, 2, -4)
Directional Light    - Scene illumination (shadow disabled)
ShadowOnlySetup      - ShadowOnlyManager (BlurQuality: Mid, BlendMultiplier: 1)
  VirtualLight_01    - VirtualLight (Orthographic, size: 3, alpha: 0.6)
Caster               - Cube (shadow caster)
Floor                - Quad (shadow receiver, 5x5)
```

## Setup

1. Import this sample via Package Manager
2. Open `ShadowOnlySample.unity`
3. Configure your URP Renderer:
   - **Option A**: Use the included `URP-ShadowOnlySample-Renderer.asset`
     - Assign it to your URP Pipeline Asset's Renderer List
   - **Option B**: Add `ShadowOnlyRendererFeature` to your existing URP Renderer
     - Select your URP Renderer Asset
     - Click "Add Renderer Feature" > "Shadow Only Renderer Feature"
4. Press Play

## Notes

- The included Renderer asset (`URP-ShadowOnlySample-Renderer.asset`) references `UniversalRendererData`. If it shows "Script Missing" in your Unity version, use Option B above.
- The Floor's material is automatically assigned at runtime by `ShadowOnlyManager`.
- The Caster cube can be replaced with any mesh (including SkinnedMeshRenderer).
