# Ground Realist Setup Guide

## Overview
Two new scripts provide a more realistic ground for your Planetarium VR application:

1. **EarthGroundRealistic.cs** - Enhanced procedural texture generation with vegetation
2. **GroundVegetationSpawner.cs** - Spawns grass and bushes for visual depth

## Installation

### Step 1: Replace or Enhance Existing Ground
Your current setup uses `EarthGroundVisual.cs`. You have two options:

**Option A: Keep both (recommended)**
- Keep `EarthGroundVisual.cs` as is
- Use it for the base ground plane
- Attach `EarthGroundRealistic.cs` to the same GameObject for better textures
- The new script will override the material

**Option B: Replace completely**
- Remove `EarthGroundVisual.cs` from your ground object
- Use only `EarthGroundRealistic.cs`

### Step 2: Setup Ground GameObject
1. In your scene, find or create a ground Plane object
2. Attach `EarthGroundRealistic.cs` to it
3. Configure the texture and material properties in the Inspector:
   - **Albedo Size**: 1024 (higher = more detail, more memory)
   - **Base Tiling**: 18 (how many times texture repeats)
   - **Vegetation Amount**: 0.3-0.5 (higher = more visible vegetation in texture)

### Step 3: Add Vegetation Objects (Optional)
1. Create an empty GameObject as a child of your ground
2. Name it "GroundVegetation"
3. Attach `GroundVegetationSpawner.cs` to it
4. Configure in Inspector:
   - **Grass Tufts Count**: 1500
   - **Bush Count**: 250
   - **Spawn Radius**: Match your ground size (typically 100-200)

## Key Parameters

### EarthGroundRealistic
- **Vegetation**: Enable/disable procedural vegetation in texture
- **Normal Strength**: Higher = more visible surface detail (0.5 is good)
- **Base Tiling**: Lower = larger features, Higher = smaller details
- **Detail Tiling**: Fine surface variation frequency

### GroundVegetationSpawner
- **Grass Height**: How tall grass tufts are (0.1-0.35 recommended)
- **Bush Height**: Taller bushes (0.5-1.2 recommended)
- **Spawn Radius**: Area around origin where vegetation spawns
- **Density Threshold**: Noise level above which vegetation appears (0.45 = moderate density)

## Visual Improvements Over Original

| Feature | EarthGroundVisual | EarthGroundRealistic |
|---------|------------------|----------------------|
| Base Colors | 3 colors | 5+ colors (grass, soil, stone, etc.) |
| Normal Maps | Single-scale | Multi-scale for depth |
| Roughness | Not used | Separate roughness map |
| Vegetation in texture | Basic | Detailed with noise-based distribution |
| 3D Vegetation | None | Grass tufts + bushes |
| Material Quality | Basic | PBR-ready (Metallic/Roughness) |

## Performance Considerations

- **Texture Generation** happens at startup (1-2 seconds)
- **Vegetation Spawning** takes 0.5-1 second with 1500+ objects
- Use **LOD (Level of Detail)** if fps is low:
  - Reduce `albedoSize` to 512
  - Reduce `grassTuftsCount` to 500
  - Reduce `bushCount` to 100

## Troubleshooting

### Ground looks flat
- Increase `normalStrength` in EarthGroundRealistic (try 0.8-1.0)
- Ensure lighting is properly setup (directional light needed)

### Vegetation not visible
- Check `enableVegetation` is true in EarthGroundRealistic
- Check `vegetationAmount` is > 0.2
- Check vegetation spawner is running (check console for "Spawned X tufts")

### Performance issues
- Reduce texture sizes: `albedoSize` from 1024 to 512
- Reduce vegetation counts
- Enable GPU Instancing on materials (material settings)

### Uneven ground
- Adjust Perlin noise scales in Inspector
- Try different `largeScale`, `mediumScale`, `fineScale` values

## Integration with PlanetariumManager

The ground textures work independently from `PlanetariumManager.cs`. The planetarium rendering and star positioning are unaffected.

For best immersion:
1. Position the ground Plane at Y=0 (camera height reference)
2. Scale it to match your expected environment size
3. Set lighting (add Directional Light for natural shadows)
4. Test from VR camera viewpoint

## Advanced: Custom Materials

To use custom materials for vegetation:

```csharp
// In Inspector or code:
// 1. Create Material assets (grass.mat, bush.mat)
// 2. Assign in GroundVegetationSpawner component
grassMaterial = Resources.Load<Material>("Materials/grass");
bushMaterial = Resources.Load<Material>("Materials/bush");
```

## Regenerating Textures at Runtime

Both scripts expose regeneration methods:

```csharp
// Regenerate textures with new parameters
EarthGroundRealistic ground = GetComponent<EarthGroundRealistic>();
ground.RegenerateTextures();

// Regenerate vegetation
GroundVegetationSpawner spawner = GetComponent<GroundVegetationSpawner>();
spawner.RegenerateVegetation();
```

This is useful for creating variations without restarting the app.

---

**Created for PlanetariuVR Project**  
Compatible with Unity 2020.3+  
Requires: Standard Shader or better
