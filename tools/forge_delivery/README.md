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
forge_delivery plan INSTALL_ROOT PACKAGE...
forge_delivery plan-file INSTALL_ROOT ABSOLUTE_PLAN_FILE PACKAGE...
forge_delivery apply PLAN_JSON
forge_delivery uninstall INSTALL_ROOT
forge_delivery status INSTALL_ROOT
forge_delivery verify INSTALL_ROOT
forge_delivery rollback-plan INSTALL_ROOT
forge_delivery launch INSTALL_ROOT game|editor
forge_delivery launch-plan INSTALL_ROOT launcher|game|editor
forge_delivery launcher INSTALL_ROOT
```

Sorties JSON et erreurs JSON sur stderr ; succès exit 0, erreur exit 1. `plan` et
`rollback-plan` n'appliquent rien. `setup ROOT PACKAGE...` présente le plan et demande
`oui` avant d'appliquer ; le hub offre le même aperçu sous forme graphique.

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
Le jeu natif actuellement fourni est une promenade : aucune clôture RPG revendiquée.

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
