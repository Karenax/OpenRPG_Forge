# Acceptation Studio — UX2 Environnement

État local au 17 août 2026 : **fonctionnel et qualifié** pour les 34 routes de la page
**Environnement**, ainsi que `ORF-AREA-034` dont l’éditeur LOD est partagé avec Navigation. Les dix
surfaces B–K conservent les agrégats voisins, distinguent les deux transactions de l’audio de zone,
et bornent clairement les plans déterministes par rapport aux effets Godot ou matériels réellement
appliqués.

## Preuves

| Lot | Routes principales | Evidence | Durée | Capture FHD inspectée |
|---|---|---|---:|---|
| ENV-B | AREA-013..016 | `ux2-env-b-area-environment` | 73,59 s | `ux2-env-b-area-environment-1920x1080.png` |
| ENV-C | AREA-017/018/020, AUD-015 | `ux2-env-c-climate-water-reverb` | 79,86 s | `ux2-env-c-climate-water-reverb-1920x1080.png` |
| ENV-D | AREA-019, EE-005..008 | `ux2-env-d-rendering-sky` | 82,97 s | `ux2-env-d-rendering-sky-1920x1080.png` |
| ENV-E | AREA-021, AUD-013/014/018 | `ux2-env-e-area-audio` | 69,11 s | `ux2-env-e-area-audio-fhd.png` |
| ENV-F | AUD-001/002/005/008 | `ux2-env-f-audio-emitters` | 73,77 s | `ux2-env-f-audio-emitters-fhd.png` |
| ENV-G | AUD-003/004/006/007/009 | `ux2-env-g-audio-workbench` | 81,26 s | `ux2-env-g-audio-workbench-fhd.png` |
| ENV-H | AUD-011/012 | `ux2-env-h-audio-range-control` | 82,39 s | `ux2-env-h-audio-range-control-fhd.png` |
| ENV-I | AUD-010/017 | `ux2-env-i-audio-scatter-events` | 88,36 s | `ux2-env-i-audio-scatter-events-fhd.png` |
| ENV-J | AREA-029/034, EE-010 | `ux2-env-j-map-fow-lod` | 99,60 s | `ux2-env-j-map-fow-lod-fhd.png` |
| ENV-K | EE-009/030 | `ux2-env-k-water-graphics` | 98,06 s | `ux2-env-k-water-graphics-fhd.png` |

Chaque résumé dans `artifacts/verification/studio-increment` confirme build, smoke, capture
Forward+ Full HD et matrice. Les filtres ciblés couvrent les validateurs de profils, handlers
transactionnels, sérialiseurs, planners climat/rendu/audio/HLOD et services runtime purs. Les
captures ont été inspectées : collections, diagnostics, scroll et footer transactionnel restent
accessibles à 1920×1080.

Le portail transversal de clôture UX2 a ensuite réussi avec **2 033 tests .NET**, **39 tests
Python**, des builds sans avertissement, un graphe Roslyn à **9 968 nœuds / 71 303 relations / 0
diagnostic**, puis les smokes Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution.

## Routes qualifiées

| Route | Lot | Qualification Studio |
|---|---|---|
| <a id="orf-area-013"></a>`ORF-AREA-013` | B | Classification intérieur/extérieur et capacités authorées. |
| <a id="orf-area-014"></a>`ORF-AREA-014` | B | Cycle jour/nuit, courbes, événements et volumes complets. |
| <a id="orf-area-015"></a>`ORF-AREA-015` | B | Lumière simple/avancée et probes, avec données voisines préservées. |
| <a id="orf-area-016"></a>`ORF-AREA-016` | B | Brouillard global, bruit, couleurs, volumes et profils qualité. |
| <a id="orf-area-017"></a>`ORF-AREA-017` | C | Vent global et zones locales, planifiés sans prétendre les appliquer live. |
| <a id="orf-area-018"></a>`ORF-AREA-018` | C | États météo, seed, persistance et réplication authorés ; simulation déterministe. |
| <a id="orf-area-019"></a>`ORF-AREA-019` | D | Ciel physique, procédural ou panorama et transitions. |
| <a id="orf-area-020"></a>`ORF-AREA-020` | C | Corps d’eau complets, inventaire et plan de production. |
| <a id="orf-area-021"></a>`ORF-AREA-021` | E | Audio de zone en deux transactions explicites, jamais présenté comme atomique. |
| <a id="orf-area-029"></a>`ORF-AREA-029` | J | Carte 2D/3D, étages, pins et brouillard de guerre. |
| <a id="orf-area-034"></a>`ORF-AREA-034` | J | LOD/HLOD par zone et asset, plan sec sans générer de GLB. |
| <a id="orf-aud-001"></a>`ORF-AUD-001` | F | Définitions d’émetteurs nommées et gouvernées. |
| <a id="orf-aud-002"></a>`ORF-AUD-002` | F | Clip-sets ordonnés et pondérés, métadonnées et provenance conservées. |
| <a id="orf-aud-003"></a>`ORF-AUD-003` | G | Recherche par facettes et plan d’audition sans lecteur Studio. |
| <a id="orf-aud-004"></a>`ORF-AUD-004` | G | Séquence, random, shuffle et pondération déterministes. |
| <a id="orf-aud-005"></a>`ORF-AUD-005` | F | One-shot, boucle, région continue, compte fini et pilotage événementiel. |
| <a id="orf-aud-006"></a>`ORF-AUD-006` | G | Intervalles et variations complets sans clamp silencieux. |
| <a id="orf-aud-007"></a>`ORF-AUD-007` | G | Volume relatif, LUFS, ducking, distance, plafond et vumètre simulé. |
| <a id="orf-aud-008"></a>`ORF-AUD-008` | F | Instances 3D, atténuation, cône, occlusion, hauteur et virtualisation. |
| <a id="orf-aud-009"></a>`ORF-AUD-009` | G | Ambiance globale ou de zone et politiques d’auditeur. |
| <a id="orf-aud-010"></a>`ORF-AUD-010` | I | Scatter spatial déterministe avec volumes, surfaces, nav, biomes et exclusions. |
| <a id="orf-aud-011"></a>`ORF-AUD-011` | H | Portée interne/externe, courbes, absorption et sphères de diagnostic. |
| <a id="orf-aud-012"></a>`ORF-AUD-012` | H | Contrôle Start/Stop/Pause/Resume/Fade authoré et simulé sans exécuter de script. |
| <a id="orf-aud-013"></a>`ORF-AUD-013` | E | Couches d’ambiance adaptatives par heure, météo, lieu et danger. |
| <a id="orf-aud-014"></a>`ORF-AUD-014` | E | États musique, stems, transitions, cooldown et synchronisation de mesure. |
| <a id="orf-aud-015"></a>`ORF-AUD-015` | C | Preset et zones de réverbération avec gouvernance des impulsions. |
| <a id="orf-aud-017"></a>`ORF-AUD-017` | I | Événements audio typés projetés sur une copie d’état. |
| <a id="orf-aud-018"></a>`ORF-AUD-018` | E | Bus hiérarchiques, snapshots, plateformes, dynamique et accessibilité. |
| <a id="orf-ee-005"></a>`ORF-EE-005` | D | Workflow PBR et instances authorés sans réécrire les assets. |
| <a id="orf-ee-006"></a>`ORF-EE-006` | D | Tone mapping, HDR, exposition, point blanc et LUT. |
| <a id="orf-ee-007"></a>`ORF-EE-007` | D | Politique Forward+/par pixel clairement distinguée du backend actif. |
| <a id="orf-ee-008"></a>`ORF-EE-008` | D | Budgets de lumières et ombres par plateforme, simulés et diagnostiqués. |
| <a id="orf-ee-009"></a>`ORF-EE-009` | K | Qualité d’eau, réfraction, SSR, profondeur et replis. |
| <a id="orf-ee-010"></a>`ORF-EE-010` | J | LOD/HLOD, seuils, budgets, cross-fade, popping et overrides. |
| <a id="orf-ee-030"></a>`ORF-EE-030` | K | AA, MSAA, anisotropie, upscaling et politique de fallback. |

## Limites honnêtes

- les aperçus climat, eau, réverbération, audio, HLOD et capacités graphiques sont des plans ou
  simulations déterministes sauf lorsqu’un adaptateur existant est explicitement consommé ;
- aucune capture ne prouve un matériel GPU particulier, une convolution, un playback réel, un
  transport réseau, une flottabilité ou une génération d’asset ;
- les zones réelles dont les collections audio sont vides restent vides ; les preuves riches sont
  injectées uniquement dans des workspaces temporaires nettoyés ;
- les valeurs et tails non édités sont conservés, notamment entre les lots B–K qui partagent
  `AreaEnvironmentProfile`.

## Reproduction

```powershell
# Remplacer <mode> par une valeur de la table et <capture> par son fichier FHD.
.\scripts\Verify-StudioIncrement.ps1 -Page environment -Evidence <mode> -Capture <capture>
```
