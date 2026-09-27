# Retouches de végétation — pinceau et commandes agrégées

## Replay regroupé habitat/intensité — 15 septembre 2026

Candidat **8C6A960AB772C3F624523490212E33AB77910F3A53C1824219CCAE78540F37F6**,
`E:/OpenRPG_Forge/.gate2-evidence/habitat-density-studio-20260915/forge-studio-candidate.exe`.
Web PASS (Rust 2 min 37 s), packaging PASS, kit gouverné inchangé ; position
configurée (-1840,90), 1600×900. Le rapport BUILD_ONLY reste distinct de ce replay.

Copie `habitat-density-ui-20260915` de `scene-index-assets-ui-20260914` :
- Arbres, altitude minimum -4096→1000 m : formulaire, diff exact, aperçu sans
  arbres, Apply par Ctrl+Entrée, Undo par Ctrl+Z et retour de la forêt observés.
  Save explicite après Undo. La pente reste à 65 % pendant ce parcours.
- Herbe, rayon 24 m, intensité 0,25 : point dans le terrain, aperçu de 446 marques
  vertes, Apply, masque accepté 257×257, minimum 193/255 et maximum 255/255,
  aucun échantillon nul. Save et Session synchronisée observés. Ce minimum,
  différent du test au centre exact, est compatible avec le clic entre sommets.

Captures dans la copie sous `.gate2-evidence/` : habitat-preview.jpg,
habitat-applied.jpg, habitat-undone.jpg, density-quarter-preview.jpg,
density-quarter-applied.jpg. L'overlay prouve le masque logique, pas une mesure
visuelle de la densité d'herbe éloignée.

Relecture par le vrai décodeur Forge : 11→14 reçus ; terrain de base, recette
végétale et placements exactement identiques après annulation d'habitat.
Archive finale C6343B05694C6BCEF39D527168C14DFE7A345D986D926285C7744A4B58F879F1,
tête 115615d1255950d58796ee5ab8a4633bad913d33268d37b2b1e4ca58fec5b196.
Source intacte FF89547BE4DFBF6C9345A2D4E9EFE340C51495158A69A08AB63C5D435ED868C0.
Runtime 31008 et WebViews 30692/34776/2736/2936/20796/33452 fermés, absence vérifiée.

Limites non déduites : variation réelle de pente, Redo d'habitat graphique,
Undo/Redo du point léger graphique, réouverture GUI, interaction en jeu et
désarmement après changement de réglage. Aucun verdict global R2-C/Gate 2,
aucune campagne de performance, aucun push.

## Intensité du pinceau — 15 septembre 2026

Intensité exposée dans le Studio (0,01–1, valeur initiale 1), capturée au début
du geste et incluse dans l'identité de commande. Le modèle existant de mélange
progressif est réutilisé. Changer rayon/intensité/sens annule le geste armé et
l'aperçu précédent ; il faut réactiver le pinceau. Le masque verrouillé reste
protégé. Aucun format ni défaut de densité des cartes existantes modifié.

Test ciblé PASS : un point à 0,25 fait passer le centre 255→191 puis, en sens
inverse, 191→207 ; extérieur intact, source non mutée, forces invalides refusées.
Compilation 0,96 s, test 0,00 s. Clippy WASM PASS (7,16 s) après extraction des
réglages et de leur validation pour conserver la limite de taille du composant.
Qualification graphique groupée avec les règles d'habitat en préparation.

## Règles de répartition — 15 septembre 2026

Le Studio expose par couche l'altitude minimum/maximum en mètres et la pente
maximum en pourcentage (100 % = gradient 1, pas 100 degrés). Le formulaire
prépare une régénération transactionnelle sur ID stable ; aperçu temporaire,
diff avant/après, application et sauvegarde restent explicites. Changer un champ
annule la proposition précédente ; le brouillon garde la protection de zone.
Les valeurs acceptées sont reprises après modification/Undo grâce à la clé du
composant. Aucun nouveau format, dépendance ni règle spécifique à une carte.

`prepare_layer_habitat_request` conserve seed, terrain, autres couches et
corrections, y compris les masques verrouillés. Ces masques ne forcent pas la
pousse hors des limites d'habitat. Validation existante : altitude [-10000,10000],
intervalle ordonné, pente gradient [0,16], nombres finis ; couche absente et
réglages inchangés refusés. Inverse/retry/audit viennent du bus de zone existant.

Deux tests world ciblés PASS (compilation 13,78 s, exécution 0,04 s) : refus,
prévisualisation sans écriture, retry identique, conservation exacte des masques,
relecture d'archive puis Undo/Redo ; génération réelle CPU excluant les arbres
au-dessus et au-dessous du terrain sans changer les instances des couches voisines.
Clippy Studio WASM strict PASS (29,51 s), après remplacement des comparaisons
flottantes par comparaisons de bits ; avertissement amont proc-macro-error2 inchangé.
Test Studio ciblé PASS (23,71 s compilation, 0,00 s exécution) : unités et
valeurs avant/après dans le diff, proposition effective, archive inchangée.
Clippy du test world PASS (2,22 s), formatage et contrôle du diff PASS.

Limites : panneau et résultat GPU non qualifiés ; DDCC9C5F est antérieur à ce lot.
Pas de nouveau candidat lancé, aucun processus graphique ni publication.
La qualification des pentes en scène et du geste complet Studio reste due.
R2-C/Gate 2 restent ouverts ; ceci ne constitue pas un catalogue d'espèces.

## Overlay du masque — codé et testé, replay graphique dû

`DensityMaskPreview` compare les densités effectives avant/après sur la grille
terrain, avec l'interpolation bilinéaire déjà utilisée par le générateur. La tête
et la révision de la proposition sont vérifiées ; aucune mutation. Jusqu'à 2048
marques régulièrement échantillonnées, positionnées à la hauteur du terrain ;
le nombre total d'échantillons modifiés reste affiché. Un verrou seul ne produit
pas de fausse différence spatiale. Le Studio efface l'overlay sur abandon/autre
commande et le refuse après changement du hash terrain. Couleurs : densité
proposée sous 50 % en magenta, au moins 50 % en vert (pas un indicateur de delta).
Cet aperçu représente le masque, pas le résultat final du scatter ni une carte
continue remplie.

4 tests `vegetation_density_brush` PASS, dont overlay déterministe, borné, fini,
source intacte et reçu périmé refusé. Clippy world/all-targets et Studio WASM PASS
(la légende a ensuite été précisée). Build Trunk/Tauri regroupant compaction,
pinceau et overlay lancé ; aucun nouveau verdict graphique à ce stade.

## Pinceau Studio raccordé — compilation acquise, replay dû

`authoring_density` utilise le picker commun point/trait et les commandes de zone.
Sélection par ID de couche, rayon 2–128 m, cible 0 ou 255, intensité 1 avec bord
radial progressif. Le hash de tête est gardé jusqu'à la fin du geste ; source
changée, couche inconnue/verrouillée ou rayon invalide sont refusés. Un geste
produit une seule proposition puis un seul Undo après Apply. Verrou/déverrouillage
et retrait complet du masque passent également par PreviewZone/Apply communs.
Le retour à 255 dans un geste n'est pas le retrait de l'override complet.

Clippy Studio WASM/web strict PASS après extraction des validations et regroupement
des contrôles de source ; aucun lint désactivé. Le sélecteur d'intensité n'est pas
encore exposé. L'aperçu donne les nombres de masques/verrous avant/après, pas encore
un overlay 3D du masque. Le geste réel, la clairière verrouillée après reseed et
le parcours avec conversion compacte restent à lancer/capturer ensemble. Le
candidat 366762df... n'inclut pas ces derniers contrôles. Aucun nouveau build
graphique lancé dans ce lot ; aucun processus graphique laissé ouvert.

## Replay Studio acquis — 13 septembre 2026

Build Trunk PASS (3 min 41 s), Tauri debug PASS (1 min 26 s).
Candidat `E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906/debug/forge-studio.exe`,
SHA-256 `366762dfaeee10261b1a0939144bdfbc5ac38287e4ae2be6b423b1af6c5b5af3`.
Configuration `E:/OpenRPG_Forge/.gate2-evidence/tauri-authoring-zone.conf.json`,
fenêtre secondaire, copie isolée dans `authoring-vegetation-ui-projects` sous
`.gate2-evidence`, zone `81de8d42-89b9-58b5-b090-406cf4c63d06`.

Observé par computer-use : ouverture de la tête existante, proposition absence→42
sans application, Apply et arbres visibles, Undo et arbres absents, Redo et retour
de la même répartition, proposition 42→43 puis Apply avec nouvelle répartition,
Save explicite, état synchronisé, Close/Open et rendu végétation restauré. Les
deux bandes de matériau/relief restent visibles. Captures affichées et inspectées
dans la conversation ; aucun PNG séparé enregistré. La saisie UIA directe a échoué
(`0x80070057`) ; saisie clavier dans le champ focalisé réussie.

Hash végétation moteur seed 43 :
`d8f074b0f9089c9054c9b9b460133a177b9e2ff32a67f189cc9d05318f1c41f1`.
Hash terrain après Apply et après Open :
`8661147c5638d031c833f860fe66605b1711f10aa7e78cd26a425eda568d5313`.
Archive disque v2, révision 17, 7 reçus, seed 43 ; 618 corrections relief et
576 matériau conservées. Taille 3 888 891 octets ; SHA-256
`b3d6aa1be6646f494434b59ea62863764414a8091756186b8bac4917d820f643`.
Source de copie inchangée : `f4ddcf5ea1cb7c5796ba7b040b441a365415e24e11d3628940e29de43269f0a1`.
Aucun staging de sauvegarde restant. Fermeture Alt+F4, PID 8844 et descendants
24060, 10268, 20780, 5592, 18900, 28808, 30904 ensuite absents ; navigateur utilisateur
préservé. Aucun build encore actif de ce lot.

Limites : preuve fonctionnelle, pas une qualification de qualité visuelle, FPS,
latence ni mémoire. Pas encore de geste de densité Studio ou clairière verrouillée
rejouée graphiquement. L'archive approche la borne 4 MiB : la capacité du journal
doit être traitée avant des sessions longues, sans relever silencieusement la borne.
Le terrain reçoit encore une mise à jour lors des révisions de végétation seules.
Gate 2 ouverte ; qualification transversale ADR-0006 toujours due.

## Actualisation des sources moteur et raccord Studio — en cours

`forge_engine::update_vegetation_sources` ajouté : validation de l'identité de
zone, du plan et du profil avant mutation ; remplacement des documents dérivés
dans les bindings existants, invalidation des uploads/préparations WASM périmés.
Un worker natif déjà lancé reste unique et son résultat est rejeté par les hashes
de source existants. Une source changée force la reprojection même à qualité égale.
La projection précédente reste visible pendant la préparation ; le caller doit
synchroniser le terrain avant cette demande. Ce chemin reconstruit la végétation
entière, pas encore les seuls chunks affectés par un pinceau.

Raccord Studio ensuite codé : un plugin végétation dans l'app/canvas existant,
projection vide dérivée quand la session ne contient aucune végétation (y compris
Undo de l'initialisation), paire de sources effective la plus récente uniquement.
Soumission après concordance du hash terrain moteur ; comparaison des hashes
terrain/végétation dans les statistiques pour l'acquittement. Hashes préparés à
la réception de source, pas à chaque frame ; documents clonés seulement à la
soumission. Erreurs de préparation signalées au panneau. Présentation High fixe
utilisant les presets existants avec identité de zone adaptée ; pas un sélecteur
de qualité auteur complet. Aucun second writer ni donnée de démonstration ajoutée
à la zone. Lancement/capture/inspection encore dus.
Clippy moteur natif/lib strict PASS. Test ciblé
`authoring_sources_reproject_without_a_quality_change` ajouté (sources successives,
qualité inchangée, no-op et entrée invalide) : 1 PASS, exécution 0,01 s après
3 min 45 s de compilation. Preuve CPU/ECS seulement, pas un test GPU ou Studio.
Clippy moteur WASM PASS (5 min 22 s, variante de features recompilée), puis
Clippy Studio WASM/web strict PASS après raccord et limitation des allocations.
Build Trunk/Tauri du candidat graphique lancé ; pas encore un résultat acquis.

## Recette initiale générique — 13 septembre 2026

`forge_world::editor_vegetation::prepare` construit une proposition originale,
sans asset : arbres et herbe en plaques, IDs stables entre seeds, identité,
révision, étendue et référence matériau grass provenant du terrain fourni.
Aucune exclusion de la scène de démonstration, aucun masque manuel inventé,
aucune mutation de source ou sauvegarde implicite. Ce preset initial ne remplace
pas une recette existante. Les collisions ne sont pas configurées dans ce preset.

Vérifié : `cargo test -p forge_world --test editor_vegetation --offline --locked`
2 PASS (déterminisme, identité, stabilité des couches entre seeds, préparation
du plan de génération, source intacte, rejet de terrain invalide).
`cargo clippy -p forge_world --all-targets --offline --locked -- -D warnings`
PASS ; formatage appliqué. Aucun processus graphique lancé.

Raccord suivant codé : `prepare_request` initialise seulement si aucune recette
n'existe ; sinon seule la seed de la recette existante change. La commande est
une régénération agrégée, hash/révision gardés, sans écriture ni Apply implicite.
Le stamp décrit la transformation et ses entrées, pas une nouvelle provenance
historique du terrain. Les reçus gardent la base précédente pour Undo.

Panneau Studio ajouté : seed saisie comme texte puis parsée en u64 (pas de perte
de précision JavaScript), état accepté, préparation PreviewZone et utilisation
du bouton Apply commun. Le reçu de régénération affiche les seeds avant/après
et les masques conservés, sans prétendre fournir un aperçu 3D de régénération.

Vérifié après raccord : 3 tests `editor_vegetation` PASS, dont initialisation,
preview sans mutation, Apply, masque verrouillé conservé après reseed, no-op
refusé, relecture d'archive en mémoire et Undo exact de la base/corrections.
Clippy world/all-targets et Studio WASM/web strict PASS. Le test a initialement
utilisé `from_archive` au lieu de `from_archive_bytes` : corrigé puis rejoué.

Non livré : projection de végétation de la tête agrégée. Les nouveaux contrôles
sont compilés mais pas encore lancés/capturés ; le binaire Studio précédemment
qualifié n'inclut pas ce panneau. Aucun verdict visuel ou de Gate 2 déduit de ces
tests de domaine. Replay graphique groupé encore dû après raccord du rendu.

## Implémenté

`forge_world::vegetation_density_brush::VegetationDensityBrushStroke` prépare
un masque de la couche désignée par ID : rayon 2–128 m, intensité (0,1], cible
absolue u8, trait rééchantillonné avec le mécanisme borné des brosses de relief.
La résolution existante reste inchangée. En l'absence de masque, la proposition
part d'une grille 257×257 à 255 (multiplicateur plein de la recette), pas d'une
carte vide. Décroissance radiale et quantification entière au plus proche.
Le document v1 de végétation supporte déjà ce masque ; aucune migration implicite,
aucune seconde tête, aucune écriture disque, aucune modification de la seed.
Le générateur existant lit ce masque en interpolation bilinéaire.

## Vérifié le 13 septembre 2026

Deux tests ciblés PASS, Clippy world/all-targets strict PASS : déterminisme,
découpage équivalent du trait, centre à zéro, coin hors zone inchangé, document
source intact, autres couches/seed identiques, grille existante conservée,
refus d'intensité non finie et de couche absente. Import de test initialement
dirigé vers un module privé, corrigé vers la réexportation publique existante.

## Non livré

Le premier lot était limité au calcul de proposition. La tête atomique ADR-0014
et le raccord aux transactions sont maintenant implémentés (voir ci-dessous),
mais les gestes UI et les projections de végétation restent à raccorder. Aucun
fichier parallèle indépendant de la session terrain. Régénération préservant les
corrections et visibilité dans le Studio non qualifiées.
Pas d'essai graphique ni de qualification R2-015/R2-C dans ce lot.

## Commandes de densité et verrou — 13 septembre 2026

`VegetationEdit` typé : SetDensityMask, SetDensityLock et ClearDensityOverride,
par ID de couche. Préparation pure sur la tête complète : garde hash/révision,
validation de la recette et du masque, insertion triée, conservation des autres
corrections. Peindre à zéro exclut ; retirer l'override restaure l'héritage de la
recette. Le verrou exige une correction existante et bloque changement/suppression.
Déverrouiller ne supprime pas le masque. La session vérifie aussi les verrous face
à une commande brute, pas seulement face au helper de l'interface.

Le pinceau fournit `prepare_request` : il lit le masque **effectif**, prépare le
trait, puis retourne la commande agrégée commune. Aucun writer, schema de fichier,
seed ou chemin UI spécial ajouté. Preview/Apply/inverse/retry/Undo/Redo restent
ceux de ZoneEditSession ; Save reste celui de FileZoneSession.

Vérifié : `cargo test -p forge_world --test vegetation_density_brush --offline
--locked` : 3 PASS. Le nouveau parcours teste preview immuable, Apply égal au reçu,
retry sans effet, clairière centrale à zéro, autres couches/seed conservées,
verrou, refus de contournement par commande brute, refus de suppression verrouillée,
déverrouillage sans perte, retour à l'héritage, Undo de ce retour et relecture
exacte de l'archive en mémoire. Aucun fichier utilisateur ni processus GPU lancé.
Clippy project/world all-targets strict PASS ; extraction du contrôle de verrou
hors du test principal pour respecter la limite de longueur, sans désactiver le lint.

Ce test ne prouve ni le rendu d'une clairière ni sa survie à une régénération :
la modification de recette acceptée, sa fusion et son raccord Studio restent dus.
La réouverture testée ici est celle de l'archive en mémoire, pas un nouveau test
disque. Gate 2 ouverte, qualification transversale ADR-0006 toujours due.

## Replay Studio densité / verrou / régénération — 13 septembre 2026

Candidat EXE SHA-256 `975b90334a61da63e1dc1f56eeba26d094ee2ea80e31f18dee4bcb0c21e3d14b`.
Build regroupé Trunk + shell Tauri réussi. Pinceau Studio sur picker commun,
Preview/Apply agrégés, point/trait, cible 0/255, verrou/déverrouillage/retrait.
Overlay borné à 2048 marques, interpolation commune au générateur, gardé par hash.
4 tests pinceau/overlay et Clippy world/Studio WASM strict PASS avant ce build.

Parcours réellement observé, sur copie isolée `authoring-density-ui-projects`
(sous `E:/OpenRPG_Forge/.gate2-evidence/`), écran secondaire :

- Conversion compacte explicite puis Save, détaillée dans R2_COMPACT_ARCHIVE.md.
- Couche Arbres (`editor_trees`), rayon 24 m, trait continu dans le canvas.
  Aperçu non appliqué : 1871 marques, magenta < 50 %, vert ≥ 50 % de densité recette.
- Apply : disparition visible des arbres dans le trait, bord progressif ; masque unique.
- Aperçu verrou : 1 masque conservé, 0 → 1 verrou, aucun changement spatial ; Apply.
- Seed 43 → 44 : aperçu annonce le masque conservé ; Apply renouvelle la répartition
  autour de la clairière, qui reste visible. Quelques arbres périphériques sont
  compatibles avec le bord progressif : ce n'est pas une exclusion binaire de tout le rayon.
- Save, Close, Open : scène et clairière restaurées, état synchronisé.

Archive finale `81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json` : v3,
10 commandes, 2 802 538 octets, tête
`9ae4ce4515c90c71eb9196b4e10cd62241f31e89410f65fd775755c05bf0fb22`, fichier SHA-256
`0b3bf389156f08b067558d5c0240aeda84747fd1c95b47379429ac6c71e1452d`.
Lecture disque : masque identique avant/après verrou, locked=true ; seed finale 44 ;
618 height_deltas et 576 material_overrides conservés. Réouverture par le lecteur
produit valide le replay des 10 commandes et le hash final.
Hash terrain affiché après régénération et réouverture :
`e1c72262565ad989fef1838d7a6791840157ddcf56d854b9c030d1b919b2d3e4`.
Hash végétation projetée après régénération :
`61a32d4fc6ad08f7e99d27dc1d9a9663aa34dbb5ea59f39dfdc05bc26f309bd2`.
Source précédente conservée, SHA-256 `b3d6aa1be6646f494434b59ea62863764414a8091756186b8bac4917d820f643`.

Captures inspectées dans la conversation, pas de fichier PNG exporté. Ce replay
qualifie le parcours fonctionnel, pas le rendu artistique ni les performances.
Pas d'aperçu 3D de régénération : le panneau le dit explicitement. Reconstruction
végétale complète, pas encore par chunks. Les variantes restauration/déverrouillage
restent couvertes par tests de domaine, pas par ce replay UI. Gate 2 ouverte.
Studio PID 16624 et descendants 5176, 12144, 4116, 15152, 22172, 18336, 31472
fermés et absence vérifiée ; aucun navigateur utilisateur arrêté. Aucun push.

## Inspecteur de couche — 13 septembre 2026, postérieur au candidat 975b9033

Modèle de lecture `ZoneAuthoringHead::inspect_density` : validation de la tête,
sélection par ID, hash de tête et provenance du générateur/seed, masque recette
et masque effectif résumés (dimensions, min/max, échantillons zéro, empreinte),
override et verrou séparés. Aucune copie de samples ni nouvelle autorité persistante.
L'absence de masque signifie facteur spatial 100 %, pas une densité absolue ni un
nombre d'arbres. Les autres règles du générateur restent actives.

Studio : affichage réactif sur la couche sélectionnée, détails techniques repliés ;
peinture désactivée si couche absente/verrouillée, verrou disponible seulement sur
override modifiable, déverrouillage seulement si verrou, retrait seulement si
override non verrouillé. Les protections métier restent requises indépendamment
des boutons. Libellé cible 255 rectifié : « Remonter le masque vers 100 % dans le
geste », sans prétendre restaurer un masque de recette non uniforme.

Test `density_inspection` PASS : sans masque, héritage d'un masque non uniforme,
override, verrou/déverrouillage sans perte, autre couche inchangée, retrait exact,
IDs absents et absence de recette refusés, lecture immuable et sans samples JSON.
Clippy project all-targets et Studio WASM/web strict PASS ; collecte min/max/zéros
en un passage sans dépendance ajoutée, signatures empruntées corrigées.
Panneau compilé mais non lancé/capturé dans ce lot. Replay groupé avec le nouveau
journal agrégé encore dû. Aucun verdict R2-049 global ni Gate 2, aucun push.

### Replay de lecture de l'inspecteur

13/09/2026, candidat `e4e75bc6cf7806d15f103c008cfe3024b03ca8286619cccd4ae4b45d3794443a` :
Arbres sélectionné dans le Studio, affichage « retouche manuelle · verrouillée »,
recette sans masque spatial, masque effectif 257 × 257, 1 échantillon exactement
zéro, plage 0/255 à 255/255. Cette statistique ne mesure pas la surface de clairière.
Peinture, verrou redondant et retour recette désactivés ; déverrouillage disponible.
Capturé/inspecté dans la conversation. Aucun clic de mutation ni Save ; archive
inchangée. Source, dimensions et boutons visibles, provenance technique repliée.
Cleanup et SHA archive dans R2_AUTHORING_HISTORY.md. Autres couches et transitions
après mutation non rejouées graphiquement ici ; R2-049 reste partiel.
