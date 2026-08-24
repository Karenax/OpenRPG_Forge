# Acceptation Studio — UX2 Terrain

État local au 17 août 2026 : **fonctionnel et qualifié** pour les huit routes Terrain UX2. Les
surfaces **Tuiles & raccords** et **Relief & propriétés** travaillent sur une zone explicitement
sélectionnée, passent par les commandes Application canoniques, puis prouvent dry-run, application,
undo/redo et sauvegarde/réouverture sans modifier la session source utilisée pour la preuve.

## Preuves

| Lot | Routes | Portail | Durée | Capture Forward+ inspectée |
|---|---|---|---:|---|
| UX2-B | AREA-003/006/007/008/009 | `ux2-b-terrain-tiles` | 70,61 s | `artifacts/verification/studio-increment/ux2-b-terrain-tiles-1920x1080.png` |
| UX2-C | AREA-004/005/010 | `ux2-c-terrain-surface` | 78,54 s | `artifacts/verification/studio-increment/ux2-c-terrain-surface-1920x1080.png` |

Les deux résumés `.summary.json` confirment build Studio Debug, tests ciblés Domain/Application/
ModuleFormat, smoke headless, capture 1920×1080 et contrôle de matrice. Les captures montrent une
surface scrollable, des sélections humaines, les aperçus et les actions transactionnelles sans
clipping. Les smokes exercent réellement la saisie de grille et de brosse, les gardes de révision et
d’empreinte, l’historique, le round-trip et la libération du workspace temporaire.

Le portail transversal de clôture UX2 a ensuite réussi avec **2 033 tests .NET**, **39 tests
Python**, des builds sans avertissement, un graphe Roslyn à **9 968 nœuds / 71 303 relations / 0
diagnostic**, puis les smokes Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution.

## Routes qualifiées

| Route | Qualification Studio |
|---|---|
| <a id="orf-area-003"></a>`ORF-AREA-003` | Construction modulaire par définition de tuile, grille et placement stable. |
| <a id="orf-area-004"></a>`ORF-AREA-004` | Relief de base édité par brosse directe et commande de terrain canonique. |
| <a id="orf-area-005"></a>`ORF-AREA-005` | Collection complète des features de tileset, sans réduction au premier élément. |
| <a id="orf-area-006"></a>`ORF-AREA-006` | Groupes de tuiles nommés, sélection multiple et identités conservées. |
| <a id="orf-area-007"></a>`ORF-AREA-007` | Effacement ou remplacement prévisualisé, avec traitement explicite des objets attachés. |
| <a id="orf-area-008"></a>`ORF-AREA-008` | Rotation, miroir et variante contrôlés sans normalisation silencieuse. |
| <a id="orf-area-009"></a>`ORF-AREA-009` | Ajustement compatible résolu par le service canonique, ambiguïtés refusées. |
| <a id="orf-area-010"></a>`ORF-AREA-010` | Propriétés complètes de tuile : animation, matériaux, lumière, collision, navigation et sockets. |

## Limites honnêtes

- l’édition porte sur les profils de terrain et leurs projections Studio ; elle ne revendique ni
  bake de navigation autonome, ni physique runtime, ni génération d’asset externe ;
- le parcours produit ne synthétise pas de tileset absent ; la preuve riche vit dans un workspace
  temporaire isolé et le projet ouvert conserve son état vide ou réel ;
- les deux lots préservent les profils voisins, les identités stables et les données non exposées ;
- ces portails restent incrémentaux (`graph_check=false`, `full_gate=false`) ; le graphe et le
  portail transversal ont été contrôlés séparément et réussis au jalon de clôture.

## Reproduction

```powershell
.\scripts\Verify-StudioIncrement.ps1 -Page terrain -Evidence ux2-b-terrain-tiles `
  -Capture artifacts\verification\studio-increment\ux2-b-terrain-tiles-1920x1080.png
.\scripts\Verify-StudioIncrement.ps1 -Page terrain -Evidence ux2-c-terrain-surface `
  -Capture artifacts\verification\studio-increment\ux2-c-terrain-surface-1920x1080.png
```
