# Tidewater — choix temporel dans le playtest Studio

Suite du [raccord RenderWorld natif270](2026-10-02-render-owned-temporal.md)
et du [rapport d’exposition Studio](2026-09-30-studio-exposure-sweep.md).
La calibration +0,10 EV est conservée. La parité visuelle complète et
l’objectif Tidewater restent ouverts.

Le panneau graphique possède maintenant un choix exclusif entre désactivé,
TAA standard Bevy et Tidewater expérimental. Le choix est mémorisé dans le
profil local existant; un ancien JSON sans ce champ reste désactivé et conserve
ses autres valeurs. FXAA garde son contrôle indépendant. Le panneau distingue
la demande de réglage, la configuration caméra, l’attente du rendu et le refus.

Le contrôleur utilise la vraie caméra joueur, avant la préparation temporelle
et le miroir de réfraction. Les changements de curseurs ne réinitialisent pas
l’historique. L’arrêt, la disparition ou le remplacement de la caméra retirent
les composants temporels possédés. Les prépasses existantes sont conservées;
la prépasse de mouvement ajoutée par le TAA peut être reprise par le flou de
mouvement et nettoyée ensuite. La prépasse de profondeur possédée par le TAA est
retirée à son arrêt : Bevy0.19.1 ne la requiert pas pour le flou de mouvement.
Le contrôleur de flou ne revendique ni ne retire une profondeur étrangère.

La capture de réfraction du TAA standard reçoit le décalage effectivement
préparé par Bevy dans RenderWorld. Le ratio utilise les deux tailles réelles,
y compris 1919×1079 vers 960×540. Une association ambiguë, une pose/projection
différente ou un marqueur de spécialisation absent refuse la mise à jour.
Ce raccord ne touche pas une caméra SourceResolve sans TAA Bevy. Un numéro de
demande sert uniquement à écarter les observations d’un ancien réglage.

## Preuves CPU obtenues

| Preuve | Résultat | Portée |
|---|---|---|
| Engine natif, bibliothèque247.6 | Compilation réussie, sources Engine stables | Bibliothèque et dépendances natives épinglées; aucun verdict GPU |
| Engine tests247.5 | 89 tests temporels passent, 1 import shader ignoré dans ce filtre | Régression CPU du propriétaire temporel existant |
| Capture CPU280 | 8 tests passent | Vrais composants Render et `World::run_system_once`, taille impaire, refus et absence de mutation SourceResolve |
| Studio lifecycle281.4 | 13 tests passent | Module Studio réel inclus par chemin, définitions de préférences extraites du source réel, adaptateur contrôlé de l’état UI seulement; ECS/MainWorld et JSON, dont TAA → flou → arrêt TAA |
| Studio WASM279.1 | Build réussi, 750 sources stables | Compilation réelle avec verrou Cargo et dépendances courantes; aucun verdict GPU |
| Frontend283 | Bindings et 1539 fichiers figés | Outil wasm-bindgen0.2.127, statiques courants, SRI recalculés et source archivé sur F |
| Candidat Tauri284 | Build réel réussi, sans dérive | 1548 fichiers frontend/kit et backend natif; `desktop-build-only`, aucun verdict de livraison |
| Essai SKY285 | Bureau verrouillé, essai UI non exécuté | Lecture de l’accessibilité initiale seulement; zéro entrée UI, aucun playtest ni capture de carte, processus possédé fermé |

Les essais281.0 et281.1 échouent à préparer le harness natif (chemin relatif,
puis variables Cargo requises par les macros). Ils sont conservés. L’essai281.2
corrige ce montage sans remplacer le moteur ou le module de production.
Les essais281.2/.3 passent avant l’ajout de la dernière régression281.4.
La revue278 confirme les APIs et l’ordre des étapes; ses constats de retrait
sans propriétaire et de revendication de profondeur sans insertion sont corrigés.

Reçus : `E:/OpenRPG_Forge/.tidewater-integration/taa-capture-cpu-280/receipt.json`,
`studio-temporal-lifecycle-build-281/tests.4/receipt.json` et
`temporal-trace-native-build-247/library.6.json` dans le même dossier parent.

## Mode Tidewater et limites

L’audit274 établit que Studio n’a pas encore de contrat couvrant les coques et
les objets effectivement soumis par une scène éditable. Le produit conserve
`HullRequirement::Required`; il n’insère aucun `NoHullsInScene`. La sélection
Tidewater expérimental refuse donc ce contrat absent avant de modifier l’intention
SourceResolve. Le registre des vrais matériaux et les observations HDR/auxiliaires
sont raccordés, mais ce refus ne constitue pas une preuve de rendu SourceResolve
dans Studio. Les assertions sans coque des tests concernent leurs seules fixtures.

Le HUD ne signale un encodage SourceResolve que lorsque la révision courante,
la caméra, le tag, l’époque, les générations, le resolve, la couverture auxiliaire
et les passes HDR concordent. Un encodage n’est pas une clôture GPU ni une
comparaison de pixels. Les capacités des adaptateurs sont fixées après installation
de tous les plugins pour la durée de l’App; leur reconfiguration exige une nouvelle App.

La compilation Studio/WASM279.0 réussit en 26 min04, mais quatre sources
changent pendant ce passage. Son statut reste
`compiled-with-source-drift-unqualified`; le WASM de 112909673 octets est conservé
sur F, SHA-256 `42072ffadd5b1880e6ea22d1ebb38cd5e427cc813e0b7648a3babd2da752aa9b`.
La compilation279.1 réussit en 16 min 37 s sur 750 fichiers source épinglés,
sans dérive ni suppression. Le WASM brut fait112924998 octets, SHA-256
`2a11bcfaaa8d35fae40c5e396f40a32d47b96b9b6a6d3e288f857203fc7b4efd`.
Son reçu279/build.1 reste `build-only`. Le frontend283 et les copies de source
sont figés sur F; aucun ancien candidat n’est utilisé comme preuve actuelle.
Les tests CPU et ce build ne qualifient pas l’extraction réelle, les pipelines WebGPU, le passage
édition → jeu → édition, une capture visuelle appariée, Source7, le readback,
deviceLost ou une distribution Windows. Le premier build n’est pas promu en
candidat qualifié; les nouveaux tests ne requalifient aucun ancien GPU/PNG.

L’audit282 confirme que le véritable parcours utilise le backend Tauri natif.
Une page HTTP autonome retourne un refus de backend; elle ne peut pas prouver
le playtest de la carte. L’accès natif Computer Use via `node_repl` et `@oai/sky`
est disponible et a été initialisé. Les nouveaux gros artefacts restent
physiquement sur F car E est presque plein.

## Candidat actuel et essai restant

Le frontend traité283 fait 121382428 octets pour son WASM, SHA-256
`c260947475f915d9e09ed622cbfcc37b28d8bdb97217e66a0c1c9aba58f10f30`.
Les 750 fichiers source sont copiés puis vérifiés; les 1539 fichiers du
frontend et les neuf fichiers du kit gouverné sont épinglés individuellement.
Le script de packaging existant valide ces entrées, copie le bundle,
compile réellement le backend Tauri puis revérifie les entrées et les copies.
Les 1548 pins du candidat correspondent aux pins du staging.

Le candidat284 fait 207264256 octets, SHA-256
`2f6ec7d787512f24a055c07974a70487d55a0e476eecf26f6f5736381cd2b823`.
La compilation native dure 3 min 06 s; copie et contrôles inclus, le passage
284 dure environ 19 min 37 s. Les sources restent stables. Le chemin relatif
`frontendDist` nécessaire à Tauri est conservé via une nouvelle jonction
possédée `E:/OpenRPG_Forge/.tidewater-integration/studio-temporal-candidates-284`
vers `F:/OpenRPG_Forge/.build/studio-temporal-candidates-284`.
Aucune sortie précédente n’est déplacée ou supprimée.

Le lancement285 utilise une nouvelle copie de la fixture d’authoring du
27 septembre et un nouveau profil WebView2, tous deux sur F. L’archive de zone
reste épinglée à `50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`;
le workspace à `0d97d924498e5411ccc848018698a090eac74a648efea2ceb1e2b9696ea61a6e`.
L’interface initiale réelle se monte et l’accessibilité montre la zone disponible.
L’activation échoue et le screenshot d’observation montre l’écran de verrouillage
Windows. La règle Computer Use impose d’arrêter les entrées et de demander le
déverrouillage. Aucun clic, ouverture de zone, mode temporel ou playtest n’est
exécuté. Cette lecture initiale ne valide pas le nouveau panneau graphique.

Le processus possédé37348 est fermé après vérification exacte de son chemin
et de son instant de création. Les deux premières comparaisons de fermeture
refusent l’action à cause de la conversion automatique des dates par PowerShell;
le contrôle avec `ConvertFrom-Json -DateKind String` confirme leur identité
exacte avant l’arrêt. Les 27 fichiers de la fixture source sont inchangés.
Le reçu285 porte `desktop-locked-ui-not-run`; les autres instances Studio
restent hors du périmètre d’action.

Reçus sur F :
`F:/OpenRPG_Forge/.build/studio-temporal-frontend-283/attempt.0/receipt.json`,
`F:/OpenRPG_Forge/.build/studio-temporal-candidates-284/build.0/receipt.json` et
`F:/OpenRPG_Forge/.build/studio-temporal-sky-285/run.json`.
Il reste à tester le panneau, la marche/nage, les transitions temporelles,
le flou, le redimensionnement impair et le retour à l’édition sur ce candidat.
Le contrat de coques Studio, Source7 et la parité complète restent ouverts.
