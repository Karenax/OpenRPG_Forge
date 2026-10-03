# Tidewater — projection du bateau dans Engine

Suite du [lecteur Rust](2026-10-02-boat-rust-loader.md) et de
l’[acquisition canonique](2026-10-02-boat-source-acquisition.md).
Le paquet `319` reste figé au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`, reçu
`44a9110a0bda0cd319488f088fc1da32f3344f6cad8b9db0a92869df0c49351a`.

## Changement livré

`forge_engine::tidewater_boat::PreparedBoat` conserve les 14 nœuds source,
leurs parents et poses locales, les 12 meshes et leurs cinq flux d’attributs,
les 46 845 triangles ordonnés et les 23 volumes de collision. Le pont marqué
`walkable=true, solid=false` reste un support statique, sans devenir un sensor.
Les intentions source `walkable` et `solid` restent distinctes ; ces volumes
ne reproduisent pas le contrôleur de marche local au bateau de `Player.js`.

Le placement utilise un `ManualPlacement` et un ID de catalogue fourni par
l’appelant. La coque HULL est un enfant du parent déclaré dans le descripteur,
avec sa pose locale source. Son empreinte vérifiée autorise les huit arêtes
ouvertes du proxy d’origine ; aucune face n’est ajoutée.

`ResidentBoat<M>` vérifie les propriétaires, parents, poses, rôles, meshes,
handles et identité HULL. La disponibilité exige également un enregistrement
Rapier réel et actuel. Un remplacement est préparé avant le retrait de
l’ancienne instance. Le retrait retire aussi les entités possédées qui ont
été reparentées, détache les enfants externes et conserve les meshes empruntés
par une autre entité. Les matériaux fournis restent possédés par l’appelant.

## Preuve CPU réelle

L’exemple obligatoire `verify_tidewater_boat_instance PACKAGE_ROOT` refuse un
paquet absent ; il ne saute aucun essai grâce à une variable d’environnement.
Il emploie des handles `StandardMaterial` **sans renderer ni GPU**, uniquement
pour exercer l’API générique de projection. Ils ne sont pas des matériaux de
substitution livrés au jeu.

Le premier candidat natif `338/339` passe sur le paquet source et sa copie
déplacée : 12 meshes identiques bit par bit, 14 parents/matrices vérifiés,
23 collisions comparées à la source, six refus avant allocation, treize cas
de dérive en scène, enregistrement Rapier, remplacement et sérialisation.
L’erreur maximale de matrice locale est `7,629394538355427e-8`. Après retrait,
aucun mesh possédé ne reste ; les matériaux partagés et les objets voisins
survivent. Ces résultats restent historiques : l’éditeur commun a été intégré
après la compilation et l’oracle a ensuite été renforcé.

- Binaire historique conservé :
  `F:/OpenRPG_Forge/.build/boat-engine-validation-339/artifact/verify_tidewater_boat_instance.exe`.
- SHA-256 : `dd57d27d8a86d3b2843b4f2ff4cf232ce9c53fd2535ddcce97cb9b0a4548fd05`.
- Logs et retours : `boat-engine-validation-339/{source-package,relocated-package}.*`.
- Reçu indépendant des 620 entrées compilées, dont 18 anciennes versions
  retrouvées dans les sauvegardes d’intégration :
  `boat-engine-build-plan-337/compiled-source-receipt-620.json`.

La qualification finale utilise les sources communes intégrées, figées dans
`boat-engine-source-341`, avec 865 fichiers et les JSON/WGSL embarqués.
L’oracle ajoute une comparaison indépendante du HULL local et global, de
l’identité du descripteur, de toutes les entités possédées et du retrait des
collisions dans le backend Rapier. La construction finale et les deux
exécutions passent avec le même inventaire et les mêmes 19 refus internes.
Le HULL canonique a une erreur de matrice `0`; le HULL déplacé et tourné passe
également la comparaison à la chaîne de matrices source. Le backend Rapier
et ses tables de correspondance ne contiennent plus aucune collision après
le retrait final. L’argument absent et le paquet absent terminent chacun
avec le retour `1` attendu, sans réussite de repli.

- Preuve finale : `F:/OpenRPG_Forge/.build/boat-engine-validation-343`.
- Binaire immuable : `artifact/verify_tidewater_boat_instance.exe`,
  60 478 976 octets, SHA-256
  `3fda272745e01f914eb9b82b70bbb399bcaaa1ab75525863a8d193cc1bf62561`.
- Reçu de build : `boat-engine-build-338/build-instance-final-result.json`.
- Sources exactes : `boat-engine-source-341/source-inputs-final.json`.
- Contrôle final : `343/source-drift-final.json`; les 865 copies figées restent
  identiques. Le seul écart avec le live est le manifeste du laboratoire
  `tools/forge_navigation_benchmark`, explicitement exclu du workspace et de
  cette compilation. Les entrées Engine/dépendances/embarquées utilisées restent
  identiques au live.
- Logs/reçus : `343/{source-package,relocated-package,missing-argument,missing-package}.*`.
- Une erreur de capture de variable dans l’outil d’écriture des reçus est
  conservée dans `343/attempt-without-receipt` ; les quatre verdicts ci-dessus
  proviennent de l’orchestration corrigée, avec empreinte explicite du binaire.

La revue indépendante `boat-engine-instance-map-335/NOTES.md` confirme la
cohérence entre les nouveaux oracles, le code figé et les quatre verdicts.
Elle ne transforme pas cette preuve CPU en inventaire global de zone ou en
qualification du workspace entier.

Le contrôle de compilation de la bibliothèque `wasm32-unknown-unknown` avec
la feature `web` termine avec le retour `0` contre le même snapshot final.
Log/reçu : `boat-engine-build-338/check-final-wasm.log` et
`check-final-wasm-result.json`. Les verdicts combinés sont scellés dans
`boat-engine-validation-343/qualification.json` et `receipt.json`, reçu SHA-256
`708b49d153bef05596d781ce950d9d11dce106bc540375bffcd6f3f98a7c6b9f`.
Ce contrôle de métadonnées ne constitue pas une preuve d’exécution WebGPU.

Le contrôle Clippy final de la bibliothèque et de l’exemple termine avec le
retour `0`, sans diagnostic sur ces deux nouveaux fichiers. Les **274
avertissements préexistants** restent visibles : le contrôle global strict
`-D warnings` ne passe pas. Les deux avertissements du premier exemple ont été
corrigés sans désactiver les lints. Le premier démarrage sur une copie incomplète
est conservé dans `clippy-snapshot-incomplete.log`; la copie complète et le
dernier contrôle réussissent séparément. Reçu :
`boat-engine-build-338/clippy-final-summary.json`.

Les compilations CPU utilisent Rust/Cargo `1.98.0` explicites, le lock existant,
le mode offline et un target isolé sur F:. Les optimisations du profil dev sont
désactivées seulement pour cette preuve ; aucune mesure de performances du jeu
ne peut être déduite de cet exécutable.

## Éclairage source acquis, raccordement encore ouvert

Le paquet indépendant `boat-source-lighting-340` conserve 68 fichiers upstream,
224 relations d’import/export et aucune dépendance relative manquante, ainsi
que les licences et crédits. Ses copies ont été revérifiées bit par bit.
Manifest SHA-256 :
`92132489cae4368a54e0e1bb74b0a74cc68572bf1f5b67bc860960683fa36c39`.
Il ne modifie pas le reçu géométrique `319`.

Il contient le `Surface` complet et les BRDF/hook sources de `lighting.js`,
`MeshShader.js`, `Frame.js`, environnement, ombres et éclairage sous-marin.
Les sept matériaux exigent notamment le clearcoat du hull, le verre double
face transparent sans écriture de profondeur ni contribution à la vitesse
temporelle, et le discard alpha `0.5` des pièges dans les passes couleur et
ombres. Ce paquet est une preuve de provenance et d’ABI, sans compilation de
shader, rendu ou qualification visuelle.

## Limites et suite

Suite : [acquisition réelle des vingt shaders source](2026-10-02-boat-source-shaders.md).
Leur compilation WebGPU source passe ; le raccordement et le rendu Rust restent
à qualifier séparément.

La preuve porte sur ces instances résidentes ; elle n’est pas l’inventaire
complet d’une zone. Le census recalcule actuellement les empreintes CPU des
meshes ; son coût dans une scène jouable n’est pas encore mesuré.

Restent à raccorder et qualifier : les sept matériaux source et leurs ombres/
vitesses temporelles, le transport/catalogue Studio, les outils communs et
leurs cycles sauvegarde/rechargement, les comportements du bateau, l’inventaire
HULL complet, les passes temporelles Source7 en scène réelle, puis les captures
natives/WebGPU appariées. `appearance_qualified`, `gpu_executed` et
`studio_qualified` restent faux. La calibration d’exposition 096 et les anciennes
preuves GPU d’eau ne sont pas requalifiées par ces essais CPU. L’objectif complet
reste actif.
