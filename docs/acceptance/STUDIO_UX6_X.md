# Acceptation Forge Studio — UX6-X

Date de qualification : 21 août 2026.

UX6-X rend `ORF-GAME-026` et `ORF-EE-026` directement accessibles sur la page Runtime dans la
surface **Expérience UI runtime**. La carte, le journal et le HUD restent dans le profil gameplay
du module ; la police et les échelles restent dans le profil utilisateur Studio. La surface expose
donc honnêtement deux aperçus secs, deux commandes et deux historiques, sans prétendre à une
atomicité inexistante entre ces autorités.

<a id="orf-game-026"></a>
## ORF-GAME-026 — Carte, journal et HUD

- sélection explicite du module ouvert, sans chargement implicite du premier profil ;
- activation et exigence runtime du shell, kit, skin et document de mise en page éditables ;
- carte, journal et HUD visibles séparément, avec échelle exacte et reflow ;
- actions d’entrée ordonnées, agnostiques du clavier ou de la manette ;
- résolution réelle par `GameplaySessionServices.ResolveShell` sur une copie ;
- dry-run et application par `SetGameplayControlProfileHandler`, avec révision, empreinte projet,
  approbation liée au brouillon, undo/redo et round-trip canonique.

<a id="orf-ee-026"></a>
## ORF-EE-026 — TrueType et scaling UI

- échelles UI et texte saisies directement, sans arrondi silencieux des valeurs canoniques ;
- police principale, chaîne de fallbacks ordonnée et aperçu Godot local ;
- gouvernance lisible depuis les manifestes de police : scripts, locales, paliers DPI et bornes ;
- les familles système héritées restent explicitement identifiées et ne créent aucun asset ;
- aperçu puis application via `EditorWorkspaceProfileCommandHistory`, avec empreinte du profil,
  undo/redo, sauvegarde et réouverture du store utilisateur.

## Transaction et préservation

Le bouton Appliquer exécute d’abord la commande projet, puis la commande du profil utilisateur. Si
la seconde échoue, la première est annulée. Undo et redo rejouent symétriquement les deux historiques.
La preuve vérifie le shell, les profils gameplay voisins, tous les voisins du profil workspace, les
zones, blueprints, dialogues, quêtes et WorldFeatures. Le projet source, son historique, la sélection,
les approbations et l’état dirty restent inchangés ; le workspace temporaire est supprimé.

## Validation obtenue

- build Studio Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **22/22** — Domain 9, Application 10 et ModuleFormat 3 ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX6_X_PASS`, avec sélection explicite, invalidités, deux
  dry-runs, deux commandes canoniques, résolutions shell/typographie, gardes périmées, undo/redo,
  round-trip, voisins, texte humain, bornage, transaction UI, ressources libérées, source inchangée
  et surface restaurée ;
- portail incrémental final : **162,88 s**, build, tests, smoke,
  capture Forward+ FHD et contrôle des 717 lignes ; `full_gate=false` reste explicite ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-x-runtime-ui.summary.json` ;
- captures Forward+ 1920×1080 inspectées :
  `artifacts/verification/studio-ux6-x-hud-shell-1920x1080.png` et
  `artifacts/verification/studio-ux6-x-type-scale-1920x1080.png`.

La première capture affichait la représentation binaire longue de `1.15`. Les trois échelles sont
désormais des champs numériques directs, lisibles et sans quantification silencieuse ; les deux
onglets, leurs résultats et les cinq actions restent visibles sans clipping à Full HD.

## Limites honnêtes

Studio résout le shell et rend un échantillon typographique local sur des copies. Il n’instancie ni
HUD de jeu, ni carte, ni journal, ni thème global, ni périphérique d’entrée, ni viewport, ni nœud
runtime ; il ne mesure aucun GPU et n’applique rien à une session de jeu live.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~GameplaySessionProfileTests|' +
  'FullyQualifiedName~EditorWorkspaceProfileTests|' +
  'FullyQualifiedName~GameplaySessionServicesTests|' +
  'FullyQualifiedName~GameplaySessionScriptServiceTests|' +
  'FullyQualifiedName~SetGameplayControlProfileHandlerTests|' +
  'FullyQualifiedName~EditorWorkspaceProfileCommandHistoryTests|' +
  'FullyQualifiedName~GameplayControlPersistenceTests|' +
  'FullyQualifiedName~EditorWorkspaceProfileStoreTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-x-runtime-ui -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-ux6-x-hud-shell-1920x1080.png
```
