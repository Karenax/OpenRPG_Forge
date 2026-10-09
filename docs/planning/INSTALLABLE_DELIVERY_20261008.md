# Livraison installable — préparation du 8 octobre 2026

Demande propriétaire : préparer launcher, éditeur, jeu et livraison séparée des assets.
Cette préparation anticipe les surfaces de distribution des Gates 4/7. La clôture de
Gate 3 conserve ses critères RPG, preuves humaines et contrôles CI sur le candidat exact.

## Utilisation

Le dossier de livraison contient `ForgeSetup.exe`, `delivery.json`, le CLI et trois
archives indépendantes : `forge-applications.forgepack`, `reference-module.forgepack`
et `reference-assets.forgepack`. Aucun environnement de développement n'est requis
sur le poste client ; Windows x64 et WebView2 sont nécessaires.

Ouvrir `Start-Forge.cmd` : le script vérifie le launcher et détecte WebView2 selon
les clés Microsoft. L'installeur Microsoft x64 hors ligne est fourni, vérifié par
hash et signature ; il n'est exécuté que si absent et accepté par l'utilisateur.
Puis préparer la livraison, examiner le plan et installer dans le hub.
Le même hub sert ensuite de launcher. Installation par utilisateur dans
`%LOCALAPPDATA%/OpenRPG Forge/Installation` ; projets, captures et sauvegardes sous
`%LOCALAPPDATA%/OpenRPG Forge/UserData`. Les dossiers peuvent être explicitement choisis
avec `--root` et `FORGE_DELIVERY_USER_ROOT`, avant préparation du premier plan.

Le hub installé est aussi référencé par l'entrée `launcher` du produit. Le CLI permet
`launch CHEMIN_INSTALLATION launcher`, `game` ou `editor`, avec arguments natifs.
Les versions précédentes restent disponibles pour rollback. Les workspaces déjà
retouchés ne sont jamais remplacés automatiquement par une nouvelle seed.

## Intégration du futur client

1. Lire l'index `forge.delivery.index.v1` et afficher taille, version et droits.
2. Télécharger les archives nécessaires séparément ; vérifier `archive_sha256`.
3. Appeler `forge_delivery::install::prepare` ou `forge-delivery.exe plan`.
4. Présenter le plan et appliquer son `command_id` après acceptation utilisateur.
5. Conserver la tête/révision retournée ; vérifier ou préparer un rollback au besoin.

Le plan référence les hashes exacts des applications, modules et assets obligatoires.
Le pack facultatif `asset-library` peut être ajouté ultérieurement sans reconstruire
les applications. Son chemin est alors fourni au Studio comme `OPENRPG_ASSET_VAULT`.
L'index complet de bibliothèque conserve les sources originales séparément des fichiers
client, les profils, métadonnées, animation et licences. Les dossiers cache/logs/inbox/
quarantine ne sont pas des assets distribuables. La préparation reste privée et locale.

### Bibliothèque complète et lots

Inventaire du Vault actif : 108 233 fichiers client, 485 634 632 475 octets ;
21 507 sources originales, 111 013 678 743 octets. Le volume dépasse la limite de
256 Gio d'un package. Le plan prévoit 57 lots, cible 8 Gio, sélection JSON distincte
pour chaque lot. Ils sont **planifiés, pas tous exportés ni envoyés**.

Un catalogue privé séparé est exporté : 2 249 fichiers de `metadata`, `licenses`
et `animation`, environ 1,99 Go. Il ne contient pas les modèles normalisés.
`export_asset_catalog.py` le reproduit ; `prepare_asset_library.py --reuse-index
--export-shard ID --delivery-cli CHEMIN` exporte un lot à la fois, sans recopier
le Vault ni dépasser inutilement le stockage disponible.

Le futur client peut installer chaque lot avec les commandes natives existantes,
puis matérialiser une racine Vault en conservant les chemins relatifs du manifeste
de chaque lot. Les fichiers identiques peuvent être partagés ; des collisions dont
les hashes diffèrent doivent être refusées. La composition de cette racine complète
et son activation via `OPENRPG_ASSET_VAULT` restent à implémenter dans le futur client.
Le binding actuel `asset-library` convient à un package autonome de taille bornée ;
il ne compose pas automatiquement les 57 lots. Ne pas appeler le catalogue seul
une bibliothèque de modèles installée ou une validation Studio.

## Reproduction

- Rust : workspaces indépendants `tools/forge_delivery` et `apps/forge_hub` ; build offline.
- Assemblage : `scripts/delivery/build_delivery.py --help` ; tous les binaires sources
  et la preuve Studio sont fournis explicitement, puis copiés et vérifiés.
- Bibliothèque : `scripts/delivery/prepare_asset_library.py --help` ; inventaire seul
  par défaut, export par flux avec `--pack --delivery-cli CHEMIN`.
- Contrat et limites : [ADR-0021](../ADR/ADR-0021-detached-delivery-installation.md) et
  [moteur d'installation](../../tools/forge_delivery/README.md).

## Limites de qualification

Le jeu natif assemblé est actuellement la promenade Tidewater. L'aventure RPG complète
reste à qualifier dans la surface de jeu prévue par Gate 3 ; cette livraison ne la
remplace pas. Signature Authenticode, catalogue authentifié, test du prérequis WebView2
sur Windows propre, publication distante et validation visuelle humaine
restent des critères de release. Les hashes assurent l'intégrité, pas la confiance
dans un serveur de téléchargement. Aucun envoi distant n'est effectué par ces scripts.
