# ADR-0021 — Network Kit autoritaire et déterministe

## Statut

Accepté pour G9 Network Beta.

## Décision

Le multijoueur repose sur un noyau .NET `Forge.Network` indépendant de l'adaptateur graphique. Le client envoie des intentions immuables; seul `AuthoritativeSession` produit une nouvelle révision du monde. Chaque décision contient un code stable et une entrée d'audit.

Le transport est une frontière (`INetworkTransport`). G9 commence avec un transport mémoire déterministe pour les tests à deux clients, puis l'hôte headless branche le transport réseau sans dupliquer les règles. Les fichiers de module portent un fingerprint et une version de protocole; une session incompatible est refusée avant le lobby.

Les identités, personnages, groupes et zones utilisent des UUID stables. Les secrets ne sont jamais stockés dans les documents de module. La persistance externe reste derrière une interface et ne devient pas une condition du jeu solo.

## Conséquences

- aucune mutation gameplay directe depuis un client ou un nœud graphique;
- rejeu déterministe des intentions acceptées;
- audit, métriques et diagnostics partagent les mêmes décisions serveur;
- le mode solo continue de fonctionner sans Network Kit;
- G10 Live Director devra employer les mêmes intentions et permissions.

