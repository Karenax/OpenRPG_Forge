# Forge Delivery

Commandes natives partagées par le launcher, l'installation locale et le futur client.
Implémentation préparatoire de [ADR-0021](../../docs/ADR/ADR-0021-detached-delivery-installation.md).
Workspace Cargo isolé pour ne pas modifier/reconstruire les composants moteur en cours.

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
son plan côté natif à partir des seuls `ModuleId` et `AreaId`. Il utilise le binaire
jeu inventorié dans les applications installées, un environnement OS borné et les
sauvegardes séparées par module/carte, sans commande shell ni chemin reçu du frontend.

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

## Atomicité et reprise

Verrou natif `File::try_lock`, automatiquement libéré à la mort du processus.
Tous les packages sont vérifiés avant publication d'une seule tête. Un échec conserve
la tête antérieure ; les packages déjà terminés restent réutilisables et les staging
interrompus sont conservés. Une relance du même Apply actif ne crée pas de révision.
Rollback change la tête et ajoute un reçu ; aucune version ni donnée utilisateur supprimée.
Les environnements dont la durabilité matérielle dépasse `sync_all` et le remplacement
atomique doivent être qualifiés séparément. Un disque plein ne vaut pas une installation réussie.

## Préparation des livraisons

`scripts/delivery/build_delivery.py` assemble trois packages depuis des exécutables
existants explicitement fournis, une preuve de build Studio et les données de scène.
Il conserve les preuves et licences, épingle les binaires et produit `ForgeSetup.exe`.
La livraison Source013 du 15 octobre et la boucle RPG du jeu séparé sont décrites
dans la [clôture Gate 3](../../docs/evidence/gate3/GATE3_CLOSURE_20261015.md).
Cette baseline reste distincte du raccord `.orpmod` v2 en cours.

`scripts/delivery/prepare_asset_library.py` inventorie le Vault actif ; `--pack` produit
un pack de bibliothèque séparé par lecture directe, sans copier/modifier le Vault.
Les données canonique d'assets et profils existants sont préservés ; les sources
originales restent identifiées séparément. Le pack garde les droits privés existants.
Les droits d'usage local ne deviennent pas automatiquement des droits de redistribution.
Les grandes bibliothèques sont planifiées en lots indépendants ; le futur client
doit composer leur racine Vault avant activation. Le catalogue de métadonnées seul
ne contient pas les modèles. Voir le [plan de livraison](../../docs/planning/INSTALLABLE_DELIVERY_20261008.md).

Avant une release : CI exacte, intégration complète RPG, notices/supply-chain,
signature/authenticité, WebView2, installation sur machine propre et validation humaine.
