# forge_assets

`source_collision_geometry` décode les buffers de collision conservés séparément
des meshes visibles. Il vérifie leurs SHA-256, identités et nœuds propriétaires,
budgets, valeurs finies, cardinalités et indices de triangles. Les coordonnées
déjà converties dans le repère local du GLB et l'ordre des triangles sont conservés.
Il ne recuit pas les convexes, n'alloue aucun collider Rapier et ne qualifie ni
les contacts, ni la simulation, ni leur disponibilité dans Studio.

Le vérificateur exige un reçu de conversion privé épinglé :

```text
cargo run -p forge_assets --example verify_source_collision_geometry -- RECEIPT_JSON RECEIPT_SHA256
```

Le lecteur `source_graph_attributes` conserve les canaux UV supplémentaires
présents (1 à 7), en Vec2 glTF et en Vec4 source distincts. Un canal absent
reste absent ; les valeurs originales ne sont ni retournées ni normalisées.
Les comptes, encodages, valeurs finies et budgets mémoire sont contrôlés avant
admission. Les canaux hors de cette plage sont refusés explicitement.


`source_mesh_shaders::SourceMeshShaderPackage` charge le contrat générique
`forge.source-mesh-shaders` v1 par callbacks natifs/WASM, sans renderer ni
JavaScript. Le hash gouverné du manifeste et les pins de tous les fichiers sont
obligatoires. Layouts, matériaux, passes et uniformes source sont typés ; les
ABI et valeurs non supportées échouent. `SourceUniformBlock::pack()` conserve
les octets little endian et le padding source. `declared_requirements()` compte
tous les bindings visibles déclarés pour l'admission GPU, y compris ceux que
le shader n'utilise pas après optimisation.

Le vérificateur exige le vrai paquet et l'oracle source épinglé :

```text
cargo run -p forge_assets --example verify_source_mesh_shaders -- PACKAGE_ROOT MANIFEST_SHA256 ORACLES_JSON ORACLES_SHA256
```

Il compare les octets packés et chaque site nommé, vérifie la lecture sync/async
immédiate et exerce des refus sémantiques/intégrité. Le profil de fixture requis
pour ces refus doit être présent ; son absence retourne une erreur. Le chargement
ne compile pas les shaders et ne dessine pas sur GPU. Textures, buffers vivants,
passes, providers du renderer et parité visuelle restent à raccorder.
[Résultats locaux du paquet réel](../../docs/evidence/tidewater/2026-10-02-boat-shader-loader.md).

`tidewater_boat::BoatPackage` lit le paquet bateau source v1 acquis au commit
Tidewater `1438b1abfcaee3267092b75573014f4d9b4a983c`. L'appelant fournit le
hash gouverné du manifeste ; `package_hash()` désigne le paquet canonique 319.
Les callbacks synchrones/asynchrones apportent les octets, sans accès disque ni
renderer dans ce crate. La lecture vérifie les budgets, chemins plats, pins et
références transitives, graphe source, hiérarchie, repères, streams mesh,
matériaux et déclarations de commande. Les sources de contrôleurs restent des
références consultables ; elles ne sont jamais exécutées par le lecteur.

`source_geometry` expose les références de buffers, repères et descripteurs
d'exclusion d'eau réutilisables. Les indices u16/u32 sont décodés sans réparation.
La coque originale est soudée exactement pour vérifier ses compteurs : 952
vertices source, 910 soudés, 2 722 arêtes et **8 bords ouverts**. Son fingerprint
reste celui de la source. La résolution fournit aussi le parent et le repère
local ; le renderer doit appliquer son admission et la pose de l'instance.
Cette résolution ne prouve pas la présence de tous les bateaux d'une scène.

Le vérificateur CPU nécessite un vrai répertoire de paquet ; aucun fallback ni
test silencieusement ignoré :

```text
cargo run -p forge_assets --example verify_tidewater_boat -- PACKAGE_ROOT
```

Il compare les lectures synchrones/asynchrones du paquet canonique, puis des
copies volontairement invalides dont les pins sont recalculés pour atteindre
les contrôles sémantiques. Le preflight du GLB d'inspection ne remplace pas le
validateur glTF complet. Le chargement ne qualifie ni les sept shaders portés,
ni le placement éditable Studio, ni la conduite du bateau, ni la parité visuelle.

`asset_browser` fournit un index consultatif du rapport de kit gouverné existant :
validation à la construction, tri par ID, recherche multiterme/catégorie avec
normalisation des accents français. Ne vérifie pas les octets GLB et ne crée ni
import ni placements. Le panneau Studio distingue les références déclarées de
la vérification volontaire d'un fichier. Voir R2_ASSET_BROWSER.md dans les preuves
Gate 2 pour les limites et l'état de qualification réel.

`VegetationPrototypeCatalog` (ADR-0010, `forge.vegetation-prototype-catalog` v1) lie chaque
couche arborée à un GLB épinglé du kit, avec pivot et hauteurs de normalisation explicites.
`validate_against` exige le hash du document et les IDs de ses seules couches arborées,
ainsi qu'un rapport de kit valide. Couverture exacte, hash GLB, révision, type arbre et
dimensions finies/bornées sont contrôlés. Le catalogue n'embarque ni poses, ni handles,
ni accès disque. L'adapter doit encore vérifier les octets, les dimensions réelles, les LOD
et la projection ; valider ce contrat ne signifie pas que le renderer l'utilise déjà.

Préparation v2 : plusieurs liaisons de présentation par couche sont permises, avec
paires couche/asset uniques et 64 liaisons maximum au total. La v1 garde strictement
une liaison par couche ; aucun catalogue existant n'est migré implicitement.
`select_binding` utilise un choix rendezvous SHA-256 à poids égaux : identité du
placement, couche et asset, indépendant de l'ordre, des octets réimportés, du pivot
et du LOD. Ajouter une variante ne peut réaffecter un arbre que vers cette variante ;
la retirer restitue le choix précédent. Ce choix ne contient ni pose ni collision.
Le renderer résout désormais une clé distincte par paire couche/asset et sélectionne
le même prototype pour ses trois LOD. Le choix est effectué à la reconstruction de
projection, pas chaque frame. La validation du contrat ne remplace pas le contrôle
des GLB ni la qualification visuelle et de performance du candidat.

Contrats de catalogue, provenance et intégrité d'assets. Gate 0 utilise uniquement des recettes
procédurales déterministes ; le validateur `validate_procedural_only` refuse toute référence externe.

Le module `governed_asset_kit` porte le contrat moteur-neutre de l'integration R1-064. Il lit le
rapport Blender `forge.blender-asset-kit-build` v1 sans refuser ses champs de preuve additionnels,
mais valide strictement le sous-ensemble consomme par Forge : provenance originale, GLB relatifs,
hashes, identifiants et export technique. Le document `forge.asset-kit-runtime-preview` v1 est ferme
aux champs inconnus ; ses placements sont bornes et relies a la liste explicite des assets charges.
`validate_against_build_report` impose en plus le meme kit/revision, le meme manifeste et exactement
le meme ensemble d'assets entre le build Blender et la composition runtime.

Les candidats `procedural_original_with_authored_edits` exigent en plus une ligne de provenance
`authored_source` valide : hashes du descripteur, du Blend et de la recette, IDs corriges uniques et
connus, revue humaine `pending`. L'apercu doit epingler explicitement `authored_source_sha256`.
Un ancien apercu sans ce champ est refuse pour un kit corrige. Les candidats proceduraux existants
restent compatibles sans source authorée ; les sources Blender ne sont pas requises au runtime.

`glb_preflight` fournit les contrôles partagés natif/WASM pour le candidat autonome R1-064 :
en-tête GLB v2 et longueur du fichier cohérents, premier chunk JSON aligné borné à 16 Mio,
version glTF 2.0, aucune clé `uri` (y compris dans les extensions). Les data URI sont aussi
refusées : le candidat gouverné utilise des ressources incorporées au buffer du GLB.
Ce sous-ensemble volontairement strict ne remplace pas la validation complète des accessors,
meshes ou collisions, ni le contrôle SHA-256. Il ne constitue pas un importeur glTF général.
Le runtime l'applique avant Bevy ; le transport du kit dans le Studio reste à raccorder.

`source_mesh_shaders` charge un paquet autonome de programmes WGSL avec hashes,
ABI des blocs uniformes et layouts vertex/bindings. Le schéma v1 reste un contrat
de transport historique. Le v2 ajoute les switches raster matériau, overrides de
passe et politiques GPU explicites, comparées aux règles source avant préparation.
Les lectures sync/async partagent les contrôles ; la vérification CPU ne prouve
ni un dessin, ni l'apparence, ni la disponibilité des ressources de scène.
La préparation GPU appartient à l'adapter Engine `source_mesh_pipeline`.

`source_local_lights::ownership` construit un index uniquement depuis un paquet
fermé et hashé. Il vérifie les ID résolus/non résolus, leurs repères et les
références producteur ; un même repère ne peut revendiquer deux propriétaires.
La sélection d'un objet utilise son token source exact et conserve l'ordre des
lumières. Un token absent n'est pas déduit d'un nom, d'un kind ou d'une position.
Cet index reste distinct de la résolution catalogue et des instances live Studio.

`source_physics_transport` lit un manifeste de mesures physiques épinglé par SHA-256.
Le caller doit fournir le hash vérifié du carrier complet et un résolveur confiné
des buffers relatifs ; les chemins de provenance sont conservés sans être ouverts.
Le lecteur compare les identités, les nœuds et les propriétés sérialisées à toutes
les observations du carrier. Les buffers de candidats restent bornés, hashés,
en coordonnées locales cible, avec leurs limites d'équivalence conservées.
Le résultat ne contient aucune politique Rapier implicite : la résolution des
contacts, couches et règles de simulation appartient à l'adaptateur moteur et
doit conserver ses preuves. Une lecture réussie ne qualifie ni la physique native,
ni le chargement ou le rendu dans Studio.

`source_physics_package` transporte séparément le manifeste et les buffers de
collision dans une enveloppe binaire ORPP v1. Le décodage exige les pins du
carrier et du manifeste détenus par le caller ; les hashes de l'enveloppe ne
suffisent pas à autoriser son contenu. Les sections sont bornées, les buffers
ne sont pas développés en tableaux JSON, les chemins sont relatifs et les
octets supplémentaires ou tronqués sont refusés. Le host Studio peut consommer
le paquet avec `install_physics_package` et des contacts explicites. Le routage
IPC depuis les fiches du Vault et son parcours UI restent à raccorder.
