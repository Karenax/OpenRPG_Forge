# État vérifié — OpenRPG Forge

Dernière mise à jour : 12 août 2026.

## Verdict

OpenRPG Forge est désormais un prototype RPG 3D consolidé, installable et utilisable pour créer,
tester et exporter des modules. Les familles Aurora sont closes ; le travail courant porte sur la
qualification de livraison, pas sur l'ajout de nouvelles fonctions.

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
- Crypte habillée avec 74 instances d'un kit Dungeon Remastered CC0 ;
- Forgotten Forest corrigée : maillage d'asset réel préféré au cube auxiliaire Blender, arbres
  stylisés gouvernés, échelle et éclairage rééquilibrés, caméra/HUD lisibles ;
- premiers découpages des orchestrateurs : politique de fenêtres Creator, décorateur artistique de
  la Crypte et mapping des ressources de quête MCP.

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

## Commandes de vérité

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
& .\scripts\Update-CompleteFunctionBacklog.ps1 -Check
& .\scripts\Verify.ps1
& .\scripts\Verify-RuntimeDistribution.ps1 -RunSmoke
& .\scripts\Verify-CreatorDistribution.ps1 -RunSmoke
& .\scripts\Verify-WindowsInstaller.ps1 -RunInstallCycle
```

Le résultat de la publication `main` et de GitHub Actions est ajouté après le push du présent lot.
