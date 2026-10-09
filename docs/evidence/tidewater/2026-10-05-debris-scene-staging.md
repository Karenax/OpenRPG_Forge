# Tidewater — adoption Debris dans la scène Bevy

Suite du [programme Nature](2026-10-05-debris-nature-material.md).
Implémentation en validation : aucun dessin, geste Studio ou verdict de parité.

`tidewater_debris::scene::PreparedScene` résout tous les placements communs
avant d'allouer des meshes ou de remplacer des racines. Les références doivent
pointer sur les prototypes acquis et la révision Debris épinglée. Le lecteur
assets expose cette révision, sans la promouvoir en qualification visuelle ou
de licence. Les identités, noms, transforms et locks restent dans ManualPlacement.
Les templates immuables exposent les frames source pour l'insertion auteur ;
ils ne choisissent pas les identités de placement ni ne modifient les documents.

Les matériaux Wood/Hard/Fabric utilisent les rôles Village vérifiés 0/1/5 ;
Nature utilise son matériau étendu. Le préparateur exige leurs handles résidents,
les shaders dédiés et le store de meshes. Les prototypes partagent les handles
entre leurs copies. Les budgets de parties et de triangles sont contrôlés.

Le staging crée des racines ManualPlacementProjection et des enfants
ManualPlacementPart. Les casters sont les meshes séparés préparés depuis les
indices source ; ils ne sont pas dessinés comme objets visibles. shadow_draws
produit les entrées du renderer d'ombres existant à partir des GlobalTransform
vivants et de la visibilité héritée, pour suivre déplacements, rotations,
échelles et copies. L'adoption d'une liste vide retire les anciennes racines.
L'éditeur doit encore appeler ce raccord et la scène doit publier ces casters.

Les 111 collisions source restent sans ownership par objet. Les racines ont
donc un contrat collision vide explicitement incomplet, sans marquage
CollisionNotRequired. Elles ne peuvent pas être déclarées prêtes au jeu par
la vérification commune. La couche n'invente pas une collision à partir de
chaque mesh, ni une attribution par proximité. Ce point reste à terminer.

## Contrôles en cours

Le test CPU réel prévu utilise les 805 prototypes acquis : adoption de 865
parties, production des casters, refus atomique d'une révision erronée,
conservation de la scène après ce refus, absence de qualification collision,
puis retrait de toutes les racines par une liste vide. Les handles de textures
et shaders utilisés dans ce test sont explicitement des doubles de test : ils
ne prouvent aucun pipeline, pixel, bind group réel ou présentation visuelle.

La session de test 16821 a rencontré E0425 après la modification de l'interface
assets pendant sa compilation : le nouveau raccord scène a lu la nouvelle
source moteur contre la dépendance assets déjà compilée, sans package_hash.
La commande termine code 1 ; aucun de ses tests n'est déclaré PASS. Ce résultat
est conservé et ne remplace pas le test mesh 93654 terminé PASS auparavant.

Après cette terminaison, `cargo check -p forge_engine --tests --locked` est
lancé sur l'ensemble courant, Rust 1.98.0 fixé, cache natif existant (session
51888). Le lecteur assets est recompilé avec l'API de révision. Le check termine
code 0 en 4 min 24 s, sur le raccord scène et ses cibles de test. Les
warnings de hardlinks provoquent des copies, sans autoriser un nettoyage du
cache ou un redémarrage d'une commande encore active.

Après ce check terminé, les trois tests Debris sont relancés avec les fixtures
procédurales v3 et Nature v2, dont l'export Rust est toujours dirigé vers un
dossier neuf. Ce nouveau run est pending ; la compilation des tests ne prouve
pas encore leur exécution ou un draw GPU. Les fichiers Rust de ce candidat
doivent rester stables jusqu'à sa terminaison.

Restent : verdict du check/tests, export du programme Nature adapté par Rust,
validation de ses receivers, staging avec les maps réelles et appel dans le
produit, ownership des collisions, édition/persistance Studio et captures
comparables dans la scène complète. Aucun de ces points n'est clos par le
seul ajout de cette API.

Suite : [ownership source des 111 collisions et conversion de leurs frames](2026-10-05-debris-collision-ownership.md).
Les associations sont désormais acquises et vérifiées en CPU ; le raccord
scène ci-dessus ne les consomme pas encore et conserve sa readiness incomplète.

## Suite du 5 octobre : primitives source et retrait backend

Le run moteur 15076 termine code 0 : les trois tests Debris passent (meshes,
Nature adaptée par Rust, adoption des placements). Ce résultat remplace le
pending ci-dessus pour ce candidat CPU, sans qualifier un affichage réel.

Le lecteur du sidecar collision passe sur les données réelles (session 56047,
1 test, code 0). Le raccord scène dispose maintenant de `with_collision_frames` :
il vérifie les pivots des prototypes procéduraux et prépare les 51 primitives
source avant toute adoption. Les owners explicitement vides du sidecar hashé
peuvent seuls recevoir `ManualPlacementCollisionNotRequired`. Une scène sans
sidecar conserve son contrat incomplet précédent.

Le yaw des boxes est porté par leur hull, sans rotation de child qui introduirait
une approximation sous scale non uniforme. Le convertisseur traite également
les convex boxes des scans ; leur adoption reste à raccorder au rendu des scans.
Les index, tags et flags source sont conservés en composant. Le controller ne
consomme pas encore leur caractère non praticable : aucune parité gameplay
n'est déduite de leur présence.

La scène conserve les identités des anciennes primitives enregistrées dans
Rapier jusqu'à leur disparition effective. `collisions_retired` contrôle ce
retrait séparément de la readiness des nouveaux placements.

Le test scène 10107 est lancé sur le nouveau candidat. Il doit vérifier avec
Rapier réel les 805 placements, 51 primitives, 762 owners sans collision,
readiness avant/après enregistrement et retrait après suppression. Son verdict
reste pending au lancement. Les textures/shaders sont toujours des doubles CPU ;
ce test n'est ni un rendu GPU ni une validation du controller.
