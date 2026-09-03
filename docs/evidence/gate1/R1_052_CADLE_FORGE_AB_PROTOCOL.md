# R1-052 — Protocole A/B Cadle / Forge

Statut : **PRÊT POUR REVUE HUMAINE — ROUTE NON QUALIFIÉE**

Ce protocole rend la comparaison `R1-052` reproductible sans copier une image Cadle dans Forge. Le
contrat machine canonique est
[`r1-052-cadle-forge-ab-protocol.v1.json`](r1-052-cadle-forge-ab-protocol.v1.json). Il fixe les
versions, la machine, la résolution, le profil, les couples d'images, les axes de jugement et les
frontières des métriques. Il ne contient aucun verdict humain et ne ferme ni `R1-052`, ni Gate 1.

## Racines locales requises

Depuis le worktree Rust de qualification :

```powershell
$forgeEvidence = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-023-water-125ba0b-001'
$cadleReference = 'E:\OpenRPG_Forge\Cadle-GateC-Overlay-Probe-A-20260830'

pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1 `
    -VerifyExternal `
    -ForgeExternalRoot $forgeEvidence `
    -CadleReferenceRoot $cadleReference
```

Le validateur recoupe les tailles et SHA-256 des images Forge et Cadle avec le paquet versionné. Les
images Cadle sont ouvertes depuis `$cadleReference` uniquement. Elles ne doivent être ni copiées dans
le worktree, ni placées dans un rapport, une archive ou un commit.

## Alignement retenu

Les deux acquisitions ont été réalisées sur la même machine physique : Windows 11 Pro
`10.0.26200`, Ryzen 5 7600, RX 7800 XT et pilote Windows `32.0.31041.1004`. Les six images sont en
1920 × 1080 et en profil High, avec VSync demandé désactivé.

Les moteurs, les backends, les mondes et les caméras ne sont pas identiques. L'alignement porte donc
sur des **classes de cadrage sémantiques**, pas sur une correspondance pixel à pixel :

La capture Forge reste issue de `125ba0b`, ancêtre de la baseline logicielle `4e476b31`. La revue du
diff runtime entre ces commits porte le patch-id stable `5b2a82aa589ec7d0fd0eedd5bb4d9ab9d15ae6ba` :
elle ne trouve que l'ajout de la route RenderDoc, la correction du validateur qualité et des refactors
sans changement de rendu. Cette liaison n'assimile toutefois pas la capture à une acquisition exacte
du commit `4e476b31` ; le paquet conserve explicitement `exact_to_qualified_software_baseline=false`.

| Couple | Forge local | Cadle local in situ | Axes principaux |
|---|---|---|---|
| terrain et composition macro | `captures/distant-clear.png` | `tools/out/gate-c-overlay-vale/shot-ground.png` | terrain, profondeur, atmosphère, lisibilité |
| végétation à hauteur humaine | `captures/human-clear.png` | `tools/out/gate-c-overlay-forest/shot-ground.png` | densité, naturel, lumière, lisibilité, game feel |
| eau et berges | `captures/water-clear.png` | `tools/out/gate-c-overlay-water/shot-golden-shore.png` | surface, rives, réflexion/réfraction perçue, atmosphère, game feel |

Le relecteur ouvre chaque couple à sa résolution originale, sans zoom différent entre les deux images,
puis examine successivement :

1. terrain : relief, échelle, silhouette, transitions de matériaux et profondeur ;
2. végétation : densité perceptible, variété, intégration au sol, LOD visible et répétition ;
3. eau : lecture de la surface, rives, mouvement perçu, profondeur et relation au paysage ;
4. atmosphère : ciel, lumière, ombres, brume, étagement des plans et cohérence colorimétrique ;
5. lisibilité : points d'intérêt, séparation des plans, bruit visuel et orientation ;
6. game feel : envie d'explorer, échelle humaine, continuité du monde et crédibilité en mouvement.

Un verdict `accepted` exige que l'impact Forge soit jugé au moins comparable à Cadle en tenant compte
des différences de direction artistique. Un déficit concret sur un axe produit `rejected` ou
`inconclusive`, avec l'observation et le problème bloquant correspondants. Le protocole n'autorise
jamais une acceptation automatique.

## Frontière des coûts

Les FPS bruts ne forment pas un duel valable : Forge couvre 512 × 512 m en Bevy/Vulkan natif, tandis
que Cadle couvre environ 2 km² dans Chrome/Three/WebGL2/ANGLE D3D11 avec une charge de jeu différente.

- Forge documente 64 chunks de terrain, 8 282 placements de végétation, 64 lots d'herbe, 64 lots HLOD
  et 1 088 triangles d'eau. Son overlay courant expose le temps du pass opaque, pas le temps GPU de
  frame entière. Les compteurs globaux soumis restent dus à `R1-037`.
- Cadle documente, pour la vue eau, 203,3 FPS, 4,92 ms moyens, p95 6,2 ms, 252 appels et 2 869 452
  triangles ; la scène dense mesure 169,5 FPS, 5,90 ms moyens, p95 7,7 ms, 227 appels et 2 872 960
  triangles. Ces valeurs servent uniquement à caractériser son coût propre.
- La RX 7800 XT reste un GPU de développement. Le benchmark cible `R1-041` sur RTX 3060 ou RX 6600
  n'est pas remplacé par cette comparaison.

Le verdict humain commente donc le coût **par système et par frontière connue**, sans ordonner les
deux produits sur leurs FPS bruts.

## Reçu humain

Le reçu séparé défini par
[`GATE1_HUMAN_REVIEW_PACKET.md`](GATE1_HUMAN_REVIEW_PACKET.md) doit citer `R1-052`, les trois couples
d'artefacts et le présent protocole, puis contenir une observation concrète pour chacun des six axes.
Jusqu'à ce reçu nommé et vérifié, `human_review_performed=false`, `r1_052_qualified=false` et
`gate1_transition_authorized=false` restent inchangés.
