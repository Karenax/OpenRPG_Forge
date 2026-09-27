# Gate 1 — Arbres procéduraux ramifiés

2026-09-05, branche Rust, travail local non publié. Candidat retenu pour poursuivre le prototype,
pas acceptation artistique humaine ni clôture de Gate 1. CI distante **NOT RUN** sous ADR-0006.

## Changement implémenté

Les trois cônes pleins proches et les deux cônes moyens sont remplacés par des volumes de
feuillage disposés autour d'un tronc plus fin : huit étages de sept branches au proche,
cinq étages de cinq branches au moyen. Angle, longueur et hauteur varient de façon déterministe
avec le bruit entier déjà utilisé par le renderer. Chaque bouquet est un tétraèdre à faces
orientées vers l'extérieur ; une petite pointe terminale ferme la cime. L'épaisseur décroît
vers le sommet, dont la hauteur reste exactement 5,65 m avant mise à l'échelle du placement.

Aucune entité par branche : ce sont toujours deux meshes partagés pour les deux LOD d'arbre,
un matériau commun, le vent existant, les mêmes plages de visibilité et les mêmes lots HLOD.
Le proche passe de 43 à 249 triangles / 731 sommets ; le moyen de 22 à 116 / 338 sommets.
La géométrie HLOD, les arbustes, les fougères et l'herbe ne sont pas changés par ce lot.
Les 7 433 placements, dont 636 arbres, 3 666 entités projetées, 64 lots herbe et 64 HLOD
restent identiques. Aucun déplacement authoré, nouvelle dépendance, texture ou asset Cadle.

## Validation et correction du candidat

- Premier candidat exécuté et rejeté visuellement : feuillage trop mince, arbres squelettiques.
  Ses captures restent sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-branched-trees-20260905`.
- Première densification refusée par deux tests : hauteur 5,6663 m au lieu de 5,65 m.
  Épaisseur des étages supérieurs corrigée ; le contrat HLOD n'a pas été relâché.
- Candidat dense : **331 tests workspace passent**, Engine 109 / Runtime 53.
  Test de reconstruction identique des positions/normales/couleurs, enveloppe et orientation
  des quatre faces d'un bouquet ; décomptes exacts et tests de culling existants conservés.
- Clippy workspace, toutes cibles et features, warnings refusés : PASS.
- `cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS.
  Compilation WASM seulement, pas nouveau lancement Chrome ou qualification des transitions LOD.
- Le test de hauteur compare explicitement les bits après correction du warning Clippy float-cmp ;
  il a été rejoué avec succès après les 331 tests, sans changement de code de rendu.

Captures natives `human_bridge` clair/pluie inspectées, vérificateur du collecteur PASS,
sources rehashées inchangées, code 0 et aucune ERROR. Dossier retenu :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-branched-trees-dense-20260905`.
Le manifeste contient les paramètres exacts, quatre entrées épinglées, PNG et métriques.

| Artifact | SHA-256 |
| --- | --- |
| Runtime capturé | `eccc363f2aa5cebb238a99502ac787097e235cf92477d7cc65b46f5a137d0959` |
| Capture clair | `a79eb9a56aea8fcb18464a9abb2fc86a3e7c3a24d116e7a4db294ae47e89a71c` |
| Capture pluie | `7c8207c195e624b54e4846bd4a003dd99bad7a2fe8e606b25130cd25cd09743e` |
| Manifest | `a52582dc449b735388115c28206cc6667e63bc5f585cedc7cc80562909c0b124` |

## Coût observé, sans qualification générale

Deux runs séquentiels sans compilation simultanée et sans capture PNG : RX 7800 XT/Vulkan,
1920 × 1080, AutoVsync, même caméra `human_bridge`, kit, terrain et clair. Entrées `source_hash`,
GPU, fenêtre, kit et placements comparées égales. Commande runtime commune :

```text
--world-sketch-visual-valley
--asset-kit-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json
--asset-kit-preview E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-after-20260905/human_bridge-clear/preview.json
--weather clear --metrics <fichier-neuf> --metrics-warmup-frames 240 --exit-after-frames 1200
```

Ancien exécutable conservé dans `r1-vegetation-normals-after-20260905/forge_runtime-baseline-candidate.exe`,
SHA-256 `d6318a1c00a197ba58692f92ee71fed9fb00f6b84bd741fa323283b1187b064d`.
Il provient du build workspace précédent ; son hash diffère du build natif isolé de la preuve
des normales. Ses compteurs confirment les anciens meshes (356 749 triangles logiques proches).
Le candidat provient également du build de tests workspace, avec les branches nouvelles.

| Mesure, 960 images chacune | Ancien | Candidat |
| --- | ---: | ---: |
| FPS moyen | 116,29 | 116,76 |
| p95 | 9,548 ms | 9,1823 ms |
| 1 % low | 89,21 FPS | 92,20 FPS |
| Images > 16,7 ms | 0 | 0 |
| Triangles logiquement sélectionnés proches | 356 749 | 487 765 |
| Triangles logiquement sélectionnés moyens | 53 409 | 113 193 |
| Triangles des meshes uniques | 305 508 | 305 808 |

Ces décomptes ne sont pas des draw calls ni le travail GPU effectivement visible. Le coût des
meshes uniques n'augmente que de 300 triangles grâce au partage ; le coût instancié augmente bien.
Un objet proche de moins est retenu au frustum après changement de son AABB, sans incohérence
de plage observée. Les deux runs finissent code 0 sans ERROR (20:51:45 et 20:51:58 UTC).

Métriques dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-branched-trees-perf-20260905` :
`baseline.json` SHA-256 `63097812d9b340d6b1e29b152cc7355c7090ebdb807af38ff06af527cd7df7d3` ;
`candidate.json` SHA-256 `d12cfde99c02eff819adf9400009e63335f1f31f5b84681b6f8b882473ceaffa`.
Une paire courte, un cadrage et un GPU ne prouvent ni un gain de vitesse ni une absence de
régression générale. Cette mesure ne ferme pas les critères de performance Gate 1.

## Limites visuelles

Le candidat dense casse les grandes surfaces coniques et conserve un feuillage fourni par
rapport au premier essai. Il reste stylisé et anguleux ; la diversité des espèces, les silhouettes
lointaines, les transitions avec le HLOD triangulaire et la cohérence avec les arbres du kit
restent à revoir. Cette inspection n'est pas l'acceptation humaine exigée par la roadmap.
Les matériaux du sol, l'eau et la densité authorée n'ont pas été retouchés ici.
