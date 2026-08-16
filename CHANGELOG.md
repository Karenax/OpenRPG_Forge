# Changelog

Toutes les modifications notables d'OpenRPG Forge sont consignées ici. Les versions produit suivent
SemVer ; les versions de schéma persistent séparément dans leurs documents.

## [Unreleased]

### Changed

- Forge Studio : préparation d'une cadence parallèle contrôlée avec deux worktrees d'implémentation
  bornés par page, un slot d'audit en avance, propriété centrale des routeurs/matrices/preuves et
  gel obligatoire avant les builds, tests, smokes et captures séquentiels ;
- Forge Studio UX1-U : événements de verrouillage et de déverrouillage avec version ForgeScript,
  liaisons avant/après, méthode, justificatif, acteur, veto contrôlé, aperçu accepté ou refusé sans
  écriture, projection RPG Kit indépendante, lecture ForgeScript/MCP, mode optionnel ou requis,
  parité commande/UI, préservation, undo/redo, round-trip et preuve Forward+ Full HD ;
- Forge Studio UX1-T : événements d’ouverture et de fermeture de placeable avec version ForgeScript,
  liaisons nommées, acteur, résultat, état précédent et autorité, aperçu de dispatch typé sans
  écriture, projection RPG Kit indépendante, lecture ForgeScript/MCP, mode optionnel ou requis,
  parité commande/UI, préservation, undo/redo, round-trip et preuve Forward+ Full HD ;
- Forge Studio UX1-S : trésor initial de placeable avec table d’objets nommés, catégories, poids,
  quantités, qualité, conditions, unicité, moments chargement/ouverture/réinitialisation, graine et
  état généré persistant, avec remise à zéro explicite, aperçu déterministe RPG Kit, lecture
  ForgeScript/MCP, parité commande/UI, préservation des événements voisins, undo/redo, round-trip et
  preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-R : piège de placeable avec déclencheur, effet, détection, désamorçage,
  réarmement, propriétaire, politique alliée, VFX, état armé et usage unique, avec aperçu RPG Kit
  sans effet de scène, sous-profils de sécurité indépendants, lecture ForgeScript/MCP, parité
  commande/UI, préservation des événements voisins, undo/redo, round-trip et preuves Forward+ Full
  HD, QHD et 4K ;
- Forge Studio UX1-Q : politique de justificatif de placeable avec conservation, consommation,
  charges, partage, permission virtuelle et audit, avec aperçu de décision RPG Kit sans mutation
  d’inventaire, lecture ForgeScript/MCP, parité commande/UI, préservation du verrou et des données
  cachées, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-P : verrouillage de placeable avec quatre états, clé item réelle, justificatifs,
  défi, code masqué, piratage, droits, reverrouillage et brouillage, avec aperçu et résolution RPG
  Kit, lecture ForgeScript/MCP sans secret, parité commande/UI, préservation, undo/redo, round-trip
  et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-O : liaisons de conversation de placeable vers un dialogue et son point d’entrée,
  avec déclencheurs interaction, état, utilisateur, objet et événement, priorité, usage unique,
  aperçu de résolution RPG Kit, lecture ForgeScript/MCP, préservation du dialogue Aurora historique,
  parité commande/UI, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-N : faction principale et appartenances de placeable, avec politiques d’hostilité,
  crime, accès et ciblage, aperçu de décision RPG Kit, lecture ForgeScript/MCP, préservation du champ
  Aurora historique, parité commande/UI, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-M : protection plot avec actions protégées, droits requis, feedback joueur et
  dérogation MJ auditée, avec aperçu des quatre décisions, enforcement RPG Kit, lecture
  ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+ Full
  HD, QHD et 4K ;
- Forge Studio UX1-L : destructibilité avec santé, armure, résistances typées, étapes visuelles,
  effet de destruction, débris, réparation et aperçu local des dégâts, avec projection et exécution
  RPG Kit, lecture ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves
  Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-K : état visuel initial parmi six états humains, animations, apparences,
  interaction et transitions ajoutables/prévisualisées, avec projection RPG Kit, lecture
  ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+ Full
  HD, QHD et 4K ;
- Forge Studio UX1-J : transformation précise avec position, rotation, échelle, pivot, grille,
  parent humain, alignement de surface et animation, avec validation de hiérarchie, projection RPG
  Kit, lecture ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves
  Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-I : identité et apparence directes, noms et invitations localisés, tags, notes,
  asset, meshes, matériaux, icône, portrait et VFX, avec projection RPG Kit, lecture ForgeScript/MCP,
  parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-H : accès direct au conteneur, capacité et masse, propriétaire, droits,
  persistance, réplication, inventaire, butin et présentation, avec projection RPG Kit,
  lecture ForgeScript, parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+
  Full HD, QHD et 4K ;
- Forge Studio UX1-G : accès direct à l’objet utilisable, actions nommées ajoutables, portée, angle,
  durée, focus, autorité, conditions et événement `OnUsed`, avec aperçu complet, exécution runtime,
  sélection 3D, parité commande/UI, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-F : accès direct au décor statique, regroupement, instanciation, occlusion,
  fusion sûre et collision optionnelle, avec aperçu d’impact, sélection 3D, parité commande/UI,
  undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-E : blueprint de décor humain avec ressource importée et usage réel, collision,
  rôle statique ou utilisable, interaction, transform indépendant du moteur, scripts et sélection 3D,
  avec aperçu canonique, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-D : arbre humain de calques et groupes avec glisser-déposer, visibilité et
  verrouillage hérités, responsables, affectation et sélection d’objets, aperçu d’export partiel,
  contrat runtime, undo/redo et round-trip, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-C : écran de chargement humain avec sélection de médias importés, astuces
  localisées, progression et préchargement réels, aperçu mouvements réduits, fallback accessible,
  undo/redo et round-trip, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-B : redimensionnement avec ancrage et traitement explicite des objets hors
  limites, copie complète ou gabarit avec transitions configurables et identités remappées, aperçu
  canonique, undo/redo et round-trip, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-A : assistant humain de création de zone, gabarits vide/jouable, profils de
  fondation, aperçu sans écriture, undo/redo et document indépendant vérifié après réouverture,
  avec overlay responsive et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX0-J : renommage humain prévisualisé avec identité stable, migration de références
  et undo/redo, puis diagnostics structurés, liens profonds, correctifs transactionnels et export
  expurgé, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX0-I : variables de projet typées par noms humains, cache ForgeScript local
  réellement compilé/incrémental et guide joueur à huit pages localisées, avec commandes canoniques,
  undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX0-H : configuration humaine et transactionnelle de l’abandon de cinématique, du
  calendrier du monde et de l’échelle de progression, avec exécution runtime et preuves Forward+
  exactes Full HD, QHD et 4K ;
- schéma projet persistant `0.6.36` : chemins/handlers ForgeScript sûrs pour repos, heartbeats
  globaux et événements utilisateur typés, avec migration compatible depuis `0.6.35` ;
- schéma projet persistant `0.6.35` : liaisons de cycle de vie par contexte et chemins/handlers
  ForgeScript facultatifs pour les événements de jeu, avec migration déterministe depuis `0.6.34`.

## [0.6.34] — 2026-08-09

### Added

- profil canonique de build déterministe et incrémental ;
- gouvernance v1 des worktrees, de la CI, des fixtures, des versions et des ADRs.
- fondation de créature persistante, éditable, validée et résolue au runtime.

### Validation

- portail intégré `scripts/Verify.ps1` ;
- fixtures tiny, vertical slice, stress, malicious et migration versionnées.
