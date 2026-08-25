# ADR-0021 — Network Kit autoritaire et déterministe

## Statut

Accepté pour G9 Network Beta. Frontière de transport concrétisée par l'ADR-0048 le 25 août 2026.

## Décision

Le multijoueur repose sur un noyau .NET `Forge.Network` indépendant de l'adaptateur graphique. Le client envoie des intentions immuables; seul `AuthoritativeSession` produit une nouvelle révision du monde. Chaque décision contient un code stable et une entrée d'audit.

Le transport reste une frontière de `Forge.Network`. G9 commence avec des scénarios mémoire
déterministes pour les tests à deux clients, puis l'hôte headless branche le transport réseau sans
dupliquer les règles. La mise en œuvre concrète actuelle est le namespace `Forge.Network.Transport`,
avec `TcpAuthoritativeServer`, `TcpGameClient` et le codec de trames commun. Les fichiers de module
portent une empreinte et une version de protocole ; une session incompatible est refusée avant sa
publication dans le monde.

Les identités, personnages, groupes et zones utilisent des UUID stables. Les secrets ne sont jamais stockés dans les documents de module. La persistance externe reste derrière une interface et ne devient pas une condition du jeu solo.

## Conséquences

- aucune mutation gameplay directe depuis un client ou un nœud graphique;
- rejeu déterministe des intentions acceptées;
- audit, métriques et diagnostics partagent les mêmes décisions serveur;
- le mode solo continue de fonctionner sans Network Kit;
- G10 Live Director devra employer les mêmes intentions et permissions.

## État de mise en œuvre

L'ADR-0048 fournit le listener TCP réel au runtime et à `Forge.Server`. Le raccord actuel réplique
l'admission, la zone et les poses ; les intentions gameplay plus riches déjà modélisées dans
`Forge.Network` ne sont pas encore toutes acheminées par ce transport. Le détail opérationnel est
maintenu dans `docs/MULTIPLAYER_INTERNET.md`.
