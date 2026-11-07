# Explosion source

Mode: built-in ImageGen, followed by one transparent-background cleanup edit.
Asset: `../Runtime/Effects/Resources/ExplosionGenerated.png`.
Actual output: 1254 x 1254 RGBA, 4 columns x 4 rows, 16 frames in reading order.
The original generated source is retained unchanged. Playback now uses the derived 64-frame atlas below.

## 64-frame playback atlas

Asset: `../Runtime/Effects/Resources/ExplosionGenerated64.png`.
4096 x 4096 RGBA, 8 x 8 grid, 512 x 512 pixels per cell, eight-pixel transparent gutters.
The 64 frames uniformly sample the same source timeline. The Particle System playback curves are scaled
by `(63/64) / (15/16)` to preserve the approved timing and finish on the final frame without wrapping.

Regenerate from the project root:

```powershell
python Assets/SpaceAI2/Editor/Tools/bake_explosion_64.py
```

Dependencies: NumPy, OpenCV and Pillow. The bake estimates expansion from alpha-weighted moments, then
solves forward/backward DIS optical flow for residual motion. Iterative inverse warping and flow consistency
weights reconstruct intermediate images in premultiplied linear RGBA. Near-transparent background noise is
excluded from tracking; edge colors are extended into transparent gutters for native straight-alpha blending.
The source file is not modified. The output is written atomically so Unity cannot import a partial PNG.

The bake checks finite data, transparent cell borders, endpoint reconstruction and 64 distinct output frames.
Diagnostic frames, a comparison against ordinary crossfades, and a hash report are written to
`Temp/SpaceAI2Captures/OpticalFlow64`. The output improves temporal sampling; it does not invent additional
spatial detail or provide the temporal accuracy of a new fluid simulation. Native playback requires no
optical-flow shader or motion-vector texture.

## Generation brief

A single photorealistic 4x4 explosion animation atlas with fixed orthographic camera, cell centers and scale.
Sixteen consecutive phases: a small ignition, rapid expansion, orange fire inside dark cauliflower-shaped
smoke lobes, cooling and dissipation. Volumetric self-shadowing, fine wispy edges, transparent background,
empty gutters, no grid lines, text, sparks, debris, blue rings or scene background. Target resolution was
2048 square or higher; the built-in tool returned the actual dimensions listed above.

## Final cleanup prompt

Edit this exact 4 by 4 explosion animation sprite sheet into a production-ready TRANSPARENT sprite atlas.
KEEP all sixteen explosion frames in their current order, positions, sizes, detailed fire and smoke, temporal evolution.
Fix only the transparency and cell isolation. REMOVE THE ENTIRE WHITE / GREY BACKGROUND AND WHITE HAZE between and
around every explosion, including the lower three rows. Every pixel outside the actual dark smoke or orange fire
must be fully alpha zero. Do NOT paint white fog or glows, no opaque background, no checkerboard drawn into RGB.
Preserve the dark charcoal smoke with soft partially transparent edges, and preserve brilliant orange flame.
Each frame must fit its own equal quarter-width by quarter-height square with at least 5 percent EMPTY transparent
padding; no sprite touching neighbors. Last frame must be tiny remaining wisps with mostly zero alpha.
Output one high resolution square RGBA PNG, same exact 4x4 atlas layout, evenly aligned centers, no lines/text/borders.
This is a technical alpha-cleanup edit, not a redesign.
