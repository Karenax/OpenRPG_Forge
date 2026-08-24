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

## Extension — contexte canonique de validation des portes

Le 17 août 2026, `DoorWorldProfile` reçoit un contexte optionnel de validation runtime : destinations,
animations, collisions, justificatifs et scripts connus. La collision attendue reste une propriété
explicitement écrite dans la fondation de la porte ; elle n'est jamais déduite du catalogue connu.

`DoorWorldValidationService`, dans `Forge.RpgKit.Domain`, est l'unique façade qui projette une porte
du catalogue vers `DoorRuntimeService` et la validation des liens. La commande
`SetWorldFeatureCatalog`, le laboratoire qualité, le packager et MCP réutilisent cette façade. Si le
profil déclare la validation obligatoire au runtime, ses diagnostics bloquent toujours la commande
et le build, même lorsque la catégorie qualité générale `references` est optionnelle.

Le format projet passe à `0.6.37` et le workspace à `1.29.0`. Les versions `0.6.36` et `1.28.0`
restent lisibles avec un contexte absent et optionnel ; aucune valeur connue n'est inventée pendant
la migration.
