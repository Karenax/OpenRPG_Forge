# Tidewater — acquisition commune des décors scannés

Les 141 variations scannées disposent d'un reader de paquet synchrone et
asynchrone, mais le bootstrap natif lit séparément le programme d'instances
final. La variante contenue dans le paquet géométrique est une étape
antérieure conservée comme provenance ; elle ne doit pas remplacer le
programme réellement joué au natif.

`forge_assets::tidewater_debris::scanned_input::ScannedInput` regroupe le
paquet, la géométrie neutre, les collisions et le programme final. Avant
tout transport, il exige les 23 573 octets du shader épinglé
`3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3`.
Le lecteur ScannedPackage conserve son pin
`f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4`
et les contrôles de ses 82 payloads. La restitution vérifie les 12
prototypes, les 141 instances, les atlas acquis et les propriétaires de
collisions : catégorie, translation, quaternion, échelle et matrice source
doivent correspondre exactement. Aucune dépendance ECS, GPU ou filesystem
n'est introduite dans le reader de production.

Le contrôle réel est lancé en session 54325 :

```text
cargo +1.98.0 test -p forge_assets tidewater_debris::scanned_input::tests
  --offline -j1 -- --include-ignored --nocapture
```

Les fixtures sont `debris-scanned-package-20261005-v1` et le programme
`debris-scan-instance-program-20261005-v1/scanned.wgsl`. Le test compare
les restitutions sync/async sur des futures immédiatement disponibles,
et refuse un programme modifié ainsi qu'un payload de collisions altéré.
Un second test exige le refus d'un programme inconnu avant tout accès au
transport. Résultat terminal encore en attente au moment de cette entrée.

Ce module est une acquisition préalable à l'édition. Le bootstrap natif
et le loader Studio ne sont pas encore raccordés à ce nouveau type.
Le candidat Studio `studio-debris-candidate-20261005-v1`, dont les builds
97404 et 30637 restent actifs, est figé avant cette addition et couvre les
805 décors procéduraux ; il ne contient pas ce nouveau reader. Même un PASS
de ses tests ne prouverait ni HTTP, ni dessin, ni picking, ni édition Studio
des scans, ni parité Three.js. L'objectif complet reste ouvert.

## Résultats et raccord natif

La session 54325 termine code 0 : deux tests réussis, zéro échec, zéro
ignoré, 109 filtrés (0,30 s ; compilation 5 min 21 s). Le test d'intégration
procédural distinct est filtré par cette commande et ses zéro tests ne sont
pas comptés comme une nouvelle preuve. Les programmes sync/async, les 141
matrices source, les 12 prototypes et les 946 propriétaires concordent.
Le shader modifié est refusé avant transport, le payload de collisions
modifié est refusé par son pin.

`examples/tidewater_plant_probe/scanned_debris.rs` utilise ensuite ScannedInput
à la place des lectures et comparaisons locales redondantes. Il conserve
ses lectures filesystem bornées avec contrôle de racine canonique, et les
contrôles d'ABI du shader, les meshes, les images et les primitives Engine.
Le nouveau reader conserve aussi le contrôle exact de matrice auparavant
absent de ce raccord. Aucune nouvelle capture native n'est attribuée à
cette modification ; le binaire portable existant n'est pas reconstruit.

La session 79853 passe Clippy Assets avec -D warnings (12,61 s), puis
le check Assets wasm32-unknown-unknown (12,22 s). Elle poursuit le check
du véritable exemple tidewater_plant_probe ; résultat natif encore en attente.
La restitution dans Studio des scans reste à raccorder et qualifier.

La session 79853 termine ensuite code 0 : le check de l'exemple natif
passe en 1 min 44 s. Il conserve le warning préexistant isolated_lod::run
dans tidewater_object_shadow_probe, hors reader/raccord modifiés. Ce contrôle
de compilation ne reconstruit ni le binaire joué ni le candidat portable.
Le reçu `scanned-shared-input-tests-20261005-v1.json` conserve les empreintes
du reader, de sa déclaration et du bootstrap natif ainsi que ces périmètres.
