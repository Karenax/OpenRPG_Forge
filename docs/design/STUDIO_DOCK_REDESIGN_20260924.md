# Studio — panneaux thématiques, 24 septembre 2026

## Portée

Implémentation Leptos/Rust dans Studio, inspirée des couleurs graphite, bronze et
ivoire et des titres serif de l'interface locale OpenNever. Aucun code ni asset
du simulateur macOS tiers n'est repris.

- Dock inférieur regroupant les huit sujets existants.
- Panneau du sujet actif ancrable à gauche, à droite ou en bas, ou flottant.
- Déplacement par la poignée du titre, réduction et réinitialisation.
- Disposition indépendante par sujet pendant la session ; les contrôles restent
  montés pour préserver leurs brouillons lors du changement de sujet.
- Bibliothèque d'objets intégrée au panneau Objets.

Les commandes métier et les documents canoniques ne sont pas modifiés par
l'ancrage. Un seul sujet est visible à la fois : les panneaux simultanés épinglés,
la persistance entre lancements et l'ancrage automatique au bord ne sont pas
implémentés dans ce premier lot.

## Vérifications

- Trois tests unitaires de disposition : réussite (bornes, coordonnées invalides,
  indépendance des sujets).
- Clippy WASM avec avertissements interdits : réussite.
- Vérification visuelle et candidat natif : à compléter après compilation.

La compilation réutilise `E:\OpenRPG_Forge\.build\cargo-target` sans cache
incrémental. Le script `scripts/Refresh-StudioGoldCandidate.ps1` remplace le candidat
local existant et invalide explicitement sa preuve précédente. Aucun historique
supplémentaire de candidats n'est créé. Ce lot ne clôture pas la Gate 2.
