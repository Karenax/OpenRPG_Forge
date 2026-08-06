# G13-E — Rôle, activité ambiante et auto-level des créatures

## Périmètre

- `ORF-CRE-030` — champs de lore et tags de rôle configurables par ruleset, sans effet mécanique implicite ;
- `ORF-CRE-047` — graphe d’activité ambiante avec idle sets pondérés, horaires, interactions,
  waypoints, conversations et politique d’interruption/reprise ;
- `ORF-CRE-048` — template de progression déterministe avec seed, cible, allocations, preview,
  diff, application atomique et verrouillage manuel.

## Contrat livré

`CreatureRoleBehaviorProfile` est un document canonique v1 attaché au blueprint de créature. Son
UUID, sa provenance, ses métadonnées de rôle, son graphe ambiant, son template et ses overrides
restent indépendants du moteur. Les champs de lore sont de simples données explicites : le runtime
ne déduit ni bonus, ni faction, ni pouvoir d’une divinité, d’une occupation ou d’un tag.

`SetCreatureRoleBehaviorHandler` impose révision, type de blueprint, lecture seule, UUID unique,
références vers la progression, validation localisée, dry-run, patch, inverse, audit et historique.
Si l’auteur active l’application automatique, le même commit remplace atomiquement le profil et la
progression calculée. Le projet agrégé passe en `0.6.26`, le workspace en `1.20.0`, et les versions
`0.6.25`/`1.19.0` migrent avec un profil absent. JSON et `.orpmod` conservent exactement le contrat.

L’écran **Rôle, activité & auto-level** fournit catalogue, trois onglets, états vide/erreur et
résumé à 21:00. Il montre le set/clip actif et jusqu’à huit changements de progression avant
application. `CreatureRoleBehaviorScriptService` expose un handle UUID/schéma en lecture seule.
Dans la Crypte, le Gardien possède le rôle `guardian`, le lore `deity.embers`, un horaire nocturne
et une animation ambiante visible ; le runtime avance ce comportement avant le combat puis conserve
le playthrough quête/combat/cadavre/butin.

## Preuves

- domaine : 4 tests de validation, horaire, absence optionnelle, verrouillage et déterminisme ;
- commande et handle script : 4 tests de dry-run/apply/undo/redo, références et lecture typée ;
- persistance : migration, round-trip JSON exact et export/chargement `.orpmod` ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_PASS` après G13-C et G13-D ;
- runtime ciblé : `OPENRPG_FORGE_RUNTIME_G13E_CREATURE_ROLE_BEHAVIOR_PASS`, puis
  `OPENRPG_FORGE_RUNTIME_SMOKE_PASS` sur le workspace `1.20.0` de la Crypte ;
- gate intégré : `OPENRPG_FORGE_VERIFICATION=PASS`, avec 876 tests .NET, 24 tests Python
  généraux, 30 tests Python du pipeline d’assets et un graphe frais de 4 739 nœuds / 11 700 relations.

## Limites explicites

- Les identifiants de waypoint et de conversation sont prêts pour résolution par un registre de
  monde ; le prototype de la Crypte n’impose aucune référence externe pour rester autonome.
- Le runtime applique un mouvement ambiant léger et sélectionne un clip d’asset compatible. Un
  éditeur graphique de nœuds et la navigation de routines multi-zone restent des extensions futures.
- Le serveur autoritaire devra répliquer l’état d’horaire et d’interruption en multijoueur ; le
  contrat déterministe et sa seed évitent déjà une décision divergente des clients.
