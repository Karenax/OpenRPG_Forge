# ADR-0020 — Outils communs pour les placables issus de sources immuables

Date : 2026-10-01. Statut : décision de correction autorisée par le propriétaire ;
validation de l'implémentation et parcours Studio à consigner séparément.

Complète [ADR-0014](ADR-0014-authoring-regeneration-overrides.md),
[ADR-0016](ADR-0016-project-module-workspace.md) et
[ADR-0017](ADR-0017-tidewater-fidelity-integration.md).

## Problème et décision

L'origine d'un placable ne doit pas déterminer ses outils de transformation,
copie, verrouillage ou suppression. La sélection d'un objet de Tidewater, d'un
autre import ou d'un placement manuel doit alimenter le même inspecteur et les
mêmes commandes. Les noms, tags, IDs de démonstration et slugs de modules ne
servent pas à choisir une politique d'édition.

Le Studio utilise une façade d'objets éditables qui résout un `EntityId` en
`ManualPlacement`, puis route le même `PlacementEdit`. Les adapters d'import
restent responsables de la lecture et de la vérification des packages et blobs.
Leur géométrie et leurs paramètres de prototype peuvent différer ; les gestes
d'auteur et les garanties de mutation sont communs.

Un catalogue global chargé n'implique aucune présence dans la zone. Les objets
statiques hérités exigent une liaison de données vérifiée : dans l'adapter actuel,
le pin `height_sha256` du descriptor `grass.json` vérifié correspond au
`sampled_source.samples_sha256` de la tête et le repère complet correspond à celui
du contrat du `TerrainPackage` vérifié. Dimensions, origine, espacement, encoding,
hauteur extérieure et étendue doivent correspondre ; le même hash n'autorise pas
une autre interprétation spatiale. Ces constantes restent dans l'adapter de
package d'acquisition. Le hash du fixture complet ne lui est pas substitué.
Sans les packages ou métadonnées nécessaires, aucun héritage n'est inventé ;
placements manuels et corrections source existantes restent résolus.
Une carte procédurale ou un autre dataset reste vide de ces
statiques implicites, sans critère de slug, tag, nom ou projet.

## Corrections clairsemées et provenance

`ZoneCorrections.source_object_edits` contient les seules sources retouchées,
avec leurs copies et suppressions. Le champ est optionnel et omis lorsqu'il est
absent ; sa présence exige une tête `forge.zone-authoring` v14. Cette correction
reste dans la tête unique d'ADR-0014 et dans l'autorité workspace d'ADR-0016.
Aucun fichier de corrections concurrent et aucune entité ECS sérialisée ne sont
introduits.

Chaque `SourceObjectEdit` conserve :

- `source` : identité, pin d'asset et snapshot moteur-neutre vérifié lors de la
  première retouche ; ce snapshot reste immuable pendant les éditions suivantes ;
- `state` : `Inherited`, `Edited { placement }` ou `Removed` pour la source ;
- `copies` : placements authorés à IDs propres, conservant le même pin vérifié.

La transformation authorée est absolue. Une copie reprend le pin et la forme
source, reçoit un nouvel ID et commence déverrouillée. Les instances végétales
importées restent groupées pour le rendu ; seuls leurs overrides sont persistés.
Leur transformation commune est projetée dans la population dérivée en conservant
les paramètres de prototype. Une sélection individuelle n'exige pas une racine
ECS persistante pour chaque instance.

Une régénération du relief conserve les statiques déjà authorés via leur snapshot
source immuable lorsque l'ID d'import, le pin d'asset et la pose déclarée du
package restent vérifiés. L'élévation calculée sur le raster lors de l'acquisition
initiale reste figée dans le snapshot ; elle n'est pas remplacée par une hauteur
du nouveau terrain. Les statiques sans record ne sont hérités que si le dataset
courant demeure associé au package. Cette distinction ne modifie pas le schéma
du domaine et préserve les suppressions enregistrées.

Les sources et copies sont triées par ID ; les identités doivent être uniques
entre corrections de sources et placements manuels. Les bornes de noms,
transformations, nombre de corrections et taille de tête sont validées avant
application. L'adapter vérifie la source réelle, son pin et les limites spatiales
de la projection ; une référence manquante ou modifiée provoque un refus explicite.

## Suppression et verrous

Supprimer la source écrit `Removed`. Le record demeure présent après suppression
de sa dernière copie, sauvegarde, réouverture ou reconstruction des caches.
L'absence d'un objet dans le renderer ne constitue jamais une suppression auteur.
Un ancien import explicite devenu placement manuel doit aussi enregistrer la
suppression de sa source lorsqu'il est retiré, pour éviter sa réapparition.

Les opérations `Transform`, `Duplicate`, `SetLocked` et `Remove` utilisent le
même `PlacementEdit` que les placements manuels. Une transformation ou suppression
d'un objet verrouillé est refusée ; le déverrouillage doit être une opération
distincte et conserve la retouche. Copier un objet verrouillé ne modifie pas
l'original et produit une copie déverrouillée.

La session revalide les transitions des requêtes `Edit` brutes : aucun rebinding
de provenance, retrait de record existant, effacement de tombstone, déplacement
ou suppression d'un objet verrouillé. La comparaison canonique de provenance
conserve aussi les différences de représentation, notamment les zéros signés.
`Undo` et `Redo` peuvent restaurer les inverses enregistrés, y compris l'héritage
antérieur à la première correction ; ils ne contournent pas une mutation libre.
Tout nouvel outil de restauration explicite devra définir sa commande et ses
garanties avant d'autoriser une résurrection hors historique.

## UI, persistance et compatibilité

La façade est le point d'entrée commun de la sélection, de l'inspection et des
gestes d'édition. Le stockage particulier d'une source importée ne justifie pas
un panneau d'édition par bâtiment, plante ou module. Une acquisition de package
peut conserver ses diagnostics et sa provenance spécifiques.

Les commandes gardent `command_id`, révision et hash de base, Preview/Apply,
inverse, audit et nouvelle révision. Sauvegarde et réouverture réutilisent les
archives existantes. Les encodages delta distinguent `Keep` de `Set(None)` et
transportent le domaine lorsqu'un reçu le contient, même après Undo. Les anciennes
corrections sans `source_object_edits` conservent leur sérialisation canonique et
leur hash. Aucune migration ou réécriture implicite des cartes ouvertes.

La nouvelle famille d'archives est v37 intégrale, v38 compacte et v39 delta. Elle
est requise dès qu'un reçu contient ce domaine dans ses corrections appliquées
ou inverses, avec ou sans audit auteur. Undo peut rétablir une tête v1 mais ne
réétiquette pas l'historique source vers une ancienne famille. L'audit injecté
est conservé par les mêmes codecs ; aucun nouvel acteur ou timestamp n'est
inventé. Les lecteurs refusent un réétiquetage vers v1–v36 ainsi qu'une v37–v39
ne contenant aucun historique source. En l'absence du domaine, les versions
historiques restent choisies, notamment v1/v3/v6 sans audit et v31/v32/v33 avec
audit pour une correction de nom seule.

Les numéros v13 de tête et v34/v35/v36 d'archive déjà attribués au domaine
`gameplay` restent inchangés. Sans corrections source, une carte contenant
seulement du gameplay conserve ces formats. Une carte combinant gameplay et
corrections source utilise une tête v14 et la famille d'archives v37/v38/v39 ;
les deux domaines, leurs inverses et leurs audits sont conservés. L'absence des
deux champs continue à préserver tous les hashes historiques.

Les anciens offsets et copies de plantes importées restent lisibles et projetés.
Cette décision n'autorise pas leur suppression ou conversion silencieuse. Un
remplacement de pin d'asset reste gouverné : la façade matérialise d'abord l'objet
comme placement auteur, avec le même ID, nom et transform, puis utilise
`PlacementEdit::ReplaceAsset`. Ces étapes forment une seule commande, avec aperçu
et inverse communs. Pour la racine, la source demeure `Removed` et peut réserver
le même ID que le placement auteur visible ; une copie matérialisée quitte le
record de copies. Cette exception n'autorise pas une autre copie à réutiliser un
ID de source. La provenance et le pin d'origine restent intacts ; seul le placement
auteur porte le nouveau pin vérifié. Un objet verrouillé refuse cette
matérialisation, et Undo restaure son état source précédent.

## Vérification et limites

Les tests doivent couvrir les mutations typées et brutes, les verrous des sources
et copies, les pins, les collisions, le refus atomique, les tombstones après la
dernière copie, Undo/Redo/retry, chaque codec d'archive et les anciens hashes.
Le parcours Studio doit vérifier sélection directe, transformation, copie,
verrouillage, suppression, historique, Save/Close/Open et projection en playtest.
La recherche des corrections dans une population massive utilise les IDs triés,
sans scan de toutes les retouches pour chaque instance.

Cette ADR établit le contrat ; elle ne constate aucun résultat de build, test,
benchmark, parcours graphique ou qualification humaine. Elle ne clôt aucune gate
et n'autorise aucune publication. La CI distante conserve son état établi dans
les preuves de gate, sans succès déduit de cette correction.

L'état des raccords et leurs limites est décrit dans
[la note d'architecture](../architecture/COMMON_PLACEABLE_EDITING.md).
