# Ajouter une source ou un pack d’assets

Cette procédure étend le pipeline sans contourner la licence, l’immutabilité du Vault ni les
commandes transactionnelles du Creator.

## 1. Qualifier une nouvelle source

Avant tout code ou téléchargement :

1. enregistrer la page officielle, l’auteur, la licence SPDX, l’URL de licence et la date de revue ;
2. vérifier séparément les conditions de l’API et les droits attachés aux fichiers ;
3. prouver usage commercial, modification et redistribution des octets visés ;
4. refuser miroir, reupload, scraping de session, secret embarqué et licence seulement supposée ;
5. classer la source comme automatisée, manuelle, différée ou refusée dans
   `ASSET_SOURCE_GOVERNANCE.md`.

Créer ensuite `asset_manifests/sources/<source>.json`. Le manifeste doit être portable : aucune
racine locale absolue, aucun jeton et aucune donnée de session.

## 2. Écrire ou étendre l’adaptateur

Un adaptateur automatisé doit :

- interroger uniquement une API/page officielle et employer un User-Agent identifiable ;
- borner délais, pagination, taille et nombre de fichiers ;
- reprendre un téléchargement partiel sans accepter un contenu différent ;
- enregistrer URL, taille, hash amont disponible, SHA-256 final et reçu ;
- écrire l’original sous `originals/<source>/` sans jamais le remplacer silencieusement ;
- extraire dans un répertoire temporaire confiné, puis publier atomiquement sous `normalized/` ;
- mettre en quarantaine traversée de chemin, lien dangereux, type incohérent ou archive excessive ;
- ne jamais exécuter un script ou binaire fourni par l’asset.

Les tests doivent couvrir au minimum découverte, licence refusée, reprise, hash discordant, archive
malveillante et idempotence. Les fixtures réseau restent petites et locales.

## 3. Proposer un pack candidat

Un pack entre d’abord comme candidat borné, pas comme expansion globale :

1. créer `asset_manifests/packs/<pack>.json` avec IDs stables, source, version et sélection exacte ;
2. normaliser seulement cette sélection en GLB 2, unités métriques, pivot et orientation Forge ;
3. produire collision, LOD, miniature et rapport qualité lorsque requis ;
4. conserver pour chaque chunk chemin relatif, taille, SHA-256, plateformes et licence ;
5. générer les crédits depuis les assets réellement sélectionnés ;
6. installer dans un workspace de revue, jamais directement dans le projet d’un utilisateur ;
7. vérifier que réinstallation et désinstallation sont idempotentes et récupérables.

## 4. Revue visuelle et technique

La revue doit comparer le pack au profil artistique de la zone cible : échelle, silhouettes,
réalisme, palette, densité de texture, shader, époque et lisibilité. Elle inclut au minimum :

- une planche contact ou une scène représentative en éclairage canonique ;
- une vue proche et une vue de composition ;
- contrôle des matériaux, normales, transparence, collision et ombres ;
- vérification des LOD/culling et des budgets sur une densité réaliste ;
- test de placement manuel et, si pertinent, de dispersion de biome ;
- export `.orpmod`, validation, chargement runtime et crédits.

Un pack peut être accepté pour une zone sans rendre toute la source stylistiquement compatible avec
tous les modules.

## 5. Admission dans le catalogue distribué

L’admission nécessite une décision explicite. Mettre à jour dans le même incrément :

- les catalogues JSON et leur validation ;
- les preuves de source/licence et le rapport d’acquisition ;
- la politique `AssetDeliveryReportService.Policy` si le plafond de 605 est levé ;
- les tests du loader, du downloader et du rapport AV5 ;
- `ASSET_SOURCE_GOVERNANCE.md`, `TASKS_MVP.md`, `STATUS.md` et le graphe d’architecture.

Exécuter ensuite les validateurs de catalogue, les tests ciblés, le rapport de livraison et
`scripts/Verify.ps1`. Aucun compte de catalogue n’est modifié pour « préparer » une extension qui
n’a pas encore franchi cette revue.

## 6. Installation dans un module

Dans le Creator, l’installation part d’un asset local et licencié, prépare un manifeste, puis passe
par `InstallAssetBlueprintHandler`. Le bouton de placement, le glisser-déposer, les brosses de biome
et le MCP emploient tous le même blueprint et le même `visual.asset`. Une mutation MCP reste séparée
en plan sec, application et revert ; elle n’écrit jamais un fichier arbitraire.

Après installation, vérifier :

1. présence du manifeste et de tous ses chunks ;
2. égalité taille/SHA-256 avec le reçu ;
3. provenance, auteur, SPDX et crédits ;
4. absence de chemin absolu ou de traversée ;
5. placement et reconstruction identiques dans Creator et runtime ;
6. export et rechargement du `.orpmod` sans dépendance au Vault externe.
