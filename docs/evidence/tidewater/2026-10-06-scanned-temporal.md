# Contribution temporelle des objets scannés — 6 octobre 2026

Statut : dérivation de shader préparée et validée CPU ; adoption Engine/native/Studio
et rendu effectif encore non qualifiés. Aucun commit, push ou passage de gate.

## Contrat source

`ScannedDebris.js` du commit figé
`1438b1abfcaee3267092b75573014f4d9b4a983c` déclare `staticVelocity = true`.
La contribution auxiliaire doit donc reprojecter la même position monde courante
avec les matrices des deux vues, et conserver le fondu de LOD obtenu dans la
surface. Les records caméra/LOD du buffer d'instances ne sont pas des matrices
précédentes d'objets édités.

Le programme final acquis est
`.tidewater-integration/debris-scan-instance-program-20261005-v1/scanned.wgsl`,
SHA-256 `3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3`.
Il conserve le varying `vIData` en location 4, le stockage `scanInstances` en
binding 106 et l'index `MeshTag` lu par le vertex.

## Préparation

Sous `.tidewater-integration/scanned-temporal-20261006-v1/staged` :

- nouveau dérivateur `tidewater_temporal/scanned.rs` ;
- nouvelle famille `Scanned` dans le dispatcher existant ;
- enregistrement `ScannedMaterial` opaque dans les deux adapters natifs
  `temporal_aux.rs` et `temporal_owned.rs`.

Les corps des fonctions source vertex, surface et `lodFadeVisible` restent
identiques. La contribution garde l'appel de surface et son alpha-test avant
d'écrire les positions courantes et les masques opaques. Une liaison modifiée,
un fondu supprimé, un discard supplémentaire, un index non acquis ou un shader
déjà dérivé est refusé.

## Vérification effectuée

`VerifyScanned.py`, processus 70744 : compilation 0, test 0, inputs inchangés.
Le test utilise le vrai dispatcher préparé avant `cfg(test)`, les fonctions
d'adaptation de matériau courantes, le programme acquis, les imports Bevy 0.19.1
et le cache de shaders/Naga. Il compile huit combinaisons : vertex/fragment,
éclairage partagé activé/désactivé et modèles mesh storage/uniform.
Un test effectivement exécuté, zéro filtré, 0,89 s d'exécution. Les assertions
contrôlent les fonctions conservées, la reprojection statique, les bindings du
packet auxiliaire et les cas de refus ci-dessus.

`preparation.json` épingle les trois originaux de production et les quatre
fichiers préparés. Les originaux sont comparés au manifeste du build natif Pebble
encore actif ; ils ne sont pas modifiés par cette préparation.

## Limites et suite requise

Le test est une compilation CPU de shaders avec dépendances précompilées, pas
une compilation du crate Engine ni une exécution GPU. Les enregistrements natifs
préparés ne sont pas actifs dans le binaire en cours de construction. Reste à
adopter les modifications après les contrôles des sources en cours, vérifier le
crate et les adapters, rendre les 141 instances avec les deux LOD et leurs
retouches communes, lire les textures auxiliaires puis comparer les scènes
fixes et mobiles à la référence. Le raccord Studio des scans reste distinct.
