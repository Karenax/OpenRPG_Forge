# Acceptation Studio — UX5 Production narrative complète

État local au 20 août 2026 : **fonctionnel et qualifié** pour les 64 routes UX5 restantes.
Avec UX5-A (`ORF-DLG-001..005`), la vague Histoire est close à **69/69**.

La surface **Production narrative UX5** réunit cinq workflows humains — Dialogue, Quête,
Localisation, Lore et Cinématiques — sans inventer de transaction inter-agrégats. Chaque workflow
garde sa commande canonique, son aperçu sec, son approbation liée à la révision et aux empreintes,
son historique, son undo/redo et son round-trip.

## Dialogue de production

La sélection d’un dialogue réel précède l’édition. La commande `SetDialogueHandler` complète et
valide les profils Authoring, Experience, Presentation et Production, puis la simulation parcourt
une copie en mémoire. Les nœuds, choix et liens UX5-A restent l’autorité structurelle.

<a id="orf-dlg-006"></a>
- **ORF-DLG-006 — Texte localisé** : les textes, variantes et sous-titres conservent clé, fallback et provenance.
<a id="orf-dlg-007"></a>
- **ORF-DLG-007 — Tokens de texte** : la mémoire typée et les paramètres de sous-graphe portent leurs types canoniques.
<a id="orf-dlg-008"></a>
- **ORF-DLG-008 — Participants multiples** : rôles et participants sont résolus depuis les blueprints réels.
<a id="orf-dlg-009"></a>
- **ORF-DLG-009 — Conditions d’apparition** : conditions de lignes, choix, interjections et variantes sont préservées et validées.
<a id="orf-dlg-010"></a>
- **ORF-DLG-010 — Évaluation dans l’ordre** : ordre des nœuds, choix, actions et conditions reste canonique.
<a id="orf-dlg-011"></a>
- **ORF-DLG-011 — Actions sur nœud** : actions de nœud, de fin et d’abandon sont authorées avec IDs stables.
<a id="orf-dlg-012"></a>
- **ORF-DLG-012 — Assistant de scripts de dialogue** : les bindings existants sont conservés ; aucun script n’est exécuté par Studio.
<a id="orf-dlg-013"></a>
- **ORF-DLG-013 — Commentaires de nœud** : commentaires, révisions, verrous et conflits sémantiques sont persistés.
<a id="orf-dlg-014"></a>
- **ORF-DLG-014 — Animations de dialogue** : une cue d’animation gouvernée est liée à une ligne réelle.
<a id="orf-dlg-015"></a>
- **ORF-DLG-015 — Sons et voix** : voix et sous-titre accessible sont authorés sans lancer de playback.
<a id="orf-dlg-016"></a>
- **ORF-DLG-016 — Caméra de conversation** : cadrage et champ de vision sont validés sans déplacer la caméra live.
<a id="orf-dlg-017"></a>
- **ORF-DLG-017 — Scripts de fin et d’abandon** : les deux frontières sont distinctes et visibles dans l’aperçu.
<a id="orf-dlg-018"></a>
- **ORF-DLG-018 — Recherche dans les conversations** : recherche humaine et remplacement sont prévisualisés sur le graphe copié.
<a id="orf-dlg-019"></a>
- **ORF-DLG-019 — Mise en évidence et filtres** : résultats, branches et états sont résumés sans masquer le graphe source.
<a id="orf-dlg-020"></a>
- **ORF-DLG-020 — Bookmarks** : signets partagés et portée sont persistés avec identité stable.
<a id="orf-dlg-021"></a>
- **ORF-DLG-021 — Mode test** : la simulation déterministe rapporte choix, erreurs et couverture sans runtime live.
<a id="orf-dlg-022"></a>
- **ORF-DLG-022 — Mémoire de conversation** : portée, durée, type et valeur initiale sont validés.
<a id="orf-dlg-023"></a>
- **ORF-DLG-023 — Interjections de compagnons** : participant, priorité et politique d’interruption sont authorés.
<a id="orf-dlg-024"></a>
- **ORF-DLG-024 — Checks de compétence** : compétence, difficulté et visibilité sont prévisualisées avec graine stable.
<a id="orf-dlg-025"></a>
- **ORF-DLG-025 — Choix temporisés** : délai, choix par défaut et pause d’accessibilité sont persistés.
<a id="orf-dlg-026"></a>
- **ORF-DLG-026 — Graphes imbriqués** : contrat, paramètres, invocation et test de sous-graphe sont cohérents.
<a id="orf-dlg-027"></a>
- **ORF-DLG-027 — Variantes procédurales** : seed, portée et variantes localisées restent data-driven.
<a id="orf-dlg-028"></a>
- **ORF-DLG-028 — Relations et conséquences** : conséquences typées couvrent quête, faction, variable et relation.
<a id="orf-dlg-029"></a>
- **ORF-DLG-029 — Validation structurelle** : le validateur bloque références, cycles de flux et valeurs invalides.
<a id="orf-dlg-030"></a>
- **ORF-DLG-030 — Couverture de branches** : politique, preuve et seuil de couverture sont calculés sur la copie.
<a id="orf-dlg-031"></a>
- **ORF-DLG-031 — Édition collaborative** : révision, verrou, commentaire et conflit sont conservés ensemble.
<a id="orf-dlg-032"></a>
- **ORF-DLG-032 — Génération IA contrôlée** : proposition, dépendances et niveau AuthorApply exigent une approbation humaine.
<a id="orf-ai-016"></a>
- **ORF-AI-016 — Création de dialogue** : le plan gouverné complète un dialogue réel, sans fournisseur externe.
<a id="orf-ai-026"></a>
- **ORF-AI-026 — Simulation de dialogue** : le parcours déterministe est séparé de toute exécution acteur, son ou caméra.

## Quête complète

`SetQuestHandler` remplace uniquement la quête sélectionnée. Journal, états, transitions,
Authoring, Experience, Operations, Simulation et Quality sont validés ensemble ; les ressources
et participants proviennent du projet réel.

<a id="orf-qst-001"></a>
- **ORF-QST-001 — Catégorie de journal** : catégorie, tri et priorité sont explicites.
<a id="orf-qst-002"></a>
- **ORF-QST-002 — Entrées numériques du journal** : ordre et progression numérique sont conservés dans les états et objectifs.
<a id="orf-qst-003"></a>
- **ORF-QST-003 — État courant par joueur** : la politique d’instance et la simulation utilisent une portée joueur.
<a id="orf-qst-004"></a>
- **ORF-QST-004 — États terminé et échoué** : les états terminaux et leurs transitions restent canoniques.
<a id="orf-qst-005"></a>
- **ORF-QST-005 — Texte d’entrée localisé** : titre, description, étapes, historique et notifications gardent leurs clés.
<a id="orf-qst-006"></a>
- **ORF-QST-006 — Objectifs explicites** : objectifs ordonnés, type, quantité et conditions sont authorés.
<a id="orf-qst-007"></a>
- **ORF-QST-007 — Transitions d’état** : transitions et dépendances sont préservées et simulées.
<a id="orf-qst-008"></a>
- **ORF-QST-008 — Récompenses** : bundle, expérience et conséquences sont validés avant application.
<a id="orf-qst-009"></a>
- **ORF-QST-009 — Priorité du journal** : priorité source et politique de tri sont visibles dans le diff.
<a id="orf-qst-010"></a>
- **ORF-QST-010 — Commentaires auteur** : intention, canon, auteur, revues et checklist sont persistés.
<a id="orf-qst-011"></a>
- **ORF-QST-011 — Plot Wizard** : zones, participants, props, dialogues, objectifs et scénarios sont liés par IDs stables.
<a id="orf-qst-012"></a>
- **ORF-QST-012 — Cast du scénario** : participants réels et rôles sont résolus sans UUID affiché.
<a id="orf-qst-013"></a>
- **ORF-QST-013 — Props du scénario** : les blueprints existants alimentent le plan de quête.
<a id="orf-qst-014"></a>
- **ORF-QST-014 — Étapes de scénario** : étapes, objectifs et transitions sont ordonnés.
<a id="orf-qst-015"></a>
- **ORF-QST-015 — Quêtes cachées ou révélées** : progression Hidden, Rumor, Discovered et conditions est authorée.
<a id="orf-qst-016"></a>
- **ORF-QST-016 — Quêtes répétables** : politique de répétition et scénarios de test sont validés.
<a id="orf-qst-017"></a>
- **ORF-QST-017 — Quêtes de groupe** : partage, synchronisation et séparation restent data-driven.
<a id="orf-qst-018"></a>
- **ORF-QST-018 — Choix exclusifs** : mutex, confirmation et conséquences sont représentés par le profil Operations.
<a id="orf-qst-019"></a>
- **ORF-QST-019 — Chronologie des événements** : historique ordonné, cause et métadonnées sont persistés.
<a id="orf-qst-020"></a>
- **ORF-QST-020 — Pins de carte** : marqueur, zone, visibilité, priorité et position sont validés.
<a id="orf-qst-021"></a>
- **ORF-QST-021 — Notifications** : texte accessible, groupement et cooldown sont authorés.
<a id="orf-qst-022"></a>
- **ORF-QST-022 — Simulation de quête** : le moteur déterministe vérifie le parcours et les événements sur copie.
<a id="orf-qst-023"></a>
- **ORF-QST-023 — Validation narrative** : ProjectValidator et QuestValidator bloquent les incohérences.
<a id="orf-qst-024"></a>
- **ORF-QST-024 — Tests générés** : cinq scénarios déterministes couvrent nominal, échec, répétition, groupe et reprise.
<a id="orf-qst-025"></a>
- **ORF-QST-025 — Génération IA de quête** : plan, dépendances, contexte et approbation AuthorApply sont persistés.
<a id="orf-ai-017"></a>
- **ORF-AI-017 — Création de quête** : la proposition gouvernée complète une quête réelle sans appel externe.
<a id="orf-ai-027"></a>
- **ORF-AI-027 — Simulation de quête** : les résultats restent une simulation en mémoire, jamais une session gameplay.

## Localisation, lore et cinématiques

<a id="orf-aud-019"></a>
- **ORF-AUD-019 — Sous-titres de sons importants** : une clé dédiée, son fallback et ses traductions sont extraits.
<a id="orf-cont-011"></a>
- **ORF-CONT-011 — Localisation** : toutes les chaînes du catalogue sont complétées pour `fr-FR` et `en-US`.
<a id="orf-cont-012"></a>
- **ORF-CONT-012 — Police TrueType et UI scalable** : police et échelle passent par l’historique du profil workspace.
<a id="orf-ai-003"></a>
- **ORF-AI-003 — Index de lore** : entrées gouvernées, tags, sources et budget de contexte sont persistés dans le workspace.
<a id="orf-ai-004"></a>
- **ORF-AI-004 — Graph de références** : relations et sources projet sont résolues, recherchées et bornées.
<a id="orf-proj-018"></a>
- **ORF-PROJ-018 — Film ou séquence de démarrage** : le startup flow référence la séquence créée atomiquement.
<a id="orf-game-019"></a>
- **ORF-GAME-019 — Cutscenes** : timeline, piste dialogue, durée et skip sont authorés via `SetCinematicProfilesHandler`.
<a id="orf-game-027"></a>
- **ORF-GAME-027 — Cinematics and dialogue control locks** : verrouillage puis déverrouillage explicite des contrôles encadre la timeline.

## Preuves

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- portail incrémental : **149,45 s**, 186/186 tests ciblés (Domain 93, Application 62,
  ModuleFormat 31), smoke global, matrice et capture Forward+ FHD ;
- smoke `OPENRPG_FORGE_STUDIO_UX5B_PASS` : cinq profils complets, deux simulations,
  dry-run, gardes source/approbation, cinq commandes/historiques, undo/redo, save/reopen,
  voisins préservés, transaction UI isolée, source et surface restaurées, ressources libérées ;
- captures FHD inspectées pour les cinq onglets :
  `ux5-b-dialogue-production-forward-plus-fhd.png`,
  `ux5-c-quest-authoring-forward-plus-fhd.png`, `ux5-d-localization-forward-plus-fhd.png`,
  `ux5-e-lore-forward-plus-fhd.png`, `ux5-f-cinematics-forward-plus-fhd.png` ;
- jalon Dialogue inspecté aussi à 2560×1440 et 3840×2160 sous
  `artifacts/verification/studio-shell/ux5-milestone/` : aucun clipping bloquant ni action cachée.

## Préservation et limites honnêtes

La preuve travaille sur une copie temporaire du module réel, conserve projet, historique, état dirty,
zone active et approbations source, puis supprime le workspace temporaire. Dialogue, Quête,
Localisation et Cinématiques utilisent chacun la commande canonique du projet ; Lore et la
typographie gardent leurs historiques workspace dédiés. La surface n’annonce donc jamais une
atomicité globale fictive.

Studio ne joue aucun dialogue ou son, ne déplace aucune caméra ou acteur, n’exécute aucun script,
modèle IA ou transport réseau. Les assistants IA sont des plans déterministes gouvernés et déjà
approuvés dans la copie ; les modes test sont des simulations en mémoire.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~Dialogue|' +
  'FullyQualifiedName~Quest|' +
  'FullyQualifiedName~Localization|' +
  'FullyQualifiedName~LoreIndex|' +
  'FullyQualifiedName~Cinematic|' +
  'FullyQualifiedName~SequencePlayer|' +
  'FullyQualifiedName~AgentReferenceGraph'
& .\scripts\Verify-StudioIncrement.ps1 -Page dialogues `
  -Evidence ux5-b-dialogue-production `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux5-b-dialogue-production-forward-plus-fhd.png
```
