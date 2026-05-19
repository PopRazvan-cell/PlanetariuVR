# Atmospheric Effects & Night Sky Setup Guide

## New Components Added

### 1. **NightSkyDome.cs** - Night Sky Environment
- Realistic night sky gradient (dark blue to horizon)
- Moon with glow effect
- 200+ background stars with twinkling
- Integrated with your star visualization

### 2. **AtmosphericEffects.cs** - Particle & Visual FX
- **Dust Particles**: 500 floating dust particles for depth
- **Light Rays**: 8 divine light rays with floating animation
- **Fog**: Exponential squared fog for atmospheric depth
- **Fireflies**: 150 bioluminescent particles drifting around

### 3. **PlanetariumSceneManager.cs** - Central Orchestrator
- Manages all environmental systems
- Handles lighting setup
- Provides interface for time-of-day transitions

---

## Quick Setup (3 Steps)

### Step 1: Create Scene Manager
1. In your scene, create an **empty GameObject** named "SceneManager"
2. Attach **PlanetariumSceneManager.cs** to it
3. Leave settings at defaults - everything auto-creates!

### Step 2: Configure (Optional)
In Inspector, you can adjust:
- **Directional Intensity**: How bright the moonlight is (0.2 is subtle)
- **Ambient Intensity**: Overall scene brightness
- **Camera Height**: Where player spawns (1.8 for human height)

### Step 3: Test!
Play the scene. You should see:
- ✓ Dark blue sky with gradient to horizon
- ✓ Moon with glow in the sky
- ✓ Floating dust particles
- ✓ Light rays coming down
- ✓ Subtle fog for atmosphere
- ✓ Fireflies drifting around (greenish glow)
- ✓ Your planetarium stars visible

---

## What Each Component Does

### NightSkyDome
```
Sky Dome Radius: 1500 (how far away sky is)
- Moon Size: 50 units
- Background Stars: 200 tiny stars for depth
- Creates dome-like feeling
```

### AtmosphericEffects
```
Dust Particles (500):
  - Slowly drifts in scene
  - Semi-transparent
  - Adds depth perception

Light Rays (8):
  - Angled rays from above
  - Subtle glow
  - Float up and down gently

Fog:
  - Exponential squared mode
  - Hides far objects gradually
  - Creates horizon feel

Fireflies (150):
  - Bioluminescent glow
  - Green-yellow color
  - Moves around like insects
```

---

## Customization

### More Dramatic Night?
- Reduce **directionalIntensity** to 0.1
- Reduce **ambientIntensity** to 0.2
- Increase **fogDensity** in AtmosphericEffects

### More Fireflies?
Edit **AtmosphericEffects**:
```csharp
public int fireflyCount = 250;  // was 150
```

### Brighter Sky?
Edit **NightSkyDome**:
```csharp
public Color nightSkyColor = new Color(0.08f, 0.08f, 0.15f, 1f);  // was 0.05
```

### Different Light Ray Color?
Edit **AtmosphericEffects**:
```csharp
public Color rayColor = new Color(1f, 0.9f, 0.8f, 0.2f);  // warmer rays
```

---

## Integration with Existing Systems

✅ **Compatible with PlanetariumManager**
- Stars render normally on top of sky dome
- No conflicts
- Both systems work independently

✅ **Compatible with Ground System**
- Grass, dust particles all work together
- Lighting affects ground realistically
- Fog affects both ground and stars

---

## Performance Tips

If experiencing fps drops:

1. **Reduce Particles**:
   - Dust: 500 → 200
   - Fireflies: 150 → 75

2. **Lower Fog Complexity**:
   - Change from ExponentialSquared to Linear (simpler)

3. **Disable Expensive Features**:
   - Disable Light Rays (they have floating animations)
   - Keep Fog enabled (cheap effect)

4. **Light Quality**:
   - Disable shadows if needed: `mainLight.shadows = LightShadows.None`

---

## Advanced: Runtime Control

You can control atmosphere from code:

```csharp
PlanetariumSceneManager manager = GetComponent<PlanetariumSceneManager>();

// Transition to dawn (foggy)
manager.SetAtmosphericDensity(0.8f);

// Clear night (no fog)
manager.SetAtmosphericDensity(0.0f);

// Toggle fog completely
manager.ToggleFog(false);
```

---

## Visual Comparison

| Before | After |
|--------|-------|
| Plain green ground | Green ground + starfield |
| No sky | Beautiful night sky dome |
| Empty space | Atmospheric depth with particles |
| No mood | Immersive nighttime planetarium |

---

## Troubleshooting

### Dust particles not visible?
- Check particle count > 0
- Increase **dustDensity** to 100

### Light rays too bright?
- Reduce **rayColor** alpha: change 0.15f to 0.08f

### Fireflies not glowing?
- Increase **fireflyBrightness** to 0.6f

### Sky too dark?
- Increase **nightSkyColor** values (0.05 → 0.1)
- Increase **ambientIntensity** to 0.5

### Performance issues?
- See Performance Tips section above

---

## File Locations

```
Assets/
  ├── NightSkyDome.cs              (sky dome + moon + background stars)
  ├── AtmosphericEffects.cs        (dust, rays, fog, fireflies)
  ├── PlanetariumSceneManager.cs   (orchestrator & lighting)
  └── ATMOSPHERIC_SETUP_GUIDE.md   (this file)
```

---

**Enjoy your immersive night planetarium! 🌙✨**
