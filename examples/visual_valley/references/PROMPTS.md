# Visual Valley reference prompts

Date: 2026-08-31  
Tool: Codex built-in `image_gen`  
Model identifier: not exposed by the built-in tool  
Publication status: review required before distribution

These images are art-direction and reconstruction inputs. They are not runtime captures, terrain
heightmaps or proof that the scene has reached the depicted quality. The canonical Forge source is
`../world-sketch.v1.json`; the final proof remains the rendered native and Studio scene.

## Top-down master plan v1

```text
Use case: stylized-concept
Asset type: production top-down world plan for a 512 x 512 metre RPG environment, intended as input for terrain, biome, spline and landmark reconstruction
Primary request: an original, highly detailed, sunlit European high-fantasy valley master plan with readable geography and believable hydrology
Scene/backdrop: a broad green valley enclosed by rugged alpine foothills; one river enters beneath a northeastern waterfall, bends naturally through the valley and exits southwest; a secondary creek joins it; a worn road enters from the south, crosses the river at one stone bridge, reaches a compact village on a dry western terrace, then climbs toward an ancient pale-stone watch keep on the northern ridge; mixed deciduous and conifer forest covers slopes while sunny meadow, rocky outcrops, wetlands, riverbanks and buildable clearings remain visually distinct
Style/medium: premium realistic 3D terrain maquette blended with satellite-quality environment concept art; production design reference, not a decorative fantasy map
Composition/framing: exact orthographic nadir view, square canvas, no perspective and no isometric tilt; the entire valley fills the image with consistent scale and clear silhouettes
Lighting/mood: bright neutral late-morning daylight, uplifting and adventurous, soft controlled shadows so terrain regions remain legible; rich natural greens, warm stone and clear blue-green water; never gloomy or post-apocalyptic
Materials/textures: detailed rock strata, varied forest canopy, grasses, meadow flowers, eroded banks, shallow water, gravel road and old European masonry
Constraints: no text, labels, icons, border, UI, compass, logo or watermark; no characters; no franchise-specific design; original geography; preserve large navigable corridors and plausible bridge/road/river relationships
Avoid: dark horror mood, muddy monochrome palette, impossible cliffs, miniature toy look, painterly blur, fisheye, oblique camera, isometric view
```

## Hero perspective v1

Input image: top-down master plan v1 as geography and composition reference.

```text
Use case: stylized-concept
Asset type: AAA game environment perspective concept for the same 512 x 512 metre Visual Valley shown in Image 1
Input images: Image 1 is the top-down geography and composition reference; preserve its river, waterfall, bridge, road, village terrace, ridge keep, forest masses and navigable meadow relationships
Primary request: show the valley as a believable in-engine hero view that can guide Blender terrain sculpting, modular assets, lighting and the final Bevy/WebGPU scene
Scene/backdrop: camera stands at human eye height just north of the southern stone bridge and looks upstream toward the northeastern waterfall; the clear blue-green river leads through the foreground and middle distance; the warm medieval village sits on the raised western terrace; the pale-stone watch keep crowns the northern ridge; layered alpine foothills and mixed forest frame the scene without hiding the navigable valley
Style/medium: premium photorealistic AAA environment concept art with physically believable terrain, architecture, foliage and water; original European high fantasy, grounded historical materials, no imitation of a specific franchise
Composition/framing: cinematic 16:9 wide establishing shot, human-scale 28 mm lens, strong foreground riverbank detail, readable midground route and settlement, grand background waterfall and keep; clear depth layers and gameplay navigation
Lighting/mood: bright golden late-morning sunlight after a light rain, uplifting sense of adventure and discovery, soft atmospheric haze and restrained volumetric rays; saturated but natural greens, warm plaster and stone, sparkling water; absolutely not gloomy or depressing
Materials/textures: individually varied mature deciduous and conifer trees, dense undergrowth, ferns and meadow flowers, wet river stones, erosion, moss, timber-and-plaster houses, layered rock strata, distant mist
Constraints: preserve Image 1 topology and relative landmark placement; no characters, text, logo, UI or watermark; no copied franchise symbols; believable traversable slopes and road grades; production-detail quality suitable for environment modeling
Avoid: simple geometric trees, low-poly shapes, empty terrain, generic procedural repetition, dark fantasy, ruined wasteland, excessive bloom, painterly blur, impossible scale
```
