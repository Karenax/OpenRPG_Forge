# R1-022/023 — candidat de composition eau/fond

2026-09-08, branche Rust, base `f786a38aca2023f08d708687f99a47d14f34e6d4`,
worktree modifié préservé. **Expérimental, non promu ; Gate 1 ouverte.**
Suite de `R1_WATER_OPTICS_REASSESSMENT.md`, sans reprise d'asset ou code Cadle.

## Implémenté

- Ressource transitoire `WaterDepthOpticsPreview`, installée avant le plugin
  d'eau ; option native `--water-depth-preview`. Le lancement sans option garde
  le matériau opaque/différé existant. Aucune nouvelle source de carte.
- Eau seule routée par la transmission forward de Bevy, une couche de copie
  de fond, profondeur opaque prépassée lue par le shader. Calcul WGSL original
  de distance sur le rayon de vue et absorption précédemment testé sur GPU.
- Distance bornée par `max_depth_m` du profil ; couleur profonde utilisée comme
  transmittance à cette distance dans ce candidat. Décalage de réfraction borné
  par la force de teinte ; diffusion colorée approchée, pas simulation volumique.
- Reflets d'environnement conservés, **SSR de l'eau désactivé**. Application
  dynamique des qualités suspendue pour ce candidat ; diagnostic explicitement
  `optical_depth_preview`, statut CPU incomplet maintenu, aucune fausse
  qualification du profil demandé. La résolution optique authorée reste une
  intention, pas une preuve du chemin expérimental.
- Géométrie, source hash, largeur, collision et placements inchangés. Test de
  régression sur source/géométrie et test du témoin opaque conservés.

Cette voie n'est pas disponible dans l'interface Studio et n'est pas promue au
rendu par défaut. Elle ne réalise pas les budgets de marche de rayon authorés.
La transmission standard ne voit pas les objets alpha-blend rendus ensuite.

## Première paire native et correction de comparaison

Dossier local :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-depth-preview-20260908/`.
Même kit corrigé, 1 662 arbres, caméra `hero_reference_match`, temps figé et
captures à frame 300, sortie bornée frame 420. `Capture.ps1` ferme uniquement
son PID et vérifie `capture_verified`, exit 0 et absence d'événement ERROR.

Premier exécutable :
`DEEC322456E3AD85993816091A8DA52AE8168D349184B62625EFF51DF54E1704`.

- `before.png` : `903E9AD0B9E7253FE4D5DDC3B48632BC4C17F74586ED4453B1DDC0779AA440F7`.
- `after.png` : `ED33962671E57F0052CE7EFDF93137B3F1D9997042055BEAA776ED9F8301AF5A`.
- PIDs 29940 et 25968 terminés, sorties 0, aucun ERROR.

Inspection : raccord aux berges moins dur, mais eau très sombre et ondulations
toujours répétitives. **Pas d'acceptation artistique.** Les overlays ne sont pas
des benchmarks (témoin moyenne 21,01 ms / p95 27,44 ; candidat 19,87 / 25,43).
Pas de gain ou de coût équivalent revendiqué.

Défaut découvert dans la comparaison : retirer la préparation SSR retirait aussi
l'installation de TAA, ce qui affectait toute l'image. Première paire conservée
comme essai, pas comme isolation stricte du matériau. Le candidat a ensuite été
corrigé pour garder TAA et un test spécifique vérifie TAA, profondeur, transmission
à une couche et absence de SSR. Aucun affaiblissement du témoin opaque.

## Validations

Premier check refusé sur le type du nombre de couches (`usize`, corrigé).
Premier Clippy refusé sur la taille du parseur ; sélection des previews regroupée
dans son helper, sans désactiver ce lint. Première version : 639 tests PASS / un
ignoré, fmt/Clippy strict/check WASM/build natif PASS, deux captures inspectées.

Après conservation de TAA : tests workspace exit 0 (sortie de session tronquée,
pas de nouveau décompte archivé revendiqué), Clippy workspace strict et check
WASM PASS. Le warning existant `proc-macro-error2` reste visible. Build/capture
du candidat corrigé consignés ci-dessous une fois terminés.

Shader partagé SHA256 :
`BA2CE1C1F36862A7A86B74C5831D0C39DE8F4F268E78101F99F5E41EB3CC70B9`.

## Reste à obtenir

Comparaison avec TAA conservé, vues rapprochées et mouvement ; meilleure teinte
et détails de courant moins périodiques ; chemin de réflexion compatible et
budgets/fallbacks complets ; intégration et pixels Studio ; mesures comparables
et validations humaines. Ne pas fermer R1-022/023 ou Gate 1 sur ce candidat.

## Capture corrigée terminée

`BuildTaa.ps1` exit 0 ; exécutable `forge_runtime_taa.exe` SHA256
`5287858D2E1EAB4FE8A40E34A46DCEBBD8D0ECE3EF5D770915CF923C7D9907AE`.
Capture `after-taa.png` SHA256
`F8282E6F21B1A22DA026C878629D93DD7E511661DBC7DABA46AF28E4A4198CB3`.
PID 4572 terminé (absence vérifiée), exit 0, capture vérifiée, aucun ERROR.

Image inspectée et comparée au témoin `before.png` : TAA conservé, raccord plus
doux, eau toujours trop sombre et relief périodique trop visible. Candidat
**non promu artistiquement** ; ne pas le présenter comme correction terminée.
Overlay moyenne 20,21 ms, p95 26,16 ms, 1 % low 35,8 FPS : non benchmark,
aucune qualification 60 FPS ou matériel cible. Le statut eau CPU incomplet
visible est attendu pour cette voie expérimentale non assimilée au profil.

Le prochain travail porte sur la transmittance et le courant/détail de surface,
avec conservation de cette composition comme base à corriger, sans nouvelle
retouche de carte. La lecture GPU et le raccordement sont maintenant implémentés
et exécutés en natif ; les budgets, la réflexion, le Studio et l'art restent ouverts.
