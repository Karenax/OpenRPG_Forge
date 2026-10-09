# ADR-0021 — Applications installables et livraison séparée des assets

Date : 8 octobre 2026
Statut : implémentation préparatoire autorisée par le propriétaire
Portée : demande « système installable complet, launcher / éditeur / jeu, assets séparés ».

## Décision

Préparer un système Windows x64 natif, installé par utilisateur, avec trois familles de
packages : applications, modules authorés et assets. Le launcher/installer Tauri appelle
les mêmes commandes Rust `forge_delivery` que le CLI et le futur client. Le Studio existant
reste Tauri/Leptos ; le jeu existant reste Bevy natif. Aucun changement de moteur ou de
représentation canonique, ni port depuis la ligne historique C#/Godot.

Le transport `.forgepack` v1 est distinct du futur contrat sémantique `.orpmod` v2.
Il transporte les archives d'auteur existantes sans les convertir en un second modèle.
La préparation de l'installable ne constitue ni l'ouverture ni la clôture automatique d'une gate.

## Contrats

- Index JSON relatif : ID, version, plateforme, type, droits, taille, hash d'archive et de manifeste.
- Manifestes bornés, chemins portables, inventaire fermé de fichiers, tailles et SHA-256.
- Gros payloads lus/écrits par flux ; pas de dépendance Python/Cargo/Node chez le client.
- Packages immuables installés sous `packages/<id>/<manifest_sha256>`.
- Une tête `installation.json` référence applications et packs compatibles.
- `Prepare` est en lecture seule ; `Apply` refuse une tête périmée ou un package modifié.
- Command ID, révision de base, résultat, audit et snapshot précédent pour rollback.
- Verrou système partagé entre processus, libéré automatiquement si un processus s'arrête.
- La publication de la tête est atomique, après contrôle de tous les fichiers.
- Les reprises réutilisent les packages terminés et utilisent des chemins de staging propres à
  chaque tentative. Les fragments interrompus sont conservés pour diagnostic, jamais activés.

Les projets utilisateur et les sauvegardes sont distincts des packages. L'installation copie
une seed d'auteur une fois ; une mise à jour ne réécrit jamais un workspace existant.
Le répertoire utilisateur figure dans le plan approuvé et dans la tête d'installation.
Un changement des données du module nécessite une nouvelle révision, pas l'effacement des retouches.

## Launcher et futur client

Le launcher offre installation avec aperçu, jeu, éditeur, vérification et retour à la version
précédente. Les arguments/env des exécutables sont déclarés dans le produit ; ils ne sont
jamais évalués par un shell. Les identités de modules et recettes de lancement restent des
données de livraison. Seul le package applications peut déclarer des points d'entrée.
Les extensions usuelles de programmes natifs et scripts d'installation sont refusées dans
les assets ; ce filtre n'est pas une analyse antivirus ni une détection de contenu déguisé.

Le futur client télécharge les fichiers référencés par l'index, vérifie leurs hashes puis
appelle `Prepare`/`Apply`. Les packs inchangés sont réutilisables entre mises à jour ; les
applications peuvent être distribuées sans renvoyer la bibliothèque. Les hashes servent
à l'intégrité : ils ne remplacent pas l'authentification de l'origine. Signature du catalogue,
signature Authenticode, hébergement et mise à jour réseau restent à qualifier avant release.

## Assets

Conserver la structure `normalized`, `metadata`, `licenses`, `animation` du Vault actif,
les IDs, profils par type, sources, transformations et droits existants. Les archives
`originals` sont inventoriées séparément comme sources ; cache, logs, inbox et quarantaine
ne deviennent pas une bibliothèque client. Aucun nouveau profil spatial artificiel.

L'export complet est privé/local par défaut. L'assemblage d'un pack n'autorise pas sa
redistribution publique. Aucun téléversement ou modification du Vault par ces outils.

## Qualification

Vérifier chemins malveillants, fichiers supplémentaires, tailles, corruption, plans périmés,
verrou concurrent, interruption/reprise, retry idempotent, upgrade/rollback, retouches et seed,
installation déplacée, démarrage du launcher/éditeur/jeu et absence de dépendance au repo.
Garder distincts build, tests installer, essais visuels, critères RPG Gate 3 et release.
Le jeu natif existant est une promenade Tidewater ; sa présence dans le package ne qualifie
pas l'aventure RPG jouée dans Studio. ADR-0006 a expiré pour la préparation de distribution :
les contrôles distants requis sur le candidat exact restent obligatoires avant qualification.
