# Acceptation Studio — UX2 Navigation

État local au 17 août 2026 : **fonctionnel et qualifié** pour les 27 routes Navigation. Le socle
partagé fournit destinations humaines, validation topologique et snapshot d’overlay ; chaque lot
mutant passe ensuite par sa commande canonique, une double garde révision/empreinte et un workspace
de preuve isolé.

## Preuves

| Lot | Routes | Evidence | Durée | Capture FHD inspectée |
|---|---|---|---:|---|
| B | AREA-031, WPT-001/005/006/017 | `ux2-nav-b` | 82,05 s | `ux2-nav-b-1920x1080.png` |
| C | WPT-002/003/004 | `ux2-nav-c` | 71,67 s | `ux2-nav-c-1920x1080.png` |
| D | WPT-007/008/009 | `ux2-nav-d` | 77,87 s | `ux2-nav-d-waypoint-patrol-fhd.png` |
| E | WPT-014/015/016 | `ux2-nav-e` | 74,13 s | `ux2-nav-e-waypoint-locators-fhd.png` |
| F | AREA-030 | `ux2-nav-f` | 76,36 s | `ux2-nav-f-area-transitions-fhd.png` |
| G | DOOR-008/009/011/019 | `ux2-nav-g` | 84,07 s | `ux2-nav-g-door-portals-fhd.png` |
| H | TRG-003 | `ux2-nav-h` | 86,11 s | `ux2-nav-h-fhd.png` |
| I | AREA-032, WPT-011 | `ux2-nav-i` | 97,65 s | `ux2-nav-i-aerial-routes-fhd.png` |
| J | AREA-033 | `ux2-nav-j-visibility-occlusion` | 102,00 s | `ux2-nav-j-visibility-occlusion-fhd.png` |
| ENV-J | AREA-034 | `ux2-env-j-map-fow-lod` | 99,60 s | `ux2-area-034-lod-navigation-fhd.png` |
| K | WPT-010 | `ux2-nav-k` | 105,30 s | `ux2-nav-k-fhd.png` |
| L | WPT-012 | `ux2-nav-l` | 102,35 s | `ux2-nav-l-fhd.png` |
| M | WPT-013 | `ux2-nav-m` | 103,26 s | `ux2-nav-m-fhd.png` |

Les portails couvrent Domain, Application, Studio.Core/Network/ModuleFormat selon le lot, puis le
smoke global et la capture Forward+ 1920×1080. Les captures ont été inspectées individuellement :
sélecteurs humains, collections, preview, diagnostics, scrollbar et footer restent lisibles sans
clipping. NAV-L et NAV-M ont respectivement repassé 66 et 5 tests ciblés avant leur portail final.

Le portail transversal de clôture UX2 a ensuite réussi avec **2 033 tests .NET**, **39 tests
Python**, des builds sans avertissement, un graphe Roslyn à **9 968 nœuds / 71 303 relations / 0
diagnostic**, puis les smokes Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution.

## Routes qualifiées

| Route | Lot | Qualification Studio |
|---|---|---|
| <a id="orf-area-030"></a>`ORF-AREA-030` | F | Portails de zone créés, modifiés ou supprimés par transaction topologique atomique. |
| <a id="orf-area-031"></a>`ORF-AREA-031` | B | Surface, agents, off-mesh et anchors authorés avec overlay déterministe. |
| <a id="orf-area-032"></a>`ORF-AREA-032` | I | Navigation volumétrique, obstacles et coûts avec services de path/steering purs. |
| <a id="orf-area-033"></a>`ORF-AREA-033` | J | Rooms, portails d’occlusion, occluders, culling, streaming et visualiseur. |
| <a id="orf-area-034"></a>`ORF-AREA-034` | ENV-J | LOD/HLOD par zone et asset ; planification sèche sans génération GLB. |
| <a id="orf-door-008"></a>`ORF-DOOR-008` | G | Destination typée et ancre globale avec yaw, preload et permissions. |
| <a id="orf-door-009"></a>`ORF-DOOR-009` | G | Lien one-way ou paire inverse atomique, voisins préservés. |
| <a id="orf-door-011"></a>`ORF-DOOR-011` | G | PortalUseRequested/Completed et veto prévisualisés. |
| <a id="orf-door-019"></a>`ORF-DOOR-019` | G | Obstacle/link/états/seuils de navigation dynamique. |
| <a id="orf-trg-003"></a>`ORF-TRG-003` | H | Trigger de transition, permissions et résolution typée sans transition moteur simulée. |
| <a id="orf-wpt-001"></a>`ORF-WPT-001` | B | Waypoint nommé, identité stable et labels sans UUID. |
| <a id="orf-wpt-002"></a>`ORF-WPT-002` | C | Icône, couleur, couche, visibilité et taille d’éditeur. |
| <a id="orf-wpt-003"></a>`ORF-WPT-003` | C | Map note, étage, catégorie, précision, policy, scope et tracking. |
| <a id="orf-wpt-004"></a>`ORF-WPT-004` | C | Révélation, événement, animation et persistance. |
| <a id="orf-wpt-005"></a>`ORF-WPT-005` | B | Point de départ depuis le catalogue d’anchors. |
| <a id="orf-wpt-006"></a>`ORF-WPT-006` | B | Destination de transition typée et inconnus hérités protégés. |
| <a id="orf-wpt-007"></a>`ORF-WPT-007` | D | Points de patrouille ordonnés à IDs stables. |
| <a id="orf-wpt-008"></a>`ORF-WPT-008` | D | Politiques forward/reverse, branches, poids, vitesse, attente et actions. |
| <a id="orf-wpt-009"></a>`ORF-WPT-009` | D | Arrivée et orientation résolues sans modifier les locators voisins. |
| <a id="orf-wpt-010"></a>`ORF-WPT-010` | K | Segments inter-zones, portail, reprise waypoint, prefetch, boucle et autorité. |
| <a id="orf-wpt-011"></a>`ORF-WPT-011` | I | Routes aériennes XYZ, courbe, banking et politique d’obstacle. |
| <a id="orf-wpt-012"></a>`ORF-WPT-012` | L | Points, voies, arrêts et politique de synchronisation des routes véhicule. |
| <a id="orf-wpt-013"></a>`ORF-WPT-013` | M | Ancres caméra, transform, regard, cadrage, FOV, durée, cadre sûr et tags. |
| <a id="orf-wpt-014"></a>`ORF-WPT-014` | E | Locators audio/VFX, position, purpose, tags, asset et blueprint humain. |
| <a id="orf-wpt-015"></a>`ORF-WPT-015` | E | Groupes, requêtes, membres ordonnés et résolution déterministe. |
| <a id="orf-wpt-016"></a>`ORF-WPT-016` | E | Sept couches de visualisation des routes et coût. |
| <a id="orf-wpt-017"></a>`ORF-WPT-017` | B | Validation topologique project-wide des destinations nouvelles. |

## Limites honnêtes

- les previews exécutent les services Domain/Application purs et dessinent un overlay ; elles ne
  déplacent pas d’acteur, ne font pas de bake Godot live et n’exécutent pas de transport réseau ;
- la route véhicule n’exécute aujourd’hui que position, vitesse et arrêt ; tangentes, largeur,
  voies, dépassement et sync restent des données authorées ;
- l’ancre caméra ne déplace aucune caméra live ; le runtime consomme directement position, FOV et
  look-at lorsqu’une séquence réelle la référence, tandis que rotation/cadrage/durée/cadre sûr/tags
  restent persistés ;
- les projets réels sans waypoint/route conservent un état vide honnête ; les fixtures riches sont
  isolées et nettoyées.

## Reproduction

```powershell
.\scripts\Verify-StudioIncrement.ps1 -Page navigation -Evidence ux2-nav-l `
  -Capture artifacts\verification\studio-increment\ux2-nav-l-fhd.png
.\scripts\Verify-StudioIncrement.ps1 -Page navigation -Evidence ux2-nav-m `
  -Capture artifacts\verification\studio-increment\ux2-nav-m-fhd.png
```
