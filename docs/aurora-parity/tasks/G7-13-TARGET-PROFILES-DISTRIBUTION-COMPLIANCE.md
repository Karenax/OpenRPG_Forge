# G7-13 — Profils de cible et conformité de distribution

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-PROJ-024` — paramètres par cible ;
- `ORF-BUILD-022` — approbation des dépendances ;
- `ORF-QA-007` — validation de licence.

## Résultat livré

`ModuleDistributionConfiguration` devient une partie canonique du module. Elle porte un UUID stable,
un schéma v1, une provenance et exactement cinq profils : `Editor`, `Client`, `DedicatedServer`,
`Test` et `Shipping`. Chaque profil contrôle activation, données d’édition, symboles de debug,
optimisation, exigence de licences, nom de sortie et référence vers un profil de secrets. Les valeurs
secrètes restent volontairement hors du document et du package.

Le registre `ThirdPartyDependencyApproval` conserve pour chaque dépendance un UUID, une version
sémantique, une licence SPDX, un propriétaire, une justification, une cadence de mise à jour, un
plan de retrait, une source, les cibles concernées et la provenance de modification. Une commande
transactionnelle unique fournit dry-run, patch, inverse, audit, undo et redo. Le workspace
**Validation** ouvre un écran Aurora spécialisé pour éditer les cinq profils, gérer le registre,
prévisualiser les diagnostics et choisir la cible du prochain export.

`DistributionComplianceValidator` autorise les itérations Editor/Test, mais bloque les profils de
distribution lorsqu’une licence ou une provenance requise est inconnue. L’export `.orpmod` inscrit
la cible et les dépendances approuvées dans le manifest 1.1, génère
`THIRD-PARTY-NOTICES.txt`, inclut ce fichier dans le hash puis revalide concordance, NOTICE et
conformité au rechargement. Le CLI accepte `export ... --target <profil>`, expose les profils dans
`package analyze` et fournit `package compliance <workspace> <profil>` en JSON structuré.

Les formats courants passent à `0.6.4` pour le document agrégé et `0.9.0` pour le workspace ; les
versions antérieures migrent vers cinq profils déterministes sans modifier les UUID à chaque lecture.

## Preuves

- `DistributionComplianceValidatorTests` couvre profils stables, séparation des secrets, blocage
  Shipping, cible désactivée et registre conforme ;
- `SetModuleDistributionHandlerTests` couvre dry-run, conflit de révision, patch, audit et undo/redo ;
- `ProjectJsonSerializerTests` couvre round-trip exact et migration déterministe ;
- `ModulePackageTests` couvre refus Shipping avant écriture, export conforme, manifest, NOTICE et
  validation de l’archive ;
- `OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS` prouve écran, diagnostics, commande, historique,
  persistance, export et NOTICE dans le smoke Creator ;
- le CLI prouve un export Editor valide et renvoie un échec structuré pour la Crypte encore
  `UNLICENSED` en Shipping.

## Limites conservées

- le registre ne consulte pas encore CVE, obsolescence ou mises à jour distantes ;
- aucune valeur secrète n’est stockée : un fournisseur de secrets séparé viendra avec les vrais
  déploiements serveur ;
- la politique SPDX intégrée couvre les licences usuelles et `LicenseRef-*`, mais pas encore un
  registre d’organisation configurable ;
- les futurs documents d’assets importés devront rejoindre le contrôle de provenance ;
- les cinq profils ne déclenchent pas encore des transformeurs multi-plateformes distincts.

Les trois capacités passent au statut `Implemented` partiel.
