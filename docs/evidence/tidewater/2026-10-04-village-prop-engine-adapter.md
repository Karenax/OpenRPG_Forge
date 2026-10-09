# Tidewater — préparation du consommateur moteur des accessoires

Suite du [lecteur Rust](2026-10-04-village-prop-rust-reader.md).
Le module `crates/forge_engine/src/tidewater_village_props.rs` prépare les
27 prototypes depuis leur paquet contrôlé, avec le mesh village partagé,
matériaux source, collisions Rapier locales et templates Forge. La projection
utilise les mêmes `ManualPlacementPart`, `ManualPlacementProjection` et
identités de collisions que les autres placements. Aucun état Bevy ne devient
une source d'auteur et aucun branchement d'édition sur le nom du module n'est
ajouté. Le consommateur est déclaré et testé isolément ; son affichage Studio
et la qualification visuelle restent ouverts.

## Cas source sans primitive solide

L'inventaire exact contient trois objets sans collision propre : deux objets
empilés et le seau. Leur absence de primitive fait partie de la source.
Le contrat commun refusait systématiquement toute liste vide comme
`MissingPrimitives`. La correction ajoute le marqueur dérivé
`ManualPlacementCollisionNotRequired(EntityId)` ; il doit être fourni par
un adaptateur ayant vérifié une source explicitement non solide. Il n'est
pas une option d'auteur pour masquer une collision manquante.

L'inspection reçoit désormais la racine du placement. Elle accepte zéro
primitive uniquement si le marqueur de cette racine porte la même identité
et si la liste attendue est vide. Une identité différente ou une liste non
vide sous ce marqueur reste un échec explicite. Sans marqueur, la liste vide
reste `MissingPrimitives`. Le nouvel adaptateur n'insère le marqueur que pour
les prototypes contrôlés dont le paquet contient zéro collision. Aucun
collider fictif n'est ajouté au contenu.

Un test de contrat couvre acceptation explicite, identité incorrecte,
contradiction avec une primitive et retrait du marqueur. Les régressions
existantes restent conservées avec la nouvelle signature de l'inspection.

## Correction du suivi des primitives

Le consommateur conserve désormais le nombre de primitives attendu par racine.
Une hypothèse héritée de la place (une primitive par objet) ne s'applique pas
aux accessoires de cour, qui en ont zéro, une ou deux. Le contrôle vérifie
le rattachement exact des primitives à chaque racine, puis leur synchronisation
réelle dans Rapier. Deux tests sur le paquet réel couvrent inventaire complet,
absence d'instances lorsque les placements sont vides, projection commune,
objets explicitement non solides, collider détaché et libération des meshes.
Les tests nécessitent une fixture épinglée et leur invocation inclut les ignorés.

## Validation en cours

Les essais terminaux restent conservés, avec entrées inchangées :

- `noncolliding-readiness-20261004-v1/result.json` : E0583, chemin du sous-module
  de tests dans le harness `#[path]` ; aucun test exécuté.
- V2 : chemin canonique explicite dans une copie vérifiée du module ; compilation
  atteint le lien puis LNK1181, bibliothèque `windows.0.52.0.lib` absente du chemin
  natif du harness. Aucun changement des tests ou du produit pour ce diagnostic.
- V3 ajoute le chemin de la bibliothèque Windows. Session 38554 terminée avec
  code 0 : compilation et trois tests PASS, zéro échec/ignoré/filtré, entrées
  inchangées. Les régressions vérifient backend réel, primitives exactes et refus
  de preview, backend périmé ou source erronée ; le nouveau test couvre le contrat
  explicitement non solide. Reçu `noncolliding-readiness-20261004-v3/result.json`.
- `village-engine-validation-20261004-v1/result.json` : lecteur compilé, adaptateur
  refusé car le harness mélangeait deux variantes Bevy du cache. Les dépendances
  du moteur sont résolues par leurs fingerprints, et non par les dates des fichiers.
- Rejeu `village-engine-validation-20261004-v2`, session 67202 terminée code 0 :
  bibliothèque du lecteur et adaptateur compilés, cinq tests moteur PASS et un
  test lecteur PASS, zéro échec/ignoré/filtré, entrées inchangées. Les cinq tests
  incluent les trois régressions communes et les deux tests du paquet réel.
  Reçu `village-engine-validation-20261004-v2/result.json`. Les bibliothèques
  exactes correspondent au graphe du moteur `338ec1863e64c1fb`.

La déclaration dans `forge_engine/src/lib.rs` ajoute une seule ligne. Le reçu
`village-engine-validation-20261004-v2/engine-registration.json` vérifie que
retirer cette ligne restitue exactement les octets préexistants. Clippy ciblé
`all` et `pedantic`, warnings en erreurs, a d'abord refusé trois sections
`# Errors` manquantes sur les API publiques. Les trois documentations sont
ajoutées, sans changement comportemental : `error-docs-only.json` vérifie
qu'enlever ces ajouts restitue exactement les octets du module testé V2.
Rejeu Clippy V3 terminé code 0, entrées inchangées ; reçu
`village-engine-validation-20261004-v2/clippy-v3-result.json`.

Le lecteur corrigé passe également son contrôle de métadonnées WASM V4 et
Clippy ciblé. Les contrôles Cargo complets, la compilation Web du consommateur
moteur et le raccordement Studio restent à effectuer. Aucun rendu GPU, disponibilité dans
le catalogue produit ou parité n'est revendiqué ici.
