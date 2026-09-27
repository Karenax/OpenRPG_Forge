# ADR-0010 — Projection de forêt à partir d'arbres gouvernés

Date : 2026-09-06
Statut : **accepté pour implémentation dans Gate 1 ; qualification non acquise**
Décision utilisateur : « oui vas y », après présentation de cet ADR.
Portée : Gate 1, complément à ADR-0007 ; aucun passage de gate.

## Problème observé

Le candidat original de feuillage est chargé en natif et en Studio WebGPU, avec 20 placements
du kit et 36 colliders. Il n'a pas remplacé les 636 arbres du scatter procédural.
Les captures montrent un mélange incohérent de grandes silhouettes détaillées et de petits
arbres anguleux. Augmenter le feuillage des seuls objets du kit ne résout pas ce problème.
Voir `docs/evidence/gate1/R1_FOLIAGE_CANDIDATE.md` pour les fichiers, métriques et limites.

ADR-0007 retient des prototypes procéduraux partagés. Passer la forêt à des meshes gouvernés
change cette décision, même si Bevy reste le seul renderer. Cette proposition ne vaut pas
autorisation de copier des assets ni acceptation artistique des arbres candidats.

## Direction acceptée

Conserver le scatter canonique, les IDs, les poses, les règles d'exclusion et les colliders
existants. Ajouter une projection optionnelle des arbres à partir d'un petit catalogue gouverné,
résolu et vérifié une fois. Ne pas instancier un `WorldAsset` complet et ses colliders pour
chacun des 636 placements ; réutiliser les meshes et matériaux par espèce/LOD comme aujourd'hui.

La résolution du catalogue reste un adapter. Aucun `Handle`, `Entity` ou chemin machine dans
les documents Forge. Avant code, spécifier le lien moteur-neutre espèce → asset/révision/hash,
la dimension de référence et le pivot ; ne pas encoder un ID de Visual Valley dans le moteur.
Le document de végétation et le catalogue ne possèdent pas deux copies concurrentes des poses.

Les arbres du kit placés manuellement restent indépendants et inchangés. La normalisation de
taille concerne uniquement les prototypes du scatter : elle doit conserver son enveloppe
physique déclarée, être explicite et testée. Aucun redimensionnement silencieux des retouches.
Une correction d'asset entraîne un nouveau dérivé lié à sa source, jamais un écrasement du blend.

Conserver le batching Bevy standard, le culling et les niveaux de qualité. Préserver les UV/PBR
et borner le vent sans déplacer les racines. Les LOD/HLOD doivent dériver de la même silhouette,
pas réutiliser arbitrairement les anciennes cartes de conifère pour un chêne.
Ni raw wgpu, ni renderer parallèle, ni dépendance nouvelle par défaut.

Un asset absent, altéré ou incompatible produit un diagnostic explicite avant projection.
Le mode procédural existant reste sélectionnable pour comparaison ; il ne devient pas un
fallback silencieux présenté comme un chargement réussi du catalogue.

## Critères avant intégration

- Identités/poses/scatter et retouches inchangés après résolution, rejeu et changement de qualité.
- Hash et provenance vérifiés ; source périmée ou LOD manquant refusé ; aucun asset Cadle.
- Partage effectif des handles par prototype, aucun doublon de collider par placement.
- Tests de pivot, échelle, UV/PBR, racines sous vent et transitions proche/moyen/lointain.
- Capture et parcours comparables natif/Web sur mêmes entrées ; ni simple compteur de chargement
  ni quatre vues Blender ne suffisent.
- Mesures avant/après sur même scène/profil/machine : moyenne, p95, 1 % low, extraction,
  mémoire et taille WASM. Documenter séparément la qualification sur GPU cible encore absente.
- Revue humaine de la silhouette/composition avant promotion ; décision Gate 1 distincte.

## Alternatives

Continuer uniquement à densifier le kit laisse intacte la majorité de la forêt : insuffisant.
Copier un arbre détaillé complet par placement dupliquerait inutilement le chargement, les
colliders et les entités : non retenu. Refaire un renderer spécialisé n'est pas justifié par
les mesures disponibles. Enrichir les prototypes procéduraux reste une alternative valide,
mais ne bénéficie pas directement des retouches gouvernées du kit.

## Frontière de décision

L'approbation éventuelle de cette direction autoriserait son implémentation dans Gate 1,
pas l'acceptation automatique des assets candidats, une nouvelle gate ou une publication.
La direction est approuvée. La projection canonique reste inchangée jusqu'à réalisation et
validation des critères ci-dessus ; les revues artistiques et le passage de gate restent distincts.

## Contrat initial

La clé de liaison est l'identifiant stable d'une couche arborée du document de végétation,
qui porte actuellement le scatter ; aucune nouvelle taxonomie d'espèces n'est inventée.
Un catalogue de présentation versionné épingle le hash de ce document ainsi que le kit, sa
révision et le GLB de chaque liaison. Le pivot est exprimé dans le repère GLB Y vertical.
Une hauteur source et une hauteur cible explicites donnent une normalisation uniforme
`(point - pivot) × hauteur_cible / hauteur_source`, appliquée avant la pose du placement.
Ces dimensions déclarées devront être confrontées aux meshes réellement chargés par l'adapter.
Le contrat ne suffit pas à prouver LOD, vent ou couverture des collisions.
