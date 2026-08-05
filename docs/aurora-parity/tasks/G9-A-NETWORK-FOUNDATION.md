# G9-A — Fondation serveur, autorité et lobby

## Exigences

`ORF-MP-001`, `ORF-MP-002`, `ORF-MP-003`, `ORF-MP-004`, `ORF-MP-005`.

## Livré

- hôte `Forge.Server` sans dépendance éditeur avec configuration JSON canonique et health marker;
- session serveur autoritaire, intentions révisionnées, refus des mutations inconnues et anti-rejeu;
- découverte directe et annuaire LAN derrière `IServerDirectory`;
- lobby, sélection/création de personnage et état prêt validés côté serveur;
- commande auteur commune au preview et à l'application, diff sémantique et audit.

## Preuves

- `Forge.Network.Tests`: 7/7;
- suite .NET globale: 439/439;
- tracker Python: 24/24;
- `Forge.Server --self-test`: `OPENRPG_FORGE_SERVER_READY` et `OPENRPG_FORGE_SERVER_HEALTH`;
- vérification globale: `OPENRPG_FORGE_VERIFICATION=PASS`.

## Limites reportées dans G9

Le transport socket, l'écran réseau auteur/client et le scénario réel à deux clients sont intégrés après les six lots de contrats afin qu'ils consomment une seule autorité complète.
