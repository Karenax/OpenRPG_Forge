# Gate 2 — mise à jour de projection auteur, en cours

12 septembre 2026, Rust, non publié.

`TerrainGenerationPlan::apply_authoring_update` ajoute un chemin explicite sans
affaiblir l'API historique des deltas. Base révision/hash et cache revalidés ;
identités/seed/structure restent fixes. Renommer ne remplace aucun chunk ; modifier
l'altitude de base remplace les 64 chunks et collisions. Les deltas peints restent
dans le document cible. Aucune mutation du cache avant validation du résultat.

Deux tests d'intégration monde PASS ; Clippy monde ciblé PASS. Le nouveau test
compare le cache après application à une génération complète, contrôle absence de
remplacement sur renommage et refus de réutilisation du cache devenu périmé.
Journal : `E:/OpenRPG_Forge/.gate2-evidence/authoring-projection-tests.log`.

Raccord moteur/Studio ajouté mais **pas encore qualifié** : soumission dédiée dans
la file existante de transactions, même validation des bindings/rendu/Rapier ;
file frontend bornée à 64 documents, soumission suivante après acquittement,
erreur explicite et arrêt de la file sur échec. Le statut ne prétend pas que
l'acquittement CPU/Rapier est une preuve de présentation GPU.

Le bundle Trunk est toujours en cours ; ne pas réutiliser les anciens résultats
WASM pour ces nouvelles sources. Tests moteur, Clippy WASM, lancement et capture
restent requis. Pas de playtest ou Gate 2 validé. L'amplitude/seed, les changements
de structure et le rattrapage de révisions sautées restent hors de ce chemin.

Complément : invalidation persistante de la vue après fermeture, saturation ou
échec, purge des demandes restantes et suppression des acquittements tardifs.
Un rechargement explicite est requis avant reprise de cette projection.
Test moteur `authoring_rename_and_elevation_preserve_bindings_and_update_rapier`
ajouté : conservation des bindings, zéro remplacement sur renommage, 64 remplacements
sur altitude, raycast Rapier à +8 m. **Test ajouté, pas encore exécuté** ; attendre
la compilation actuelle puis le lancer avec les features physiques.
Formatage et diff check PASS, aucune preuve de fonctionnement moteur déduite.

Validation suivante : premier bundle Trunk debug PASS (20 min 54 s pour Cargo,
cache de code généré reconstruit), WASM produit 154 033 242 octets. Ce n'est ni
une mesure de performance ni une taille de distribution optimisée.
Clippy WASM strict PASS après consommation explicite des wrappers `Res` via
`into_inner`, sans désactiver le lint. Journaux `authoring-live-wasm-clippy.log`
(échec initial) et `authoring-live-wasm-clippy-fixed.log` (PASS), dossier preuves E:.
Rafraîchissement du bundle puis build Tauri séquentiels en cours : journaux
`authoring-trunk-refresh.log` et `authoring-native-build.log`. Configuration locale
`E:/OpenRPG_Forge/.gate2-evidence/tauri-authoring.conf.json`, route `authoring=1`,
sans kit de démonstration et sans bundle installateur. Aucun lancement encore.

## Parcours Studio réellement observé — 12 septembre

Les états de build « en cours » ci-dessus sont historiques : Trunk rafraîchi et
Tauri natif ont terminé avec code 0. Binaire lancé :
`E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906/debug/forge-studio.exe`,
SHA-256 `f50332bf157c9987c35abc8fa3ae8aaa7cb661b7bfac1f667c4f9538f3670911`.
Route `authoring=1`, racine native isolée
`E:/OpenRPG_Forge/.gate2-evidence/authoring-ui-projects`. Essai ensuite déplacé sur
l'écran secondaire gauche, conformément à la demande du propriétaire.

Parcours effectué par les boutons et champs du produit, sans édition du JSON :

1. Créer `mon-projet / zone-1` : terrain plat de 512 m affiché, 64 chunks.
2. Sauvegarder, saisir « Vallée test auteur », prévisualiser : diff avant/après,
   sans changement du hash moteur pendant l'aperçu.
3. Appliquer : état non sauvegardé, acquittement CPU/Rapier puis hash identique
   visible dans l'overlay moteur :
   `5a7a7b9aaaa549744f5c6ca6b0e462f2aa8589dc2b96ad977d66f485b9fc3374`.
4. Sauvegarder, fermer la zone et ouvrir : session restaurée. Annuler restaure
   « Nouvelle zone », Rétablir restaure « Vallée test auteur ».
5. Sauvegarder à nouveau : trois reçus persistés, révisions 2/3/4,
   hash final `3da50f11eecaed54e909e7c4446fea35215d85faa7cc89cd042eedf5b5775a2e`.

Archive observée : `81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`
sous cette racine, SHA-256
`a31ebb6a277fda7e4ed36986deb160e12744f31f11f96702a2bcbb313def0be9`.
Captures inspectées dans le flux d'outils ; pas de PNG autonome conservé pour
ce passage. Ce résultat ne qualifie ni l'altitude dans la vue, ni le playtest,
ni la performance (compilation concurrente, overlay glissant non benchmark).

Défauts observés : contrôles trop serrés ; texte historique de scène de preuve
inapproprié ; projection déclarée périmée après fermeture/réouverture. La vue
initiale finit bien par afficher le terrain, mais son message reste « Construction ».
La conservation du nom et de l'historique ne constitue donc pas un parcours 3D
complet. Le test graphique a été fermé après sauvegarde : PID 30280, WebView 30196
et console 30352 absents au contrôle suivant, aucun descendant direct de 30196
restant. Aucun autre navigateur n'a été arrêté.

Correctif de navigation implémenté ensuite, **non présent dans le binaire testé** :
lecture de la session native au montage de la route auteur ; navigation pour
libérer l'ancien moteur après un changement de session accepté ; aucun reload
sur commande refusée, preview ou retouche. La session native reste l'autorité,
aucune sauvegarde implicite. Le texte de scène de preuve n'apparaît plus dans la
route auteur. Formatage et diff check PASS ; Clippy WASM en attente du cache
occupé par le nouveau test moteur (`authoring-engine-test.log`). Aucun PASS
de ce correctif ou du test moteur n'est encore revendiqué.

### Contrôle de conservation pendant un rechargement

Test `viewport_remount_inspection_preserves_unsaved_session_without_saving`
PASS : trois lectures Inspect conservent la retouche non sauvegardée et les octets
de la sauvegarde précédente ; Close/Open refusés ne remplacent pas la session ;
sauvegarde explicite puis réouverture rendent exactement le document édité.
Ce test de l'hôte natif ne remplace pas le replay de navigation WebView.

Suite core/project rejouée : **17 + 83 = 100 tests PASS**, aucun ignoré,
`authoring-session-navigation-domain-tests.log`. Clippy strict project/all-targets
PASS dans `authoring-remount-domain-clippy.log` (journaux sous `.gate2-evidence`).
Le statut initial du viewport a aussi été raccordé à la présence de l'état moteur :
il distingue chargement CPU et présentation GPU, sans rester indéfiniment sur
« Construction ». Ce changement frontend reste à compiler et à lancer.

La configuration locale Tauri du test utilise désormais l'écran secondaire
observé (`x=-1920`, `y=52`, maximisé). Pas de modification des réglages Windows ni
de règle d'écran imposée au produit distribué. Test moteur toujours en compilation
(session 82319) ; Clippy WASM attend son cache (session 30636). Aucun processus
graphique de ce test n'a été relancé.

Reprise après interruption : les sessions 82319/30636 ont disparu sans résultat
terminal dans leurs journaux ; aucun processus Cargo/rustc correspondant ne
subsistait. Aucun verdict n'en est déduit. Commandes reprises séquentiellement
sur le même cache, journaux distincts `authoring-engine-test-resumed.log` puis
`authoring-session-navigation-clippy-resumed.log` (ce dernier seulement si le test
moteur réussit). Nouvelle session 90336, compilation reprise à Bevy GLTF/UI.

### Résultat du test moteur et correction Clippy

La reprise termine la compilation native en 11 min 55 s, puis le test
`authoring_rename_and_elevation_preserve_bindings_and_update_rapier` **PASS**
(1 test, 331 filtrés, 1,46 s). Bindings conservés ; renommage sans remplacement
des meshes ; altitude à +8 m sur les 64 chunks et raycast Rapier concordant.
Ce test ciblé ne remplace pas la qualification workspace et n'est pas une capture GPU.

Clippy WASM a ensuite échoué sur `too_many_lines` (119/100) dans les contrôles
d'édition. Extraction des fonctions de statut et navigation, sans désactivation
du lint ; replay strict **PASS**, `authoring-session-navigation-clippy-fixed.log`.
La construction Trunk/Tauri du correctif est lancée séquentiellement (session
92830), journaux `authoring-navigation-trunk.log` et `authoring-navigation-native.log`.
Frontend distinct : `E:/OpenRPG_Forge/.gate2-evidence/studio-authoring-web-navigation`.
L'ancien binaire testé est préservé dans
`E:/OpenRPG_Forge/.gate2-evidence/studio-authoring-first-tested.exe`, hash vérifié
égal à `f50332bf157c9987c35abc8fa3ae8aaa7cb661b7bfac1f667c4f9538f3670911`.
Le lancement du nouveau binaire et le parcours altitude/rechargement restent requis.

### Replay du correctif de navigation — 12 septembre 2026

Builds Trunk et Tauri terminés avec succès (journaux ci-dessus, respectivement
54,22 s Cargo et 1 min 06 s natif). Binaire réellement lancé :
`E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906/debug/forge-studio.exe`,
SHA-256 `06c4e7a8151c5a8ab389e391570e003ee62685648392ad127e77f787519874d9`.

Parcours Studio : aperçu altitude 0 → 8 m, application, sauvegarde explicite,
fermeture de zone (canvas vidé), puis réouverture par le bouton Ouvrir.
La capture de réouverture inspectée montre « Vallée test auteur », altitude 8,
session synchronisée et terrain rendu (64 chunks). Le hash affiché côté moteur
et dans l'overlay correspond exactement au `final_hash` de l'archive :
`42b8905a985e50495ef29ab38ee232a279c264e3ef89b002c3d3f9a6252a3248`.
Dernier reçu : révision 5, `set_base_elevation` 8 m, inverse 0 m.

Archive conservée sans édition JSON manuelle :
`E:/OpenRPG_Forge/.gate2-evidence/authoring-ui-projects/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`,
SHA-256 fichier `a11fd4aa8cdf5ac2496b59b1ec5d59437c622c453d7990abb5e85c19158b2430`.
Captures inspectées dans la conversation, pas de PNG autonome archivé pour ce replay.
Le titre interne de l'overlay reste hérité de Visual Valley : ce libellé ne
qualifie ni la scène Gate 1 ni le playtest de la zone auteur.

Essai sur l'écran secondaire ; fermeture du Studio par Alt+F4 après sauvegarde.
Contrôle CIM suivant : PID 29320, console 30736, WebView 5840 et descendants
27408/12844/18712/19412/18900 absents. Aucun navigateur utilisateur arrêté.
Ce replay valide la reprise visible de cette zone plane et sa retouche globale,
pas les brosses locales, la régénération avec overrides, le playtest ou Gate 2.
