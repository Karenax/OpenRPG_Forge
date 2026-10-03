# Édition commune des placables

Contrat : [ADR-0020](../ADR/ADR-0020-common-source-object-editing.md).
Portée initiale : placements manuels, objets issus de packages vérifiés et
instances végétales importées. Les preuves des autres domaines générés conservent
leur portée propre.

## Du clic à la commande

La façade `apps/forge_studio/src/authoring_assets/objects.rs` expose une liste et
la résolution d'un `EntityId` vers `ManualPlacement`. Les sources de packages
sont exposées par l'adapter d'acquisition ; une instance importée groupée est
résolue par `ImportedPopulation::editable_placement` dans
`crates/forge_world/src/imported_vegetation/editable.rs`.

L'inspecteur et les gestes de transformation, copie, verrouillage et suppression
envoient `PlacementEdit` à la façade. Un placement manuel utilise
`ZoneAuthoringHead::prepare_placement`. Une source ou sa copie utilise
`ZoneAuthoringHead::prepare_source_object` dans
`crates/forge_project/src/zone_authoring/source_objects.rs`. Les deux préparent
`ZoneRequest` sur la même tête, la même révision et le même hash.

Le préparateur `objects/planning.rs` résout toutes les identités sur la tête
acceptée avant de figer la provenance ou de matérialiser un remplacement. Les
lookups d'un lot réutilisent ces snapshots. La population et le raster vérifiés
restent liés à cette tête acceptée ; seule la vue finale utilise les corrections
proposées. Un hash spéculatif n'est jamais présenté comme une révision acceptée.
La requête conserve la révision et le hash d'origine, et une population réellement
périmée ou une vue candidate invalide reste refusée.

Les différences de stockage restent derrière cette façade. Le choix d'un outil
ne dépend pas d'un nom de bâtiment, d'un tag de Tidewater ou d'un chemin d'asset.
Les contrôles propres à un import concernent la provenance et la résolution des
octets, pas une autre famille de gestes d'édition.

L'inspecteur secondaire Atelier résout aussi la sélection et prépare ses
transformations par cette façade, dans la fenêtre principale. Son cœur de
validation garde les contrôles de zone, hash, verrou et playtest avant les
callbacks ; les brouillons et le protocole entre fenêtres conservent leurs
gardes de révision.

Après une ouverture, les choix peuvent attendre la population importée vérifiée.
La clé du sélecteur suit la génération de bibliothèque et le propriétaire de
cette population, même si les placements statiques restent identiques. Une
transition de disponibilité réveille les choix sans reconstruire la géométrie.
Le propriétaire est relu depuis les caches validés, sans recalcul du hash à chaque
frame. Les propositions en cours gardent leurs gardes et leur aperçu.

Restaurer le brouillon conserve cette clé : la tête acceptée n'a pas changé.
L'inspecteur mémorise le couple aire/descripteur sélectionné. Une notification
de disponibilité qui ne modifie pas ce couple conserve les champs saisis et
le panneau avancé ; une autre aire, une fermeture ou une modification réelle
de l'objet renouvelle l'inspecteur avec les gardes de la session courante.

L'existence d'un package chargé ne suffit pas à exposer ses objets hérités dans
une zone. L'adapter des statiques utilise `height_sha256` de `grass.json`, déjà
vérifié dans `PlantPackage`, et le compare au pin `sampled_source.samples_sha256`
de la tête. Le `TerrainPackage` vérifié fournit aussi le repère de son contrat
v1/v2 dans la couche d'acquisition : dimensions, origine, espacement, encoding,
hauteur extérieure et étendue. Le helper compare toutes ces métadonnées et le
repère du document. Un même hash de samples réinterprété dans un autre repère ne
suffit pas. `source_fixture_sha256` identifie le fixture complet et n'est pas un
hash de raster. Sans liaison du dataset, une carte procédurale ou un autre import
ne reçoit aucun statique implicite. Sans package terrain vérifié, plantes ou pin
de raster, l'héritage automatique reste absent ; les placements manuels et
records sparse déjà authorés conservent leur route de résolution. Le nom de la carte, ses tags et le projet
contenant le même dataset n'interviennent pas dans cette décision.

Les records déjà authorés restent résolus après une régénération du relief à
partir de leur snapshot immuable, de leur ID d'import et des pins du package
vérifié. La pose source complète est revalidée pour les objets à transform
déclaré dans le package. Quand seule l'élévation était calculée sur le raster
d'origine, elle reste celle du snapshot acquis ; X/Z, rotation, échelle,
identité et pin doivent toujours correspondre au package. L'adapter ne recalcule
pas cette provenance sur le nouveau relief et ne réintroduit aucun tombstone.
Un pin ou une pose source incompatible exige une réconciliation explicite.

## Ce qui est persisté

`ZoneCorrections.source_object_edits` est un ensemble clairsemé de records triés.
Un record épingle sa source immuable, son état effectif et ses copies. `Removed`
reste enregistré quand aucune copie n'est visible. Les transformations et
verrous sont des données de la tête v14 ; les packages, blocs d'instances,
paramètres de prototypes, meshes et textures importés restent immuables.

La session valide les transitions des `Edit` bruts en plus des préparateurs.
Supprimer un record ou modifier son snapshot source est refusé. Restaurer un
objet supprimé passe actuellement par Undo/Redo. Déverrouiller conserve les
corrections ; le déplacement ou la suppression suit dans une commande distincte.

Le renderer et le playtest consomment une projection identifiée par le hash de
tête. Les copies d'instances groupées gardent les paramètres de leur prototype ;
les caches ne deviennent pas une autorité. Un réimport ou changement de source
exige une réconciliation explicite, sans rapprochement par nom ou par proximité.

Le remplacement d'asset commun matérialise l'objet comme placement manuel et
applique `ReplaceAsset` dans la même commande revue. L'ID, le nom et la
transformation sont conservés ; la racine importée reçoit un tombstone, ou la
copie sort de son record. Le placement visible porte le nouveau pin sans réécrire
le snapshot source. Le seul chevauchement d'identité admis est un placement auteur
avec l'ID de sa source `Removed` ; aucun ID de source n'est disponible pour une
autre copie. Une source verrouillée refuse le remplacement. Undo restaure l'état
source et son pin d'origine.

Avant une opération manuelle, la façade fige l'état effectif des sources
statiques des pins concernés qui n'ont pas encore de record. Une nouvelle
insertion enregistre leur état `Inherited` ; la présence préalable d'un import
manuel historique conserve leur état masqué avec `Removed`. Cette acquisition
est incluse dans la même commande revue, sans réécriture à l'ouverture. Les
records existants, leurs poses, leurs verrous et leurs copies restent intacts.
Le remplacement tient compte de l'ancien et du nouveau pin. Ainsi, ajouter puis
supprimer un manuel ne masque ni ne supprime les sources sœurs du même asset ;
effacer le dernier import historique ne les fait pas réapparaître. Undo/Redo
restaure l'ensemble de la commande et sa provenance.

## Compatibilité et vérification

Le champ absent est omis pour préserver les anciens hashes ; aucune correction
historique n'est réécrite à l'ouverture. Les lecteurs conservent les anciens
offsets et placements de plantes. Les deltas de session transportent le nouveau
domaine uniquement lorsqu'il est nécessaire et conservent `Keep`/`Set(None)`.

Les archives avec historique de corrections source utilisent v37 intégrale,
v38 compacte ou v39 delta, y compris avec audit et après Undo à une tête sans
override. La détection examine les corrections appliquées et inverses des reçus.
Le replay vérifie hashes, audit, résultat et version minimale de famille ; un
simple réétiquetage vers les versions historiques est refusé. Sans le domaine,
les formats et octets canoniques historiques restent inchangés.

Le domaine gameplay conserve sa tête v13 et ses archives v34/v35/v36 quand
aucun reçu ne contient de correction source. La présence des corrections source
exige une tête v14 et des archives v37/v38/v39, capables de conserver les deux
domaines. Undo d'une correction source peut ramener une tête avec gameplay à v13
tout en conservant la famille source pour l'historique.

`crates/forge_project/tests/source_object_transitions.rs` couvre les transitions
brutes, la provenance exacte, les verrous de source et copies, la suppression
durable, les codecs et les inverses, ainsi que les familles v37–v39, les audits,
Undo et les refus de réétiquetage. L'existence de ces tests ne signifie pas
qu'ils ont été exécutés. Les résultats locaux et les captures de parcours doivent
être consignés avec leur candidat, séparément de cette note.

`apps/forge_studio/tests/source_object_binding.rs` inclut le helper de production
sans GPU ni WebView : exclusion des zones étrangères malgré un catalogue chargé,
liaison au hash et repère complet de raster plutôt qu'au fixture, refus de package
absent ou de repère incompatible malgré un hash identique, conservation de snapshots/copies
après régénération, tombstones après réouverture et refus de provenance altérée.
Cette description indique la portée des tests ; leurs résultats restent à
consigner dans la preuve locale.

`apps/forge_studio/tests/manual_source_compatibility.rs` inclut le helper de
production sans WebView : nouvelle insertion et suppression, compatibilité des
imports historiques, conservation des sources éditées et verrouillées,
remplacement des pins, batch et historique, et absence de correction superflue
pour un asset étranger. Les tests du cœur Atelier couvrent aussi une source hors
placements manuels, le verrou, le playtest et le refus d'un brouillon périmé.

`apps/forge_studio/tests/accepted_object_planning.rs` exerce le préparateur de
production avec des résolveurs stricts : première retouche de plante, remplacement
multiple et gel du nouveau pin statique, refus atomique d'un lot verrouillé,
population périmée, insertion manuelle après retouche et vue candidate invalide.

La généralisation des adapters d'acquisition et des recettes de rendu Tidewater
reste distincte de cette façade d'édition. Un panneau commun ne démontre pas
que tous les autres objets générés sont déjà sélectionnables ou éditables. La
parité visuelle, la performance de la population maximale et la qualification
transversale de Gate 2 nécessitent leurs preuves respectives.
