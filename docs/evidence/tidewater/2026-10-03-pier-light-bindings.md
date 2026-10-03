# Liaison source des onze lanternes du quai — 3 octobre 2026

`scripts/tidewater/prepare-pier-light-bindings.py` raccorde les identités de
lanternes du paquet lumineux aux sous-groupes du quai converti. Les deux paquets
et leurs payloads sont relus avec tailles et SHA-256 ; les fichiers Village.js
et Pier.js courants correspondent aux pins du descripteur lumineux acquis.

La relation vient de la construction source : `_buildLanterns` parcourt
`pierInfo.hung`, transmet les mêmes objets pivot/rest/live et conserve l'ordre.
Pour chaque ordinal 0–10, le contrôle exige l'identité observée
`Village.lanterns[ordinal].obj`, le binding `live-vector-object-identity`,
le même émetteur et sa frame. Les positions ne choisissent jamais un owner.
Elles vérifient ensuite la correspondance : frame monde = origine du quai +
pivot local ; centre local lumineux = rest − pivot ; centre monde = origine
du quai + rest. Les onze correspondances passent à moins de 1e-12 m.

La commande réelle termine avec retour 0 sur `pier-package-001` et
`source-local-lights-preserved-phase-identities-20261003-v3/package`.
Elle écrit `F:/OpenRPG_Forge/.build/pier-light-bindings-20261003-v1/pier-light-bindings.json`,
SHA-256 `bad01a8f2a9a917dd77ee12ee48cc6fad87fd3518f550dc46789df1d7dd8e527`.
Les phases lumineuses restent dans le paquet original, référencées par identité ;
aucune nouvelle phase n'est générée. Les paquets, placements et compilations
existants sont conservés.

Ce résultat prépare le raccord aux onze vraies entités lanternes sous une racine
`ManualPlacement` du quai. Il ne l'installe pas : il reste à transmettre ces
owners au projecteur générique, à suivre le balancement, à gérer les copies et
le retrait du quai, puis à observer les pixels dans Studio. Aucune preuve
de rendu, de parité ou de distribution n'est acquise par ce reçu.

## Adaptateur moteur en validation isolée

`crates/forge_engine/src/tidewater_pier_lights.rs` prépare les onze émetteurs
depuis les deux paquets vérifiés et refait le contrôle des identités et ancres.
Il exige la révision d'asset du quai, une unique racine `ManualPlacementProjection`
identique au placement et enregistrée dans `ResidentPier`, puis les onze vrais
`PierLanternPivot` enfants de cette racine. Leur rotation courante peut évoluer ;
leur pivot local doit rester celui du maillage. Le projecteur générique conserve
les phases, vérifie les poses propagées et refuse les conflits avant de publier.

Le harness `F:/OpenRPG_Forge/.build/pier-light-ecs-validation-20261003-v1/harness.rs`
prépare la vraie géométrie du paquet et deux placements, dont une copie déplacée
et agrandie. Il vise la conservation des phases, le mouvement lumineux après
rotation du vrai pivot, le remplacement, le refus des doublons et le retrait.
La compilation native isolée (session 99255) et Clippy de bibliothèque (15123)
sont lancés contre les bibliothèques natives existantes, avec hashes avant/après.
Ils ne sont pas encore terminés lors de cette inscription : aucun PASS d'essai
n'est revendiqué. Le module n'est pas déclaré dans lib.rs pendant les builds
concurrents ; aucun chargement ou raccordement produit n'est encore acquis.

Les premiers contrôles terminent avec refus conservés : Clippy relève une
fonction de 106 lignes et deux comparaisons exactes de tableaux flottants ;
la liaison native du harness manque `windows.0.52.0.lib` (LNK1181), sans erreur
de typage Rust. La validation de la frame source est extraite dans une fonction
séparée, qui documente explicitement les comparaisons exactes de l'identité
sérialisée épinglée. Le harness V2 ajoute le chemin natif de la bibliothèque
Windows déjà installée et son hash à ses entrées vérifiées.

Clippy de bibliothèque V2 passe avec retour 0, warnings en erreurs (`all` et
`pedantic`), entrées inchangées. Le module porte alors le SHA-256
`896bd0207f7073d88227b6c9811706ce204094170ff6c568399c1907c1eb0a85`.
La liaison V2 est confirmée vivante (rustc 37884, link.exe 30648) ; l'essai
comportemental n'est pas encore exécuté à cette observation. Ce contrôle
isolé ne remplace pas un Cargo test du worktree ni une validation Studio/GPU.

La liaison V2 réussit et l'essai démarre, puis refuse la préparation du quai
(`Pier material is not resident`). Le harness avait omis `material-6` du
paquet réel, utilisé par la corde. V3 déclare les trois matériaux réellement
référencés (`material-0`, `material-1`, `material-6`), conserve le harness V2
et ses journaux, puis relance compilation et essai (session 71771).
L'adaptateur moteur n'est pas changé ; son Clippy V2 reste acquis dans sa
portée de bibliothèque. Aucun succès comportemental V3 n'est encore revendiqué.

V3 termine ensuite : un test passé, zéro échec, zéro test ignoré, 2,81 s.
`result-v3.json` vérifie les neuf entrées et conserve le SHA-256 de l'adaptateur
`896bd0207f7073d88227b6c9811706ce204094170ff6c568399c1907c1eb0a85`.
La vraie géométrie du quai est préparée sur deux racines auteur ; 22 émetteurs
sont récoltés. La rotation du pivot réel de la première lanterne déplace sa
lumière de plus de 5 cm sans déplacer les onze lumières de la copie. Les bits
des phases sont identiques au paquet source. Le remplacement passe ; une
seconde projection de la même instance est refusée sans ajout d'entités.
Le retrait de l'original conserve les onze lumières de la copie, puis le retrait
de celle-ci laisse un inventaire vide. Ces résultats portent sur l'ECS natif
isolé contre les bibliothèques existantes : module toujours non déclaré,
aucun dessin GPU ni installation Studio acquis.

Le harnais V4 termine avec retour 0 à 06:38:47 UTC : un test passe,
sans échec ni test ignoré. Les positions des 22 émetteurs (original et copie)
sont comparées aux transformations attendues ; erreur maximale
0,000008534101038015685 m, sous le seuil 0,00005 m. Une révision d'asset
incorrecte est refusée à la préparation. Un pivot d'ordinal dupliqué est
refusé au remplacement sans supprimer les 22 lumières existantes.
Le module reste non enregistré et cet essai ECS isolé ne prouve pas
le dessin GPU ni son intégration Studio. Reçu :
F:/OpenRPG_Forge/.build/pier-light-ecs-validation-20261003-v1/result-v4.json.

## Raccordement au collecteur Studio, candidat isolé

La préparation studio-pier-light-integration-20261003-v1 reprend le snapshot
editor-water déjà observé en UI. Trois overlays sont prévus : export du module
moteur tidewater_pier_lights, son fichier validé en ECS et extension du collecteur
Studio local_lights. L'inventaire dérivé réunit les bâtiments et les quais depuis
les mêmes ManualPlacement que la géométrie. La préparation des lumières du quai
utilise PierPackage et PreparedPierLights ; la collecte GPU existante conserve
le même temps, état nocturne et politique de tri. Les copies reçoivent des plages
de registration distinctes sans modifier les phases acquises.

La géométrie ResidentPier doit être synchronisée avant staging. Le receipt suit
les placements triés ; retouche, copie, retrait ou changement de paquet rendent
l'ancien packet lumineux invalide. Les deux types d'émetteurs restent possédés
par le même collecteur et retirés par le chemin générique ResidentSourceLights.
La copie figée protège les entrées des validations globales actives du dépôt.
Le script Prepare.py prépare les trois overlays et leurs hashes ; aucune
intégration dans les exports du dépôt principal ni nouveau rendu GPU n'est encore
revendiquée. La validation WASM du consommateur est la prochaine vérification.

La préparation termine à 06:59:19 UTC avec 1 344 fichiers et exactement les
trois overlays annoncés. Run-Check.ps1 lance Cargo check release WASM du vrai
frontend Studio (session 65495, Cargo PID 42356 confirmé vivant). Le verdict
compilateur et la garde de sources finale restent en attente ; aucun second
build ni relancement n'est effectué pendant cette observation.

La continuation suivante confirme le même Cargo 42356 et son enfant rustc
11504 compilant wgpu pour WASM ; le check est vivant, pas terminé. Le contrôleur
Continue-AfterCheck.ps1 (session 66449) attend le parent Run-Check 19492 dont
l'identité et l'heure de création sont vérifiées. Après son arrêt, il exige le
reçu Cargo avec retour 0 et Verify-Check.py (inventaire et hashes inchangés)
avant Build-And-Assemble.py. Le contrôleur ne relance aucun check à l'expiration
d'une attente. La sortie frontend, le worker et les reçus sont créés frais.
Build-Native.py prépare attempt.5 avec contrôle des entrées natives réelles et
du frontend embarqué ; sa construction n'est pas encore lancée.

Inspection complémentaire de Village.js : _updateLanterns utilise un pendule
avec état t/ph/x/z/vx/vz et le vent source. Ces états de mouvement ne sont pas
présents dans le paquet géométrique capturé au repos. Le suivi des pivots par
les émetteurs est prouvé séparément ; l'animation automatique des lanternes
et du panneau reste à acquérir et raccorder, sans déduire ses phases de celles
du scintillement lumineux ni inventer des valeurs de référence.
