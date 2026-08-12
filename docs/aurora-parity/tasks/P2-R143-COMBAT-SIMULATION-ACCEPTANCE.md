# P2 R143 — Simulation de combat

`ORF-AI-028` est désormais un profil canonique de l'espace Assistant : UUID, schéma, provenance,
archétypes, seeds, budgets, pathing et seuils de phases. L'écran Assistant permet de configurer et
lancer la simulation sans éditer de fichier. La mutation du profil utilise la commande existante
avec plan, dry-run, permission, diff, audit, undo/redo et sauvegarde atomique.

Le moteur compare chaque paire d'archétypes sur des seeds reproductibles et rapporte taux de
victoire, durée, ressources consommées, temps de pathing et déclencheurs de phases, avec empreinte
stable. Le contrat MCP exige plan, transaction et permission, reste borné et ne modifie jamais le
projet.

Tests ciblés : commandes Assistant (7/7), simulation (incluse), round-trip profil (1/1) et MCP
(2/2). Preuve Toolset : `OPENRPG_FORGE_TOOLSET_R143_COMBAT_SIMULATION_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
