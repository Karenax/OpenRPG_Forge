# ADR-0032 — Laboratoire qualité déterministe et non mutant

Date : 5 août 2026
Statut : accepté

## Décision

Les validations de localisation et les tests ForgeScript, dialogue, quête et rencontre sont réunis
dans un laboratoire de module déterministe. Les services d’analyse restent dans les couches
Application et Scripting ; `WorkspaceQualityLabService` orchestre les données persistées sans
introduire de modèle parallèle ni modifier les sources du module.

La localisation distingue les erreurs structurelles bloquantes — placeholders ou formes plurielles
incohérents — des traductions absentes et risques de débordement, qui restent des avertissements
visibles. La pseudo-localisation est calculée en mémoire ; l’interface auteur et le CLI peuvent
écrire une preuve SVG dans `.forge/quality`, tandis que runtime et MCP restent strictement en
lecture seule.

Les scripts sont compilés et exécutés par le runtime ForgeScript natif sous quotas, événements
autorisés et monde simulé. Les dialogues et quêtes sont parcourus avec des fixtures d’état
déterministes ; les quêtes vérifient aussi l’injection d’événements, les portées joueur/groupe/monde
et la reprise. Les rencontres utilisent les entités réellement placées, la surface de navigation
canonique, trois profils de groupe et les seeds 17, 42 et 2026, puis imposent un nettoyage sans
résidu.

## Conséquences

- le rapport possède une empreinte SHA-256 stable et est partagé par Creator, runtime, CLI et MCP ;
- les exécutions de test ne changent ni la révision ni les JSON du projet ; seul l’aperçu SVG
  explicitement demandé rejoint le cache `.forge` ignoré par Git ;
- une langue incomplète n’empêche pas le playtest tant que ses replis sont structurellement sûrs ;
- aucune migration n’est requise : projet `0.6.19` et workspace `1.13.0` restent courants ;
- les tests de rencontre v1 évaluent les créatures placées ; un futur générateur d’encounters
  dynamique devra fournir ses propres fixtures au même contrat.
