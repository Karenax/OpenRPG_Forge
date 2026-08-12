# G9-D — Opérations, chat et persistance

## Exigences

`ORF-MP-017`, `ORF-MP-018`, `ORF-MP-019`, `ORF-MP-020`, `ORF-MP-022`.

## Livré

- rôles Player, Moderator, Builder, GameMaster, Administrator et Spectator, capacités standard et
  personnalisées, portées de zone ABAC;
- commandes distantes en preview/apply, diff sémantique, révision et anti-rejeu;
- sanctions mute/kick/ban avec motif, expiration, requêtes d'état et audit;
- chat local, groupe, tell, shout, DM, global et système avec proximité, langue, séquence,
  accessibilité, journal et bridges;
- snapshots de monde canoniques, hashés et transactionnels derrière `IWorldSnapshotStore`.

## Invariants testés

Un joueur ne peut pas gérer le monde. Un modérateur hors de sa zone est refusé. Une commande rejouée ou obsolète ne s'applique pas. Un compte muet ne publie pas. Une sauvegarde concurrente obsolète lève `NET_PERSISTENCE_REVISION_STALE`.

## Fermeture P1 R103

Le centre réseau expose les politiques d’accès et de chat sans édition JSON. Preview/apply/reject,
Undo/Redo, roundtrip, handles ForgeScript et plans MCP conservent la même autorité serveur. Le
scénario R103 vérifie une capacité custom Builder, la visibilité locale proche/lointaine, un tell,
le filtre de langue, le texte accessible et les reçus de bridge.
