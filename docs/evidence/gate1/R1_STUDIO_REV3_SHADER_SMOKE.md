# Gate 1 — Révision 3 et shaders corrigés dans Chrome réel

2026-09-05, Rust local non publié. Gate 1 reste ouverte.

## Exécuté

Trunk reconstruit depuis les sources courantes, sortie neuve
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-studio-taa-rev3-20260905\dist`.
Chrome charge `http://127.0.0.1:14624/?forge_scene=world-sketch-visual-valley`.
SHA-256 WASM : `e83375946e0ac030b4af4b128283620a8ba848b82e1121fa324038de6f75a13c`.

La vérification utilise les ledgers DOM réels, le lecteur de logs et deux captures de navigateur,
clair et pluie. Les captures sont conservées dans la conversation uniquement, pas comme fichiers
versionnés. L'extraction traçable des valeurs est dans `R1_STUDIO_REV3_SHADER_SMOKE.json`.

## Preuves obtenues

- Source/révision 3 : sélection et runtime concordants, une initialisation du monde et une caméra.
- Cinq hashes communs (terrain, végétation, environnement, eau, météo) identiques aux métriques
  natives finales de `r1-water-taa-20260905-final/human_bridge-clear/metrics.json`.
- Hash joueur identique au probe natif embarqué de révision 3 ; hash pont identique au même probe
  et au CLI, 201 meshes et cinq colliders créés côté CPU.
- Clair → pluie → clair effectué par les boutons du produit : 8 192 gouttes dans un batch,
  humidité 0 → 0,78 → 0, retour exact confirmé, sans commande de sauvegarde.
- Scène, variation météo et pluie visibles dans les captures ; les trois lectures de logs
  retournent zéro erreur/avertissement. Les anciennes erreurs de variante de shader ne sont pas
  observées dans ce bundle, y compris après activation de la pluie.
- 120 frames après 60 de préchauffage : moyenne CPU 12,5683 ms, p95 14,9 ms,
  1 % low 52,36 FPS. Rendu 1408 × 741, pas Full HD ; aucun benchmark de gate déduit.

Le lecteur du ledger conserve `gpu_presentation=not_observed` : c'est le statut de son probe
instrumenté, pas un échec d'ouverture du navigateur. Les captures sont une preuve visuelle séparée.
Contrairement au natif, aucune mesure isolée de la passe GPU TAA n'est obtenue dans ce navigateur.
Un lancement sans erreur et une scène affichée ne prouvent pas à eux seuls l'efficacité du lissage
ou l'absence de traînées dans tous les déplacements.

## Limites et prochaine étape

La vue joueur de départ montre beaucoup de surface sombre et une capsule de diagnostic ;
son placement et son cadrage doivent être inspectés avant de qualifier le parcours jouable.
Le correctif précédent de `human_bridge` concernait une caméra fixe d'asset-kit, pas cette caméra
joueur. Le kit Blender externe n'est toujours pas chargé dans le Studio. La traversée Chrome,
la vidéo, les douze vues et la revue humaine demeurent ouvertes.

Le serveur et l'onglet de preuve ont été arrêtés. Les autres onglets utilisateur sont préservés.
Trunk PASS, avertissement amont `proc-macro-error2 2.0.1` conservé. Aucun code produit modifié
dans ce lot ; pas de nouvelle suite Rust revendiquée. Aucun commit, push ou asset Cadle.
