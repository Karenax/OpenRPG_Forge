# Forge Delivery

Commandes natives partagées par le launcher, l'installation locale et le futur client.
Implémentation préparatoire de [ADR-0021](../../docs/ADR/ADR-0021-detached-delivery-installation.md).
Workspace Cargo isolé pour ne pas modifier/reconstruire les composants moteur en cours.

Le lecteur privé `vault_storage` et les exports `pack*` prennent en charge
le stockage partagé sans perte, selon [ADR-0058](../../docs/ADR/ADR-0058-shared-vault-storage.md).
Les paquets exportés contiennent les fichiers autonomes reconstruits. Voir le
[guide](../../scripts/assets/README.md) pour publication, migration et reprise.
Ce support ne signifie pas que la bibliothèque physique a déjà été migrée.

## Flux

`index → archives vérifiées → plan lié à la tête → Apply → packages immuables → tête atomique`

La première installation exige les applications, leurs assets et leur module exacts.
Un pack de bibliothèque optionnel peut s'ajouter ultérieurement sans réinstaller les applications.
L'éditeur reçoit le Vault installé via `OPENRPG_ASSET_VAULT` et le workspace utilisateur
via `FORGE_AUTHORING_ROOT`. Aucun fichier du dépôt ou du Vault source n'est écrit.

## Interfaces

```text
forge_delivery pack SOURCE DESCRIPTOR OUTPUT
forge_delivery pack-selected SOURCE DESCRIPTOR OUTPUT FOLDER...
forge_delivery pack-list SOURCE DESCRIPTOR OUTPUT SELECTION_JSON
forge_delivery inspect PACKAGE
forge_delivery orpmod-validate SOURCE [--publish]
forge_delivery orpmod-pack SOURCE DESCRIPTOR ABSOLUTE_OUTPUT.orpmod [--publish]
forge_delivery orpmod-inspect PACKAGE [--publish]
forge_delivery plan INSTALL_ROOT PACKAGE...
forge_delivery plan-file INSTALL_ROOT ABSOLUTE_PLAN_FILE PACKAGE...
forge_delivery apply PLAN_JSON
forge_delivery uninstall INSTALL_ROOT
forge_delivery status INSTALL_ROOT
forge_delivery verify INSTALL_ROOT
forge_delivery rollback-plan INSTALL_ROOT
forge_delivery launch INSTALL_ROOT game|editor
forge_delivery launch-plan INSTALL_ROOT launcher|game|editor
forge_delivery modules INSTALL_ROOT
forge_delivery module-check PACKAGE AREA_ID ABSOLUTE_CACHE_ROOT
forge_delivery module-launch-plan INSTALL_ROOT MODULE_ID AREA_ID
forge_delivery module-launch INSTALL_ROOT MODULE_ID AREA_ID
forge_delivery recover-legacy-modules LEGACY_ROOT
forge_delivery import-legacy-save GAME_ROOT LEGACY_ROOT PROJECT_ID MODULE_ID AREA_ID
forge_delivery import-recovered-save GAME_ROOT LEGACY_USER_ROOT PROJECT_ID MODULE_ID AREA_ID
forge_delivery launcher INSTALL_ROOT
```

Sorties JSON et erreurs JSON sur stderr ; succès exit 0, erreur exit 1. `plan` et
`rollback-plan` n'appliquent rien. `setup ROOT PACKAGE...` présente le plan et demande
`oui` avant d'appliquer ; le hub offre le même aperçu sous forme graphique.

`modules` découvre les modules v2 admis dans la tête active et conserve un diagnostic
par package refusé. Le gestionnaire de contenu propose une action Jouer pour chaque
zone admise ; le launcher Aube et la recette de l'aventure de référence restent ceux
de l'installation. Les modules v1 sans `module.json` conservent leur parcours existant.
`module-launch-plan` est en lecture seule ; `module-launch` reconstruit et vérifie
son plan côté natif à partir des seuls `ModuleId` et `AreaId`. Il utilise l'entrée
`module-game` inventoriée dans les applications installées, ou `game` pour les
anciennes installations qui n'ont pas cette entrée. Il conserve un environnement OS borné et les
sauvegardes séparées par module/carte, sans commande shell ni chemin reçu du frontend.

Avant de retirer l'installation historique, `recover-legacy-modules` conserve
ses modules actifs et ceux du retour arrière dans `recovered-modules` sous
le dossier utilisateur épinglé. Les `.orpmod` sont importables dans le Studio
selon son contrat d'admission. Les anciens paquets de contenu sans `module.json`
gardent l'extension `.forgepack`. Le reçu JSON donne le fichier, ses empreintes
et ses droits. Une relance vérifie les copies déjà présentes et refuse une
archive altérée sans la remplacer.

`import-legacy-save` est une opération explicite de migration entre deux
installations. Elle vérifie le projet, le module et la version de la carte
dans les deux têtes actives, puis copie la sauvegarde historique vérifiée
vers l'espace du jeu exporté. Une copie identique renvoie la même empreinte.
Une sauvegarde différente déjà présente à destination est conservée et
l'opération est refusée.

Après retrait de l'ancienne installation, `import-recovered-save` utilise le
dossier utilisateur conservé. Le module récupéré dans `recovered-modules`
doit correspondre au projet, au module et à la tête de carte du jeu exporté.
Le launcher recherche ce dossier dans le profil du Studio enregistré, puis
dans le profil Forge par défaut si le premier ne contient pas de récupération.
Le CLI accepte le chemin explicite d'un profil personnalisé conservé.

Le jeu reçoit trois pins distincts avant de construire la scène : `--expected-head`
épingle la tête authorée, `--expected-archive-sha256` les octets du document d'archive
de cette zone, et `--expected-package-manifest-sha256` le descripteur canonique du
transport installé avec son inventaire complet. Le deuxième ne désigne pas le hash
du fichier `.orpmod`. Une substitution valide du descripteur avec une zone identique
reste refusée par le troisième pin.

`module-check` utilise le même chargeur sans démarrer le GPU. Il rend les IDs, pins
de tête/zone/descripteur et la racine admise ; le cache explicite peut recevoir une
révision dérivée neuve, les données source restent en lecture seule. Le JSON indique
`gpu_started=false` et `runtime_qualified=false` : cette admission ne prouve pas le jeu.

Le builder natif propose `build_zone_with_origin` pour réexporter une carte importée.
Le host sélectionne les octets du package original et leur SHA-256 depuis l'ancre
canonique du workspace ; le builder vérifie ce même tampon, son module et ses droits
avant de créer une sortie. Les manifestes, licences, preuves et crédits originaux
voyagent sans modification sous `_forge-export/origin/`, avec le hash du package
d'origine et la tête retouchée. La licence saisie concerne les nouveaux apports ;
une origine `private_local_only` interdit un réexport redistribuable.
L'admission mémoire des origines est bornée à 256 Mio ; `build_zone` conserve son
contrat pour les créations sans origine importée. La lecture fichier des grands
packages reste un autre chemin d'admission.

`plan-file` écrit le plan canonique dans un chemin absolu avec `create_new` et ne remplace
jamais un fichier existant. `uninstall` prend le même verrou natif que l'installation,
valide les manifests dont le hash correspond au dossier de révision, puis retire seulement
les fichiers dont la taille et le SHA-256 correspondent. Les fichiers modifiés, étrangers
et toutes les données sous `user_root` sont conservés. Il supprime la tête
`installation.json`; `installation.lock` reste en place pour conserver le même inode entre
deux opérations concurrentes. Une installation déjà inactive renvoie un rapport JSON nul
sans créer de verrou ni supprimer de fichier.

Racine par défaut : `%LOCALAPPDATA%/OpenRPG Forge/Installation`.
Sous Linux : `$XDG_DATA_HOME/openrpg-forge/installation` ou
`$HOME/.local/share/openrpg-forge/installation`, avec `userdata` pour les données
utilisateur ; aucune variable Windows n'est requise.

Les archives `linux-x64` sont inspectables depuis Windows. Leur installation,
comme celle des archives `windows-x64`, exige l'hôte et l'architecture compatibles.
Les paquets `any` gardent leur rôle indépendant de l'hôte. Sous Unix, seuls les
points d'entrée applicatifs déclarés deviennent exécutables (`0755`) ; les autres
fichiers restent à `0644`. Les droits source ne sont pas transportés et les modes
spéciaux ou bits d'exécution altérés sont refusés à la vérification. Voir ADR-0037.
Données utilisateur : `%LOCALAPPDATA%/OpenRPG Forge/UserData`.
Les variables `FORGE_DELIVERY_INSTALL_ROOT` et `FORGE_DELIVERY_USER_ROOT` permettent
les installations/probes isolées. Le chemin utilisateur est épinglé à la première installation.

## Transport v1

8 octets ASCII `FORGEPK1`, longueur du manifeste sur u64 little-endian, JSON UTF-8
émis par le sérialiseur du contrat, puis fichiers dans l'ordre du manifeste.
Pas de compression, scripts d'installation tiers ou extractions de métadonnées système.
Lecture streaming 64 Kio ; limites 64 Mio/manifeste, 250 000 fichiers, 16 Gio/fichier,
256 Gio/package. Archive tronquée, octets supplémentaires, path aliases Windows,
liens/junctions, fichiers non listés et conflits fichier/dossier sont refusés.

Schemas : `forge.delivery.package.v1`, `forge.delivery.product.v1`,
`forge.delivery.plan.v1`, `forge.delivery.installation.v1`, `forge.delivery.index.v1`.
Formats initiaux expérimentaux, figés pour la première livraison de ce chantier.

## Admission sémantique `.orpmod` v2 — première tranche Gate 4

Le conteneur streaming `FORGEPK1` est réutilisé, avec un document fermé
`module.json` (`forge.module-package`, version 2). Un `.forgepack` historique
renommé ne devient pas un module v2. Le manifeste décrit les archives auteur
canoniques, sans les convertir en un autre modèle. Voir
[ADR-0024](../../docs/ADR/ADR-0024-orpmod-v2-semantic-admission.md).

`orpmod-validate` contrôle les fichiers du dossier ; `orpmod-pack` revalide le
conteneur terminé avant sa publication ; `orpmod-inspect` vérifie les payloads
par flux sans les extraire. L'installation revalide les modules portant
`module.json` dans le staging, avant activation de la tête. Les anciens packs
conservent leur contrat v1. Le descripteur externe doit porter le `ModuleId`
comme ID, la même version et provenance, les mêmes droits/licences et `platform=any`.

Le catalogue commun `forge.governed-import-catalog` v1 épingle chaque média,
source durable et vignette, avec provenance et droits distincts local/publication.
Les preuves de droits sont embarquées et hashées. `--publish` refuse les modules
privés et les assets dont la redistribution n'est pas explicitement autorisée.
Une déclaration de droits ne remplace pas leur établissement par l'auteur.

Premiers contrôles de format : GLB v2 autonome par le préflight existant, signatures
PNG/JPEG et WAV/Ogg/FLAC. Ils ne constituent pas des décodeurs complets ni une
qualification visuelle/audio. Les chemins internes sont ASCII et portables ;
les noms affichés et les destinations d'installation conservent Unicode.
Les recettes et sources authorées sont des fichiers `asset_auxiliary`, pas des
assets médias supplémentaires. Tout auxiliaire doit être référencé par le catalogue.

Cette tranche ne raccorde pas encore le sélecteur de module au launcher, les
imports Studio ou le chargement runtime. Les catalogues d'espèces, prototypes de
végétation importés et packages annexes physiques/particules ne sont pas résolus
par ce premier contrat : leur présence est refusée explicitement jusqu'au raccord.
Le second module testé ici est une fixture de transport ; le second module jouable
et R4-P01/R4-P02 restent à qualifier selon le
[protocole Gate 4](../../docs/testing/GATE4_PACKAGE_ACCEPTANCE.md).

Les substitutions d'arguments sont `{app}`, `{install}`, `{user}`, `{workspace}` et `{pack:ID}`.
Une dépendance requise épingle ID/type/hash ; un pack optionnel peut seulement alimenter
une variable d'environnement. Si absent, cette variable est retirée du processus enfant,
sauf le Vault externe explicitement sélectionné dans les données utilisateur.
Une collection privée locale peut être raccordée sans copier ses assets ni modifier les
packages : `user_root/asset-vault.v1.json` contient
`{"schema":"forge.delivery.asset-vault.v1","path":"F:/OpenRPG_AssetVault"}`.
Le chemin doit être absolu, exister et contenir le dossier `metadata`. Ce réglage est prioritaire
pour les applications déclarant `OPENRPG_ASSET_VAULT` ; les autres applications ne le lisent pas.
Une configuration invalide produit une erreur au lancement au lieu d'utiliser une autre
collection silencieusement. Les lecteurs Forge conservent leurs vérifications de catalogue.
Les arguments ne sont pas interprétés par PowerShell/cmd. Le module et ses IDs sont des
données du produit, jamais des branches de comportement dans l'installateur.
Les processus lancés écrivent leurs journaux sous le dossier utilisateur `logs` ;
ils ne retiennent pas les pipes du CLI appelant.

## Sources immuables des modules

L'API de module 4 ajoute les environnements explicites par aire (ADR-0047).
Le lecteur vérifie les index et la fermeture des ressources avant de charger
les payloads, puis expose les codecs communs de `forge_assets`. Il ne recherche
aucune ressource dans le workspace ou le Vault. Les anciennes API conservent
leur sérialisation sans ce champ. Le transport autonome et la corruption sont
testés ; l'exécution native, l'éclairage des surfaces et la parité restent des
qualifications séparées. L'export depuis une origine avec environnement est
refusé tant qu'il ne peut pas en conserver toute la fermeture.

L'API de module 3 ajoute les compagnons végétaux explicites d'ADR-0040, sans
changer les API 1/2 ni la version des schémas v2/v3. Le transport et le lecteur
installé décodent les catalogues d'espèces et le kit embarqué, vérifient leurs
pins auteur, modèles et inventaire gouverné, puis exposent les chemins relatifs
du module. Les chemins du kit d'origine ne sont pas des sources de chargement.
Le décodeur de prototypes importés avec shaders/paramètres de source reste
refusé. Les nouveaux compagnons ne sont pas encore raccordés au rendu natif,
à l'export Studio et à la restauration auteur/historique ; ces parcours ne sont
pas qualifiés par les tests du transport. Les exports existants restent API 2.

Les nouveaux exports `.orpmod` annoncent l'API de module 2 : la liste de sources
de chaque zone couvre toute l'archive auteur, y compris les sources historiques
nécessaires à undo/redo. Les blobs partagés sont dédupliqués par SHA-256. Le lecteur
continue d'admettre l'API 1 selon son contrat initial, limité à la tête courante ;
une API inconnue reste refusée. Le format de manifeste v2/v3 et les anciens
packages ne sont pas réécrits. Le snapshot d'export borne archives et sources à
256 Mio ; le runtime contrôle aussi la borne de la zone sélectionnée avant toute
allocation de blob. Source absente, altérée, surnuméraire ou conflit de chemin :
refus avant publication. [ADR-0039](../../docs/ADR/ADR-0039-module-archive-source-closure.md)
décrit cette compatibilité et les preuves encore requises au niveau produit.

## Atomicité et reprise

Verrou natif `File::try_lock`, automatiquement libéré à la mort du processus.
Tous les packages sont vérifiés avant publication d'une seule tête. Un échec conserve
la tête antérieure ; les packages déjà terminés restent réutilisables et les staging
interrompus sont conservés. Une relance du même Apply actif ne crée pas de révision.
Rollback change la tête et ajoute un reçu ; aucune version ni donnée utilisateur supprimée.
Les environnements dont la durabilité matérielle dépasse `sync_all` et le remplacement
atomique doivent être qualifiés séparément. Un disque plein ne vaut pas une installation réussie.

## Préparation des livraisons

`scripts/delivery/build_delivery.py studio-only` assemble le paquet Studio
avec ses binaires, notices et reçus épinglés. Le Studio exporte ensuite
chaque jeu avec son launcher et le runtime de modules. Les recettes et
preuves de l'ancien installateur groupé restent dans l'historique Gate 7.

`scripts/delivery/prepare_asset_library.py` inventorie le Vault actif ; `--pack` produit
un pack de bibliothèque séparé par lecture directe, sans copier/modifier le Vault.
Les données canonique d'assets et profils existants sont préservés ; les sources
originales restent identifiées séparément. Le pack garde les droits privés existants.
Les droits d'usage local ne deviennent pas automatiquement des droits de redistribution.
Les grandes bibliothèques sont planifiées en lots indépendants ; le futur client
doit composer leur racine Vault avant activation. Le catalogue de métadonnées seul
ne contient pas les modèles. Voir le [plan de livraison](../../docs/planning/INSTALLABLE_DELIVERY_20261008.md).

Avant une release : contrôles locaux sur les entrées exactes selon ADR-0034,
intégration complète RPG, notices/supply-chain,
signature/authenticité, WebView2, installation sur machine propre et validation humaine.


API 7 adds explicit per-area object resources. `module_objects::resolve` admits the
closed source-surface profile, exact catalog IDs/hashes and license provenance.
No binding returns no package and performs no reads. Origin-aware exports preserve
the binding and every immutable resource while canonical placement transforms may
change. Source object identities are separate from governed GLB catalog identities.
The resource reader/export proof is not Studio/native rendering qualification.

## Native object consumer qualification (26 October)

`LoadedModuleZone::objects` invokes the same closed per-area object resolver over
the installed package. The private fixture `module-object-runtime/qualification-002`
adds two canonical source house placements to the admitted raster module, repins
its actual effective vegetation document after the command, and verifies that the
other area acquires no object palette. The native consumer replay passed with
real source materials, GPU inventories, collisions and inspected captures. Its
scope is recorded in central `module-object-runtime-validation-001.json`; the
transport/export proof alone still establishes no render or Studio verdict.

## Explicit module terrain resources (API 8, 26 October)

`module_terrain::resolve` uses the existing shared terrain codec. Each area binds
its full canonical sampled-source descriptor hash to a closed, pinned resource
package. Compatible geometry alone never acquires a package. Index closure,
file roles/sizes/hashes and the original licence are checked before admission.
Origin-aware exports preserve these resources across canonical author edits.
APIs 1–7 retain their empty encodings and reject the new field even when empty.

Central `module-terrain-resources-validation-001.json` records 2,751 root tests,
126 tests from this separate workspace and one actual private admission/retouch/
re-export transaction. Run delivery tests explicitly with
`cargo test --manifest-path tools/forge_delivery/Cargo.toml --lib --tests`;
the root workspace excludes this tool. Studio terrain transport and consumption
by the common module renderer remain pending; no render/performance claim follows.

The subsequent `module-terrain-transport-validation-001.json` qualifies
`LoadedModuleZone::terrain` over the same admitted directory and the Studio native
authority/common acquisition through wire chunks. Native and Studio readers resolve
the same package. The common module renderer still needs to consume it, preserving
paint and physics; actual WebView and GPU qualification remain pending.

The 27 October native replay now consumes that profile through
`forge_engine::source_terrain::host`. An independent private API-8 package combines
the admitted terrain, environment and objects, preserving the original module.
The existing shared renderer observes all 13 images and 5 buffers, with inspected
captures and synchronized cell physics. Its separate assembly test runs explicitly
as `terrain_packages::private_source_terrain_joins_the_real_module_scene` under
the `module_builder` test target; a directory uses `validate_directory`, not the
archive inspector. Inputs are explicit and the fresh E: fixture is bounded to
1 GiB / 7,500 files. Immutable payloads are linked only for this read-only fixture;
the resulting archive remains autonomous. See central
`module-source-terrain-runtime-validation-001.json`. This is not Studio editing,
full distribution, clean-machine or performance qualification.


## Transition Gate 7 vers des jeux autonomes (2 novembre 2026)

`forge_delivery` reste le propriétaire des contrôles communs d'archives, d'installation,
de vérification, de rollback et de lancement. Le Studio et chaque launcher de jeu
utiliseront ces services après raccordement ; le CLI conserve ses consommateurs actuels
pendant la transition. Le Hub reste présent jusqu'aux preuves de transfert et de
migration sans perte. Voir ADR-0021 et l'inventaire de la campagne Gate 7
`editor-launcher-d1-inventory-001.md`.

Le contrat de produit autonome v2 vérifie l'identité du jeu, son manifeste dérivé et
la fermeture exacte des modules requis avant l'activation, puis à nouveau sous verrou
au lancement. Les sélections partielles, packs additionnels et médias de présentation
restent refusés tant que leur fermeture n'est pas prise en charge. Les chemins déclarés
par le produit sont bornés aux racines substituées du paquet, de l'installation et de
l'utilisateur ; l'environnement système hérité reste distinct. Les sauvegardes vont
dans `games/<ProjectId>/saves` sous la racine utilisateur. Les mises à jour conservent
la révision précédente pour rollback ; après rejet, seuls les nouveaux packages encore
reconnaissables comme ceux de la transaction peuvent être nettoyés. Les tests utilisent
des jeux et des lanceurs simulés : ils ne prouvent pas encore un export Studio complet,
un lanceur graphique propre au jeu ni une installation sur machine propre.
