# Validation Gate 3 dans le checkout commun

Toutes les commandes partent de `E:\OpenRPG_Forge\OpenRPG_Forge-Rust`. Le code
actuel, y compris les fichiers locaux non suivis, est compilé directement. Les
dossiers de preuve ne sont pas des copies du projet. Utiliser un dossier de reçus
neuf ; conserver les résultats précédents.

```powershell
$env:CARGO_INCREMENTAL = '0'
$env:RUSTFLAGS = '-C debuginfo=0'
python scripts/Validate-Gate3WorkingTree.py native --evidence E:/OpenRPG_Forge/.gate3-evidence/20261011/shared-NNN --target-dir E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002
python scripts/Replay-Gate3PrivateInputs.py --evidence E:/OpenRPG_Forge/.gate3-evidence/20261011/shared-NNN
python scripts/Validate-Gate3WorkingTree.py wasm --evidence E:/OpenRPG_Forge/.gate3-evidence/20261011/shared-NNN --target-dir E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002
python scripts/Validate-Gate3WorkingTree.py release --evidence E:/OpenRPG_Forge/.gate3-evidence/20261011/shared-NNN --target-dir E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002
```

Les quatre étapes exigent des entrées de build inchangées. La carte comprend les
exemples locaux, les locks Cargo et configurations locales, la CI et les helpers
de build. Seuls les scripts Python d'orchestration sous `scripts/hivemind` qui ne
sont consommés par aucune de ces étapes sont hors périmètre bloquant. Les autres
formats restent inclus, notamment le candidat Rust inclus par un exemple moteur.
L'inventaire complet du checkout et ses changements avant/après sont conservés
séparément avec leurs SHA ; une conversation parallèle reste ainsi visible.

Le replay utilise exactement les
binaires de la suite native et refuse un manifeste devenu obsolète. Avant le build
release, contrôler que le reçu worker du `dist` correspond aux sources et que ses
assets sont présents pour les parcours locaux. Les tests peuvent copier uniquement
des données de fixture pour protéger le workspace utilisateur.

Le release épingle les deux exécutables effectivement produits, le frontend,
le worker et les assets. La livraison commune peut adopter ces preuves via
`--validated-gate3-evidence` : elle vérifie la chaîne avant de réutiliser le même
`release/forge-studio.exe`, sans reconstruire ou copier une autre application.

Pour les parcours publics, définir `FORGE_GATE3_VALIDATED_EVIDENCE` avec ce même
dossier. `Assert-Gate3CandidateInputs` appelle le helper canonique : il ferme la
chaîne release/WASM, l'identité exacte de l'exécutable et le dist courant avant
les vérifications kit/imports. Une preuve demandée invalide est refusée sans
repli. La voie d'archive `candidate/web` exige explicitement
`FORGE_GATE3_HISTORICAL_CANDIDATE=1` ; elle ne qualifie pas la version commune.

La fixture actuelle authentifiée contient 21 placements et l'archive originale
`8d5a9b037a20eeff6df74092829727e8e7fd94f0ccc0898992052f916b9eb919`.
Les scripts comparent la liste complète avant/après, avec identités et
transformations. Le parcours courant enchaîne `GameplayAuthoringSmoke
-IncludeImpactApproach`, `PhysicsAuthoringSmoke -CompletePhysicsFixture` puis
`NavigationAuthoringSmoke`, sur le même exécutable. Les preuves sont des données
de test et des ledgers ; aucun nouveau build de candidat n'est créé entre étapes.

## Assets statiques locaux du Studio commun

Le dossier unique est `E:\OpenRPG_Forge\.forge-local\studio-assets`. Il contient
seulement `forge-imports`, `forge-kit` et leur manifeste `frontend-static-inputs.json`.
Le hook `Stage-StudioLocalAssets.ps1`, appelé après le worker par Trunk, authentifie
les entrées avant/après et les copies dans `apps/forge_studio/dist`. Il n'utilise
aucun frontend historique ni aucun ancien WASM. La CI publique peut construire
sans ce dossier : son reçu annonce explicitement `LOCAL_ASSETS_ABSENT`. Un dossier
présent mais incomplet/corrompu est refusé.

La récupération initiale des seuls assets de la livraison installée utilise
`E:\OpenRPG_Forge\.module-navigation-20261010\candidate-003\frontend`, avec
son manifeste voisin `frontend-static-inputs.json`, SHA-256
`3d7f2dd80580b65dee3da1b88b3d826d0d98aac940621f85be794787dcb18ac6`.
Vérifier son inventaire exact (3000 imports et 11 fichiers kit), les chemins
physiques et tous les SHA avant copie ; refuser une destination déjà présente.
Copier seulement les deux répertoires d'assets et le manifeste, puis vérifier les
SHA source et destination. Le reçu de migration est externe au dossier d'assets.
Les mentions de provenance/licence et fichiers source internes aux packages sont
conservés comme données épinglées. Aucun index, bootstrap ou WASM n'est récupéré.

Une réussite de compilation ne clôture pas Gate 3. Scénarios publics, observation
GPU, revue humaine du build actuel et CI sur sa révision exacte restent distincts.
