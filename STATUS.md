# État vérifié — OpenRPG Forge

Dernière mise à jour : 13 août 2026.

## Verdict

OpenRPG Forge est désormais un prototype RPG 3D consolidé, installable et utilisable pour créer,
tester et exporter des modules. Les familles Aurora sont closes ; le travail courant porte sur la
qualification de livraison, pas sur l'ajout de nouvelles fonctions.

Le dernier lot d’édition manuelle et non destructive est validé dans le worktree local mais n’est pas
encore committé ni publié. Il ne faut donc pas le confondre avec la baseline disponible sur
`origin/main`.

## Produit disponible

- Creator et runtime 3D séparés, exclusivement présentés sous la marque OpenRPG Forge ;
- neuf espaces Creator, mode Débutant à quatre espaces et outils spécialisés intégrés ;
- projets, zones, blueprints, assets gouvernés, dialogues, quêtes, scripts et contenu RPG ;
- validation, correctifs prévisualisés, undo/redo, playtest, sauvegarde/reprise et export `.orpmod` ;
- Assistant et host MCP transactionnels, bornés, facultatifs et sans shell arbitraire ;
- distributions Windows x64 autonomes du Creator et du Runtime ;
- installateur/désinstallateur NSIS avec contrôle de mise à jour et préservation des données utilisateur ;
- Crypte des Cendres et Forgotten Forest jouables en Forward+ avec modèles 3D gouvernés.

## Couverture et preuves

- 717/717 fonctions au statut `Tested`, réparties dans 187/187 lots clos ;
- P0 : 196/196, P1 : 298/298, P2 : 190/190, P3/P4 : 33/33 ;
- 5 461 chemins de preuve Aurora contrôlés sans chemin absent ;
- parcours produit automatisé : projet, zones, assets, contenu RPG, narration, validation, playtest,
  export et Assistant gouverné ;
- matrice et procédure d'acceptation de bout en bout sous `docs/acceptance/`.
- portail local courant réussi avec 1 860 tests .NET, 39 tests Python, un build sans avertissement et
  le graphe Roslyn déterministe à 8 225 nœuds, 51 653 relations et 0 diagnostic.

Ces nombres prouvent la présence et la non-régression des capacités. Ils ne remplacent pas une
qualification humaine de release publique.

## Consolidation réalisée

- CI rendue indépendante de la cadence du runner et des dossiers locaux `.artifacts` ;
- contexte réduit à `PROJECT_CONTEXT.md`, `STATUS.md`, `TASKS_MVP.md` et la roadmap courante ;
- manifestes SHA-256, crédits, licences agrégées, notices et SBOM CycloneDX 1.6 ;
- installateur validé sur un profil Windows temporaire propre : installation, smoke Creator, mise à
  jour sur place, désinstallation et conservation des données utilisateur ;
- détails techniques UUID/hash/provenance repliés par défaut et fenêtres spécialisées intégrées,
  redimensionnables et non exclusives ;
- parcours Plaçables simplifié dans le Monde : assets prioritaires, sélection de scène,
  déplacement/rotation/échelle avec snap, action de placement fixe et propriétés avancées repliées ;
- Crypte habillée avec 74 instances d'un kit Dungeon Remastered CC0 ;
- Forgotten Forest corrigée : maillage d'asset réel préféré au cube auxiliaire Blender, arbres
  stylisés gouvernés, échelle et éclairage rééquilibrés, caméra/HUD lisibles ;
- module connecté démarrant dans l’Auberge du Cerf d’Ambre : déplacement au sol accéléré, mini-carte,
  atlas des six zones sur `M` et HUD RPG noir/ambre compact validés en Forward+ à 1920×1080 et 2560×1440 ;
- géométrie jouable de l’auberge sécurisée : plancher physique continu, quatre limites murales
  infranchissables (porte comprise jusqu’à interaction), pivots visuels des murs recalés sur le sol,
  pieds du personnage alignés et récupération automatique après toute chute sous une carte intérieure
  ou extérieure ;
- caméra de déplacement stabilisée par interpolation physique, point de visée lissé et évitement des
  murs ; l’ancien bouton central de sortie est supprimé et la porte 3D de l’auberge se clique directement
  pour charger Clairval ;
- premiers découpages des orchestrateurs : politique de fenêtres Creator, décorateur artistique de
  la Crypte et mapping des ressources de quête MCP ;
- cycle de vie des outils Creator centralisé : aucun outil secondaire au démarrage, un seul outil
  actif à la fois, fermeture, déplacement, redimensionnement et maximisation vérifiés sous Windows ;
- édition manuelle consolidée : collections Dialogue, Quête, Blueprint, contenu, propriétés d’objet
  et slots d’équipement présentées sous forme de listes et champs nommés, sans syntaxe brute visible ;
- sauvegarde non destructive des profils Objet et Terrain : les entrées non affichées par un
  formulaire rapide sont conservées, et les suppressions Zone/Blueprint/Dialogue/Quête passent par
  une prévisualisation transactionnelle, la validation des dépendances et l’historique undo/redo.

## Distribution locale qualifiée

- version : `0.1.0-dev` ; cible : Windows x64 ;
- conformité : 38 composants documentés et 38 licences agrégées ;
- Runtime : manifeste validé et smoke rejoué depuis l'exécutable exporté ;
- Creator : manifeste validé et smoke rejoué depuis le lanceur autonome ;
- installateur : `OpenRPG_Forge_Setup_0.1.0-dev_x64.exe`, cycle propre réussi ;
- signature constatée : `NotSigned`.

## Limites externes avant release publique

- le dépôt ne possède pas encore de licence explicite pour son propre code ; cette décision juridique
  appartient au propriétaire du projet ;
- aucun certificat Authenticode n'est fourni, donc l'installateur reste un candidat de développement
  susceptible de déclencher SmartScreen ;
- aucun tag ni aucune GitHub Release n'est créé sans autorisation distincte ;
- une revue sur un second écran physique et avec un fournisseur IA réel reste une qualification
  humaine de release, pas un blocage du prototype local.

## Publication Git

- baseline publiée sur `origin/main` au commit `69f968baec68106f16999342bb43dfcb7509f45f` ;
- `HEAD` et `origin/main` sont identiques (`0/0`), mais le worktree courant n’est plus propre : le
  lot d’édition manuelle et d’autres changements locaux restent non publiés ;
- portail local vert sur le worktree courant, distributions et installateur inclus ;
- GitHub Actions n'a exécuté aucune étape du commit final : les jobs CI et graphe ont été refusés
  avant démarrage parce que le compte signale un paiement échoué ou une limite de dépenses à relever ;
- la CI distante devra être relancée après correction de **Billing & plans**. Ce blocage externe ne
  doit pas être présenté comme une validation distante réussie.

## Commandes de vérité

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
& .\scripts\Update-CompleteFunctionBacklog.ps1 -Check
& .\scripts\Verify.ps1
& .\scripts\Verify-RuntimeDistribution.ps1 -RunSmoke
& .\scripts\Verify-CreatorDistribution.ps1 -RunSmoke
& .\scripts\Verify-WindowsInstaller.ps1 -RunInstallCycle
```
