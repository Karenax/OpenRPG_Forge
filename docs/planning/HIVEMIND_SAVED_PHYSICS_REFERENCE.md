# Réouverture des collisions des assets importés

## Nettoyage natif vérifié — v900

Les deux tests du host source v871 et les quatre tests du montage persistant
v893 passent sur les modules réels. Le check WebGPU v892 passe également.
Le scellement v900 revalide 1138 entrées distinctes de leurs sources sans
changement. La preuve couvre le nettoyage des collisions/nodes détenus, la
préservation des entités étrangères et du root d'auteur, la composition des
transformations et le refus d'un repère monde implicite. Elle ne couvre pas
encore admission GPU, rendu, simulation ou parcours GUI.

Preuve : `F:/OpenRPG_ImportWork/native-ownership-and-vault-delta-delivery-v900/RESULT.v1.json`.
Les attentes de compilation ci-dessous sont historiques.

## Nettoyage des nodes détachés corrigé — v891 à v894

Les nodes dérivés explicitement détenus sont désormais retirés même s'ils ont
perdu leur parent avant la disparition du root. Une pose non inversible nettoie
également les dérivés et refuse le montage, en conservant le root d'édition.
Le check WebGPU v892 passe avec 1128 entrées inchangées. Les quatre tests réels
du module de placement restent en cours dans v893 : nettoyage avec contrôle
d'ownership, pose invalide, composition parent/enfant et repère monde explicite.
Le banc fournit seulement les labels de schedule UI et le hook d'annulation
pour isoler ces modules ; il ne constitue pas une preuve de parcours Studio.
L'essai v891 interrompu pour configuration de dépendance incorrecte est conservé.
Preuve d'avancement :
`F:/OpenRPG_ImportWork/persistent-source-cleanup-progress-v894/RESULT.v1.json`.

## Montage persistant raccordé, compilation vérifiée — v888 à v890

Le chemin `ManualPlacement` distingue désormais les carriers source déclarés du
chargeur GLB ordinaire. Le paquet physique sauvegardé est chargé par son asset et
ses pins modèle/manifeste/paquet complet. Le montage conserve le root d'édition
commun ; ses nodes et collisions dérivés possèdent un propriétaire explicite.
Un changement de pose ou de contacts acceptés reconstruit ces dérivés ; le retrait
du root libère les collisions détenues. La conversion de repère monde doit être
déclarée explicitement, indépendamment de la conversion locale de l'objet.

Le check WebGPU v889 passe sur 1128 entrées épinglées ; v890 les revalide et scelle
les preuves. Cela reste une preuve de compilation : les roots source restent
masqués, en attente d'admission GPU et de qualification du rendu. Focus/picking,
matériaux ordinaires mixtes, simulation native et parcours GUI restent à vérifier
ou raccorder. Aucun asset complet supplémentaire n'est qualifié.

Le census v881 terminé compte 13 673 GLB pour 14 219 fiches sans déclaration
`OPENRPG_source_graph`. L'absence de déclaration ne prouve ni fidélité à Unity ni
admission du chargeur. Preuve consolidée :
`F:/OpenRPG_ImportWork/placement-and-census-delivery-v890/RESULT.v1.json`.
Les sections suivantes conservent leur état historique daté.

## Cache historique raccordé — v884 à v886

Le lecteur historique utilise maintenant un cache indexé par asset, carrier,
manifeste et paquet complet. Chaque insertion vérifie le paquet ; un échec ne
retire aucune entrée déjà vérifiée. La rétention est limitée à 128 Mio de paquets
et 32 entrées. Les octets sont partagés par `Arc` : l'éviction du cache conserve
les handles détenus par les consommateurs. Un paquet plus grand que le budget
peut être lu et validé, mais reste hors cache. Les requêtes recontrôlent le cache
après l'attente du permis IPC. Les trois tests du module réel v885 passent et le
check WebGPU v886 passe avec 1127 fichiers épinglés inchangés. Ce branchement sert aux détails historiques ; le
montage persistant commun reste à raccorder. Preuves et sauvegardes sur F :
`saved-physics-cache-v884`, `saved-physics-cache-tests-v885` et
`saved-physics-cache-wasm-v886`.

## Chargement physique conservant les pins — v877/v878

Le payload transporté conserve désormais le SHA du modèle, du manifeste et du
paquet complet. Le lecteur historique renvoie les octets avec ces trois pins ;
la fiche de détails utilise ce lecteur. L'aperçu revalide le paquet contre les
octets du modèle juste avant de créer le root et ses collisions. La compilation
WebGPU v878 passe avec 1126 entrées épinglées inchangées. Cela ne raccorde pas encore le chargement persistant
des `ManualPlacement`, ni leur cache ou leur simulation. Sauvegardes et contrôles :
`F:/OpenRPG_ImportWork/saved-physics-payload-v877` et
`F:/OpenRPG_ImportWork/saved-physics-payload-wasm-v878`.

Lot suivant de l'import Hivemind. Ce document décrit le travail restant ; il ne
qualifie ni sa réalisation ni les assets. Les preuves v755/v758 concernent la
persistance des paramètres de scène, l'adaptateur de contact et la compilation.

## Problème à résoudre

Le problème initial était que `ManualPlacement` sauvegardait seulement l'identité
et le SHA du modèle. La référence physique optionnelle est désormais persistée ;
le montage commun et le parcours produit restent à réaliser et qualifier.
`Library::read_historical_model` retrouve un ancien GLB dans les snapshots de pack
épinglés. Le lecteur historique physique et ses trois pins existent désormais ;
ils servent à l'inspection et transportent les octets jusqu'au futur host.
Le chargement persistant doit encore appeler ce lecteur pour chaque référence
physique sauvegardée. Le parcours des placements refuse toujours le chargement
source graph. Ce refus doit être remplacé par un parcours fidèle et vérifié,
sans supprimer les contrôles.

## Référence persistante et lecture

- Ajouter une référence physique optionnelle à la donnée de placement commune,
  avec empreintes du manifeste et du paquet. Le SHA du carrier reste le SHA du
  modèle du placement. Aucun chemin disque, handle Rapier ou entité Bevy persisté.
- Le champ absent reste omis. Les placements existants, IDs, retouches, tombstones,
  transformations et formats historiques sont conservés. Les archives contenant
  ce nouveau champ ont leur propre version ; les anciens en-têtes doivent refuser
  les données nouvelles au lieu de les accepter silencieusement.
- Lors de la sélection, reprendre les empreintes du profil réellement épinglé et
  du paquet vérifié. Sauvegarder les mêmes empreintes que celles utilisées pour
  l'aperçu ; ne pas découvrir un paquet en explorant le disque ou par nom de prefab.
- Pour la réouverture, requête typée par identité et empreintes uniquement. Rechercher
  le modèle et le profil associés dans la révision courante puis les snapshots
  historiques du registre. Un SHA de modèle identique ne suffit pas lorsque les
  empreintes physiques diffèrent : rechercher le profil exact ou refuser.
- Réutiliser validation des métadonnées, confinement des chemins, licence, SHA des
  fichiers, limite des révisions et budgets du lecteur natif. Lire et vérifier le
  registre avant et après la réponse. Une disparition, corruption ou modification
  concurrente ne doit pas produire une réponse mélangée.

## Projection dans Studio

- Charger le modèle source et son paquet sur le même `SourceSceneHost`, avec les
  propriétaires de composants d'origine et les mêmes objets d'édition communs.
- Résoudre les paramètres physiques depuis la tête acceptée de la scène. L'absence
  de paramètres requis reste un refus explicite ; aucune matrice ou épaisseur
  implicite dérivée de l'asset. Le contact offset source ne devient pas contact skin.
- Monter avant admission du rendu, conserver les handles dans le résident commun,
  nettoyer lors d'un retrait/remplacement et reconstruire après modification des
  paramètres acceptés. Ne pas perdre les collisions lors d'un déplacement ou undo.
- Le rendu, les shaders, LOD et simulation doivent conserver leurs contrôles
  d'admission. Un montage physique réussi ne rend pas une source masquée visible.

## Preuves attendues par sous-lot

1. Domaine : insertion, duplication, remplacement de référence, transformation,
   suppression, undo/redo et réouverture des trois formats ; anciennes archives et
   hashes inchangés lorsque le champ est absent ; références malformées refusées.
2. Lecteur : anciens modèle/profil/paquet réouverts après une mise à jour de pack,
   recherche affichant seulement la carte courante ; mêmes carriers mais paquets
   différents distingués ; chemins, licence, pins, budgets et courses refusés.
3. Montage : collisions attachées aux nodes du carrier sauvegardé, sensor/enabled
   conservés, changement de paramètres invalidant le montage, retrait sans objet
   physique orphelin, retouches et sélection commune conservées.
4. Produit réel : sélectionner depuis la bibliothèque, régler les collisions,
   appliquer une pose, sauvegarder, fermer, rouvrir, retrouver et modifier l'objet.
   Vérifier images/filtres/métadonnées et rendu sur les variantes concernées.

Chaque sous-lot conserve sources, licences, captures, hashes et refus. Aucun
verdict d'import complet avant les preuves applicables de l'éditeur et du runtime.
Les autres packs, rigs, animations, effets et images restent dans l'objectif total.

## État et points de raccordement observés le 11 octobre

Lot v865-v868 en vérification : le résident fournit une libération explicite
des collisions détenues avant retrait d'un root. L'annulation d'un aperçu appelle
cette libération ; un root disparu nettoie aussi les enfants physiques survivants.
Un échec d'installation des routes d'ombre après montage physique annule les
collisions avant retrait du root. Deux tests des modules réels et la compilation
WebGPU sont lancés, sans verdict anticipé. La bibliothèque Studio native ordinaire
exclut ces modules UI : elle ne peut pas, seule, exécuter ces tests. Ce lot ne
raccorde pas encore le chargement source aux placements persistants et ne qualifie
ni simulation, ni rendu, ni GUI. État :
`F:/OpenRPG_ImportWork/source-resident-release-progress-v868/RESULT.v1.json`.

Lot v864 vérifié sur les sources courantes : le test refuse un autre paquet
valide avec le même carrier/manifeste et des buffers différents ; Clippy
Studio natif passe avec `-D warnings` ; Studio WebGPU compile. Les pins
de chaque contrôle ont été réouverts et rehashés sans changement. La
sélection, les détails, l’aperçu et la réouverture sauvegardée vérifient
le SHA du paquet complet avant décodage. Preuve :
`F:/OpenRPG_ImportWork/physics-package-pin-delivery-v864/RESULT.v1.json`.
La compilation WebGPU conserve ses warnings ; aucune qualification de GUI,
montage persistant, simulation ou import complet n’est déduite de ce lot.

Relevé antérieur v855-v860, avant clôture technique du lot v864 : sélection, aperçu et lecture historique utilisent
désormais le décodage épinglé par le SHA du paquet complet, en plus du carrier
et du manifeste. Le champ du profil courant est `source_physics_package.sha256` ;
la requête historique conserve `package_sha256`. Un test soumet un autre paquet
valide ayant les mêmes carrier/manifeste et des buffers différents. Test natif,
compilation WebGPU et Clippy restent à terminer avant de qualifier ce lot.
Les contrôles initiaux v856/v857 ont été volontairement annulés pour corriger
le nom du champ courant ; leur état et leur motif sont conservés sous
`F:/OpenRPG_ImportWork/physics-selection-package-pin-v855/PENDING.v1.json`.

Mise à jour des lots v822-v831 : l'inspecteur affiche la référence physique
sauvegardée et demande sa vérification au même IPC natif. Le SHA du paquet reçu
et les pins carrier/manifeste sont vérifiés avant décodage. Compilation Studio
WebGPU réussie sous `F:/OpenRPG_ImportWork/saved-physics-inspector-wasm-v823`.
Ce contrôle ne monte aucun collider et ne modifie pas la tête de scène.

Le remplacement par un asset ordinaire du catalogue demande désormais une case
explicite de retrait du paquet physique. Le lot commun exclut les objets qui ont
le même carrier mais d'autres pins physiques. Les deux tests du helper réel et
du domaine passent, y compris l'undo qui restaure les références et retouches,
sous `F:/OpenRPG_ImportWork/reviewed-replacement-ui-tests-v831` ; compilation
WebGPU sous `F:/OpenRPG_ImportWork/reviewed-replacement-ui-wasm-v828`.
La sélection d'un nouveau paquet physique depuis le Vault, son montage persistant,
la simulation et le parcours graphique réel restent non qualifiés.

La décision de réutilisation d'un root compare maintenant identité, SHA du modèle
et référence physique complète. Transformer, renommer ou verrouiller sans changer
ces références conserve le root ; changer le paquet demande une reconstruction.
Ce comportement ne prouve pas encore le nettoyage et la reconstruction effective
de colliders persistants.

Mise à jour v812-v814 : les 25 tests ciblés `placement_physics`,
`manual_placements` et `source_object_transitions` passent sur la dernière
extraction des remplacements. Clippy domaine et test physique passe sans
neutralisation de lint (151 entrées épinglées inchangées). Studio WebGPU compile
sur ces mêmes sources (1 124 entrées inchangées). Les résultats et logs sont sous
`F:/OpenRPG_ImportWork/placement-reviewed-physics-{tests-v812,clippy-v813,wasm-v814}`.
La comparaison de réutilisation inclut désormais les pins physiques ; la commande
`ReplacePinnedAsset` et ses dispatchs de provenance/planning sont implémentés.
Ces contrôles ne prouvent ni le montage persistant, ni le parcours de remplacement
dans l'interface, ni une qualification visuelle ou runtime. Les observations
chronologiques ci-dessous conservent les étapes et refus antérieurs.

La lecture historique est ajoutée au même IPC `read_asset_physics` : requête
`LibrarySavedPhysicsRequest`, profil exact courant ou historique, vérification du
carrier/manifeste/paquet et des registres. Banc v772 réussi sur 24 lectures courantes,
24 historiques et 121 refus, uniquement dans une copie privée. La référence
`PhysicsPackageReference` est ajoutée à `ManualPlacement` ; domaine/archives v771 et
compilations v773/v774 restent en contrôle à ce relevé. Aucun parcours Studio réel
qualifié par ces preuves.

### Observations historiques avant les lots v812-v831

Les points ci-dessous conservent leur état au relevé initial. Les corrections
décrites ci-dessus remplacent leurs constats sur la comparaison des pins et le
retrait explicite lors du remplacement. Les lacunes de montage, de cache physique
et d'invalidation par les paramètres acceptés de scène restent à raccorder.

- `authoring_placement_reuse::retained` compare seulement `asset_id` et
  `asset_hash`. Ajouter les pins physiques à cette décision : même modèle et
  paquet différent doit reconstruire le root. Transformer/renommer/verrouiller
  sans changement des pins doit le garder.
- `placement_controls::insertion::prepare` crée encore `physics: None`.
  `VaultActions` refuse actuellement la pose d'un modèle avec paquet. Reprendre
  la référence vérifiée depuis la sélection puis la transmettre à la commande
  commune, sans écrire directement dans la tête ou injecter une seconde scène.
- `placements::Prepared` et le résident mettent en cache les modèles par
  identité/SHA uniquement. Partager les octets du carrier reste possible, mais
  les paquets doivent rester distingués par leurs propres empreintes ; deux
  objets avec le même modèle et des collisions différentes ne doivent pas
  partager par erreur la même politique ou les mêmes handles.
- `placements::prepare` refuse toujours la route source graph. Le montage devra
  utiliser le host et les paramètres acceptés de scène, avec nettoyage lors du
  retrait et conservation des outils communs d'édition. Ne pas retirer ce refus
  avant d'avoir ce parcours ; ne pas utiliser le chargeur ordinaire pour contourner
  l'admission du rendu source.
- Le remplacement ordinaire d'une référence physique est encore refusé dans
  le domaine. La commande `ReplacePinnedAsset` est ajoutée au lot v800 : garde
  sur les anciens pins modèle/paquet, nouvelle référence complète avec absence
  de paquet explicite, mêmes retouches et inverse par le journal commun.
  Tests/Clippy/WebGPU v801-v803 en contrôle, sans verdict anticipé. Raccorder
  ensuite la sélection et la proposition revue dans l'interface ; la commande
  seule ne prouve pas le chargement, le montage ni la visibilité d'un asset.

### Cycle de vie observé avant raccordement du montage (v797)

Le contrôle des sources a identifié trois points supplémentaires. Ce sont des
constats de code, sans preuve de simulation ou de parcours Studio réel ; leurs
empreintes sont conservées sous
`F:/OpenRPG_ImportWork/saved-physics-mount-lifecycle-audit-v797`.

- `placements::show` retourne lorsque la liste désirée reste identique et que
  le chargement précédent n'a pas échoué. Sa clé ne comprend pas les paramètres
  physiques acceptés de la scène. `source_graph_physics::update` change la
  ressource de contacts et annule l'aperçu temporaire ; cela ne constitue pas une
  reconstruction des collisions des placements persistants. Le raccordement
  doit invalider leur montage lors d'un changement de matrice, skin ou preuve,
  y compris après undo/redo, et refuser proprement la suppression des paramètres
  nécessaires sans conserver des collisions calculées avec l'ancien état.
- Les résidents du runtime conservent la politique fournie lors de `retain`.
  Une retouche de transformation conserve le root commun : le raccordement doit
  aussi mettre à jour les mappings source et les poses physiques, sans dupliquer
  le root ni perdre sélection, ID, nom ou verrou. Tester déplacement, rotation,
  échelle admissible et undo/redo avec les mêmes références physiques.
- `world_mapping` traite explicitement un offset de présentation. Le mapping
  d'un placement persistant doit être dérivé du carrier et de sa transformation
  complète, avec contrôles affine/inverse, selon le contrat de coordonnées source.
  Ne pas réutiliser le décalage de l'aperçu comme position d'auteur ni supposer
  que la translation seule couvre une transformation orientée ou mise à l'échelle.

Ces cas complètent le sous-lot montage ; ils ne remplacent pas les critères de
rendu, sauvegarde/réouverture et qualification de chaque asset.
