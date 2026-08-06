# G13-F — Événements avancés de créature

## Périmètre

- `ORF-CRE-039` — fin de tick de combat ou de tour selon le mode, sans imposer des rounds au Core ;
- `ORF-CRE-041` — inventaire consulté et objet transféré avec acteur, cause et résultat ;
- `ORF-CRE-042` — repos terminé avec durée, ressources restaurées et interruptions ;
- `ORF-CRE-044` — heartbeat optionnel avec cadence, suspension hors intérêt, budget et backlog borné ;
- `ORF-CRE-045` — événement utilisateur namespacé, versionné et doté d’un payload typé.

## Contrat livré

`CreatureEventProfile` est un document canonique v1 optionnel attaché au blueprint de créature. Il
porte UUID, provenance, obligation runtime, API ForgeScript, autorité serveur, bindings et overrides.
Les frontières `CombatTickCompleted` et `TurnEnded` restent explicites : le Core ne dépend d’aucun
modèle de rounds. Les types utilisateur supportés sont chaîne, entier, nombre, booléen, entité et item.

`SetCreatureEventsHandler` impose révision, type de blueprint, lecture seule, UUID unique, validation
localisée, dry-run, diff, inverse, audit et historique. Le projet agrégé passe en `0.6.27`, le
workspace en `1.21.0` ; `0.6.26` et `1.20.0` migrent avec un profil absent. JSON et `.orpmod`
conservent exactement les bindings.

L’écran **Événements avancés de créature** fournit catalogue, trois onglets, états vide/erreur,
résumé du scheduler, dry-run et application. `CreatureEventScriptService` expose un handle typé en
lecture seule. MCP expose une ressource filtrée par blueprint et le dry-run de la commande canonique.
Dans la Crypte, le Gardien exécute réellement heartbeat, fin de tick, inventaire, repos et événement
`crypt.braziers_lit` avant de terminer combat, quête, cadavre et butin.

## Preuves

- domaine : 4 tests de validation, payloads, déterminisme, suspension, budget et backlog ;
- commande et handle script : 4 tests de dry-run/apply/undo/redo, lecture et dispatch typé ;
- persistance : 3 tests de migration, round-trip JSON exact et export/chargement `.orpmod` ;
- MCP : lecture filtrée et bornée, plus mutation en dry-run sans changement de révision ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_PASS` après G13-B à G13-E ;
- runtime ciblé : `OPENRPG_FORGE_RUNTIME_G13F_CREATURE_EVENTS_PASS`, puis
  `OPENRPG_FORGE_RUNTIME_SMOKE_PASS` sur le workspace `1.21.0` de la Crypte.
- porte intégrée : `OPENRPG_FORGE_VERIFICATION=PASS` avec 888 tests .NET, 24 tests Python
  généraux et 30 tests Python du pipeline d’assets ;
- graphe d’architecture : 4 782 nœuds, 11 852 relations, génération puis contrôle déterministes.

## Limites explicites

- Le prototype distribue les événements vers des handlers identifiés et trace les payloads ; le
  chargement dynamique de code arbitraire reste interdit. Les scripts passent par le pipeline
  ForgeScript gouverné existant.
- La réplication multijoueur des séquences et payloads restera sous l’autorité du serveur G9 ; le
  contrat impose déjà l’autorité serveur et des UUID stables.
- Les heartbeats hors zone d’intérêt sont suspendus et le rattrapage est limité à quatre dispatches,
  afin qu’un profil auteur ne puisse créer une reprise non bornée.
