# Acceptation — interface de combat dynamique du runtime

Dernière validation locale : 15 août 2026.

## Périmètre accepté

Le runtime projette désormais chaque `CombatActionResult` résolu vers une couche de présentation
sans recalculer ni modifier les dégâts, soins, ressources, recharges, décès, butin ou objectifs.
La tranche verticale de référence est l'attaque réelle `forest-strike` de Forgotten Forest :
12 dégâts, cible à 22/34 PV, cue `cue.forest-strike` et recharge exacte de 0 seconde. La recharge
non nulle, le soin, la restauration de ressource et le chip joueur restent des fixtures de
présentation explicitement marquées hors gameplay.

Le lot couvre :

- barres animées avec front et chip retardé, ou mise à jour instantanée en mouvements réduits ;
- cadre de cible et plaques ennemies projetées à l'écran avec priorité et occlusion bornées ;
- nombres flottants distincts pour dégâts, soins et ressource, sans dépendre uniquement de la couleur ;
- état réel du slot d'attaque, recharge radiale et numérique uniquement quand la capacité en possède une ;
- toasts d'objectif, combat, butin, alerte et système, avec déduplication et capacité de trois ;
- huit pings temporaires de mini-carte, sans mutation de la carte persistante ni du brouillard ;
- pool d'impacts, accent écran, impulsion caméra, micro hit-stop et pool audio SFX ;
- cue d'aire quand il existe et son procédural déterministe en repli ;
- contraste élevé, modes de vision des couleurs, texte ×1,2/×2 et politique complète de mouvements réduits.

Les overlays de carte, roue d'actions, dialogue et menus masquent la couche de feedback. Le bus
`SFX` est déclaré dans `apps/Forge.Runtime/default_bus_layout.tres`; le couper conserve le retour
visuel et ne change aucun état autoritaire.

## Bornes et budgets

| Ressource | Capacité stable |
|---|---:|
| Nombres flottants | 48 |
| Plaques, cible réservée comprise | 32 |
| Impacts particulaires | 12 |
| Lecteurs audio 3D | 8 |
| Pings de mini-carte | 8 |
| Toasts simultanés | 3 |

Le stress réchauffé de 600 frames ne fait croître aucun pool et reste sous les deux budgets dédiés :

| Profil | Allocations managées | p95 présentation | Temps cumulé |
|---|---:|---:|---:|
| Normal, texte ×1,2 | 27 824 octets | 0,008 ms | 1,419 ms |
| Mouvements réduits, texte ×1,2 | 27 824 octets | 0,009 ms | 1,872 ms |
| Normal, texte ×2 | 27 824 octets | 0,008 ms | 1,591 ms |

Budgets : au plus 64 Kio d'allocations managées et 1 ms p95 pour le contrôleur de présentation.
En mouvements réduits, les particules, l'impulsion caméra et le hit-stop restent à zéro ; les
barres sont instantanées, les nombres immobiles et la recharge numérique.

## Preuve Forward+

Rapport de suite :
`artifacts/verification/runtime-combat-feedback/run-20260814-214156/capture-suite-report.json`.

La suite contient 30 PNG vérifiés (dimensions PNG, taille minimale, contrat JSON et revue visuelle) :

| Cas | Étapes | Statut |
|---|---:|---|
| 1920×1080 normal, texte ×1,2 | 6 | accepté |
| 1920×1080 mouvements réduits, texte ×1,2 | 6 | accepté |
| 2560×1440 normal, texte ×1,2 | 6 | accepté |
| 2560×1440 mouvements réduits, texte ×1,2 | 6 | accepté |
| 1920×1080 normal, texte ×2 | 6 | accepté |

Chaque cas contient : pré-impact, pic d'impact, chip retardé, état stabilisé/prêt,
objectif/toast/ping et fixtures hors gameplay. Les cinq premières images utilisent le vrai résultat
de combat ; seule la sixième est une fixture. La revue à résolution native confirme l'absence de
troncature dans les cadres cible/joueur, plaques, objectif, toasts et prompt d'interaction.

## Passage interactif

`scripts/Run-ForgottenForest.ps1 -SkipAuthoring` a été rejoué en Forward+ dans une vraie fenêtre :

- le slot d'attaque sans cible affiche `Aucune cible hostile à portée.` ;
- l'atlas des six zones s'ouvre et masque le HUD de combat, puis restaure la composition ;
- la roue d'actions est exclusive et son choix `Interagir` est renvoyé au même circuit de commande ;
- cette interaction a chargé l'aire `clairval`, confirmée par les événements `AreaExited`,
  `AreaEntered` et `OPENRPG_FORGE_RUNTIME_AREA_TRANSITION_PASS` ;
- aucun `SCRIPT ERROR`, échec de démarrage ou exception non gérée n'a été observé.

La synthèse clavier de l'outil d'automatisation Windows n'est pas interprétée par cette fenêtre
Godot ; le raccourci `[1]` est donc couvert par le smoke moteur et la commande équivalente a été
validée interactivement par son bouton. L'automatisation ne capture pas la sortie sonore : le routage
cue/fallback et le mute SFX sont prouvés par les marqueurs dédiés, pas par une appréciation auditive.

Le journal interactif contient des avertissements Godot préexistants sur l'interpolation de caméra,
qualifiés `possibly benign`; ils n'empêchent ni la transition ni les gates de feedback.

## Portail intégré

Le portail final `scripts/Verify.ps1` passe sur le worktree complet : restauration verrouillée,
audit NuGet sans vulnérabilité, format, build Release sans avertissement, 1 894 tests .NET,
39 tests Python, documentation, 717 exigences Aurora, 5 461 preuves, graphe Roslyn déterministe
à 8 392 nœuds et 52 352 relations sans diagnostic, Toolset, Forward+, distributions, réseau et MCP.
Les profils `runtime-combat-feedback-normal`, `runtime-combat-feedback-reduced` et
`runtime-combat-feedback-text-scale-2` font partie de ce portail et passent tous les trois.

## Reproduction

```powershell
dotnet test tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj --configuration Debug --no-restore `
  --filter "FullyQualifiedName~Forge.Domain.Tests.Combat.CombatResolverTests"

dotnet test tests\Forge.Application.Tests\Forge.Application.Tests.csproj --configuration Debug --no-restore `
  --filter "FullyQualifiedName~Forge.Application.Tests.Combat.CombatFeedbackPresentationServiceTests|FullyQualifiedName~Forge.Application.Tests.Combat.ProceduralCombatFeedbackAudioServiceTests"

& .\scripts\Capture-RuntimeCombatFeedback.ps1 `
  -WorkspaceRoot .\apps\Forge.Runtime\content\forgotten-forest `
  -OutputRoot .\artifacts\verification\runtime-combat-feedback `
  -Resolutions 1920x1080,2560x1440 `
  -StressFrames 600

& .\scripts\Run-ForgottenForest.ps1 -SkipAuthoring
& .\scripts\Verify.ps1
```
