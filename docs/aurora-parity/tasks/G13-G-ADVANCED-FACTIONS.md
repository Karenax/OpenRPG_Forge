# G13-G — Factions avancées

## Périmètre

- `ORF-FAC-002` — hiérarchie de factions avec héritage explicite de tags, politiques et relations ;
- `ORF-FAC-008` — propagation de réputation par témoins selon perception, preuve, portée, mémoire et partage ;
- `ORF-FAC-009` — affiliations multiples avec rang, couverture, loyauté, poids, faction principale et expiration ;
- `ORF-FAC-010` — factions dynamiques temporaires, états alliance/guerre/trêve, expiration et révision serveur.

## Contrat livré

`FactionCatalog` est désormais un document canonique optionnel du module. Son profil avancé v1
porte UUID stables, provenance et autorité serveur. `FactionValidator` refuse références absentes,
cycles, doublons, bornes invalides et transitions temporelles incohérentes.
`FactionRuntimeResolver` calcule l’héritage, les affiliations actives et les rapports de témoins de
façon déterministe. La création dynamique, la diplomatie et l’expiration refusent toute mutation
cliente et produisent des événements révisionnés.

`SetFactionCatalogHandler` fournit dry-run, diff, inverse, audit et historique. L’écran **Factions
avancées** permet l’édition sans JSON dans trois onglets : hiérarchie, témoins/affiliations et
dynamique. ForgeScript expose des handles typés ; MCP fournit une lecture filtrée et bornée ainsi
qu’une prévisualisation sans mutation. Le projet agrégé passe en `0.6.28`, le workspace en `1.22.0` ;
les versions `0.6.27` et `1.21.0` migrent avec un catalogue optionnel vide. JSON, workspace scindé et
`.orpmod` conservent le même contrat.

La Crypte des Cendres contient trois factions, une hiérarchie, trois affiliations et une faction
dynamique. Le runtime résout quatre tags hérités, émet un rapport de témoin au premier assaut du
Gardien, passe réellement de guerre à trêve à sa mort, puis vérifie l’expiration autoritaire.

## Preuves

- domaine : 5 tests de validation, héritage, témoins, affiliations, autorité et expiration ;
- application : commandes et services script testent dry-run/apply/undo/redo et refus client ;
- persistance : round-trip JSON exact, save/close/reopen, migration et export/chargement `.orpmod` ;
- MCP : lecture filtrée/bornée et prévisualisation sans changement de révision ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13G_ADVANCED_FACTIONS_PASS`, suivi du smoke jouable complet ;
- porte intégrée : `OPENRPG_FORGE_VERIFICATION=PASS` avec 902 tests .NET, 24 tests Python généraux
  et 30 tests Python du pipeline d’assets ;
- graphe d’architecture : 4 835 nœuds, 12 006 relations, génération et contrôle déterministes.

## Limites explicites

- la simulation distribue aujourd’hui les événements diplomatiques dans le runtime local ; le
  transport multijoueur réutilisera l’autorité et les révisions de G9 sans dupliquer ce modèle ;
- l’interface utilise des tableaux structurés et des lignes guidées. Un éditeur matriciel visuel
  plus riche pourra se brancher sur la même commande sans changer le format ;
- les factions restent optionnelles : un module historique sans profil avancé reste jouable.
