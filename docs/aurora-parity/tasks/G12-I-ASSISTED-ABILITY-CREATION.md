# G12-I — Création de capacité assistée

## Portée

- exigence : `ORF-AI-020` ;
- créer une capacité autonome à partir du combat canonique existant ;
- rendre explicites ciblage, coût, recharge, effets, VFX, audio, tags IA et intention d'équilibrage ;
- résoudre le ForgeScript et l'item optionnel avant toute mutation ;
- exécuter cinq scénarios avec le vrai résolveur de combat ;
- appliquer la capacité et son orchestration runtime par une transaction partagée entre Toolset et MCP.

## Résultat livré

Le service G12-I produit une définition de capacité canonique et un document d'orchestration à UUID
stable, schéma v1 et provenance explicite. Le porteur est un blueprint de créature ou de joueur
disposant déjà d'un profil de combat éditable. Les dépendances ForgeScript et item sont résolues par
leur chemin ou slug réels ; une référence absente produit un diagnostic stable avec chemin localisé.

Avant toute application, le vrai `CombatResolver` exécute cinq scénarios reproductibles : cible
valide, cible invalide, coût de ressource, recharge et effet. Le rapport d'équilibrage expose score,
magnitude, coût, recharge et intention auteur. Une erreur de ciblage, de ressource, d'effet, de
style ou de dépendance bloque le plan.

La fenêtre **Capacité assistée** sépare design de combat, dépendances/gouvernance et preuves. Elle
affiche tests, équilibrage, citations, provenance, coût, diagnostics et diff. Toute correction
invalide la proposition. Le handler lie approbation, permission, révision, transaction, plan et
empreinte exacte ; le profil de combat et le document runtime sont sauvegardés et annulés ensemble.
Les quatre outils MCP propose/plan/apply/revert utilisent le même service, le même handler et la
même persistance.

## Non-objectifs

- aucun ForgeScript ou item absent n'est inventé silencieusement ;
- aucun nœud moteur n'est la source de vérité de la capacité ;
- aucun fournisseur IA réseau n'est obligatoire ;
- la création de modèle 3D via Blender reste le périmètre de G12-J (`ORF-AI-021`).

## Preuves

- 5 tests RPG Kit : génération, diagnostics localisés, tests combat, gouvernance, concurrence et
  undo/redo ;
- test MCP : propose/plan/apply/revert et round-trip disque du profil de combat et du document
  runtime ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12I_ABILITY_PASS` ;
- `scripts/Verify.ps1` protège le marqueur dans la porte intégrée.
