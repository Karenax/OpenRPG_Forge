# ADR-0059 — Bloom, exposition et traitement final communs

Date : 27 octobre 2026. Statut : réalisation Gate 7, qualification en cours.

## Décision

Les neuf fichiers existants de `tidewater_plant_probe/source_final_post`
rejoignent `forge_engine::source_final_post`. La référence utilise directement
ce propriétaire, sans façade ni deuxième implémentation. Les cinq shaders,
leurs UUID, le calcul du bloom, la mesure d'exposition pour la frame suivante
et le traitement final restent identiques.

Le moteur expose `FinalPass`. L'adapter atmosphérique ordonne son passage
avant cet ensemble ; le moteur ne dépend plus de la crate de carte pour
connaître l'ordre. Le contrat temporel conserve ses vérifications de tag,
époque d'historique et completion, avant toute progression d'historique.
Toutes les pipelines restent exigées avant le ping-pong et les écritures.

Les options diagnostiques deviennent `FORGE_SOURCE_FINAL_TRACE`,
`FORGE_SOURCE_FINAL_CLOCK`, `FORGE_SOURCE_FINAL_CLOCK_SHA256` et
`FORGE_SOURCE_FINAL_HDR_CAPTURE`. Leurs schémas utilisent désormais
`forge.source.*`. Les preuves historiques restent inchangées ; un nouveau
rejeu doit fournir un document d'horloge sous ce nouveau nom, avec son hash.
Les limites de taille, nombre d'étapes, ordre, dt et fraction nocturne restent
identiques. Aucun format canonique d'auteur ou de package ne change.

## Qualification

Le plan central `shared-final-post-plan-001.json` épingle 14 originaux,
278 957 octets. `shared-final-post-transfer-001.json` consigne les neuf
déplacements, dont cinq shaders conservés octet pour octet. Les tests
vérifient l'ABI uniforme de 64 octets et les cinq programmes WGSL ; les
frontières interdisent toujours les dépendances du moteur vers une carte.
Clippy strict natif/Web et référence, deux tests moteur et 26 frontières passent
sur les sources 036 inchangées (`shared-final-post-validation-001.json`).
L'audit d'équivalence compare les neuf fichiers après les seuls changements
déclarés de propriétaire, options et ordre exprimé par ensemble. Les cinq
shaders restent identiques octet pour octet. Les sondes GPU restent dues.

L'extraction ne vaut pas activation dans le jeu de modules, mesure de
performances ou acceptation visuelle du profil Medium. Ces contrôles
restent dus avant clôture de Gate 7.

## Rejeu GPU et raccord du jeu de modules

Les sources 037 inchangées passent la sonde native sur deux scènes procédurales
Radeon/Vulkan : vingt PNG, témoins HDR identiques, six comparaisons du traitement
final/bloom/exposition et 227 étapes d'exposition GPU consécutives. Chaque valeur
« avant » retrouve exactement les bits « après » de la précédente. Huit images
sans grain restent identiques à la sonde 024. La trace observe une vue et ne se
dit pas appariée à un ordinal précis de capture. Le reçu central est
`shared-final-post-native-assessment-001.json` ; 22 artefacts, 542 304 octets.
Cela ne qualifie pas encore les matériaux réels du module ou sa reconstruction
temporelle.

Le raccord suivant est en qualification sur les sources 040. Le jeu consomme
`PluginSourceFinal` et ses réglages communs plutôt que le bloom et le traitement
final Bevy. L'identité de politique inclut les bits des réglages effectifs.
Les indicateurs bloom et couleur restent respectés ; le soleil du host commun
alimente la fraction nocturne. Les compteurs de soumission et dt/nuit ne sont
pas des réglages graphiques persistés.

Le passage lit une profondeur mono-échantillon : Low/Medium reprennent la voie
FXAA utilisée par la référence sans TAA, au lieu du MSAA 2 précédent de Medium.
High/Ultra gardent provisoirement leur TAA Bevy ; le raccord temporel commun
reste exigé séparément. Aucun verdict visuel Medium n'est acquis.

Un reçu d'encodage par vue suit la taille, les réglages, les frames et la
retraite des contrôles. Il est publié après les commandes du passage, jamais
sur la seule présence des composants. L'inventaire des effets exige ce reçu
frais et, lorsque demandé, la vraie pipeline FXAA. La barrière GPU existante
reste nécessaire : un reçu d'encodage ne constitue pas une completion GPU.

Sur 040 inchangé, cinq tests moteur, dix tests des profils, Clippy strict
natif/Web et les 26 frontières passent. Le second rejeu GPU confirme les
six comparaisons, des reçus frais pour les deux vues et leur retraite lorsque
les contrôles sont retirés. L'audit
`shared-final-post-module-algorithm-audit-001.json` compare aussi le corps des
gardes et calculs du rendu à l'original, après retrait de la seule publication
ajoutée : il reste identique, tout comme les cinq shaders.

Le premier rejeu du module réel, avec le candidat release 040, échoue avant
l'état prêt : la profondeur n'a pas l'usage `TEXTURE_BINDING` requis par le
passage final. Le reçu de commandes ne suffit donc pas à établir une exécution
GPU réussie. Ce refus reste conservé dans
`shared-final-post-profiles-native-001/cycle/game.log`.

Sur 041, le plugin commun prépare cet usage sur les seules caméras portant
ses contrôles, sans retirer leurs usages existants. Le passage et sa barrière
temporelle attendent une profondeur liée, mono-échantillon et de la bonne
taille avant de progresser. Les six tests moteur et Clippy strict moteur,
référence et Studio Web passent. L'audit `shared-final-post-module-algorithm-audit-002.json`
vérifie les gardes originales et calculs inchangés après exclusion de cette
précondition supplémentaire et de l'observation ; les cinq shaders restent
identiques. Le candidat dev 041 est scellé après vérification des mêmes
2 536 entrées avant/après compilation. Le rejeu `002` s'arrête au démarrage
avec `Mismatched pop_error_scope call` dans une tâche asynchrone wgpu. Le
rejeu `003` du même binaire passe le cycle Low/Medium/High/Ultra et la
réouverture Ultra, avec inventaires GPU prêts et préférences persistées.
Le plantage intermittent n'est pas résolu par ce PASS. Les cinq captures
sont inspectées ; le rejeu `004` passe à nouveau le cycle et la réouverture,
mais un rectangle noir reste visible sans menu dans les quatre profils, après
600 ticks en Medium et après réouverture Ultra. La validation visuelle est
refusée, sans cause attribuée à ce stade. Le bilan est
`shared-final-post-profiles-assessment-001.json`. Ces essais utilisent le
même paquet autonome et ne qualifient ni migration du Vault ni performances.

## Pixel HDR non fini — diagnostic du 28 octobre

Le candidat 043 ajoute une lecture GPU opt-in (`FORGE_SOURCE_FINAL_VIEW_PROBE`)
de trois zones : entrée/sortie du passage final et bloom, avec contrôles et
exposition de la soumission réelle 124. Le tampon est borné à 2 654 336 octets.
Il ne fournit aucune entrée au renderer. Six tests moteur, 26 frontières et
Clippy strict natif/Web passent ; les 2 537 entrées restent inchangées avant
et après build. L'audit `shared-final-post-view-probe-algorithm-audit-001.json`
retrouve le corps du rendu 041 après exclusion de la ressource et de la copie
diagnostiques ; les cinq shaders restent identiques.

Les lectures réelles High et Low trouvent chacune un pixel RGB NaN à l'entrée,
respectivement (348,355) et (348,353). Le bloom contient 28 900 puis 26 180
pixels non finis dans les zones lues. Le problème existe donc aussi au
démarrage sans TAA. Les captures écran ne sont pas appariées à cette
soumission : une image ultérieure High sans rectangle ne réfute pas la lecture
GPU précédente. Le bilan est `shared-final-post-nonfinite-assessment-001.json`.
Le plantage asynchrone `pop_error_scope` se reproduit sur le premier lancement
043, puis les lectures réussissent ; il reste ouvert.

La nervure de l'herbe utilise `pow(1-abs(across),6)`, dont une base négative
est hors du [domaine WGSL](https://gpuweb.github.io/gpuweb/wgsl/#pow-builtin).
La correction commencée sur 044 borne cette seule base à zéro dans le préparateur commun aux
matériaux natif et monde, en gardant la formule dans son domaine nominal.
Il refuse toute dérive de l'expression acquise. La correction vise un
dépassement de borne par interpolation ; sa responsabilité exacte dans le
pixel observé et le résultat GPU restent à confirmer. Aucun clamp HDR général,
changement des données auteur ou des cinq shaders finaux n'est effectué.
Le premier Clippy refuse un argument `String` non consommé ; la fonction
emprunte désormais `&str` sur les sources 045. La validation et le build de ce
candidat restent en cours, sans modification de la réparation mathématique.

Le rejeu réel 045 contredit cette hypothèse : un pixel RGB NaN demeure en
entrée et 28 900 pixels non finis dans le bloom lu. Les 68 tests courants,
le test explicitement fourni du shader réel, Clippy natif/Web et les 26
frontières passent, mais ne prouvent pas une correction GPU. La tentative
est retirée du code actif ; le manifeste 046 retrouve exactement les 2 537
entrées et 283 842 569 octets de 043. Le patch ciblé et le bilan refusé
`shared-grass-midrib-hypothesis-001.json` restent conservés. Le calcul qui
introduit le pixel HDR invalide doit être identifié plus précisément.

La sonde étendue ajoute une copie avant post-traitement, appariée par vue,
frame et taille à la soumission finale, ainsi que le tampon de matrices
effectivement lié et la profondeur. Le candidat 048 révèle un usage `COPY_SRC`
manquant sur la profondeur Low ; 050 le prépare seulement pour la sonde,
mais sa copie partielle `Depth32Float` est refusée par la validation GPU.
Ces erreurs du diagnostic restent distinctes du défaut HDR initial et des
évolutions de stockage des assets. Le candidat 052 utilise une copie complète
de profondeur du fixture 1280×800 puis un découpage CPU après lecture ; son
tampon temporaire est borné à 7 933 952 octets. Sept tests moteur passent.
Le résultat GPU de cette version reste à établir. Les cinq shaders et leurs
calculs demeurent inchangés ; le diagnostic ne qualifie pas les performances.

Le rejeu GPU Low `shared-final-post-view-probe-011` de 052 passe : le même
pixel RGB NaN (348,355) est présent avant post-traitement et à l'entrée finale,
sur la vue et frame 127 appariées. Sa position reconstruite depuis la
profondeur et les matrices GPU est proche de (0,983046 ; 3,004554 ; -97,134101).
Le défaut précède FXAA dans ce rejeu ; le matériau responsable reste à
identifier. Le bloom contient encore 28 900 pixels non finis dans la zone lue.
Clippy strict natif/Web et le manifeste avant/après build passent également.

Le candidat 053 ajoute seulement un opt-in qui active le mode blanc de
couverture déjà acquis sur les trois matériaux d'herbe. Les deux exécutions
Low normale/couverture `012`/`013` conservent le pixel non fini (348,355),
sa profondeur et les matrices caméra identiques bit à bit, bien que 30 814
pixels soient effectivement blancs en couverture contre zéro normalement.
Ce contre-essai n'élimine donc pas le défaut ; il n'identifie pas encore son
matériau (`shared-final-post-grass-coverage-assessment-001.json`). Clippy
natif/Web, sept tests et le manifeste du build passent. Les frames et
expositions des deux lancements diffèrent ; aucune parité globale des images,
qualification visuelle ou correction du rendu n'est déduite de ce contrôle.

Le contre-essai terrain de 055 active à son tour le commutateur blanc existant,
sans changement des shaders. Il est effectif sur 83 887 pixels du crop lu,
contre zéro normalement, mais le pixel NaN (348,355), sa profondeur et les
matrices restent identiques dans les deux exécutions de frame 127. Le bloom
contient toujours 28 900 pixels non finis
(`shared-final-post-terrain-coverage-assessment-001.json`). Clippy natif/Web,
sept tests et le manifeste du build passent. La surface responsable reste à
identifier ; aucune correction des équations terrain n'est déduite de cet essai.

Les contre-essais plantes 057 `016`/`017` éliminent les NaN en remplaçant la
sortie des plantes hors herbe par leur couverture magenta. Les sorties albédo
et normale `018`/`019` restent invalides en deux pixels. La sonde de domaine
058 `020` y lit un tronc à hauteur positive mais à Y local négatif. La formule
d'écorce `pow(yn,2.2)`, avec `yn=y/H`, est alors hors domaine réel ; elle est
utilisée tant pour la couleur que pour la perturbation de normale. Le candidat
059 remplace seulement sa base par `max(yn,0)` sur les deux chemins communs
de plantes. Le profil acquis est validé avant dérivation et reste inchangé.
Cette extension conserve la formule dans son domaine initial et prolonge son
terme de courbure par zéro sous l'origine. La validation GPU reste requise ;
aucun assainissement global des buffers HDR n'est ajouté au traitement final.

Le rejeu 059 confirme cette réparation ciblée : `021`/`022`/`023`/`024`
Low/Medium/High/Ultra ne contiennent aucune composante RGB non finie dans les
copies avant post-traitement, entrée, sortie et bloom. Le rectangle opaque du
témoin Low `016` est absent des quatre captures inspectées. Le bilan
`shared-plant-bark-domain-assessment-001.json` vérifie les plans, le binaire,
les entrées et les cinq shaders finaux inchangés. Clippy natif/Web, 77 tests
moteur dont le shader acquis réel et 26 frontières passent. La portée reste
celle de cette scène et de ses copies GPU bornées : le chemin temporel source
complet, la qualification visuelle générale et les performances restent dus.
