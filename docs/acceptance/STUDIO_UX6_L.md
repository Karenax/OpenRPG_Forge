# Acceptation Studio — UX6-L Outils runtime ForgeScript

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour `ORF-SCR-019`,
`ORF-SCR-020` et `ORF-SCR-023`, lot R099. UX6 atteint **50/84** ; l’incrément reste local
(`full_gate=false`).

La page **ForgeScript · Outils runtime R099** conserve l’autorité source UX6-F. L’auteur choisit
explicitement une source `.as`, configure les trois scénarios par formulaires humains, construit un
bloc versionné à relire, l’insère dans le brouillon puis passe simulation, compilation, aperçu sec,
enregistrement, annulation et rétablissement. Les scripts `.forge` historiques restent protégés.

<a id="orf-scr-019"></a>
## ORF-SCR-019 — API son et musique

Le formulaire couvre cue/émetteur, bus, volume, boucle, fondus, snapshot, auditeur et politique
d’écoute. La simulation `ForgeScriptRuntimeTools` exerce lecture/complétion, listener et snapshot,
puis le handle générationnel start/pause/resume/fade/stop. Le runtime natif compile et exécute les
intentions audio du bloc revu.

<a id="orf-scr-020"></a>
## ORF-SCR-020 — API temps

L’auteur règle échelle de simulation, avance, délai, répétition, handler et schedule. L’aperçu
exerce horloge, calendrier dérivé, timer dû, replanification et annulation sans consulter l’horloge
système. Le runtime natif produit les intentions `world_clock`, timer et délai déterministe.

<a id="orf-scr-023"></a>
## ORF-SCR-023 — Événements custom

Le formulaire expose nom namespacé, version, champ de payload typé, valeur et deux abonnés. La
simulation enregistre le schéma, publie sous autorité serveur avec corrélation, vérifie l’ordre
stable des livraisons et refuse autorité cliente ou payload invalide. Le bloc natif exerce
abonnement, construction et publication.

## Preuves

- build Debug final `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **15/15** — Scripting R099 et runtime natif **10**, adaptateur RPG Kit **5** ;
- portail incrémental final : **156,29 s**, build, 15/15 tests, smoke global, capture
  Forward+ FHD et contrôle des 717 lignes de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_L_PASS` : sélection explicite, autorité UX6-F, source revue,
  audio, temps, événements, catalogue API, exécution native, invalidités et approbation périmée,
  dry-run, compilation, apply/undo/redo, persistance, voisins, transaction UI, isolat libéré et
  surface restaurée ;
- captures Forward+ FHD inspectées, toutes en 1920×1080 :
  `ux6-l-script-runtime-tools-forward-plus-fhd.png`, `ux6-l-time-forward-plus-fhd.png` et
  `ux6-l-events-forward-plus-fhd.png` sous `artifacts/verification/studio-increment/` ; les trois
  onglets, la source, les diagnostics, la trace bornée et les six actions restent lisibles sans
  clipping ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-l-script-runtime-tools.summary.json`.

## Préservation et limites honnêtes

La preuve copie Forgotten Forest et une source UX6-F canonique dans un workspace temporaire. Elle
compare projet, historique, révision, journal, état dirty, zone active et approbations UX6-F/J/K,
vérifie le round-trip de la source, puis supprime l’isolat. La commande et l’historique restent ceux
de UX6-F ; aucune autorité de source parallèle n’est créée.

La simulation C# et l’exécution native sont réelles, mais leurs états restent jetables. Studio ne
crée aucun `AudioStreamPlayer`, ne pilote aucune horloge Godot, ne distribue aucun événement à une
session de jeu et n’ouvre aucun transport réseau. Ces effets restent l’autorité du runtime produit.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptRuntimeToolsTests|' +
  'FullyQualifiedName~Audio_time_custom_event_hot_reload_and_debugger_apis_emit_typed_tool_intents|' +
  'FullyQualifiedName~ForgeScriptRuntimeToolAdapterTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-l-script-runtime-tools -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-l-script-runtime-tools-forward-plus-fhd.png
```
