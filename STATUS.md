# État vérifié — OpenRPG Forge

Dernière mise à jour : 22 août 2026.

## Verdict

OpenRPG Forge est désormais un prototype RPG 3D consolidé, installable et utilisable pour créer,
tester et exporter des modules. Les familles Aurora sont closes ; le travail courant porte sur la
qualification de livraison et la consolidation des vertical slices.

La phase consolidée couvre l’édition directe, la qualité visuelle, le lanceur configurable et
l’interface de combat. Elle a franchi le portail complet et constitue une baseline cohérente ; elle
ne doit pas être confondue avec une release publique signée.

## Produit disponible

- Creator et runtime 3D séparés, exclusivement présentés sous la marque OpenRPG Forge ;
- neuf espaces Creator, mode Débutant à quatre espaces et outils spécialisés intégrés ;
- projets, zones, blueprints, assets gouvernés, dialogues, quêtes, scripts et contenu RPG ;
- validation, correctifs prévisualisés, undo/redo, playtest, sauvegarde/reprise et export `.orpmod` ;
- Assistant et host MCP transactionnels, bornés, facultatifs et sans shell arbitraire ;
- distributions Windows x64 autonomes du Creator et du Runtime ;
- lanceur Windows configurable par jeu avec branding, réglages persistants et hébergement local autoritaire ;
- installateur/désinstallateur NSIS avec contrôle de mise à jour et préservation des données utilisateur ;
- Crypte des Cendres et Forgotten Forest jouables en Forward+ avec modèles 3D gouvernés ;
- interface de combat runtime autoritaire, animée, accessible et bornée par des pools stables.

## Couverture et preuves

- 717/717 fonctions au statut `Tested`, réparties dans 187/187 lots clos ;
- P0 : 196/196, P1 : 298/298, P2 : 190/190, P3/P4 : 33/33 ;
- 5 461 chemins de preuve Aurora contrôlés sans chemin absent ;
- parcours produit automatisé : projet, zones, assets, contenu RPG, narration, validation, playtest,
  export et Assistant gouverné ;
- matrice et procédure d'acceptation de bout en bout sous `docs/acceptance/` ;
- portail local de la baseline consolidée : 1 894 tests .NET, 39 tests Python, build sans avertissement et
  graphe Roslyn déterministe à 8 392 nœuds, 52 352 relations et 0 diagnostic.

Ces nombres décrivent la baseline déjà qualifiée. Ils ne constituent pas une preuve d’exécution du
nouveau lot de reconstruction de la Crypte tant que celui-ci n’a pas été rejoué sur l’environnement
Windows/Godot canonique.

## Gameplay systémique — preuve actuelle

- ruleset clean-room canonique dans `docs/design/OPENRPG_FORGE_RULESET_FR.md` ;
- manifeste versionné de la Crypte dans
  `apps/Forge.Runtime/content/crypt-of-ashes/systemic-gameplay.json` ;
- cinq interactions environnementales : huile et feu, eau et givre, brasero suspendu, pilier
  fracturé et fosse de pointes ;
- contrôles isolés au module : `X` poussée, `F` feu, `G` givre, `B` force et `Y` concentration ;
- dégâts indirects résolus par le chemin de combat autoritaire existant afin de conserver phases,
  événements, butin, progression et transitions de quête ;
- contrat Python historique : 8 tests déjà exécutés avec succès avant la reconstruction spatiale.

## Crypte des Cendres — reconstruction spatiale en cours de qualification

La branche `feat/systemic-rules-crypt-of-ashes` remplace désormais la salle canonique 20 × 28 m par
une zone 48 × 76 m. Les identifiants critiques sont conservés mais répartis dans un petit donjon :

1. Seuil des Braises ;
2. Sanctuaire d’Elyra ;
3. Catacombes et reliquaires ;
4. porte scellée et salle de la Sentinelle ;
5. Ossuaire inondé ;
6. Grande Nef ;
7. Sanctuaire du Gardien.

Le snapshot de zone lui-même a été modifié : sols, murs internes, verrou spatial de la porte, zones
latérales et positions des acteurs ne reposent plus uniquement sur un décorateur runtime. La couche
artistique `CryptArtKitDecorator` compose les mêmes espaces à partir du lot KayKit Dungeon
Remastered CC0-1.0 déjà gouverné. Des colliders de sécurité ferment les silhouettes étroites jusqu’à
leur exposition complète dans le Creator.

Les interactions systémiques ont été repositionnées dans les nouvelles salles : huile et givre dans
l’Ossuaire inondé, brasero, pilier et pointes dans le Sanctuaire. Les prompts d’interaction sont
maintenant masqués au-delà de 7,5 m afin de ne pas traverser visuellement tout le donjon.

Deux contrats statiques couvrent le lot : `tests/test_crypt_systemic_gameplay.py` et
`tests/test_crypt_dungeon_rebuild.py`. Le second a été ajouté avec la reconstruction mais n’est pas
présenté ici comme exécuté : compilation .NET/Godot, portail Windows, navigation, collisions,
performances et revue Forward+ restent à rejouer avant qualification.

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
- parcours direct du viewport : Forgotten Forest au démarrage normal, choix d'objet ou de brosse,
  clic dans la scène, sculpture de terrain transactionnelle et cadrage de la zone active ;
- rendu Forward+ enrichi par profil avec ciel procédural cinématique, SSAO/SSIL, glow et brouillard
  volumétrique, avec repli GL sûr et neutralisation explicite d'une LUT absente ;
- baseline précédente de la Crypte habillée par KayKit Dungeon Remastered CC0 ; la branche système
  reconstruit désormais cette couche en sept espaces et doit être requalifiée visuellement ;
- Forgotten Forest corrigée : maillage d'asset réel préféré au cube auxiliaire Blender, arbres
  stylisés gouvernés, échelle et éclairage rééquilibrés, caméra/HUD lisibles ;
- module connecté démarrant dans l’Auberge du Cerf d’Ambre : déplacement au sol accéléré, mini-carte,
  atlas des six zones sur `M` et HUD RPG noir/ambre compact validés en Forward+ à 1920×1080 et 2560×1440 ;
- feedback de combat branché sur les résultats réels : barres/chip, cible et plaques, nombres,
  recharge exacte, toasts, pings, impacts, caméra, hit-stop et SFX ; 30 captures Forward+ acceptées
  en 1920×1080/2560×1440, mouvements réduits et texte ×2, puis parcours interactif jusqu'à Clairval ;
- géométrie jouable de l’auberge sécurisée : plancher physique continu, quatre limites murales
  infranchissables, alignement visuel murs/sol et récupération automatique après chute ;
- caméra de déplacement stabilisée par interpolation physique, point de visée lissé et évitement des murs ;
- premiers découpages des orchestrateurs : politique de fenêtres Creator, décorateur artistique de
  la Crypte et mapping des ressources de quête MCP ;
- cycle de vie des outils Creator centralisé : aucun outil secondaire au démarrage, un seul outil
  actif à la fois, fermeture, déplacement, redimensionnement et maximisation vérifiés sous Windows ;
- édition manuelle consolidée : collections Dialogue, Quête, Blueprint, contenu, propriétés d’objet
  et slots d’équipement présentées sous forme de listes et champs nommés, sans syntaxe brute visible ;
- sauvegarde non destructive des profils Objet et Terrain et suppressions transactionnelles ;
- profil de lanceur v1 persistant, application WPF RPG cinématique, générateur `Client` atomique,
  manifestes SHA-256 complets et modèle embarqué dans la distribution Creator ;
- réglages d'affichage, qualité, VSync, audio et accessibilité appliqués au runtime ; hébergement
  local autoritaire validé, connexion distante maintenue comme aperçu explicitement désactivé.

## Distribution locale qualifiée

- version : `0.1.0-dev` ; cible : Windows x64 ;
- conformité : 38 composants documentés et 38 licences agrégées ;
- Runtime : manifeste validé et smoke rejoué depuis l'exécutable exporté pour la baseline ;
- Creator : manifeste validé et smoke rejoué depuis le lanceur autonome ;
- installateur : `OpenRPG_Forge_Setup_0.1.0-dev_x64.exe`, cycle propre réussi ;
- signature constatée : `NotSigned` ;
- modèle de lanceur de jeu : export Godot et smoke runtime réussis pour la baseline ;
- isolation NuGet de distribution : les restaurations RID utilisent des verrous temporaires.

## Limites externes avant release publique

- le dépôt ne possède pas encore de licence explicite pour son propre code ; cette décision juridique
  appartient au propriétaire du projet ;
- aucun certificat Authenticode n'est fourni, donc l'installateur reste un candidat de développement ;
- aucun tag ni aucune GitHub Release n'est créé sans autorisation distincte ;
- une revue sur un second écran physique et avec un fournisseur IA réel reste une qualification humaine ;
- les workflows GitHub Actions peuvent rester bloqués par l’état de facturation du compte.

## Publication Git

- `main` est la branche canonique et `origin/main` la publication de référence ;
- le lot Crypte reste sur sa branche et sa PR tant que les validations natives ci-dessus ne sont pas vertes ;
- distributions, installateur et captures constituent des preuves distinctes d’un simple push Git.

## Commandes de vérité

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
& .\scripts\Update-CompleteFunctionBacklog.ps1 -Check
& .\scripts\Verify.ps1
& .\scripts\Verify-RuntimeDistribution.ps1 -RunSmoke
& .\scripts\Verify-CreatorDistribution.ps1 -RunSmoke
& .\scripts\Verify-WindowsInstaller.ps1 -RunInstallCycle
```
