# Tidewater — ownership et repères locaux des collisions Debris

Suite du [raccord de scène](2026-10-05-debris-scene-staging.md).
Les acquisitions ci-dessous ne changent pas les verdicts de rendu ou gameplay.

## Association aux émissions source

L'exporteur CPU accepte maintenant le mode supplémentaire `--observe-collisions`
après `--retain-payloads --observe-ownership`. Les observers sont propres à
l'instance DebrisPlacer. Ils appellent les corps source sans modification,
n'ajoutent aucun tirage aléatoire et ne modifient pas le clone épinglé.

Les primitives des groupes procéduraux sont associées au marqueur count après
émission du groupe, avec les mêmes frontières de géométrie acquises auparavant.
Les primitives des troncs utilisent l'identité exacte de l'objet retourné par
_scanLog puis transmis à _logCollider : la WeakMap relie ce retour à l'index
de l'instance scannée effectivement ajoutée. Aucun classement par proximité.
Une émission manquante, un retour inconnu, une association sans géométrie ou
une collision restante sans owner provoque un refus explicite.

Deux acquisitions `debris-collision-owners-20261005-v1` et `v2` terminent code 0.
Le vérificateur `verify-debris-collision-ownership.py` réouvre les fichiers,
exige leur identité entre les deux exécutions et compare tous les fichiers
antérieurs avec `debris-ownership-20261005-v4`. L'inventaire, ownership des
meshes, géométries, tints de contact, masque et collisions source sont tous
identiques. Le vérificateur CPU complet des payloads est également rejoué.

Reçu `debris-collision-owners-20261005-repeat.json` : PASS dans cette portée.

- 946 owners : 805 procéduraux et 141 scannés ;
- 111 primitives, couvertes une seule fois : 86 boxes et 25 cylinders ;
- 51 primitives sur 43 owners procéduraux, 60 sur 60 troncs scannés ;
- ownership SHA e5e4567b3515b77b4da370b11b29c7b5695724f708a3d1bd117c7d76eae6928f ;
- primitives source SHA b758462cf1398377cb7dd9357c3b91e4ea99e0806114079b70b4c8cf00f24a81.

Le reçu contrôle identités/catégories, absence de doublon et de trou dans les
index, bases d'émission, finitude, dimensions positives et paramètres des
primitives. Les owners sans primitive sont conservés explicitement : aucune
collision n'a été émise pour eux dans cette exécution source. Le runtime ne
consomme pas encore ce contrat, donc aucun marquage de readiness n'est promu.
Le device headless possédé par l'acquisition est fermé à la fin du traitement.

## Normalisation des repères de collision

`package-debris-collision-frames.py` utilise le reçu d'ownership terminé et
vérifie ses empreintes contre les données réouvertes. Le paquet procédural
reste la révision immuable 0a84592f…5189a ; ses frames, licence et crédits
sont réouverts par références SHA/tailles. Le descripteur des scans conserve
les matrices/quaternions source acquis précédemment.

Les boxes et cylinders des objets procéduraux sont déplacés dans leurs
pivots locaux sans changer formes, yaw ou flags source. Les boxes des troncs
exigent un traitement différent : le repère source du tronc comprend roll,
pitch et scale non uniforme, alors que sa boîte de collision source est
orientée uniquement autour de Y. Appliquer l'inverse du repère au cuboid peut
produire un parallélépipède local, irréductible à une boîte et un pose rigide.
Les huit coins sont donc transformés par la matrice inverse et conservés
en float32 avec 12 triangles fermés, sans approximation par des demi-extents.

Deux préparations finales v2/v3 sont identiques. La v1, avant l'ajout du
contrôle quantifié des dimensions des cylinders, reste une étape conservée.
Le vérificateur `verify-debris-collision-frames.py` réouvre les deux sorties,
reconstruit indépendamment les matrices à partir des quaternions/scales,
compare les corners mondiaux aux primitives source et vérifie la couverture
des index. Les boîtes convexes ont huit sommets, douze triangles, des arêtes
partagées deux fois et un volume orienté positif. Licence et crédits sont
identiques aux références du paquet acquis.

Reçu `debris-collision-frames-20261005-repeat.json` : PASS pour la reconstruction.

- 26 boxes locales, 25 cylinders locaux et 60 convex boxes ;
- 946 frames, dont les owners sans collision explicites ;
- écart maximal avec géométrie locale float32 : 1,1659432981635246e-7 m ;
- collision-frames.json SHA 8f46a42c3c94fdea04fba2174bc7c85a26c0f56c61887b1c7b8a017c185c7ac2.

Le contrôle utilise les matrices source en f64 et les valeurs/géométries
locales quantifiées en f32. Il ne qualifie pas encore le TRS Bevy réel,
l'upload/contact Rapier, les changements d'échelle auteur, le controller
capsule/step, le bateau ou les gestes/persistances Studio. Ce sidecar local
intermédiaire n'est ni un GLB normalisé ni une promotion Vault.

Les fichiers Rust restent inchangés pendant le test moteur 15076, toujours
actif lors du dernier polling. Prochaine étape : lecteur du sidecar, adoption
des 51 primitives procédurales puis des 60 primitives des scans, et contrôle
de leurs frames/contact/readiness dans Rapier. L'affichage de Debris dans la
scène entière et sa comparaison visuelle restent distincts et ouverts.

Le nouveau lecteur compile aussi pour `wasm32-unknown-unknown` :
`cargo check -p forge_assets --lib --locked --target wasm32-unknown-unknown`,
Rust 1.98.0 fixé et cache lecteur v2, termine code 0 en 4,18 s. Ce résultat
prouve la compilation portable du lecteur, sans chargement par le navigateur,
adoption Studio, rendu ou contacts Rapier. Clippy reste en échec comme détaillé
dans le rapport d'adoption native ; ses corrections attendent la fin de 10107.
## Résultats supplémentaires du 5 octobre

Le test moteur 15076 mentionné plus haut est terminé : trois tests PASS,
code 0. Le lecteur Rust `forge_assets::tidewater_debris::collisions` est ajouté
avec SHA compilé, limites, validation des 946 owners, frames et partition des
111 primitives. Le test sur le sidecar réel (session 56047) termine code 0 :
1 PASS, refus des bytes altérés et d'une matrice modifiée compris.

Le raccord Bevy/Rapier est ajouté et son test scène 10107 est lancé ; verdict
pending. Le rendu complet, les contacts du personnage et l'adoption des scans
restent ouverts. Voir la suite du rapport de staging.
