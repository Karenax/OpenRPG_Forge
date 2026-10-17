# Gate 4 — avancement des 15 et 16 octobre 2026

Statut courant au 16 octobre : **ouverte, qualification produit à terminer**.
Le candidat `0.1.0-preview.20261016.014` est construit sur Source008,
empreinte `e3f080053b746f84eb6dd6d9199338bfb1e80b385376d9aab79563d1c36ddf4c`.
Native (2 333 PASS, 56 ignorés), WASM, Release et supply passent ; le transport
ajoute son contrôle séparé de 86 PASS et deux ignorés. Les limites de distribution
documentées, dont GLib, restent inchangées.

Le correctif du menu Jouer ouvre maintenant les modules installés via les API
publiques du transport, avec sélection module/carte et admission par identifiants.
Les 12 tests launcher, Clippy strict et le témoin navigateur avec IPC simulé passent.
Source009 est gelée à `ca270f722a1e76f9782a0d72834bfca64dbfc4e51a1babb151eb89fad91e3752` :
exactement six fichiers launcher et son témoin UI changent depuis Source008.
Les quatre chaînes Source009 passent : native (2 333 PASS, 56 ignorés), WASM,
Release et supply. Aucun assemblage015 n'a été lancé : le diagnostic des commandes
du jeu impose un nouveau candidat016 sur Source010, après correction et tests.
La qualification du vrai launcher installé reste à terminer.

L'installation CLI, la mise à jour/rollback, le Setup NSIS silencieux en destination
Unicode, la réinstallation/désinstallation et la conservation utilisateur passent.
Les vrais `.orpmod` corrompus/tronqués et dépendances absentes/incompatibles sont
refusés. Une interruption observée après 5 fichiers du staging puis le rejeu du
même plan et la vérification complète passent également. Les contenus détachés014
sont préparés séparément ; le Setup canonique Source013 n'est pas promu.

Le propriétaire a réautorisé les parcours Studio/jeu le 16 octobre. Les imports
GLB, texture et audio, les vignettes, la variante gouvernée, son binding gameplay,
Annuler/Rétablir, le réimport puis son annulation et la réouverture ont maintenant
été exercés dans Studio014. Les deux exports UI des Trois Cairns sont identiques
octet pour octet ; l'export du Relais après retouche du dialogue Solaren conserve
les autres données et l'origine. Le refus public dû aux droits audio incomplets et le contrôle positif
original CC0 sont observés dans Studio, puis inspectés nativement. Les preuves
datées se trouvent sous `.gate4-evidence/20261016/ui-authoring-001` ; elles ne
constituent pas encore les parties finales sur ces mêmes exports installés.

Les deux vrais exports sont maintenant installés et vérifiés dans l'installation014
ordinaire, passée en révision2, avec 91 fichiers utilisateur conservés à l'octet.
Le parcours UI a révélé un défaut produit : le menu Jouer du launcher Aube014 ne
présente que Tidewater. Le raccord des modules installés et de leur lancement est
en cours dans cette interface existante ; il faudra qualifier ce nouveau launcher
et reconditionner le candidat final avant promotion.

Restent l'installation isolée et les parties/reprises sur ces packages, avec la référence
sans accès source/Vault/IA. Studio auteur est qualifié séparément sur DATA ; son
confinement n'est pas un critère ajouté. La première admission004 a créé le profil
et reçu les sept inputs, puis a refusé son témoin hôte avant installation : erreur
.NET sur un chemin long et collecte des processus court-circuitée par une requête
d'image après sortie. Ces reçus restent des échecs du dispositif de qualification,
sans résultat de jeu ni PASS d'isolation. La correction des auxiliaires est en
cours. Depuis, la reprise stricte avec deux nouveaux témoins hôte/confiné passe,
puis installation, idempotence, vérification et témoins postérieurs passent.
La session Cairns002 a réellement chargé l'export final orange, répondu à G/F5
et quitté normalement : trois processus sur trois attribués au même SID zéro
capacité, tous codes0 et handles signalés. Les rapports confirment les empreintes
attendues et le binding orange avec scène prête, parent gameplay et collider.
La quête reste disponible et l'inventaire vide : ce parcours ne prouve pas la
boucle RPG. Le journal conserve un refus Windows de capture du curseur ; le
propriétaire confirme aussi à la main que le personnage reste immobile. Le défaut
est identifié : le focus clavier dépendait du verrouillage natif du curseur,
que Bevy annule après le refus Windows. La correction conserve l'intention du
clic indépendamment de ce verrouillage et annule les actions en attente sur
Échap, panneau ouvert ou perte de focus. Quatre régressions CPU passent, dont
les translations désirée et effective du contrôleur Rapier ; Échap et la perte
de focus parcourent l'ordre Main réel avec un tick physique avancé. Le test
du panneau exerce la transition commune, sans instancier son UI réelle.
Format et Clippy strict passent ; les anciens essais restent conservés.
Le reçu est `module-control-intent-001/RESULT.json`, SHA-256
`a3ba9ded9a343216e5dd6b2d74d3bd0ac33d68765e4da43564524aace97b85a0`.
La qualification complète Source010 démarre sur l'empreinte
`ae4232832cc6d9fcca72ed5a63ba0ed4074835e19363da8447b692a6148eda2a` :
seuls `module_game/scene.rs` et son nouveau fichier de tests changent depuis009.
Ces contrôles CPU ne prouvent pas encore le déplacement dans le client final.
Les sessions002 et003 sont fermées normalement et leurs familles complètes
comptent chacune trois membres, tous codes0. La collecte `pre-fix-001` conserve
les preuves et données ; le profil004 a ensuite été supprimé avec vérification
du namespace absent. Ces faits ne transforment pas les parties bloquées en PASS.
Le nouveau contrôleur005 est préparé, sans profil acquis ni pins016 inventés.
Ni Windows vierge ni une release ne sont qualifiés. Les sections suivantes
conservent les tranches et preuves historiques.

Le propriétaire demande « ok lancons le travail gate 4 », après la clôture Gate 3.
Les modifications sont réalisées dans le checkout Rust commun, à partir de
`3e0044d5f63b26c678d9db19380a66db832d9de3`, avec les travaux préexistants conservés.
Le [plan](../../planning/GATE4_IMPLEMENTATION_PLAN.md) et
[ADR-0024](../../ADR/ADR-0024-orpmod-v2-semantic-admission.md) définissent cette tranche.

## Réalisé dans les sources

- Catalogue commun Mesh/Texture/Audio, stable par `AssetId` et épinglé par hash/taille.
  Provenance, droits locaux/publication, filiation acyclique, source durable ou
  recette pour les retouches, vignette optionnelle et budgets sont validés dans
  `forge_assets::governed_import`.
- Manifeste `forge.module-package` v2 dans `forge_project::module_package` :
  projet/module/zones, compatibilité exacte, archives canoniques et gameplay,
  blobs de terrain, assets, dépendances, licences, preuves et crédits.
- Adapter `forge_delivery::orpmod` : lecture confinée, inventaire fermé, hashes
  par flux, JSON bornés vérifiés sur le tampon interprété, préflight média à
  travers le lecteur de hash, archive gardée ouverte pendant l'inspection.
- Commandes réelles `orpmod-validate`, `orpmod-pack`, `orpmod-inspect`, avec
  refus de publication privée. L'assemblage revalide le staging avant publication.
- Admission sémantique à la vérification du staging d'installation, avant la
  tête active. Les `.forgepack` historiques gardent leur contrat v1 ; un simple
  renommage en `.orpmod` est refusé. Les verrous Cargo du transport et du hub
  sont actualisés pour les seules nouvelles dépendances de domaine.

Les chemins internes des nouveaux contrats sont ASCII afin de refuser les alias
Unicode Windows. Destinations d'installation et métadonnées gardent Unicode.
Recettes, sources authorées et vignettes sont des auxiliaires épinglés ; ils ne
sont pas artificiellement transformés en entrées de médias.

Les références aux espèces et prototypes de végétation importés, copies de
plantes et packages annexes physiques/particules sont refusées par cette première
version tant que leurs catalogues et ressources ne sont pas résolus. Ce sous-ensemble
ne constitue pas encore l'export complet de Tidewater.

## Validation

**66 tests locaux passent** : 19 pour les imports gouvernés, 19 pour le manifeste
de module et 28 pour la livraison (14 nouveaux tests et 14 régressions existantes).
Les commandes CLI réelles, les dossiers d'installation Unicode, le retry, le
rollback, les fichiers personnels et le refus avant activation sont exercés.
Les fichiers personnels de ces tests sont des fixtures ; ils ne constituent pas
une nouvelle acquisition de partie RPG dans le produit installé.

Format et Clippy sans warnings passent sur les deux crates de domaine et le
transport. `cargo check --all-targets --locked --offline` passe pour `forge_hub`.
Ce dernier contrôle est une compilation, sans nouvelle observation de son UI.

Les 2 636 entrées sont conservées dans
[source-before.json](../../../../.gate4-evidence/20261015/foundation-001/source-before.json)
et [source-after.json](../../../../.gate4-evidence/20261015/foundation-001/source-after.json).
L'empreinte des tests est
`936bb278bfa27087a208699d5deaccd72f1912353f14b431cf21ea47098641da`.
L'empreinte finale est
`63fed6683e0480a57df8745ef5c4c679f90946414be3227938ca1be198d4123f` :
seul `tools/forge_delivery/README.md` a ensuite corrigé sa description historique
de la boucle RPG. Aucun code, shader, contrat, verrou ou contenu de test n'a changé
pendant ces contrôles. Le [résultat consolidé](../../../../.gate4-evidence/20261015/foundation-001/result.json)
contient les hashes, l'inventaire du lot et les contrôles finaux.

Les tests rouges initiaux ont été exécutés avant l'implémentation des contrats :
14 échecs comportementaux sur les assets, 9 sur le module. Les régressions ajoutées
par revue ont ensuite reproduit les alias Unicode, l'activation sans admission
sémantique, le refus injustifié de recette et les références annexes non résolues.
Les journaux rouges restent conservés, y compris les échecs corrigés ; aucun
échec initial n'est relabellisé PASS.

## Qualification produit encore ouverte

Les tests de deux modules utilisent des fixtures de domaine et de transport.
Ils ne prouvent pas deux aventures jouables. Le protocole
[GATE4_PACKAGE_ACCEPTANCE](../../testing/GATE4_PACKAGE_ACCEPTANCE.md) reste à exécuter
pour les seize critères, notamment :

- import et inspection GLB/texture/audio depuis Studio, avec vrais décodeurs ;
- chargement runtime, choix du module et livraison complète de Tidewater ;
- variante de visuel effectivement lié au gameplay et réimport des retouches ;
- second module jouable dans le même client et données Solaren sélectionnées ;
- installation et boucle RPG en machine isolée sans source, Vault ou IA.

Les signatures PNG/JPEG/WAV/Ogg/FLAC et le préflight GLB ne sont pas des décodeurs
complets ni une acceptation artistique. La présence de preuves épinglées ne vaut
pas établissement juridique des droits. Les packs existants restent privés.

Le Setup013 et les packs Gate 3 ne sont pas remplacés par cette tranche source.
L'interface du launcher et le lanceur propriétaire ne sont pas modifiés.
GitHub reste informatif selon la consigne propriétaire, sans attente de facturation.
Aucun commit/push, verdict Windows vierge ou qualification release n'est effectué.

## Poursuite de la réalisation — lot `implementation-002`

Le travail continue vers les seize critères. Les résultats ci-dessous ne
remplacent pas la qualification finale sur les applications recompilées.

- Le constructeur natif de modules exporte une archive de zone canonique, les
  bindings de visuels, le catalogue et les fichiers gouvernés. Ses cinq tests
  passent, dont le refus avant écriture des sources non prises en charge,
  des médias invalides et des droits de publication insuffisants.
- Les trois contrôles du lecteur de médias du transport, les trois contrôles
  d'arguments du jeu générique et les quatre contrôles de chargement de modules
  passent. Deux modules indépendants sont chargés après indisponibilité de
  leur dossier auteur, avec refus des caches altérés.
- Les quatre contrôles de sélection Solaren passent, dont la relecture des
  véritables octets historiques épinglés au commit
  `a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`. La sélection ne reprend que
  deux textes et une transition sans effet, conserve les droits d'origine
  et exclut scripts, audio et actions historiques non converties.
- Les huit contrôles de snapshot d'export Studio passent, y compris les pins
  historiques, la fermeture de filiation et les sources/licences modifiées.
  Les suites d'import, variante et compilation des interfaces restent en
  cours ; aucun parcours Studio n'est déduit de ces tests de composants.

Les journaux de ces contrôles sont
`implementation-002/delivery-combined-green-002.log` et
`implementation-002/studio-import-green-003.log`, sous l'arbre local
`.gate4-evidence/20261015`. Le second journal contient aussi un échec de fixture
d'import, corrigé ensuite mais à rejouer. Les premiers essais d'export échouaient
dès `Prepare` dans la publication de fichiers ; ils ne constituent pas une
preuve rouge de l'implémentation d'export elle-même. Les essais conservent ces
échecs et leurs conditions : dossier temporaire du sandbox, puis dossier de
test explicitement situé dans l'arbre de preuves. La publication immuable
utilise maintenant un stage synchronisé puis un renommage sous verrou, avec
refus des destinations et commandes existantes différentes.

### Acquisition isolée préparatoire sur Source013

L'installation CLI, la vérification des fichiers, les plans de lancement et
deux lancements UI ont été exécutés avec un SID AppContainer dédié sans capacité
réseau. Les sondes ont effectivement refusé la source, le Vault et le réseau.
Le parcours UI a accepté une quête, sauvegardé puis repris dans un second
processus. Le replay natif a ensuite accompli combat, collecte, retour,
quête terminée et reprise exacte dans un nouveau processus. Il emploie les
commandes et le contrôleur du produit, sans téléportation ni injection OS.

Le résultat consolidé préparatoire est
[`SOURCE013_REPLAY_RESULT.md`](../../../../.gate4-evidence/20261015/isolation-prototype-001/SOURCE013_REPLAY_RESULT.md),
SHA-256 `52ea89b008031d32c7adb7cb9d8a8f3f83561f011d89e055791e3cf0937e618e`.
Le résultat causal du replay est
[`NATIVE_REPLAY_A_RESULT.json`](../../../../.gate4-evidence/20261015/isolation-prototype-001/NATIVE_REPLAY_A_RESULT.json),
SHA-256 `05ca0f1a2d2c582e76f6ba699ca13c676d99dede0d6a3ab02105610765517592`.
Les processus, profils, permissions propres aux essais et alias de lecteur
ont été nettoyés. Le Setup classique n'a pas pu démarrer dans cet AppContainer ;
seule l'installation CLI confinée y est prouvée.

Cette acquisition porte sur Source013 de Gate 3. Elle prépare le protocole,
mais **ne qualifie pas R4-P01 ni la révision Gate 4 en construction**. Le jeu
générique, la variante liée au gameplay, les parcours Studio et le nouvel
installateur restent à acquérir sur les mêmes binaires finaux.

### Contrôles complémentaires de réalisation

Les suites regroupées `assets-combined-green.log` (41 tests),
`project-combined-green.log` (37) et `delivery-all-green.log` (54) passent.
Les cinq cas supplémentaires de formats d'accessors GLB passent dans
`glb-usage-green.log`. Ces contrôles ont notamment exercé des fichiers qui
provoquaient auparavant des paniques de décodeurs ; les refus interviennent
maintenant avant ces appels. Les tests ne qualifient pas le rendu artistique.

Les 19 tests natifs du shell Studio passent dans `studio-native-tests.log`.
Les 27 tests import/export/variante/pins passent après les derniers changements
de protocole dans `studio-import-green-006.log`. Le journal `-005` est un refus
de commande Cargo : un nom de cible erroné, sans exécution de tests. Les premiers
contrôles Clippy ont relevé des corrections de style encore en cours de reprise ;
ils ne sont pas présentés comme une validation globale réussie.

Le client debug de SHA-256
`407ee734493cbcb114ae28b6767f4b7ef408f74c9a50138cf694fe1481dd213d`
a exécuté les deux archives factory001, avec des identités module/carte distinctes.
Les deux processus sont sortis normalement avec le code 0. Leurs rapports
`native-module-probe-001/module-runtime-report.json` et
`native-module-probe-002/module-runtime-report.json` observent trois visuels
gouvernés prêts, deux placements dont les primitives sont enregistrées dans
Rapier, et 47 pipelines sans attente ni erreur au moment de la capture.
Les PNG ont été effectivement produits, puis inspectés. Cette observation a
révélé le panneau de diagnostic recouvrant le HUD : sa présentation est corrigée
dans la scène générique, à reconstruire. Les données factory001 ont également
été revues pour donner aux deux aventures des approches et textes propres.
Ces captures initiales restent des observations debug, sans parcours de quête,
révision finale ou acceptation humaine déduits de la seule présence des modèles.

Le Studio Source013 a aussi ouvert et affiché Tidewater dans un AppContainer
sans capacité réseau. Sa fermeture normale sans enregistrement a conservé les
89 fichiers métier du workspace. Les neuf processus observés partageaient le
SID confiné. Les ressources de l'essai ont été retirées et leur absence vérifiée,
y compris par un scan ACL avec chemins longs. La preuve est
[`STUDIO_FEASIBILITY_RESULT.json`](../../../../.gate4-evidence/20261015/isolation-prototype-001/STUDIO_FEASIBILITY_RESULT.json),
SHA-256 `cc9518421639743124f7f8edd72a097af30de216ae05b5cfd9747811a2ad4fee`.
Ce résultat démontre la faisabilité du protocole Studio isolé, sans retouche ou
export Gate 4 acquis sur ce binaire historique.

En revanche, l'essai confiné des deux nouveaux modules avec le client debug
ci-dessus a réellement échoué avant le rendu : code 1, « Accès refusé », aucun
rapport de projection ni PNG. Les sondes d'interdiction source/Vault/réseau
passent avant et après ; aucun processus n'a nécessité de terminaison forcée.
Le diagnostic brut n'identifie pas l'appel fautif : la canonicalisation de
l'alias vers les ancêtres physiques reste une hypothèse à mesurer avec des
erreurs contextualisées. La preuve figée est
[`LOWBOX_DEBUG_RESULT.json`](../../../../.gate4-evidence/20261015/isolation-prototype-001/module-lowbox-debug-001/LOWBOX_DEBUG_RESULT.json),
SHA-256 `e4ede012d469e7eb902e7b847f2f64ef299f3c32994c7bf0bfb1746e36236fd1`.
Ce refus doit être résolu ou le protocole d'isolement adapté avec preuve équivalente
avant de conclure sur les nouveaux modules. Aucun élargissement d'accès aux
ancêtres, à la source ou au Vault n'est effectué pour masquer l'échec.

### Reprise éditable et conservation de l'origine

Les cinq tests `workspace-module-origin-green-001.log` et les douze tests
`module-workspace-bootstrap-green-002.log` passent. L'origine du module reçu
est épinglée dans le document canonique à sa création ; supprimer son reçu ou
son archive ne transforme donc pas le module importé en création sans origine.
Les contrôles couvrent réouverture, Undo/Redo, archive originale sur le même
buffer, manifeste brut, droits privés malgré des médias CC0, filiation, médias
non placés et refus du profil non supporté avant staging. Le bootstrap est un
outil natif de préparation passant par les commandes partagées, pas une preuve
d'import `.orpmod` par interface Studio.

La suite livraison complète et son Clippy strict passent dans
`delivery-all-green-003.log` et `delivery-clippy-002.log`, dont les dix tests du
builder d'export et son origine. La nouvelle factory002 conserve deux aventures
aux identités propres, avec approches spatiales et textes distincts ; le Relais
inclut uniquement les deux textes historiques sélectionnés et leur transition.
Les nouveaux parcours UI, quêtes et réouvertures restent à observer.

### Localisation du refus en environnement isolé

Le diagnostic CLI contextualisé `module-check-lowbox-001/MODULE_CHECK_RESULT.json`
localise le refus à l'inspection d'un ancêtre physique de l'arbre de preuves sur E,
après canonicalisation. Un second essai place physiquement les mêmes octets dans
le stockage privé Windows du nouveau profil AppContainer. Sa sonde peut y lire
le témoin, tandis que l'admission produit refuse les métadonnées du parent du
dossier AC créé par Windows. La preuve est
`private-storage-cli-002/result.json`, SHA-256
`d57548833415efa13fbd0c74117e42ed487ac081ec1b4603767783ed70c0c98c`.
Les deux commandes sortent avec le code 1 ; les sondes avant/après confirment le
refus source/Vault/réseau, sans capacité accordée ni arrêt forcé. Le profil et son
namespace sont supprimés. L'essai précédent `private-storage-cli-001` s'arrêtait
sur le délai du seul témoin hôte avant tout lancement lowbox ; il reste conservé
comme tel. Aucun de ces refus n'est transformé en PASS runtime ou Gate 4.

### Boucles RPG des deux aventures, binaire de développement

Les replays `native-module-replay-plans-004` ont terminé les deux aventures
factory002 avec le même jeu debug, SHA-256
`f8b475d75aa658651c5e9ddd18e922a3455734e3d6d38760b4efd5e44bbbe202`.
Les quatre processus sont sortis normalement : Cairns A/B, PID 28852/32352 ;
Relais A/B, PID 29168/10496. Deux attaques de vingt dégâts ont des événements
`contact=true`, suivies de la collecte, de la quête terminée et de la récompense.
Chaque second processus a constaté `boot.resumed=true`, sans refus, avec un état
initial strictement identique à celui de la sauvegarde engagée et des octets de
sauvegarde conservés. Le Relais a parcouru les deux nœuds Solaren sélectionnés.
Les captures de fin et de reprise ont été ouvertes et vues ; le HUD est lisible.

Les audits sont `trois-cairns-audit.json`, SHA-256
`90d37eb7d05b08b0c397443e9f645abacb2c3f38d9bd677bf4118faaaea32778`,
et `relais-des-lanternes-audit.json`, SHA-256
`c4b3245ea0e0df2ff6b78cf902cf20392ac3b4a360f7442d7f30d6344cec8824`.
Les candidats précédents restent conservés : cible de déplacement à l'intérieur
de la séparation physique, puis attaque sans contact confirmé. Le quatrième
parcours aligne et pousse réellement le contrôleur avant les mêmes attaques ;
aucune position ni condition de combat du produit n'a été modifiée pour réussir.
Ces preuves restent locales, sur un binaire debug non confiné, avant les parcours
Studio et la qualification de la livraison finale.

La couverture des seize critères et les preuves encore manquantes sont détaillées
dans `.gate4-evidence/20261015/implementation-002/GATE4_CRITERIA_COVERAGE_20261015_001.md`,
SHA-256 `cdbd5ca6bec4f87491976fc9626419168be0f51060054bc97bd632bf01007cd4`.

### Qualification commune 006 et diagnostic Windows

L'empreinte des entrées compilées de `qualification-006` est
`4abffbf4732ea181b90b52943e34b701ce46f7dbfa4641f03c412aa9c2eedc6f`.
Format, diff et Clippy strict passent, avec sources inchangées. La suite native
complète s'arrête toutefois sur
`physical_regression_capsule_resync_cannot_reuse_previous_contact_for_push`
(`gameplay_physics.rs:1161`, objet encore en mouvement). Les assertions de refus
du Push sans contact confirmé précédentes ont passé. Cet échec reste un échec
global à diagnostiquer ; aucun PASS natif global n'est retenu. WASM et les builds
Release poursuivent leur acquisition distincte.

Le prototype hors produit `NtCreateFile` avec `OBJ_DONT_REPARSE` a passé douze
témoins hôte : fichiers ordinaires reçus, Unicode et identité canonique ; quatre
refus explicites `0xc000050b` couvrent les jonctions finales, intermédiaires,
suivies d'un fichier absent et pendantes. Le fichier simplement absent conserve
son erreur distincte. Les deux jonctions possédées ont été retirées sans parcours
récursif, après terminaison des jobs ; aucun profil, ACL ou code produit modifié.
La preuve est
`isolation-prototype-001/nt-metadata-host-001/attempt-002/result.json`, SHA-256
`c4759a4c90ae9cc392f60321a4472792ea3f295b3c727809dae6388ed7d658cd`.
Ce contrôle de faisabilité hôte ne prouve pas encore le fonctionnement sous
AppContainer ni l'admission par le lecteur du produit. Un témoin confiné est
préparé avant de décider du raccord Windows.

Le témoin confiné suivant passe également les douze cas avec le même binaire
de sonde, dont les quatre refus reparse spécifiques. Le processus natif PID 26148
partage le SID AppContainer dédié, sans capacité ; les sondes hôte et confinée
avant/après distinguent les accès ordinaires des refus source/Vault (erreur 5)
et réseau externe (10013). Aucun ACL ou alias ajouté ; les jobs sont vides, les
liens possédés retirés, le profil et son namespace entièrement supprimés.
La preuve est `isolation-prototype-001/nt-metadata-lowbox-001/result.json`,
SHA-256 `5fd50a957c65aff79061722e15d18c711e54cd6003ad87f665e6d502d05485b4`.
Le raccord Rust est maintenant autorisé dans cette réalisation ; ses propres
tests et une admission réelle des modules restent nécessaires.

Le gel de la campagne 006 est levé après son échec natif pour instrumenter le
test physique et intégrer ce raccord. Sa compilation WASM encore active reste
préparatoire ; tout changement d'inventaire doit invalider son adoption et
empêcher la Release enchaînée. Une nouvelle campagne figée reprendra la validation
globale après les corrections. Aucune livraison finale n'adopte ces entrées mixtes.

### Raccord Windows intégré et admission réelle des modules

La trace `implementation-002/physics-diagnostic-002/measure.log` montre que
l'objet du test physique était déjà en mouvement avant la requête refusée.
La correction du test conserve les assertions de queue, synchronisation,
contact périmé et séquence, puis vérifie directement l'absence d'impulsion et
d'initiateur après refus, et leur présence exacte après un nouveau contact valide.
Le test ciblé passe ; aucune force, règle ou ordonnance de production n'a changé.
La revue indépendante est `physics-diagnostic-003/INDEPENDENT_REVIEW.md`, SHA-256
`9235dba85f3ce5b40db3e5900d13d8ba28f355c46c107badd39241d3d1d94c7d`.
La cause de la différence numérique du solveur entre Source013 et 006 n'est pas
déduite de cette correction ; la suite native complète est rejouée en 007.

Le lot `implementation-002/windows-guard-rust-003` passe le formatage,
86 tests de livraison, l'import des véritables données Solaren sélectionnées,
Clippy strict et la construction du CLI. Le test Solaren relancé ajoute un cas
historique réellement exécuté au total distinct ; les deux autres ignorés du
premier passage sont ce cas et le témoin privilégié de liens symboliques.
Ce dernier a ensuite été demandé explicitement et sa création a été refusée
par Windows (1314), avant ses assertions ; ce n'est pas une preuve négative de
lien symbolique. Les jonctions finales, intermédiaires, pendantes et suivies
d'une queue absente sont réellement créées et refusées sans privilège ajouté.
Les cinq pins sources du lot sont inchangés avant/après.

Le garde utilise des Vec de taille fixe, les pointeurs bruts et UnsafeCell pour
préserver les buffers jusqu'à la fin de l'appel ; il ne reprend le handle que
sur succès. Les objections initiales d'aliasing Box et de handle sur erreur ont
été corrigées avant ce lot. Les chemins Disk ordinaires acceptent les séparateurs
portables Win32, avec les mêmes refus d'alias ; les slashs verbatim restent
refusés. Le test d'intégration a aussi fait conserver le refus d'un fichier
utilisé comme préfixe d'une destination. Les deux revues indépendantes
`WINDOWS_PATH_GUARD_INDEPENDENT_REVIEW_001.md` et `..._002.md` conservent les
observations et limites, dont les réouvertures ultérieures par nom.

Le CLI debug de 14 245 888 octets, SHA-256
`8b0f96f814b41217670b4ce1b5682b69717e37b88632b3cfadafe8e2013904fa`,
a ensuite admis les deux factory002 sous AppContainer, sans ACL manuel ni alias.
Les PID 32224 et 33968 sortent avec code 0, avec leurs identités exactes et
`gpu_started=false`, `runtime_qualified=false`. Les quatre jobs ont terminé
normalement ; les huit tokens observés partagent le SID attendu, sans capacité.
Les sondes avant/après refusent réellement source/Vault et connexion externe.

Le collecteur de preuves initial a échoué sur un chemin DOS de 260 caractères,
puis une récupération distincte a conservé les 36 preuves et inventorié les
25 fichiers de cache avec leurs chemins longs, sans omission. Après revalidation
des octets et des processus terminés, le profil exact et tout son namespace
ont été supprimés. Les erreurs de collecte et de récupération restent conservées.
Le bilan est `isolation-prototype-001/private-storage-cli-005/CLI005_RESULT.json`,
SHA-256 `1520b2469c8ff48e23828bac805e07ad51388e5a759f29e2f57d67e72e86af50`.
Cette admission CPU réelle ne vaut pas encore essai Release, Studio ou jeu.

### Campagne commune 007

Les modes native, WASM et supply ont la même empreinte de sources
`74a513b582c39b200a7b4ff7fd2f43b538aae45645d5a948f6fac869833a9843`.
Les reçus finaux native, WASM et supply passent avec sources inchangées.
Le mode native comprend format, diff, Clippy workspace strict et 2 333 tests
réussis dans 248 suites ; 56 tests sont explicitement ignorés et ne sont pas
comptés comme exécutés. Le mode WASM comprend le contrôle WebGPU moteur et le
bundle Trunk Release avec ses assets locaux fermés. Le frontend a été adopté
sans reconstruction dans `delivery-adoption-007-001`, à partir du reçu WASM
authentifié. Les trois exécutables Release ont ensuite passé leur construction
et leur authentification sur cette même empreinte ; Studio et jeu ont été adoptés
sans reconstruction. La synthèse est `qualification-007/BUILD_VALIDATION_SUMMARY.md`.
La réserve de distribution GLib reste distincte et n'est pas effacée par supply.
Les vrais parcours d'import/variante/réouverture/export Studio et la livraison
finale restent à acquérir. Un nouvel arrêt Computer Use signalé par Échap
interrompt les actions UI ; une demande de reprise est en attente du propriétaire.
Les contrôles sans interface continuent sans modifier le launcher ou le Setup
canonique Source013.

Après réauthentification des quatre chaînes, `non-ui-pipeline-007-001` a adopté
Studio et jeu, puis l'assemblage a échoué : le verrou indépendant du launcher
était périmé et `cargo test --locked` l'a refusé. Les tests et la construction du
CLI de livraison et du Hub avaient passé ; les essais silencieux suivants n'ont
pas été exécutés. Aucun candidat complet n'a été produit ou promu, et le Setup
canonique Source013 est conservé. L'échec et ses journaux restent dans
`classic-build-007-001` et `non-ui-pipeline-007-001`.

La revue indépendante de native007 est conservée dans
`qualification-007/native-qualification-review-001/REVIEW.md`. Elle confirme
les 2 333 PASS, 56 ignorés, 248 suites et les 2 716 entrées de build courantes.
Le changement du rapport documentaire pendant le run est déclaré hors entrées
compilées ; le checkout documentaire entier n'est pas présenté comme figé.

Le superviseur des essais silencieux est relu dans
`packaging-review-007-001/REVIEW.md`. Ses témoins console004 vérifient la sortie
du parent avant l'enfant, puis un timeout contrôlé : job vide, tous les handles
signalés, temps OS et codes de sortie conservés. L'appartenance au job est vérifiée
sur chaque handle ouvert pour éviter la réutilisation d'un PID étranger.
Ces témoins ne sont pas des essais du produit ou du NSIS.

Le superviseur du client privé est préparé séparément sous
`isolation-prototype-001/final-recipe-002`. Sa révision002 corrige l'attente des
handles descendants et conserve la première version dans un historique explicite.
Sa revue reste statique : le succès du superviseur silencieux ne lui est pas
transféré. Le contrôle à blanc `validate` retourne WAITING_PINS et
BLOCKED_ESCAPE_HUMAN_REAUTHORIZATION_REQUIRED ; namespace et sortie de campagne
sont absents. Aucun profil final n'est acquis avant la vraie reprise humaine.
Les deux workspaces auteur complets ont leurs inventaires de données reçues
fermés et relocalisables ; leur copie ne sera pas présentée comme un import UI.

### Nouvelle qualification commune 008 — 16 octobre

Le gel 007 a été levé pour réconcilier uniquement le verrou Cargo du launcher
et corriger les contradictions du README de livraison. La résolution hors ligne
n'a retiré aucun package ni changé de version ou de checksum existant ; elle a
ajouté les dépendances déjà nécessaires aux imports médias du domaine partagé.
Le mode `--locked` reste obligatoire dans les validations et l'assembleur.

Le nouvel inventaire est dans
`.gate4-evidence/20261016/qualification-008/SOURCE_FREEZE.json`, SHA-256
`f413444514f5072190821587cdc3dc6a45af95aab6b890a0605b381d64786b90`.
L'empreinte des entrées de build est
`e3f080053b746f84eb6dd6d9199338bfb1e80b385376d9aab79563d1c36ddf4c`.
Les deux seuls changements depuis 007 sont `apps/forge_launcher/Cargo.lock`
et `scripts/delivery/README.md`. La version préparée est
`0.1.0-preview.20261016.014`. Les quatre chaînes sont relancées dans de nouveaux
dossiers ; les reçus 007 restent historiques et ne sont pas réétiquetés 008.
Les interactions Studio/jeu et l'acquisition du client privé final restent
arrêtées après Échap, dans l'attente de la réautorisation humaine demandée.

Le test launcher corrigé a ensuite passé ses dix cas avec
`cargo test --manifest-path apps/forge_launcher/Cargo.toml --locked --offline -j1 --features production`,
verrou inchangé ; son journal a le SHA-256
`efe948155a3916bf25894225d83fb058421b2dadcd78e1c9d71939fcb65200a1`.
La revue indépendante du verrou est dans
`.gate4-evidence/20261016/launcher-lock-review-001/REVIEW.md`.
Le mode native008 passe format, diff, Clippy strict et les 2 333 tests
workspace (56 ignorés, 248 suites), avec sources inchangées. Son reçu a le SHA-256
`e5cb3371c1f8506e9017f2fc9e0b213b1237e2f44b2bd79fe5be8e15b008862f`.
Supply008 passe selon la politique locale ; son reçu a le SHA-256
`61be0ccbc7d098bbbf3ee6fe9698b425fe85a1b5739ab2b8b664285dbf0f0253`.
Ces succès ne lèvent pas la réserve GLib ni ne préjugent du bundle WASM,
des builds Release, de l'assemblage ou des parcours du client final.

WASM et Release008 ont ensuite passé sur la même empreinte. Les quatre chaînes
ont été rouvertes et authentifiées avant adoption ; la synthèse et les trois
empreintes de binaires sont dans
`.gate4-evidence/20261016/qualification-008/BUILD_VALIDATION_SUMMARY.md`.
Le reçu WASM a le SHA-256
`727fd6998df1815393bbc57edfede12a2b928f33fcceb02b1ce44b35dbc8036d`,
le reçu Release
`3b6547460d8453150e7e71bcaedf3e97c5a6192b1a58b649a88a04778431f954`.
Le Studio Release courant fait 187 926 528 octets, SHA-256
`575a3734ac28207f7e1561af5d5d56a904bbf3ef847cdb37a7f6b1f457771da5`.
Runtime et jeu séparé conservent leurs octets Release007 ; ils ont néanmoins
leurs nouveaux contrôles et reçus008, sans réétiquetage des anciens résultats.

L'adoption fraîche des trois stages a réussi dans `delivery-adoption-008-001`.
Le pipeline `non-ui-pipeline-008-001` assemble maintenant le candidat014 puis
doit effectuer les contrôles CLI et NSIS silencieux relus. Aucun verdict anticipé
d'installateur, de client isolé ou de clôture n'est attribué à cette préparation.
La matrice des seize critères et leurs preuves encore nécessaires se trouve dans
`.gate4-evidence/20261016/closure-matrix-audit-008-001/MATRIX.md`.

La révision auxiliaire003 du superviseur final002 remplace seulement cinq
libellés Source007 par Source008 et actualise le pin du contrôleur dans ses
paramètres. Les deux anciennes pièces sont préservées ; gestion native,
fichiers, profil, jobs, limites et autorisation humaine sont inchangés. La revue
Root est `final-recipe-002/ROOT_REVIEW_003.md`. FINAL_PINS reste WAITING et
l'autorisation UI reste bloquée ; aucun profil ni parcours final n'est acquis.

L'assemblage008 a ensuite terminé, sources compilées inchangées. Le candidat
`classic-candidate-008-001/OpenRPG-Forge-Setup.exe` fait 1 181 857 081 octets,
SHA-256 `f2bc758abd7288bc288dd1f7ae8c3d6dfb848941a363ee5a536c4e6698d5fd90`.
Son reçu d'assemblage a le SHA-256
`1b933daf0fcf50c11123c981729f10c00720cf8a3881605dabfe49917a479f84`.
CLI de livraison, Hub et launcher passent leurs tests et constructions propres.
Le Setup canonique Source013 reste inchangé et n'a pas été remplacé.

L'installation CLI008-001 passe, avec trois packs vérifiés et rejeu d'application
idempotent. Le contrôle upgrade a ensuite été interrompu par son superviseur :
QueryFullProcessImageNameW a renvoyé erreur5 lors de l'observation d'un descendant
dont la durée de vie OS relevée est de 11 ms. Son handle avait été reconnu dans
le job ; son image n'est pas connue et ne sera pas déduite de son seul PID.
L'exception a coupé le recensement à 13 handles pour 14 processus au total.
L'essai reste FAIL, sans résultat métier upgrade complet. Le nettoyage confirme
job vide, tous les handles conservés signalés, puis absence des PID observés ;
les processus encore actifs du seul job ont été arrêtés avec code125.
Le pipeline008-001 est donc FAILED_OR_STOPPED et les essais NSIS n'ont pas débuté.
Une correction auxiliaire d'observation est préparée dans de nouveaux dossiers,
sans modification ou reconstruction des sources et binaires008.

Les deux contenus détachés ont été réellement admis, relus puis copiés dans
`content-export-008-014-001/Forge-Contenus-20261016-014`. Les 8 138 membres assets
et 90 membres module ont été vérifiés ; les archives, leurs manifestes et les
deux métadonnées forment les six fichiers attendus. Aucun pack applicatif ou Vault
complet n'y figure ; droits privés conservés et contenus canoniques Source013
inchangés. Les exports Studio v2 restent en attente de parcours UI réels.

Le helper de proposition des pins du client final a aussi été relu et exécuté
sans produit ni profil : 54 champs null remplis, 126 entrées mesurées, WebView2
réel enregistré et épinglé. La proposition SHA-256
`5703ee65b056942c5bee3b2f1630e88836f35bdacbd5a8eee2e573aa019fb40b`
reste WAITING dans son propre dossier ; elle ne remplace pas les contrôles actifs.
L'autorisation humaine après Échap reste en attente et aucun client final n'est créé.

Le correctif auxiliaire008-002 conserve tous les gardes de comptage, d'appartenance
et de fermeture. Une image inaccessible ne peut rester inconnue qu'après preuve
de sortie sur le handle exact ; elle demeure explicitement partielle dans le reçu.
Revue indépendante et témoins console passent : refus sur processus vivant,
acceptation annotée après sortie, parent terminé avant son enfant et timeout
volontaire nettoyé. Les deux essais de cycle de vie comptent chacun4/4 membres,
tous signalés, temps OS non nuls et job vide. L'ancien13/14 reste FAIL.

Les nouveaux contrôles CLI008-002 passent ensuite : installation, vérification
complète des trois packs, rejeu idempotent, mise à jour et rollback. Les90 fichiers
utilisateur, dont la source retouchée, sont conservés. Les jobs comptent10/10 puis
18/18 membres, tous codes0, images connues, temps OS non nuls et fermeture normale.
Les reçus métier sont `cli-install-008-002/verification.json` et
`cli-upgrade-008-002/verification.json` ; la revue Root est
`packaging-silent-review-008-002/ROOT_CLI_RESULTS.md`. Aucun produit graphique
n'a été lancé. L'essai NSIS silencieux distinct a maintenant débuté dans
`silent-classic-008-002` sur le candidat014 inchangé ; aucun PASS n'est anticipé.

Cet essai NSIS a ensuite terminé avec
`PASS_LOCAL_SILENT_INSTALL_REINSTALL_UNINSTALL` : destination Unicode choisie,
réinstallation, trois recettes de lancement vérifiées sans exécution,91 fichiers
utilisateur et deux fichiers étrangers conservés. Les huit jobs ferment normalement,
codes0 et recensements complets ; le désinstalleur relocalisé termine après son parent
et a bien été attendu. L'intégration shell existante reste inchangée. WebView2 était
déjà présent : aucun prérequis ni produit Forge n'a été lancé. Ce succès ne qualifie
ni l'interface interactive de l'installateur, ni WebView/Studio/jeu, ni Windows vierge,
ni une release. Le Setup canonique Source013 reste distinct du candidat014 testé.

L'addendum `closure-matrix-audit-008-001/SCOPE_CORRECTION.md` corrige une exigence
excessive de la matrice : R4-P01 demande installation et jeu sans source/Vault/IA,
pas Studio auteur dans le même AppContainer. Les parcours d'auteur réels peuvent
être qualifiés séparément sur DATA, puis leurs packages exacts admis/installés/joués
en environnement isolé. Les86 tests livraison PASS et deux ignorés de l'assemblage008
sont distincts des2 333 tests workspace. Les protections communes peuvent être
rapprochées de leur CLI014 exact sans rejouer chaque combinaison ; la troncature
d'un vrai `.orpmod` et l'interruption observée du staging restent à compléter.

Les compléments console d'archives ont ensuite été acquis. Le premier lot003
conserve son FAIL : positif Cairns passé, corruption correctement refusée mais
recensement1/2, descendant manquant non identifié. Le lot neuf004 emploie un
superviseur spécialisé au seul CLI014 et à ses huit commandes fermées, avec
DETACHED_PROCESS et les mêmes gardes stricts. Il passe deux inspections positives,
les corruptions et troncatures des deux vrais `.orpmod` factory002, puis les refus
de dépendance absente et incompatible. Chaque job compte1/1 membre réellement
observé, image et cycle complets, handle signalé, temps OS et code0/1 attendu ;
aucune racine d'installation ou utilisateur n'est créée. Le reçu final
`archive-negative-protocol-008-004/results-001/verification.json` a le SHA-256
`d8417eb0b41efffbfb9a831d0b0d84c35aa95989f895fc3cd4a9624bb9fdc1f5`.
Les originaux, le candidat et les gros packs sont inchangés. Ces factory ne
remplacent pas les deux exports Studio à obtenir. Le contrôle de staging reste
préparé séparément, sans verdict anticipé.

Le contrôle de staging008-003 a ensuite passé sur les mêmes entrées008 : arrêt
du seul job possédé avec code126 après 5 fichiers/632 899 octets sur 8 138 fichiers/
496 953 319 octets attendus. Le stage est incomplet et retenu ; manifeste, tête
active et destination finale absents après l'arrêt. Les deux sentinelles sont
inchangées. Le même plan SHA-256
`abab601ce8f5ffa81a0f6ba33df8fc430b901959cefab98951021f82238d5610`
est rejoué sans replanification : révision1, un seul reçu d'installation, vérification
complète et fragments conservés intacts. Les quatre jobs comptent chacun 2/2 membres,
tous signalés avec images/temps complets et job vide ; seul l'arrêt demandé porte126,
les trois phases normales sortent0. Le reçu
`staging-interruption-results-008-003/verification.json` a le SHA-256
`b6084d8748170d444938f25943da1dd9c669628cc7750af18e8ea1b84943bd70`.
Root a aussi rehashé les fragments, les deux sentinelles et le plan après le parcours.
Ce succès reste une preuve console d'installation neuve, pas une partie RPG,
une interface Studio, un profil isolé ou une clôture Gate4.

La relecture indépendante des résultats NSIS est dans
`packaging-silent-review-008-002/NSIS_RECEIPTS_REVIEW.md` ; celle de la reprise
dans `staging-interruption-protocol-008-003/STAGING_RECEIPTS_REVIEW.md`.
Elles concordent avec les reçus réels sans relancer les programmes. Aucun parcours
UI ou profil final n'a été acquis après l'arrêt par Échap ; la proposition de pins
finale reste WAITING, et aucune clôture de Gate4 n'est prononcée.

Après ces acquisitions, les quatre chaînes et l'empreinte des entrées compilées
ont été réauthentifiées sans changement. Le postcontrôle hôte
`qualification-008/POST_DELIVERY_SOURCE_CHECK_FULL_HOST_002.json`, SHA-256
`c375f8cf2e814ac0c1de8794da18d252df38b23a83ba96c7e295d2e20ef5d47b`,
est PASS. La première tentative sandbox a été refusée en lecture sur
`.tidewater-integration/INDEX.json` ; son stderr est conservé et aucun droit
n'a été modifié. Les SHA du Setup canonique et de `Lancer-Editeur.cmd` restent
respectivement `0eca18e3…17ad0` et `de93abbd…fedd0e`. Aucun commit ou push nouveau.
