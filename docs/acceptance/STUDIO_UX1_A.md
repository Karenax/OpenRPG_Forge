# Acceptation Studio — UX1-A Créer un document de zone

État local au 16 août 2026 : **fonctionnel et qualifié** pour les deux identifiants de ce lot.
Cette acceptation ouvre UX1 avec un parcours de création réellement utilisable dans le Studio
autonome. Elle ne qualifie pas les 69 autres routes UX1, les 66 fenêtres historiques, les
distributions Windows/Linux ni une publication Git.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests canoniques ciblés : Application 6/6 et ModuleFormat 19/19, soit 25/25 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1A_PASS wizard_preview=true empty_template=true playable_template=true
  foundation=true dry_run=true invalid_blocked=true atomic_undo_redo=true
  independent_document=true persistence_roundtrip=true stable_identity=true
  neighbors_preserved=true human_no_uuid=true ui=true ui_transaction=true
  project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec une zone voisine, prévisualise les
  gabarits vide et jouable, bloque un nom en doublon, applique/annule/rétablit, sauvegarde, rouvre le
  projet et vérifie l’identité des documents et profils ;
- le smoke UI applique aussi une création au projet courant en mémoire, l’annule, la rétablit puis
  restaure exactement le projet et la zone active sans écrire Forgotten Forest ;
- journal headless : `artifacts/verification/studio-shell/ux1-a/headless-smoke.log` ;
- journaux Forward+ : `forwardplus-fhd.log`, `forwardplus-qhd.log` et `forwardplus-4k.log` dans
  `artifacts/verification/studio-shell/ux1-a/`, sous Godot 4.7.1 Mono, Vulkan et
  AMD Radeon RX 7800 XT, sans avertissement, erreur ni échec ;
- captures Forward+ inspectées : `wizard-fhd.png` en 1920×1080, `summary-qhd.png` en 2560×1440 et
  `wizard-4k.png` en 3840×2160.

La première revue FHD a montré le libellé de la deuxième étape tronqué : le rail fait désormais
250 px et les trois étapes restent lisibles. La première revue 4K a montré un assistant trop petit
par rapport au viewport : son échelle responsive passe de 100 % en Full HD à 125 % en QHD et 175 %
en 4K. Les trois captures finales gardent titre, résumé, sécurité, aperçu, actions et statut visibles
ensemble, sans débordement.

### ORF-AREA-001

La page **Zones & scène** expose **Nouvelle zone** à côté de l’action de placement. Un projet sans
zone remplace cette action principale par **Créer la première zone**. La recherche globale `Ctrl+K`
propose la même commande et `Échap` ferme l’assistant intégré sans ouvrir de fenêtre native.

L’assistant demande seulement un nom, intérieur/extérieur, un gabarit vide ou jouable, des
dimensions, un point de départ nommé, puis des profils humains de kit, terrain, lumière, navigation,
streaming et biome, une graine et un budget mémoire. Aucun UUID, hash, chemin absolu ou document JSON
n’est montré dans le parcours normal. Le point de départ jouable est choisi parmi les blueprints
canoniques disponibles ; ce gabarit est explicitement indisponible s’il n’en existe aucun.

**Prévisualiser** appelle `CreateAreaHandler` avec `CreateAreaCommand.DryRun`, produit le document
candidat complet et ne modifie ni l’historique ni les fichiers. **Créer la zone** revalide la
révision courante, repasse par le même handler Application, enregistre le résultat dans
`ProjectCommandHistory`, active le nouvel onglet et laisse la sauvegarde explicite. Les commandes de
l’assistant et celles du shell annulent ou rétablissent la mutation atomique.

### ORF-AREA-002

Le document créé possède son propre `AreaId`, ses dimensions, son `AreaFoundationProfile` et les
identités enfants stables du kit. La création ajoute seulement ce document : la zone déjà présente
reste la même instance métier, avec ses entités et profils voisins inchangés.

`ProjectSession.Save` écrit atomiquement la nouvelle zone sous `areas/` sans dupliquer ni fusionner
le document existant. Le smoke exige exactement un nouveau fichier de zone, ferme la session,
rouvre le workspace et compare nom, type, dimensions, paramètres de fondation et identités stables.
L’interface annonce avant application que le document indépendant ne sera écrit qu’au prochain
enregistrement.

## Limites honnêtes

- UX1-A couvre la création et l’indépendance du document ; redimensionnement, duplication, calques,
  édition de terrain et autres opérations de zone gardent leur statut `planned` ;
- la création appliquée reste volontairement non enregistrée jusqu’à `Ctrl+S` ; fermer sans sauver
  relève encore du parcours général de récupération, prévu en UX7 ;
- les détails techniques du profil de fondation restent accessibles aux diagnostics, mais ne sont
  pas promus dans l’assistant débutant ;
- les 717 routes, la parité des 66 fenêtres, l’export du Studio et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~AreaWorkflowTests|FullyQualifiedName~SetAreaFoundationHandlerTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~AreaJsonSerializerTests|FullyQualifiedName~ProjectSessionTests"
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-a-wizard -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-a-wizard -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-a/wizard-fhd.png -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-a-summary -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-a/summary-qhd.png -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-a -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-a/wizard-4k.png -Smoke -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-A, 44 fonctions disposent désormais d’une preuve Studio spécifique ; les
673 autres lignes restent explicitement `planned`.
