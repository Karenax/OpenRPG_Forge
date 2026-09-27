# forge_assets

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
