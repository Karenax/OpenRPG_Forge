# G9-D — Opérations, chat et persistance

## Exigences

`ORF-MP-017`, `ORF-MP-018`, `ORF-MP-019`, `ORF-MP-020`, `ORF-MP-022`.

## Livré

- rôles joueur, modérateur et administrateur, capacités explicites et portées de zone;
- commandes distantes en preview/apply, diff sémantique, révision et anti-rejeu;
- sanctions mute/kick/ban avec motif, expiration, requêtes d'état et audit;
- chat local, groupe, global et système avec séquence et filtrage de visibilité;
- snapshots de monde canoniques, hashés et transactionnels derrière `IWorldSnapshotStore`.

## Invariants testés

Un joueur ne peut pas gérer le monde. Un modérateur hors de sa zone est refusé. Une commande rejouée ou obsolète ne s'applique pas. Un compte muet ne publie pas. Une sauvegarde concurrente obsolète lève `NET_PERSISTENCE_REVISION_STALE`.

