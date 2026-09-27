# R2-025 — rangée de modules sur tracé

13 septembre 2026. Outil générique de disposition, branche Rust, sans import
d'asset, dépendance nouvelle, commit ou publication. Ce lot n'est pas une preuve
graphique de clôture/mur et ne clôt pas R2-025.

## Implémentation

`forge_world::path_placement::prepare` répète un placement manuel épinglé sur
un chemin accepté de la même zone. Espacement horizontal cumulé sur tous les
segments, départ en mètres, décalage latéral signé, hauteur de pivot par rapport
au terrain effectif (incluant rivière), correction d'orientation. L'axe X local
est orienté selon la tangente, inclinaison et échelle source conservées. Au coin
exact, tangente sortante. Aucun ajout forcé au dernier point, pas de premier
module doublé à la fermeture d'une boucle démarrée à zéro.

Chaque copie conserve asset ID/hash/nom, reçoit un ID déterministe et son propre
transform absolu déverrouillé. Source et chemin peuvent rester verrouillés :
ils ne sont pas modifiés. Inclinaison source avec X vertical refusée faute de
direction horizontale ; pas de rotation arbitraire masquant cette ambiguïté.

Insertion atomique `PlacementEdit::InsertMany`, 1–256 objets ; limite totale
existante de 4096, IDs uniques/triés, transforms et taille de tête toujours
validés. Paramètres finis, pas 0,1–128 m, décalages ±64 m, absence de source ou
tracé et sortie du terrain refusés avant mutation. La proposition rejoint
ZoneRequest/PreviewZone/Apply, avec une seule entrée d'historique et un Undo.
L'archive reste le schéma de corrections/placements existant, sans migration.

Studio : sous-panneau « Répéter sur un tracé · clôture ou mur » dans l'inspecteur
d'un objet placé. Sélecteur de chemin accepté, cinq paramètres, bouton d'examen.
La prévisualisation collective utilise `placements::proposal` existant, qui
accepte un vecteur de placements et vérifie les références du kit ; les bytes
d'un même asset sont résolus une fois dans la préparation. Aucun nouveau writer.

## Vérification

5 tests `path_placement` PASS (0,02 s) : espacement intersegments, tangentes,
offsets, terrain retouché, source épinglée intacte, boucle, inclinaison source,
déterminisme, options invalides, source absente, sortie du terrain, limite de
copies, batch vide/IDs en conflit/trop grand. Preview sans mutation, retry,
Undo/Redo atomiques, retouche individuelle d'une copie, régénération, relecture
des archives intégrale/compacte/delta et refus d'une nouvelle commande périmée.

6 régressions `manual_placements` PASS (0,01 s), test `zone_host` PASS (0,05 s).
Clippy world/lib/test strict PASS (0,30 s), Studio WASM strict PASS (5,20 s final).
Formatage et diff vérifiés. Avertissement transitif de compatibilité future
`proc-macro-error2 2.0.1` conservé ; aucune règle de validation désactivée.
Fixtures d'assets synthétiques : ces tests ne lisent ni ne qualifient un GLB,
sa licence, ses pivots ou ses collisions. Aucun essai graphique ouvert.

## Ce qui reste

### Préparation du candidat d'édition — 13 septembre 2026

Les derniers frontends rivière construits directement par Trunk ne contenaient
pas `forge-kit`, alors que le navigateur d'assets lit `./forge-kit/build.json`.
Ce manque de packaging empêchait d'utiliser le vrai mur dans ces candidats.
`Build-StudioCandidate.ps1 -Authoring -GovernedKit <source>` réutilise désormais
la copie épinglée existante (rapport, catalogue, preview et GLB), ouvre
`index.html?authoring=1` et refuse les options de scènes/probes Gate 1. La source
explicite est obligatoire ; absence de kit et mélanges de modes sont refusés
avant compilation. Aucun changement du lancement normal de Gate 1.

`Test-StudioCandidateKit.ps1` PASS : route d'édition, pins inchangés, absence de
sortie en ValidateOnly, refus sans source et des dix options incompatibles ;
les cas existants de chemins/hashes/identités restent PASS. Ce sont des fixtures
de packaging, pas une qualification du rendu, des GLB ou de la rangée de murs.

Kit réel retenu, source externe en lecture seule :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-staged-20260911/studio/web/forge-kit`.
Le rapport conserve `internal_gate_proof_only`, licence
`LicenseRef-OpenRPG-Forge-Project-Generated`, revue humaine pending et publication
non approuvée. Six références, dont `vv-architecture-wall-a`, GLB épinglé
`6d6c05986c8ef35779b7cad7fbc8013e5fad05634b30d2d6335872b109b4f073`.
Pas de nouveau contenu Cadle, d'import Vault ou de reconstruction Blender.

Candidat construit :
`E:/OpenRPG_Forge/.gate2-evidence/studio-path-array-kit-20260913/forge-studio-candidate.exe`.
Trunk et Tauri PASS, rapport local `build-proof.json` conserve les 13 pins
(4 frontend + 9 kit), sans verdict runtime global. EXE SHA-256
`D264A3F00FF90947569CFD30626BBADFA82E706D6BBD6D08AC1263CACF12CD4B` ; WASM
`C19F882E3DD1A334E4635D6413FCA94291F4ADAB8BBEDCBCF901FF8A887196E8`.

Essai sur écran secondaire (x=-1920, y=52) : bibliothèque chargée, six références,
fiche `vv-architecture-wall-a`, bouton de vérification du fichier : **1 928 420
octets, SHA-256 conforme, précontrôle GLB autonome réussi**. Capture affichée et
inspectée dans la conversation ; aucun PNG local prétendu. Aucun placement,
terrain, aperçu 3D ni collision testé : ce lancement via le contrôleur de fenêtre
n'héritait pas de `FORGE_AUTHORING_ROOT`, refus explicite visible. Pour le prochain
parcours, définir cette variable au lancement vers le dossier autorisé existant
`E:/OpenRPG_Forge/.gate2-evidence/authoring-rivers-ui-20260913`, puis rouvrir la
zone sauvegardée ; ne pas recréer sa rivière. Cette garde n'a pas été désactivée.
Session fermée par Alt-F4, PID 15724 et descendants
6292/5724/23164/1044/32212/21120/30252 contrôlés absents.

### Qualification produit restante

### Parcours réel du 13 septembre, suite

Même candidat D264A3F0, sans recompilation : lancement avec `FORGE_AUTHORING_ROOT`
vers `E:/OpenRPG_Forge/.gate2-evidence/authoring-path-tools-ui-20260913` (carte
existante avec chemin, plutôt que recréer un tracé dans la carte rivière).
Liste : une zone disponible, aucune archive indisponible. Réouverture acquise,
terrain et chemin rendus, deux placements préexistants préservés.

Mur source posé par clic dans le terrain puis Preview/Apply, ID
`83d3d7c3-4a48-5665-b043-fc9eac824143`, position (-68,74 ; 3 ; 103,27) m.
Répétition sur « Chemin », pas saisi 4 m, autres réglages 0 : proposition
**3 → 71 placements** (68 copies), rangée visible sur les segments du chemin.
Apply puis un Undo : **3 placements / 7 primitives Rapier** ; Redo :
**71 placements / 75 primitives Rapier / 0 en attente**. Il s'agit de l'état
CPU/physique déclaré, pas d'un parcours de collision par personnage.

Sauvegarde par le bouton Studio, statut « Session synchronisée ». Archive v7,
commandes sur révisions attendues 25, 26, 27 et 28 (source, rangée, Undo, Redo).
Fichier `81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`, SHA-256
`4B564C2FB5913AB533D52D77F6EF6542F1CCE3889D4520E1FEC5F6D875E8521D`,
head_hash `e3786ac17207f66832260905bbc576fb899ca2e46ddfea95d85aa935a49926da`.
Captures affichées et inspectées, pas de fichier PNG local déclaré. Fermeture
Alt-F4 puis absence vérifiée de 6680/1768/7488/29112/22588/32676/32596/30952.

Défaut observé : les champs reviennent à « Choisir… / 2 / 0… » après la
proposition, car l'inspecteur reconstruit ses signaux à chaque retour de commande.
Correctif codé : le brouillon de répétition appartient au panneau stable de
placement ; il reste intact sur une nouvelle révision du même objet et se
réinitialise au changement de projet/zone/objet. Aucun document canonique ajouté.
Clippy Studio WASM strict PASS (5,71 s) après annotation explicite du type de
l'effet ; formatage/diff PASS. Ce correctif est postérieur au candidat testé :
conservation des champs encore à vérifier dans le prochain binaire.

### Restant après ce parcours

### Réouverture et vue joueur — 14 septembre 2026

Candidat avec correctif de brouillon construit : Trunk PASS (compilation 23,79 s),
Tauri PASS (15,23 s), mêmes fichiers du kit épinglé. Exécutable :
`E:/OpenRPG_Forge/.gate2-evidence/studio-path-draft-kit-20260914/forge-studio-candidate.exe`,
SHA-256 `7F31D8F4FCDDD949FE67F39594A672C6D1E7D557770BA111D9CA3DC6B3D3928F` ;
WASM `E873DBCE738182368C6A70A3CCD5A83A90169DDE85639FF97A95230DBFBC5264`.

Archive 4B564C2F rouverte depuis la racine des tracés existante. Bouton Ouvrir
du projet/zone courant : « Session synchronisée ». Départ temporaire X=0/Z=5,
« Jouer à cet endroit » : personnage et rangée rendus de près, sources gelées.
Retour à l'édition acquis, état synchronisé ; fichier archive SHA-256 inchangé
après le parcours. Aucune nouvelle sauvegarde ni mutation de la rangée pendant
cette session. Fermeture et absence de 31008/2524/33412/33044/1412/10072/6764/3976
vérifiées. Capture affichée et inspectée dans la conversation.

**Défaut révélé, pas de verdict de continuité** : fente verticale laissant voir
le fond entre deux modules au pas de 4 m. LOD0 déclare X [-1,99445 ; 1,99820],
Y [0,00765 ; 3,19604] ; collider déclaré X [-2 ; 2], Y [0 ; 3,2], Z ±0,36.
Ces valeurs viennent des accessors du GLB, pas d'une nouvelle mesure complète.
Le générateur `architecture_wall_geometry` réduit les pierres à 96 % de largeur
et le support de mortier à 98 % de largeur/hauteur pour LOD0/1. Il explique un
raccord visuel ouvert malgré des colliders contigus. Corriger la règle de module
et produire un dérivé gouverné, sans écraser ce kit ni modifier silencieusement
ses placements épinglés. La continuité aux angles reste également à qualifier.

Ce parcours ne vérifie pas le déplacement contre le mur, seulement démarrage et
retour du playtest. Le maintien des champs du brouillon n'a pas été exercé dans
ce nouveau candidat : la compilation seule ne le qualifie pas.

### Restant actualisé

#### Dérivé du module — 14 septembre 2026

`architecture_wall_geometry` porte le support de mortier à la largeur/hauteur
totales du module, au lieu de 98 %. Le relief reste dans la profondeur.
Manifeste séparé : `examples/visual_valley/asset-kits/r2-025-wall-joint/asset-kit.v1.json`.
Aucun changement des GLB historiques ni des placements sauvegardés.

Premier build `E:/OpenRPG_Forge/.gate2-evidence/wall-joint-20260914` échoué :
la prévisualisation imposait six offsets à un manifeste d'un seul asset.
Sorties partielles conservées. Correctif : disposition calculée depuis les
bounds réels pour les autres tailles de kit ; disposition historique six objets
conservée. Nouveau build isolé `wall-joint-20260914-b` PASS, Blender 4.5.10
portable épinglé, sortie du processus normale. GLB SHA-256
`D51C291DAA372E047941DC814494A7B09459EE9C6A7EB49CB687889F66A785F8` ;
rapport `asset-kit-build.v1.json` SHA-256
`e365a7f64f82e6aee6385f42d05b9093c374825bd45e5b7f2013d2b033204c44`.

Rendu `asset-kit-lineup.png` ouvert et inspecté : panneau et repère humain
entiers, mortier visible. Cette vue isolée ne prouve pas un raccord sans trou.
Réimport indépendant, continuité entre panneaux et intégration Studio du dérivé
restent à réaliser ; ni validation humaine ni qualification runtime acquise.
Pas de rebuild Rust ou de campagne performance pour ce correctif Python.

#### Raccord droit réimporté — contrôle ciblé

`scripts/blender/Test-ModularWallJoint.py` réimporte le GLB sans modifier la
source, duplique deux panneaux au pas de 4 m et interroge uniquement leurs
meshes visuels (colliders exclus). Par LOD : 2 562 rayons depuis les deux faces,
bande de ±5 cm autour du joint, pas horizontal 5 mm, hauteurs 0,1 à 3,1 m.

- Dérivé D51C291D : LOD0/1/2, zéro rayon manquant sur 7 686.
- Témoin historique 6D6C0598 : LOD0 678 manquants, LOD1 790, LOD2 zéro ;
  sortie 1 attendue. Le contrôle détecte donc bien le défaut historique.
- Rapports et rendus conservés dans
  `E:/OpenRPG_Forge/.gate2-evidence/wall-joint-probe-20260914` et
  `E:/OpenRPG_Forge/.gate2-evidence/wall-joint-probe-old-20260914`.
- Rendu du dérivé ouvert et inspecté : deux panneaux entiers, joint vertical
  encore visible. Le contrôle démontre la fermeture aux points échantillonnés,
  pas une invisibilité esthétique de la jonction ni une preuve exhaustive.

Deux processus Blender terminés (sorties 0 et 1), aucun serveur ou navigateur
lancé. Réimport et raccord droit échantillonné acquis ; angles, collisions
jouées et remplacement explicite des références dans Studio restent dus.

#### Remplacement explicite de référence — code produit

`PlacementEdit::ReplaceAsset` prépare un lot atomique de 1 à 256 identifiants
uniques, exige l'ancien couple asset/hash attendu et refuse une cible identique,
un objet absent, une référence périmée ou un objet verrouillé. Seuls asset_id et
asset_hash changent : noms, identités, positions, rotations et échelles restent
les corrections manuelles existantes. Commande ordinaire liée à la révision,
aperçu, Apply et historique existants ; aucune écriture directe des archives.

Inspecteur Studio raccordé au catalogue : nouvel asset sélectionné, option
explicite pour inclure les objets partageant exactement l'ancienne référence.
Vérification asynchrone du fichier avant proposition ; changement de sélection,
annulation ou changement de tête invalide la demande en cours. Pas de recalage
silencieux des pivots ni de propagation future automatique des réimports.

Deux tests ciblés PASS : conservation des retouches, absence de mutation durant
la préparation, Undo/Redo et réouverture ; lots invalides/verrouillés refusés
sans modification. Clippy project/tests PASS. Parcours UI et remplacement réel
du mur dérivé non exécutés : le nouveau kit n'est pas encore proposé au catalogue.
Clippy Studio WASM strict PASS (30,02 s). Cargo signale séparément une future
incompatibilité de la dépendance `proc-macro-error2 2.0.1` ; pas un échec de
compilation actuel. Aucun navigateur ni processus graphique lancé pour ce lot.

#### Catalogue additif du mur corrigé

Le navigateur dérive les identités du couple nom du kit/nom de l'asset. Pour
préserver les sauvegardes, `Append-GovernedKitAssets.py` conserve le nom du kit
et toutes ses références et ajoute une nouvelle entrée, révision de catalogue 2.
Les noms/chemins en collision sont refusés ; contrôles SHA des entrées et copies,
sources relues avant écriture, liens refusés, sortie neuve sur E: obligatoire.
Rapport de génération additionnel complet conservé dans `additive_assembly`.

Le manifeste du mur utilise désormais `vv-architecture-wall-joint-a` afin que
les noms des nœuds GLB/LOD/UCX correspondent à sa nouvelle entrée. Build PASS :
`E:/OpenRPG_Forge/.gate2-evidence/wall-joint-named-20260914` ; GLB
`1be108a0113dcad4ff4fcd4d39de7d38e96967b615106eb52a865231efd9b3b9`.
Réimport/rayons ciblés sur ce fichier exact PASS (7 686, zéro traversée), dossier
`wall-joint-named-probe-20260914`. Aucun changement de la règle géométrique.

Catalogue assemblé : `E:/OpenRPG_Forge/.gate2-evidence/studio-additive-wall-kit-20260914`.
Build JSON `40A55F7570003E9A4944BF04118512B722B8CCEC94F3BDAA9D0F75079DD67976`.
Six références antérieures conservées, une ajoutée, aucune instance de scène
remplacée. Contrôle indépendant des six couples noms/pins PASS. Qualification
humaine/publication restent en attente ; fichier catalogue n'est pas preuve UI.

Candidat complet construit : Trunk PASS (compilation Rust 1 min 18 s, puis
assemblage WASM), Tauri PASS (30,07 s). Premier lancement de Trunk refusé avant
compilation par `NO_COLOR=1` hérité ; valeur locale `true` appliquée puis réussite.
Pas de modification globale de l'environnement.
`E:/OpenRPG_Forge/.gate2-evidence/studio-replacement-kit-20260914/forge-studio-candidate.exe`
SHA-256 `C53FDDEAAC6213978138DC55F635695CFC0018B561CB25403D4AB920C56DFFA6`,
WASM `B86F223E1113A56C937EEDF9622946F8A02E428462C424293D1D014E81D3051E`.
Packaging Authoring avec sept références, pins source/copie contrôlés ;
`build-proof.json` porte `runtime_verified=false`, `gate_qualified=false`.
Application non lancée à ce stade ; sauvegarde 4B564C2F non modifiée.

#### Remplacement réel dans Studio — 14 septembre 2026

C53FDDEA a ouvert la carte mais sur l'écran principal. Ajout de `-WindowX` et
`-WindowY` au packager (paire obligatoire, défaut inchangé), tests ciblés de
packaging PASS. Candidat secondaire reconstruit, sans nouvelle compilation WASM :
`E:/OpenRPG_Forge/.gate2-evidence/studio-replacement-secondary-20260914/forge-studio-candidate.exe`,
SHA-256 `182C2A03E7511CADAC48AF698178CBFA06C65EC2E9B07E543FDD7CC40820244C`.
Position demandée -1920/52, capture observée -1913/52. Le contrôle Windows a
présenté des coordonnées UIA périmées après scroll et un décalage des clics ;
reprise depuis captures et clavier. Pas de mutation par script de l'archive.

Parcours acquis : Ouvrir la carte → catalogue (7 références affichées) → choisir
`vv-architecture-wall-joint-a` → inspecter le mur 035b30c3 à (-38,3,0) → inclure
les objets portant le même pin → Prévisualiser (71→71) → Appliquer → Sauvegarder.
Statut final « Session synchronisée », rangée rendue en vue générale.
Commande `89cffb18-8b85-551b-b7bd-610a004780dc`, révision attendue 29.
Archive dans la racine des tracés existante, SHA-256
`F432409EFC0A292F4D371A829620D05F2112332C24E8A0B4AA49134AE8E64CAA`.
Comparaison des snapshots de placements avant/après dans le journal : 71 IDs,
noms, transformations et verrous identiques ; 69 pins remplacés par 1BE108A0,
deux arches conservées. Ce contrôle n'est pas une réouverture produit.

Les processus 33472/31816/17100/10680/18072/3752/31972/26828 sont absents après
fermeture. Le premier candidat 31032 est également terminé. Aucun autre
navigateur fermé. Vue rapprochée des raccords, collisions jouées, Undo/Redo UI
du remplacement et réouverture de cette nouvelle sauvegarde restent dus.

#### Aperçu explicatif et relecture de l'archive

Le parcours affichait seulement « 71 → 71 ». `PlacementChanges::between`
compare maintenant les IDs validés et dénombre séparément ajouts, retraits,
pins, transformations, noms, verrous et objets strictement inchangés. Le résumé
est raccordé à `preview_label` ; les champs peuvent se cumuler pour un objet.
Test ciblé PASS (pin seul, retouches multiples, ajout/retrait, doublon invalide),
Clippy Studio WASM PASS après extraction du formateur (premier échec de limite
de taille de fonction corrigé, sans suppression du lint). Pas de nouveau
candidat graphique pour ce seul texte : affichage réel encore non vérifié.

Relecture Rust de l'archive F432409E acquise. L'ancien utilitaire de contrôle
tentait ensuite un encodage compact à commandes pleines : dépassement 4 Mio
refusé, malgré une source delta valide de 2 617 505 octets. Ajout d'un argument
explicite `compact|delta` à `inspect_zone_archive` (défaut compact conservé),
sans changement de format produit ni relèvement du quota. Exécution `delta` :
20 reçus, réencodage 2 617 505 octets, tête et reçus égaux après réouverture,
`lossless=true files_written=0`, tête
`6e15d7356c5dba1ab7064647da088aeb5b8b347814f83fe2c7a05455b52c1526`.
La relecture du modèle ne remplace pas le parcours de réouverture graphique.

#### Réouverture et raccord droit en playtest

Candidat 182C2A03 réutilisé, aucune compilation : archive F432409E ouverte dans
Studio, état « Session synchronisée », rangée corrigée rendue. Départ temporaire
X=0/Z=5 puis X=0/Z=1, entrées et retours du playtest acquis. Captures de près
affichées et inspectées : panneaux jointifs à cet endroit, mortier visible,
pas de fond visible traversant le raccord droit observé. Ce constat ne qualifie
ni tous les raccords de la rangée ni le raccord d'angle.

Essai bref de marche après clic dans le canvas : pas de déplacement suffisamment
lisible pour établir une collision. Aucun verdict de contact ou de blocage du
personnage ; ne pas substituer l'absence de mouvement à cette preuve. Retour
édition synchronisé. Archive après essai toujours F432409E, aucun Save.
Processus 25296/5604/28816/27140/33092/17148/32928/2604 absents après fermeture.
Restent pour le parcours du mur : contact joué démontré, angles et Undo/Redo UI
du remplacement. La relecture graphique de la sauvegarde est maintenant acquise.

#### Observation du mouvement en playtest

Le statut existant ne donnait que position/tick. `authoring_input::publish`
affiche maintenant entrée active, simulation prête et dernier mouvement
horizontal observé : translation X/Z demandée et résolue, tick et nombre de
contacts (sol inclus explicitement). Observation échantillonnée par le renderer,
pas un historique exhaustif de tous les ticks physiques ni un verdict automatique
de collision. Le dernier mouvement reste lisible après relâchement ; il est
effacé quand le joueur disparaît ou que son tick redémarre. Aucune commande
de mouvement artificielle et aucune écriture des sources ajoutées.
Clippy Studio WASM PASS (5,52 s), après déplacement d'un import pour respecter
le lint existant. Valeurs réelles en interface et contact joué restent à observer.

Candidat regroupant résumé des retouches et observation de mouvement construit :
Trunk PASS (Rust 1 min 16 s), Tauri PASS (29,97 s), écran secondaire configuré.
`E:/OpenRPG_Forge/.gate2-evidence/studio-movement-secondary-20260914/forge-studio-candidate.exe`,
SHA-256 `2300327EC47B73723271BAC9E974EF7B8DEFA2797106262FA43F76538DDCD26C` ;
WASM `BEDFA60C5480B97783742F820AD8F367FD3AA9A6ECCC18E953E240E9AF4B4190`.
Sept références du catalogue additif embarquées, pins contrôlés. `BUILD_ONLY`,
application non lancée ; aucun processus graphique ouvert pour ce lot.

#### Essai borné du candidat et portée visible du remplacement

Le candidat 2300327E a ensuite été lancé sur écran secondaire (PID 27892),
archive F432409E ouverte, départ temporaire X=0/Z=1. Diagnostics effectivement
visibles : simulation prête, entrée active après clic canvas, séquences de
commandes reçues, position (0, 3,91, 1). Aucune translation horizontale observée
après une brève touche `z` via automatisation ; cela ne démontre ni un défaut
du contrôleur ni une collision réussie. Essai arrêté sans répétition prolongée.
Fermeture du candidat et descendants 29392/18176/16268/31108/30340/29252/29892,
absence vérifiée via processus. Aucun Save, archive SHA-256 toujours
F432409EFC0A292F4D371A829620D05F2112332C24E8A0B4AA49134AE8E64CAA.

Développement suivant : `placement_controls/replacement.rs` expose maintenant
avant le clic les références source/cible avec SHA-256, le nombre d'objets
visés et de verrous, et les avertissements cible identique/quota dépassé.
Calcul en lecture seule sur les signaux empruntés, sans clone de toute la zone,
sans vérification de fichier supplémentaire ni écriture. La commande domaine
reste seule autorité de validation, Preview/Apply/Undo ne changent pas.
`cargo fmt --all` et Clippy Studio WASM strict PASS (5,94 s). Pas de nouveau
build Tauri ni de replay graphique pour ce complément ; présentation à qualifier
lors du prochain essai regroupé. Gate 2/R2-025 toujours ouverts.

Le module gouverné est choisi et son fichier disponible ; qualifier pivot/longueur et
réparer/qualifier la continuité en vue rapprochée, puis parcourir les collisions.
Preview/Apply/Undo/Redo/Save/Open et entrée/sortie playtest acquis ci-dessus.
Les copies restent des placements manuels : pas de suivi automatique
d'un tracé modifié, d'effacement implicite après régénération, de raccord d'angle,
d'étirement des panneaux ou de résolution des chevauchements. Une rangée de
modules ne prouve pas encore un mur continu ni un bâtiment paramétrique R2-032.

La règle est réutilisable sur d'autres cartes ; les retouches sont durables et
indépendantes après application. Pas de retouche de fixture particulière pour
obtenir une image. Gate 2 ouverte ; CI distante **NOT RUN — infrastructure billing**.

#### Kit d'angle procédural — qualification locale Blender (21 septembre 2026)

Le kit additif `visual-valley-r2-025-wall-corner`, manifeste
`examples/visual_valley/asset-kits/r2-025-wall-corner/asset-kit.v1.json`, ajoute
le seul asset `vv-architecture-wall-corner-a`. Il est procédural original,
interne à la preuve de gate, sans contenu Cadle ni contenu issu d'un jeu
commercial ; la revue humaine et toute publication restent explicitement en
attente. Son pivot est le coin extérieur au sol, avec deux bras de 4 m sur
`+X` et `+Z`, pour une hauteur de 3,2 m.

La construction primaire v4 et sa reconstruction isolée v4 ont été réalisées
avec Blender **5.2.1 LTS** depuis le même manifeste SHA-256
`149d2c2d52f47bd98e6b24cd4ebc5598d10843aa5f49d8813dd8e601ffdac706` et le
script `Build-VisualValleyAssetKit.py` SHA-256
`97629966a1092d1fc897b0ad3b755b35f1cdda1ce3c4574e14f0bc9e201bfe2d`.
Le GLB produit est byte-identique entre les deux sorties :
`B636B486B0544E1ADD619C7F5D4BD3085B1A8BC496704295FC6DD5DB0023FCDA`
(2 525 452 octets). Le fichier Blend n'est pas byte-identique et ne fait pas
partie de la condition de reproductibilité déclarée. L'export contient LOD0
15 012 triangles, LOD1 2 772, LOD2 24, et deux colliders UCX
(`..._ArmX`, `..._ArmY`).

La sonde Blender v5 a réimporté le GLB exact et le mur droit joint
`1BE108A0113DCAD4FF4FCD4D39DE7D38E96967B615106EB52A865231EFD9B3B9`.
Pour chacun des trois LOD, ses quatre séries de jointures donnent 1 281 rayons
et zéro miss chacune ; les faces de coin intérieure et extérieure donnent
chacune zéro miss sur 61 rayons. Le témoin volontairement décalé de 2 cm échoue
comme attendu, à raison de 61/61 misses pour chaque LOD. Cette preuve qualifie
un réimport Blender et un échantillonnage des joints visuels ; elle ne qualifie
ni Studio, ni Rapier, ni collision jouée, ni revue humaine.

L'assemblage additif `studio-additive-wall-corner-kit-20260921` porte le
catalogue à la révision 3 et à huit assets. Les sept assets historiques sont
byte-identiques à la révision 2 ; seul l'angle est ajouté, avec le hash GLB
ci-dessus. Le catalogue assemblé est identifié par
`9DC5DA1A9D33AACD45E4EEE7743F767D242EDC817E7C909949B51244770076A1`.

Un candidat Studio EXE/WASM est préparé pour la suite de qualification, mais
ce dossier de preuve n'enregistre pas encore de parcours Studio accepté pour
l'angle. Studio/Computer Use est donc **NON QUALIFIÉ** dans cet environnement
hôte : l'accès direct Sky retourne `Trusted RPC service is not configured: sky`
et le wrapper MCP ne recense aucune application alors qu'une fenêtre native
est présente. Ce constat de service hôte ne constitue pas un défaut produit et
ne peut pas être remplacé par la preuve Blender.

R2-025 et Gate 2 restent ouverts. Revue humaine non acquise. CI distante :
**NOT RUN — infrastructure billing**. Aucun commit, push ou publication n'est
impliqué par cette preuve.

#### Résultat frontend navigateur Luna max et consolidation visuelle (21 septembre 2026)

Le parcours Luna max a atteint le frontend navigateur en mode autonome. Le
backend Tauri est absent : ce résultat est donc une **preuve frontend seulement**.
Le catalogue affiche huit références ; `vv-architecture-wall-corner-a` est
présent avec le GLB de 2 525 452 octets, SHA-256
`B636B486B0544E1ADD619C7F5D4BD3085B1A8BC496704295FC6DD5DB0023FCDA`, et son
précontrôle de fichier passe. L'aperçu demeure bloqué tant qu'aucune zone n'est
ouverte. Le serveur local `127.0.0.1:52611` a été arrêté après l'essai.

Les rendus existants ont été copiés, sans suppression ni modification des
sources, vers
`artifacts/forge-runtime/gate2-resume-20260919/r2-025-validation-20260921/visual-evidence/` :

- `turntable-000.png` — SHA-256
  `b6fc5cb329c039ae3cfe73923aadb4309f121997f07ee3b9b759b7bbc5c35d05`
  (768×768).
- `turntable-045.png` — SHA-256
  `5cf6173258dcc21b617961fca5906d3b3e17c28cb3bc60add016a7298a7d1e81`
  (768×768).
- `turntable-090.png` — SHA-256
  `c4ba766202f17ee14c118de32f55ad579183cdb00c5073f9128608a3e3ccdd1b`
  (768×768).
- `corner-lod0.png` — SHA-256
  `45ea902879776a890f1f0039599987630597b983f4d7040cedac907bfeef8a6f`
  (1200×800), composition mur-angle LOD0 issue de la sonde Blender.

`visual-evidence.json` relie chaque copie à son chemin source, son type de
preuve, son empreinte et ses limites. Ces images complètent la qualification
Blender ; elles ne qualifient pas le rendu natif, Rapier, une collision jouée ou
la revue humaine. R2-025 et Gate 2 restent ouverts.

#### Correction du kit d'angle et preuve v2 (21 septembre 2026)

La preuve Blender v4 et son GLB
`B636B486B0544E1ADD619C7F5D4BD3085B1A8BC496704295FC6DD5DB0023FCDA` sont
**REJETÉS** pour R2-025 : l'inspection a relevé un parement généré sur les faces
intérieures et un lissage extérieur insuffisant. Cette observation invalide sa
qualification artistique et les captures v4 ne doivent plus être utilisées pour
prouver le coin corrigé.

Le générateur a été corrigé pour borner le relief aux parements extérieurs et
les tests de relief ont été ajoutés à la sonde. Blender 5.2.1 a produit le GLB
primaire v5 puis sa reconstruction isolée, byte-identiques :
`A1F7CF7BDEC79540DFD37210B61DE99B46DCBF5F8FE1E5C9034412F799BB499C`,
2 525 444 octets. Il contient LOD0/LOD1/LOD2 à 15 012 / 2 772 / 24 triangles,
ainsi que deux colliders UCX (24 triangles). La sonde v6 confirme, sur chaque
LOD, quatre jointures à 1 281 rayons sans miss, les faces intérieure et
extérieure à 61 rayons sans miss, le témoin de 2 cm à 61/61 misses et le relief
extérieur sur LOD0/LOD1 (107/117 et 115/117 ; 78/78 et 72/78 échantillons
saillants, au-dessus des seuils 82 et 55). LOD2 est volontairement un fallback
silhouette sans assertion de relief.

Le kit additif corrigé est à la révision 4. Son attestation confirme que les
sept pins historiques, le catalogue, l'aperçu et les placements sont préservés ;
seul le hash de l'angle est remplacé. Le candidat Authoring v2 porte l'EXE
`C1BD2A6AF4BA825D43A8502B77BEA89BD4DBE4A5BFEA9080AC9815C65336B025` et huit
références. Luna max a confirmé dans le navigateur le catalogue à huit références,
le hash A1F7, le précontrôle GLB et la taille du fichier. Cette observation reste
frontend seulement : le backend Tauri est absent. L'essai natif reste bloqué par
le service hôte : Sky retourne `Trusted RPC service is not configured: sky` et
le wrapper MCP ne répertorie aucune application alors qu'une fenêtre est active.

Les rendus corrigés sont conservés sous
`artifacts/forge-runtime/gate2-resume-20260919/r2-025-validation-20260921-v2/visual-evidence/` ;
`visual-evidence.json` enregistre leurs sources, empreintes, dimensions et limites.
Ces fichiers démontrent le rendu Blender corrigé, pas Tauri natif, Rapier, une
collision jouée, une revue humaine, ni la CI distante. R2-025 et Gate 2 restent
ouverts ; revue humaine non acquise ; CI distante **NOT RUN — infrastructure billing**.

#### Arrêt factuel de la tentative d'angle v7h/v7i (21 septembre 2026)

La revue propriétaire a ensuite **rejeté A1F7/v2** : la composition fournie
présente de grands défauts de parement et ne vaut pas approbation artistique,
quelle que soit l'acceptation antérieure du turntable isolé. A1F7/v2 ne doit
plus être cité comme preuve visuelle de R2-025.

La dernière tentative technique est conservée, sans promotion, assemblage ni
candidat Studio, sous les [preuves v7h](../../../artifacts/forge-runtime/gate2-resume-20260919/r2-025-composition-v7h/) :
`failure-summary.v1.json`, `validation-report.json`,
`probe-v7i/report.json` et `probe-v7i/visual-review.v1.json`. Le GLB
`C858DAFE613C281CF06A9E6DB749ABE85BB6C0F354651D0A31D1D4DA1E91D6B0`
(2 601 128 octets) est byte-identique primaire/rebuild, sous les budgets
LOD0/1/2 (14 736 / 2 940 / 24), avec validation PBR et colliders PASS
(2 objets, 24 triangles, 16 positions uniques après réimport).

Cette preuve technique ne qualifie pas le coin : la sonde v7i échoue le
contrat de projection extérieur à LOD0/1 (écart maximal ~0,038 / ~0,040 m,
tolérance 0,001 m), ainsi que le parement intérieur (~0,747 / ~0,746 m et
101 / 72 normales visibles incohérentes). La revue stricte Luna échoue aussi :
bandes/panneaux lisses apparents et style non continu avec le mur droit. Les
joints sans miss, PBR, budgets et colliders ne compensent pas ces échecs.

R2-025 demeure ouvert et bloque la qualification artistique du mur ; aucun
passage de Gate 2 n'est revendiqué. Le correctif artistique est repris dans
R2-026, sans transformer ce déplacement de travail en clôture de R2-025.

#### Candidat Astra final — qualification locale, revue visuelle propriétaire approuvée

Les variantes B636, A1F7/v2 et v7h restent **rejetées** pour R2-025. Le nouveau
candidat construit par Astra, sans promotion, append de kit ni candidat Studio,
est conservé sous les [preuves Astra](../../../artifacts/forge-runtime/gate2-resume-20260919/r2-025-composition-astra-v1/).
Son GLB `73A4637E3B2E334F9DB86323D0D8B38C6F4C56E475A798D12E1060CBC75D0F79`
(2 924 036 octets) est identique entre `final-primary` et `final-rebuild`, avec
19 456 / 4 744 / 50 triangles aux LOD0/1/2. Les sondes finales et la QA Luna
PASS incluent les vues extérieure et intérieure.

Les corrections attestées portent sur le winding de la source, le déterminant
du bras miroir et la superposition de tous les LOD dans l'ancienne composition.
Luna n'a réalisé que la QA indépendante (`QA-Luna`) ; Astra a construit le
candidat. Instruction propriétaire du 22 septembre 2026 : **« je valide le
visuel »** pour ce GLB exact ; la revue visuelle propriétaire est donc
**APPROUVÉE**. Cette approbation ne couvre ni publication, ni kit intégré,
ni Studio, Rapier ou collision jouée, qui restent à tester. R2-025 et Gate 2
restent ouverts.

Le kit révision 5 est assemblé localement sous
`artifacts/forge-runtime/gate2-resume-20260919/r2-025-kit-rev5-astra-approved-20260922/` :
sept records d'assets sont préservés, seul `vv-architecture-wall-corner-a` est
remplacé par ce SHA et les placements d'aperçu restent inchangés. Son attestation
au niveau assemblage/runtime conserve `human_art_review: pending` et
`publication_approved: false` ; elle ne transforme donc pas cette approbation
visuelle ciblée en qualification d'intégration ou de publication.

Le candidat Studio build-only est créé sous
`artifacts/forge-runtime/gate2-resume-20260919/r2-025-validation-20260922-astra-approved/`.
`Test-StudioCandidateKit` passe avant build ; les 15 entrées sont `INPUTS_VALID`
et le build est `BUILD_ONLY PASS`. L'EXE est
`D7E7CECCE015CBFA4B9E231C86351FDBC24BB14E5D80CCBAEF130C60D37E24ED` ; le
WASM est `A09DE1B4A68E35FBC90E60EF7ECA17ED19F4B17C2BC05674A5ACDF0336BBFBFB`.
Les huit assets/pins embarquent le corner
`73A4637E3B2E334F9DB86323D0D8B38C6F4C56E475A798D12E1060CBC75D0F79` ; les
sept autres, le preview, le catalogue et les placements sont préservés.
`runtime_verified: false` et `gate_qualified: false` : aucun lancement Computer
Use/Studio, Rapier ou collision jouée n'a eu lieu. La revue visuelle propriétaire
reste approuvée, mais R2-025 reste ouvert.
