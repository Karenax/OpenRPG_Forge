# G7-09 — Palettes Standard et Custom

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-SHELL-005` — séparation des palettes Standard et Custom ;
- avancées partielles de `ORF-SHELL-004`, `ORF-PAL-006`, `ORF-PAL-007` et `ORF-PAL-016`.

## Résultat livré

Chaque `BlueprintDefinition` possède maintenant une origine canonique (`InstalledPack`,
`ProjectContent`, `Generated` ou `Temporary`), un indicateur de favori et une provenance schéma v1.
Cette provenance conserve source, auteur, licence SPDX, hash de contenu, origine IA et liste
ordonnée des modifications. Les anciens projets migrent vers un contenu projet éditable avec une
provenance explicite ; le format agrégé passe à `0.6.3` et le workspace à `0.8.1`.

`BlueprintPaletteCatalog` fournit la projection unique utilisée par le Creator : toutes les
palettes, packs installés, projet, généré, favoris et temporaire. La recherche porte aussi sur la
source, l’auteur et la licence. `configure_blueprint_palette` applique les changements avec révision
optimiste, dry-run, diagnostic stable, patch, inverse, audit et undo/redo. Un blueprint provenant
d’un pack exige une source `pack://`, une licence distribuable et un SHA-256, puis devient en lecture
seule ; son favori peut toujours être modifié sans altérer son contenu.

La fenêtre Blueprints Aurora aligne quatre cartes : catalogue filtré, définition, origine et
provenance, références inversées. Les badges `STANDARD · lecture seule` et `CUSTOM · éditable`
rendent la frontière visible. L’action de provenance prévisualise par le même handler avant
d’appliquer, et les listes affichent favoris, origine, droits, auteur et licence sans exposer une
voie de mutation parallèle.

## Preuves

- `BlueprintProvenanceTests` couvre normalisation, ordre déterministe et valeurs de migration ;
- `BlueprintCatalogTests` couvre les six vues, la recherche de provenance, dry-run, undo/redo,
  exigences des packs et protection en lecture seule ;
- `ProjectJsonSerializerTests` couvre le round-trip et la migration `0.6.2` ;
- `OPENRPG_FORGE_TOOLSET_G709_PALETTE_PASS` prouve le parcours UI, les cinq origines/vues, la
  recherche, la persistance workspace `0.8.1` et la restauration historique ;
- `scripts/Verify.ps1` constitue le gate intégré final du lot.

## Limites conservées

- le registre qui découvre, installe, met à jour et remplace réellement les packs n’est pas encore
  branché au catalogue ;
- les favoris sont actuellement persistés avec le projet, sans couche personnelle par auteur ;
- versions de blueprint, namespaces de package, permission explicite, signature, miniatures, tags,
  style et compatibilité restent à ajouter ;
- le blocage des licences inconnues est strict pour un pack installé, mais pas encore généralisé à
  toute build de distribution.

Ces limites conservent les capacités au statut `Implemented` partiel.
