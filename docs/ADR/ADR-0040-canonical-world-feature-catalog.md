# ADR-0040 — Catalogue canonique du monde vivant

## Statut

Accepté — 7 août 2026.

## Contexte

G13-N à G13-AB ajoutent 55 capacités avancées réparties entre placeables, portes, triggers,
rencontres, routes, marchands, compagnons et scripts. Les stocker dans des nœuds du moteur, des
fenêtres d'éditeur ou des tables séparées créerait plusieurs sources de vérité et empêcherait un
round-trip fiable dans le projet, le workspace et le `.orpmod`.

## Décision

`ModuleDefinition` possède un `WorldFeatureCatalog` v1 à identité égale au module. Chaque profil a
un UUID, une version de schéma, une provenance et des références typées vers les blueprints, zones,
dialogues ou quêtes canoniques. `WorldFeatureCatalogValidator` vérifie les références et invariants.
Les décisions d'exécution sont produites par `WorldFeatureRuntimeResolver`, pur et déterministe.

L'écriture passe uniquement par `SetWorldFeatureCatalogCommand` avec révision, dry-run, diff, audit
et historique. Le Studio Monde vivant, ForgeScript, MCP et le runtime consomment ce même agrégat.
Les nœuds 3D ne deviennent jamais propriétaires des règles.

Le format projet passe de `0.6.31` à `0.6.32`, le workspace de `1.25.0` à `1.26.0`. Un document
ancien migre vers un catalogue vide stable. La Crypte fournie peut construire un profil de
démonstration déterministe si — et seulement si — son ancien catalogue est entièrement vide ; une
configuration créée par l'auteur est toujours prioritaire.

## Conséquences

- Les huit familles partagent validation, transaction, persistance, scripting et MCP.
- Les résolveurs se testent sans démarrer le moteur et le smoke 3D prouve l'intégration réelle.
- Ajouter une famille ou un champ exige migration de format et preuve de compatibilité.
- Le lot ne simule pas une économie ou une IA globale permanente : il fournit les contrats et
  décisions bornées que les futurs systèmes autoritaires pourront orchestrer.
