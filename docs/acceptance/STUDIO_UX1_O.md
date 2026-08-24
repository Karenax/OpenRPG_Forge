# Acceptation Studio — UX1-O Conversation

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-011`. Ce lot permet de
relier un placeable à des dialogues et de choisir le contexte qui déclenche chaque conversation
depuis l’unique fenêtre `Forge.Studio`. Il ne qualifie aucune autre route placeable ni une
distribution publique.

## Preuves communes

- `dotnet format OpenRPGForge.sln --no-restore --verify-no-changes` réussit sans changement requis ;
- build Debug `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : validation Application 1/1, runtime RPG Kit 4/4, ModuleFormat 5/5, MCP 1/1 et
  conservation des 66 fenêtres historiques 2/2, soit 13/13 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1O_PASS five_triggers=true dialogue_entries=true priority_once=true
  conversation_preview=true dry_run=true command_parity=true instance_selection=true
  preservation=true runtime_projection=true script_read=true optional_required=true undo_redo=true
  persistence=true invalid_blocked=true human_no_uuid=true ui=true ui_transaction=true
  project_restored=true windows=1` ;
- le même lancement conserve les lots Studio antérieurs au vert puis termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux dialogues, leurs points d’entrée, un
  objet, plusieurs liaisons et les cinq déclencheurs ; il refuse les références et sélecteurs
  invalides, compare le dry-run direct à la commande canonique, applique, annule, rétablit,
  sauvegarde puis rouvre ;
- le smoke UI utilise le vrai catalogue de Forgotten Forest, applique uniquement en mémoire puis
  annule et restaure exactement le projet courant ; aucune liaison de preuve n’est sauvegardée ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-o/` :
  `bindings-fhd.png` en 1920×1080, `sources-qhd.png` en 2560×1440 et `preview-4k.png` en
  3840×2160, chacune accompagnée de `.png.log`.

Les trois captures utilisent Godot 4.7.1, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets, zéro
fallback et une seule fenêtre. Elles montrent les entrées réelles **L’appel de Maëlys**, **Entrée
principale** et **Graine de lune** du catalogue Forgotten Forest. Full HD montre les liaisons et
l’aperçu sans écriture. QHD rend les champs conditionnels objet/événement et le résumé des cinq
déclencheurs lisibles. 4K confirme la priorité, l’usage unique et l’action principale sans coupe.
Cette inspection réelle porte sur le catalogue et l’interface ; la persistance canonique reste
qualifiée par le workspace synthétique afin de ne pas modifier le module authored.

### ORF-PLC-011

La page **Objets & interactions** expose désormais **Conversation** à côté des autres fiches
placeable. `Ctrl+K` fournit aussi **Configurer la conversation du placeable**. Les deux entrées
ouvrent le même document canonique, sans UUID, hash ni chemin de fichier dans la surface humaine.

Chaque carte choisit un dialogue par son nom, un point d’entrée, un déclencheur, une priorité et
l’option **Une seule fois par utilisateur**. Les cinq sources imposées par le dossier Aurora sont
présentes : interaction, état du placeable, profil utilisateur, objet présenté et événement reçu.
Les quatre sources textuelles affichent une situation attendue ; la source objet la remplace par un
choix d’item humain. Plusieurs liaisons peuvent viser le même dialogue.

Le nouveau `PlaceableConversationProfile` porte une identité stable, une provenance et des
`PlaceableConversationBinding` typés. Le champ Aurora historique `PlaceableWorldProfile.DialogueId`
reste conservé tel quel : une valeur existante peut initialiser une liaison d’interaction pour
l’auteur, mais n’est ni supprimée ni réinterprétée lors d’une édition inchangée.

**Prévisualiser la conversation** résout réellement la même fiche que le runtime sans écrire le
module. Les liaisons compatibles sont ordonnées par priorité, puis de façon déterministe ; une
liaison à usage unique déjà consommée est ignorée. Le résultat humain distingue conversation
lancée, liaison déjà utilisée et absence de correspondance.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au module**
réutilise le handler à la révision courante et rejoint `ProjectCommandHistory`, donc undo/redo et
sauvegarde explicite restent atomiques. Une fiche de conversation existante ne peut pas être
retirée accidentellement depuis une autre fiche placeable.

Le domaine exige des identités stables et uniques, au moins une liaison, des références vers un
dialogue et un point d’entrée présents, un item connu pour le déclencheur objet, un sélecteur pour
les autres sources et une priorité valide. Les identités partagées avec une autre capacité
placeable, les références inconnues et l’absence d’un profil requis au runtime sont localisées par
des diagnostics canoniques puis humanisées en français.

`PlaceableConversationRuntimeAdapter` conserve le repli optionnel et bloque l’absence requise. Son
runtime protège aussi contre un handle devenu obsolète. `PlaceableConversationService` exerce les
cinq déclencheurs, l’ordre de priorité et la règle d’usage unique dans le smoke et les tests ciblés.

`WorldFeatureScriptService`, donc la ressource MCP filtrée, expose `dialogue`,
`conversationBindingCount`, `conversationDialogueIds`, `conversationTriggers`,
`conversationEntryPoints` et `conversationRequiredAtRuntime`. Le plan MCP reste un dry-run et le
champ brut `dialogue` demeure lisible même lorsqu’aucun profil typé n’existe.

Les données hors périmètre restent conservées : identité et provenance du placeable, dialogue
Aurora historique, événements réactifs, fondation, opérations, placeables voisins, dialogues et
items du catalogue. Un profil typé inchangé conserve son objet canonique ; un profil édité conserve
son identité stable et le profil complet survit au round-trip.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-011` ; le verrouillage `ORF-PLC-012`, les clés, pièges et
  événements restent portés par leurs routes dédiées ;
- UX1-O résout la sélection canonique d’un dialogue, mais n’ajoute ni lecteur audiovisuel de
  conversation, ni synchronisation réseau, ni nouvel éditeur de graphe de dialogue ;
- les captures valident l’interface avec le catalogue réel de Forgotten Forest, pas une nouvelle
  liaison authored et sauvegardée dans Forgotten Forest ou Crypt of Ashes ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet format OpenRPGForge.sln --no-restore --verify-no-changes
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableConversationValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableConversationRuntimeAdapterTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~LegacyWindowMigrationTests"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-o-bindings -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-o-bindings -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-o/bindings-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-o-sources -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-o/sources-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-o-preview -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-o/preview-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-O, 59 fonctions disposent d’une preuve Studio spécifique ; les 658 autres lignes
restent explicitement `planned`.
