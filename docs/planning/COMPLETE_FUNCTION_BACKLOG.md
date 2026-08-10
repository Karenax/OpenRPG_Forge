# Plan canonique de complétude fonctionnelle

> Fichier généré par `scripts/Update-CompleteFunctionBacklog.ps1`. Ne pas modifier à la main.

## Portée et interprétation

Ce plan couvre exactement les 717 fonctions du catalogue Aurora clean-room immuable. Il ne
déclare pas que les 717 fonctions sont terminées : un gate vertical fermé prouve un parcours
utilisateur, tandis qu'une fonction n'est close ici que lorsque le tracker projet la classe
`Tested` avec une preuve automatisée ou une procédure reproductible liée.

Les identifiants `FC-<DOMAINE>-<PRIORITÉ>-<NN>` sont stables tant que le catalogue source reste
immuable. Chaque lot contient de une à cinq fonctions d'un même domaine et d'une même priorité.
La présence d'un lot dans ce plan ne l'ouvre pas automatiquement : une décision explicite choisit
un seul lot actif après vérification de ses dépendances.

## État de couverture

| Mesure | Valeur |
|---|---:|
| Fonctions source et tracker | 717 |
| Lots bornés générés | 187 |
| Lots clos | 103 |
| Lots à compléter et prouver | 82 |
| Lots à livrer | 2 |
| Lots P0 ouverts | 0 |
| Fonctions P0 non closes | 0 |
| Tested | 398 |
| Implemented partiel | 314 |
| Designed | 0 |
| Researched | 5 |
| NotStarted | 0 |

## Contrats de sortie hérités par chaque lot

- `DELIVER` : relire les critères source, définir le contrat canonique, implémenter le parcours
  humain sans JSON, ajouter les tests de domaine/application et le smoke Toolset/runtime/MCP
  applicable, puis lier les preuves avant tout passage à `Tested`.
- `COMPLETE` : auditer la note `Partial`, fermer chaque comportement manquant, ajouter ou renforcer
  les tests ciblés et la preuve visible/headless applicable, puis seulement passer à `Tested`.
- `MAINTAIN` : conserver les preuves existantes et ne rouvrir le lot qu'en cas de régression ou
  de critère source explicitement non satisfait.

Avant implémentation, une fiche détaillée est créée sous `docs/aurora-parity/tasks/` avec les
fichiers probables, dépendances, critères utilisateur et commandes de test exactes. Le présent
registre garantit la couverture et la taille ; il ne remplace pas cette conception bornée.

## Ordre d’exécution recommandé

Le rang `R###` est une file de travail, pas un nouvel identifiant fonctionnel. Il ordonne les
lots par priorité, premier gate cible, parcours utilisateur puis identifiant stable. Le rang ne
change pas lorsqu’un lot est fermé ; les lots clos disparaissent simplement de la file ouverte.
Les dépendances déclarées viennent du catalogue source. Avant activation, elles sont
réévaluées contre le tracker : une dépendance non close passe avant le lot ou reçoit une décision
explicite. Un seul lot de une à cinq fonctions est actif à la fois.

| Rang | Lot | Priorité | Gate(s) | Parcours | Dépendances déclarées | Progression | Contrat |
|---:|---|---|---|---|---|---:|---|
| `R051` | `FC-PROJECT-P1-01` | P1 | G7 | Fondations | CREATURE, GAMEPLAY, ITEM, MULTI, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R052` | `FC-PROJECT-P1-02` | P1 | G7 | Fondations | AREA, CONTENT, GAMEPLAY, SCRIPT | 0/3 `Tested` | `COMPLETE` |
| `R053` | `FC-AREA-P1-01` | P1 | G7 | Creator | BUILD, PALETTE, QA | 0/5 `Tested` | `COMPLETE` |
| `R054` | `FC-AREA-P1-02` | P1 | G7 | Creator | BUILD, EE, GAMEPLAY, PROJECT | 0/5 `Tested` | `COMPLETE` |
| `R055` | `FC-AREA-P1-03` | P1 | G7 | Creator | AUDIO, CONTENT, EE, QA, SCRIPT, WAYPOINT | 0/5 `Tested` | `COMPLETE` |
| `R056` | `FC-AREA-P1-04` | P1 | G7 | Creator | BUILD | 0/1 `Tested` | `COMPLETE` |
| `R057` | `FC-PALETTE-P1-01` | P1 | G7 | Creator | BUILD, CONTENT | 0/5 `Tested` | `COMPLETE` |
| `R058` | `FC-PALETTE-P1-02` | P1 | G7 | Creator | AREA, BUILD, QA | 0/3 `Tested` | `COMPLETE` |
| `R059` | `FC-SHELL-P1-01` | P1 | G7 | Creator | AI, AREA, DM, PALETTE, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R060` | `FC-SHELL-P1-02` | P1 | G7 | Creator | BUILD | 0/1 `Tested` | `COMPLETE` |
| `R061` | `FC-AUDIO-P1-01` | P1 | G7 | Writer | AREA, PALETTE, QA | 0/5 `Tested` | `COMPLETE` |
| `R062` | `FC-AUDIO-P1-02` | P1 | G7 | Writer | AREA, GAMEPLAY, PROJECT, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R063` | `FC-AUDIO-P1-03` | P1 | G7 | Writer | QA | 0/1 `Tested` | `COMPLETE` |
| `R064` | `FC-WAYPOINT-P1-01` | P1 | G7 | Writer | AREA, CREATURE, QUEST, SCRIPT, SHELL | 0/5 `Tested` | `COMPLETE` |
| `R065` | `FC-WAYPOINT-P1-02` | P1 | G7 | Writer | AUDIO, CREATURE, QA, SCRIPT | 0/4 `Tested` | `COMPLETE` |
| `R066` | `FC-GAMEPLAY-P1-01` | P1 | G7 | RPG | AREA, EE, MULTI, PLACEABLE, PROJECT | 0/5 `Tested` | `COMPLETE` |
| `R067` | `FC-GAMEPLAY-P1-02` | P1 | G7 | RPG | DIALOGUE, EE, MULTI, QA, WAYPOINT | 0/5 `Tested` | `COMPLETE` |
| `R068` | `FC-DIALOGUE-P1-01` | P1 | G8 | Writer | BUILD, CREATURE, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R069` | `FC-DIALOGUE-P1-02` | P1 | G8 | Writer | BUILD, CREATURE, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R070` | `FC-DIALOGUE-P1-03` | P1 | G8 | Writer | AI, BUILD, FACTION, QA, QUEST | 0/5 `Tested` | `COMPLETE` |
| `R071` | `FC-QUEST-P1-01` | P1 | G8 | Writer | BUILD, CREATURE, DIALOGUE, FACTION, ITEM, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R072` | `FC-QUEST-P1-02` | P1 | G8 | Writer | AREA, DIALOGUE, DOOR, ITEM, PLACEABLE, QA, TRIGGER, WAYPOINT | 0/5 `Tested` | `COMPLETE` |
| `R073` | `FC-QUEST-P1-03` | P1 | G8 | Writer | AI, QA | 0/2 `Tested` | `COMPLETE` |
| `R074` | `FC-COMPANION-P1-01` | P1 | G8 | RPG | CREATURE, DIALOGUE, PROJECT, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R075` | `FC-COMPANION-P1-02` | P1 | G8 | RPG | CREATURE, GAMEPLAY, ITEM, MULTI, SCRIPT, WAYPOINT | 0/5 `Tested` | `COMPLETE` |
| `R076` | `FC-COMPANION-P1-03` | P1 | G8 | RPG | AREA, CREATURE, EE, ITEM, QA, WAYPOINT | 0/3 `Tested` | `COMPLETE` |
| `R077` | `FC-CREATURE-P1-01` | P1 | G8 | RPG | CONTENT, ENCOUNTER, FACTION, PALETTE, QA | 0/5 `Tested` | `COMPLETE` |
| `R078` | `FC-CREATURE-P1-02` | P1 | G8 | RPG | ITEM, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R079` | `FC-CREATURE-P1-03` | P1 | G8 | RPG | AREA, DIALOGUE, DM, FACTION, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R080` | `FC-CREATURE-P1-04` | P1 | G8 | RPG | AREA, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R081` | `FC-DOOR-P1-01` | P1 | G8 | RPG | AUDIO, ITEM, PLACEABLE, SCRIPT, TRIGGER | 0/5 `Tested` | `COMPLETE` |
| `R082` | `FC-DOOR-P1-02` | P1 | G8 | RPG | SCRIPT | 0/1 `Tested` | `COMPLETE` |
| `R083` | `FC-ENCOUNTER-P1-01` | P1 | G8 | RPG | CREATURE, MULTI, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R084` | `FC-ENCOUNTER-P1-02` | P1 | G8 | RPG | AREA, AUDIO, MULTI, QUEST, SCRIPT, WAYPOINT | 0/5 `Tested` | `COMPLETE` |
| `R085` | `FC-ENCOUNTER-P1-03` | P1 | G8 | RPG | QA | 0/1 `Tested` | `COMPLETE` |
| `R086` | `FC-FACTION-P1-01` | P1 | G8 | RPG | BUILD, GAMEPLAY, MULTI | 0/5 `Tested` | `COMPLETE` |
| `R087` | `FC-FACTION-P1-02` | P1 | G8 | RPG | DIALOGUE, DM, QA, SCRIPT | 0/4 `Tested` | `COMPLETE` |
| `R088` | `FC-ITEM-P1-01` | P1 | G8 | RPG | CONTENT, MULTI, PALETTE, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R089` | `FC-ITEM-P1-02` | P1 | G8 | RPG | CREATURE, DM, MULTI, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R090` | `FC-ITEM-P1-03` | P1 | G8 | RPG | QA, SCRIPT, SHELL | 0/3 `Tested` | `COMPLETE` |
| `R091` | `FC-MERCHANT-P1-01` | P1 | G8 | RPG | DIALOGUE, FACTION, ITEM, PALETTE, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R092` | `FC-MERCHANT-P1-02` | P1 | G8 | RPG | EE, MULTI, QA, SCRIPT | 0/3 `Tested` | `COMPLETE` |
| `R093` | `FC-PLACEABLE-P1-01` | P1 | G8 | RPG | DM, GAMEPLAY, ITEM, MULTI, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R094` | `FC-PLACEABLE-P1-02` | P1 | G8 | RPG | ITEM, SCRIPT, TRIGGER | 0/5 `Tested` | `COMPLETE` |
| `R095` | `FC-PLACEABLE-P1-03` | P1 | G8 | RPG | GAMEPLAY, PALETTE, SCRIPT | 0/3 `Tested` | `COMPLETE` |
| `R096` | `FC-TRIGGER-P1-01` | P1 | G8 | RPG | FACTION, GAMEPLAY, QA, SCRIPT | 0/4 `Tested` | `COMPLETE` |
| `R097` | `FC-SCRIPT-P1-01` | P1 | G8 | Scripter | CREATURE, DIALOGUE, GAMEPLAY, ITEM, QUEST, SHELL | 0/5 `Tested` | `COMPLETE` |
| `R098` | `FC-SCRIPT-P1-02` | P1 | G8 | Scripter | AREA, CREATURE, DIALOGUE, FACTION, ITEM, QUEST | 0/5 `Tested` | `COMPLETE` |
| `R099` | `FC-SCRIPT-P1-03` | P1 | G8 | Scripter | AUDIO, BUILD, MULTI, PROJECT, QA | 0/5 `Tested` | `COMPLETE` |
| `R100` | `FC-SCRIPT-P1-04` | P1 | G8 | Scripter | AI, EE, GAMEPLAY, QA | 0/5 `Tested` | `COMPLETE` |
| `R101` | `FC-SCRIPT-P1-05` | P1 | G8 | Scripter | AI | 0/1 `Tested` | `COMPLETE` |
| `R102` | `FC-MULTI-P1-01` | P1 | G9 | Network | AREA, BUILD, COMPANION, GAMEPLAY, QUEST, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R103` | `FC-MULTI-P1-02` | P1 | G9 | Network | CONTENT, DM, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R104` | `FC-MULTI-P1-03` | P1 | G9 | Network | QA | 0/3 `Tested` | `COMPLETE` |
| `R105` | `FC-DM-P1-01` | P1 | G10 | GM | MULTI, QA | 0/5 `Tested` | `COMPLETE` |
| `R106` | `FC-BUILD-P1-01` | P1 | G11 | Fondations | AI, AREA, CONTENT, DIALOGUE, QA, SCRIPT | 2/5 `Tested` | `COMPLETE` |
| `R108` | `FC-CONTENT-P1-01` | P1 | G11 | Fondations | AREA, BUILD, EE, MULTI, PALETTE, QA | 0/5 `Tested` | `COMPLETE` |
| `R109` | `FC-CONTENT-P1-02` | P1 | G11 | Fondations | AI, BUILD, CREATURE, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R110` | `FC-CONTENT-P1-03` | P1 | G11 | Fondations | BUILD, QA | 1/2 `Tested` | `COMPLETE` |
| `R114` | `FC-EE-P1-01` | P1 | G11 | Runtime | AREA, CONTENT, QA, SCRIPT, SHELL | 1/5 `Tested` | `COMPLETE` |
| `R115` | `FC-EE-P1-02` | P1 | G11 | Runtime | CONTENT, CREATURE, GAMEPLAY, ITEM, QA, SCRIPT | 3/5 `Tested` | `COMPLETE` |
| `R116` | `FC-EE-P1-03` | P1 | G11 | Runtime | AREA, GAMEPLAY, PALETTE, QA, SCRIPT | 4/5 `Tested` | `COMPLETE` |
| `R117` | `FC-EE-P1-04` | P1 | G11 | Runtime | CONTENT, QA, SCRIPT | 3/4 `Tested` | `COMPLETE` |
| `R120` | `FC-AI-P1-03` | P1 | G12 | Agent | BUILD, DIALOGUE, QA, QUEST | 1/5 `Tested` | `DELIVER` |
| `R121` | `FC-MULTI-P2-01` | P2 | G9 | Network | AREA, CONTENT, CREATURE, FACTION | 0/5 `Tested` | `COMPLETE` |
| `R122` | `FC-MULTI-P2-02` | P2 | G9 | Network | AREA, DM, EE, QA | 0/5 `Tested` | `COMPLETE` |
| `R123` | `FC-MULTI-P2-03` | P2 | G9 | Network | EE, GAMEPLAY, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R124` | `FC-MULTI-P2-04` | P2 | G9 | Network | QA | 0/2 `Tested` | `COMPLETE` |
| `R125` | `FC-DM-P2-01` | P2 | G10 | GM | AREA, MULTI, PALETTE, SHELL | 0/5 `Tested` | `COMPLETE` |
| `R126` | `FC-DM-P2-02` | P2 | G10 | GM | COMPANION, CREATURE, MULTI, QUEST, SCRIPT, WAYPOINT | 0/5 `Tested` | `COMPLETE` |
| `R127` | `FC-DM-P2-03` | P2 | G10 | GM | AUDIO, CREATURE, DIALOGUE, DOOR, MERCHANT, MULTI | 0/5 `Tested` | `COMPLETE` |
| `R128` | `FC-DM-P2-04` | P2 | G10 | GM | CREATURE, GAMEPLAY, MULTI, SCRIPT, SHELL, TRIGGER | 0/5 `Tested` | `COMPLETE` |
| `R129` | `FC-DM-P2-05` | P2 | G10 | GM | AREA, AUDIO, BUILD, QA, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R130` | `FC-DM-P2-06` | P2 | G10 | GM | ENCOUNTER, QUEST | 0/2 `Tested` | `COMPLETE` |
| `R131` | `FC-BUILD-P2-01` | P2 | G11 | Fondations | CONTENT | 0/2 `Tested` | `COMPLETE` |
| `R132` | `FC-CONTENT-P2-01` | P2 | G11 | Fondations | EE, MULTI, QA | 1/4 `Tested` | `COMPLETE` |
| `R135` | `FC-AREA-P2-01` | P2 | G11 | Creator | AUDIO, CONTENT, MULTI, PALETTE, SCRIPT | 0/5 `Tested` | `COMPLETE` |
| `R136` | `FC-AREA-P2-02` | P2 | G11 | Creator | CREATURE, EE, FACTION, GAMEPLAY, MULTI, QA, SCRIPT, TRIGGER | 4/5 `Tested` | `COMPLETE` |
| `R137` | `FC-AREA-P2-03` | P2 | G11 | Creator | CONTENT, GAMEPLAY, QA | 1/2 `Tested` | `COMPLETE` |
| `R138` | `FC-PALETTE-P2-01` | P2 | G11 | Creator | QA | 0/2 `Tested` | `COMPLETE` |
| `R139` | `FC-AUDIO-P2-01` | P2 | G11 | Writer | AREA, CONTENT, CREATURE, DIALOGUE, SCRIPT | 3/4 `Tested` | `COMPLETE` |
| `R140` | `FC-WAYPOINT-P2-01` | P2 | G11 | Writer | AREA, GAMEPLAY, MULTI | 2/3 `Tested` | `COMPLETE` |
| `R142` | `FC-GAMEPLAY-P2-02` | P2 | G11 | RPG | AREA, AUDIO, PROJECT, SCRIPT | 2/3 `Tested` | `COMPLETE` |
| `R143` | `FC-AI-P2-01` | P2 | G12 | Agent | CONTENT, CREATURE, ENCOUNTER, QA, SCRIPT | 2/3 `Tested` | `DELIVER` |

## Synthèse par domaine

| Domaine | Total | Tested | À terminer/prouver | Lots |
|---|---:|---:|---:|---:|
| AI | 33 | 28 | 5 | 8 |
| AREA | 37 | 14 | 23 | 9 |
| AUDIO | 20 | 8 | 12 | 5 |
| BUILD | 25 | 20 | 5 | 7 |
| COMPANION | 24 | 11 | 13 | 6 |
| CONTENT | 31 | 17 | 14 | 8 |
| CREATURE | 49 | 29 | 20 | 11 |
| DIALOGUE | 32 | 17 | 15 | 8 |
| DM | 37 | 5 | 32 | 8 |
| DOOR | 21 | 15 | 6 | 7 |
| EE | 31 | 23 | 8 | 7 |
| ENCOUNTER | 20 | 9 | 11 | 6 |
| FACTION | 14 | 5 | 9 | 5 |
| GAMEPLAY | 29 | 18 | 11 | 7 |
| ITEM | 32 | 19 | 13 | 8 |
| MERCHANT | 19 | 11 | 8 | 6 |
| MULTI | 36 | 6 | 30 | 9 |
| PALETTE | 18 | 8 | 10 | 5 |
| PLACEABLE | 27 | 14 | 13 | 8 |
| PROJECT | 25 | 17 | 8 | 7 |
| QA | 30 | 30 | 0 | 7 |
| QUEST | 25 | 13 | 12 | 6 |
| SCRIPT | 41 | 20 | 21 | 11 |
| SHELL | 24 | 18 | 6 | 7 |
| TRIGGER | 20 | 16 | 4 | 6 |
| WAYPOINT | 17 | 7 | 10 | 5 |

## Registre exhaustif des lots

| Lot | Domaine | Priorité | État | Progression | Contrat | Fonctions et état actuel |
|---|---|---|---|---:|---|---|
| `FC-AI-P0-01` | AI | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-AI-002` — Contexte structuré du projet (`Tested`)<br>`ORF-AI-004` — Graph de références (`Tested`)<br>`ORF-AI-005` — Outils sémantiques (`Tested`)<br>`ORF-AI-006` — Plan avant mutation (`Tested`)<br>`ORF-AI-007` — Dry-run (`Tested`) |
| `FC-AI-P0-02` | AI | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-AI-008` — Transaction atomique (`Tested`)<br>`ORF-AI-009` — Diff sémantique (`Tested`)<br>`ORF-AI-010` — Niveaux d’autorisation (`Tested`)<br>`ORF-AI-011` — Audit IA (`Tested`)<br>`ORF-AI-013` — Budget et confidentialité (`Tested`) |
| `FC-AI-P0-03` | AI | P0 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-AI-023` — Pas d’exécution Python libre (`Tested`)<br>`ORF-AI-031` — Résultats structurés (`Tested`)<br>`ORF-AI-032` — Protection contre prompt injection (`Tested`)<br>`ORF-AI-033` — Mode hors ligne (`Tested`) |
| `FC-AREA-P0-01` | AREA | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-AREA-001` — Assistant de création de zone (`Tested`)<br>`ORF-AREA-002` — Document de zone indépendant (`Tested`)<br>`ORF-AREA-003` — Construction par tuiles (`Tested`)<br>`ORF-AREA-008` — Rotation et variantes de tuile (`Tested`)<br>`ORF-AREA-022` — Événements d’entrée/sortie (`Tested`) |
| `FC-AREA-P0-02` | AREA | P0 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-AREA-030` — Transitions de zone (`Tested`)<br>`ORF-AREA-031` — Navigation de surface (`Tested`)<br>`ORF-AREA-036` — Play From Here (`Tested`)<br>`ORF-AREA-037` — Validation de zone (`Tested`) |
| `FC-AUDIO-P0-01` | AUDIO | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-AUD-001` — Objet sonore (`Tested`)<br>`ORF-AUD-002` — Liste de clips (`Tested`)<br>`ORF-AUD-005` — Lecture unique ou répétée (`Tested`)<br>`ORF-AUD-008` — Position spécifique (`Tested`)<br>`ORF-AUD-020` — Validation audio (`Tested`) |
| `FC-BUILD-P0-01` | BUILD | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-BUILD-001` — Dépôt Git canonique (`Tested`)<br>`ORF-BUILD-002` — Structure de projet standard (`Tested`)<br>`ORF-BUILD-003` — Build CLI (`Tested`)<br>`ORF-BUILD-004` — Build déterministe (`Tested`)<br>`ORF-BUILD-005` — Build incrémental (`Tested`) |
| `FC-BUILD-P0-02` | BUILD | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-BUILD-006` — Cache jetable (`Tested`)<br>`ORF-BUILD-008` — Normalisation des documents (`Tested`)<br>`ORF-BUILD-009` — Migrations (`Tested`)<br>`ORF-BUILD-010` — Références inverses (`Tested`)<br>`ORF-BUILD-011` — Diff sémantique (`Tested`) |
| `FC-BUILD-P0-03` | BUILD | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-BUILD-013` — Worktrees et agents (`Tested`)<br>`ORF-BUILD-014` — CI (`Tested`)<br>`ORF-BUILD-015` — Fixtures de référence (`Tested`)<br>`ORF-BUILD-018` — Versioning sémantique (`Tested`)<br>`ORF-BUILD-019` — ADRs (`Tested`) |
| `FC-BUILD-P0-04` | BUILD | P0 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-BUILD-022` — Dependency approval (`Tested`)<br>`ORF-BUILD-023` — Third-party notices (`Tested`) |
| `FC-COMPANION-P0-01` | COMPANION | P0 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-CMP-024` — Validation compagnon (`Tested`) |
| `FC-CONTENT-P0-01` | CONTENT | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CONT-001` — Package de contenu versionné (`Tested`)<br>`ORF-CONT-002` — Ordre de priorité (`Tested`)<br>`ORF-CONT-003` — Dépendances (`Tested`)<br>`ORF-CONT-006` — Sources textuelles (`Tested`)<br>`ORF-CONT-007` — Compilation de package (`Tested`) |
| `FC-CONTENT-P0-02` | CONTENT | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CONT-011` — Localisation (`Tested`)<br>`ORF-CONT-013` — Textures (`Tested`)<br>`ORF-CONT-014` — Modèles 3D (`Tested`)<br>`ORF-CONT-019` — Resource namespaces (`Tested`)<br>`ORF-CONT-020` — Content browser (`Tested`) |
| `FC-CONTENT-P0-03` | CONTENT | P0 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-CONT-023` — Provenance d’asset (`Tested`)<br>`ORF-CONT-024` — Quarantaine d’import (`Tested`)<br>`ORF-CONT-030` — Credits and notices generation (`Tested`)<br>`ORF-CONT-031` — IP boundary enforcement (`Tested`) |
| `FC-CREATURE-P0-01` | CREATURE | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CRE-002` — Blueprint de créature (`Tested`)<br>`ORF-CRE-003` — Identité et texte (`Tested`)<br>`ORF-CRE-014` — Santé et ressources (`Tested`)<br>`ORF-CRE-027` — Vitesse et profils de déplacement (`Tested`)<br>`ORF-CRE-033` — OnSpawn (`Tested`) |
| `FC-CREATURE-P0-02` | CREATURE | P0 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-CRE-036` — OnDamaged (`Tested`)<br>`ORF-CRE-043` — OnDeath (`Tested`)<br>`ORF-CRE-049` — Validation de créature (`Tested`) |
| `FC-DIALOGUE-P0-01` | DIALOGUE | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-DLG-001` — Fichier/graphe de conversation (`Tested`)<br>`ORF-DLG-002` — Nœuds PNJ et joueur (`Tested`)<br>`ORF-DLG-003` — Nombre de branches non limité (`Tested`)<br>`ORF-DLG-004` — Liens vers un nœud existant (`Tested`)<br>`ORF-DLG-005` — Réordonnancement des réponses (`Tested`) |
| `FC-DIALOGUE-P0-02` | DIALOGUE | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-DLG-006` — Texte localisé (`Tested`)<br>`ORF-DLG-009` — Conditions d’apparition (`Tested`)<br>`ORF-DLG-011` — Actions sur nœud (`Tested`)<br>`ORF-DLG-021` — Mode test (`Tested`)<br>`ORF-DLG-029` — Validation structurelle (`Tested`) |
| `FC-DOOR-P0-01` | DOOR | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-DOOR-001` — Portes de tileset et universelles (`Tested`)<br>`ORF-DOOR-002` — États ouverte/fermée/verrouillée (`Tested`)<br>`ORF-DOOR-003` — Interaction d’ouverture (`Tested`)<br>`ORF-DOOR-008` — Transition vers zone (`Tested`)<br>`ORF-DOOR-009` — Transition unidirectionnelle/bidirectionnelle (`Tested`) |
| `FC-DOOR-P0-02` | DOOR | P0 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-DOOR-019` — Navigation dynamique (`Tested`)<br>`ORF-DOOR-021` — Validation de porte (`Tested`) |
| `FC-EE-P0-01` | EE | P0 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-EE-002` — Mise à l’échelle dans le Toolset (`Tested`)<br>`ORF-EE-004` — Optimisation Build Module (`Tested`)<br>`ORF-EE-028` — Pathfinding amélioré (`Tested`) |
| `FC-ENCOUNTER-P0-01` | ENCOUNTER | P0 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-ENC-002` — Volume d’activation (`Tested`)<br>`ORF-ENC-003` — Liste de créatures candidates (`Tested`)<br>`ORF-ENC-020` — Validation de rencontre (`Tested`) |
| `FC-FACTION-P0-01` | FACTION | P0 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-FAC-014` — Validation factionnelle (`Tested`) |
| `FC-GAMEPLAY-P0-01` | GAMEPLAY | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-GAME-001` — Déplacement au sol (`Tested`)<br>`ORF-GAME-010` — Système d’interaction (`Tested`)<br>`ORF-GAME-012` — Caméras configurables (`Tested`)<br>`ORF-GAME-013` — Contrôles et input mapping (`Tested`)<br>`ORF-GAME-020` — Sauvegarde et chargement (`Tested`) |
| `FC-GAMEPLAY-P0-02` | GAMEPLAY | P0 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-GAME-022` — Variables et blackboards (`Tested`)<br>`ORF-GAME-023` — Système de règles plugin (`Tested`) |
| `FC-ITEM-P0-01` | ITEM | P0 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-ITEM-002` — Type de base immuable (`Tested`)<br>`ORF-ITEM-003` — Blueprint d’objet (`Tested`)<br>`ORF-ITEM-004` — Identité et localisation (`Tested`)<br>`ORF-ITEM-032` — Validation d’objet (`Tested`) |
| `FC-MERCHANT-P0-01` | MERCHANT | P0 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-MER-012` — Transactions atomiques (`Tested`)<br>`ORF-MER-019` — Validation marchand (`Tested`) |
| `FC-PALETTE-P0-01` | PALETTE | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-PAL-001` — Blueprint réutilisable (`Tested`)<br>`ORF-PAL-002` — Instance de blueprint (`Tested`)<br>`ORF-PAL-003` — Création depuis une instance (`Tested`)<br>`ORF-PAL-006` — Palette standard (`Tested`)<br>`ORF-PAL-007` — Palette projet (`Tested`) |
| `FC-PALETTE-P0-02` | PALETTE | P0 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-PAL-012` — Drag and drop (`Tested`)<br>`ORF-PAL-015` — Dépendances automatiques (`Tested`)<br>`ORF-PAL-016` — Provenance et licence (`Tested`) |
| `FC-PLACEABLE-P0-01` | PLACEABLE | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-PLC-001` — Blueprint de placeable (`Tested`)<br>`ORF-PLC-002` — Décor statique (`Tested`)<br>`ORF-PLC-003` — Objet utilisable (`Tested`)<br>`ORF-PLC-005` — Identité et apparence (`Tested`)<br>`ORF-PLC-006` — Transformation précise (`Tested`) |
| `FC-PLACEABLE-P0-02` | PLACEABLE | P0 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-PLC-018` — OnUsed (`Tested`)<br>`ORF-PLC-027` — Validation de placeable (`Tested`) |
| `FC-PROJECT-P0-01` | PROJECT | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-PROJ-001` — Assistant de création de projet (`Tested`)<br>`ORF-PROJ-002` — Métadonnées localisées du projet (`Tested`)<br>`ORF-PROJ-003` — Identité technique stable (`Tested`)<br>`ORF-PROJ-004` — Première zone obligatoire (`Tested`)<br>`ORF-PROJ-005` — Point de départ du joueur (`Tested`) |
| `FC-PROJECT-P0-02` | PROJECT | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-PROJ-006` — Événement de chargement du module (`Tested`)<br>`ORF-PROJ-019` — Variables de projet (`Tested`)<br>`ORF-PROJ-020` — Contenu personnalisé ordonné (`Tested`)<br>`ORF-PROJ-021` — Scripts mis en cache (`Tested`)<br>`ORF-PROJ-024` — Paramètres par cible (`Tested`) |
| `FC-PROJECT-P0-03` | PROJECT | P0 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-PROJ-025` — Compatibilité et migrations (`Tested`) |
| `FC-QA-P0-01` | QA | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-QA-001` — Validation de schéma (`Tested`)<br>`ORF-QA-002` — Validation de références (`Tested`)<br>`ORF-QA-003` — Validation de graphes (`Tested`)<br>`ORF-QA-004` — Validation géométrique (`Tested`)<br>`ORF-QA-005` — Validation navigation (`Tested`) |
| `FC-QA-P0-02` | QA | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-QA-007` — Validation de licence (`Tested`)<br>`ORF-QA-008` — Tests unitaires du domaine (`Tested`)<br>`ORF-QA-010` — Tests headless runtime (`Tested`)<br>`ORF-QA-015` — Tests de sauvegarde (`Tested`)<br>`ORF-QA-020` — Logs structurés (`Tested`) |
| `FC-QA-P0-03` | QA | P0 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-QA-021` — Diagnostics cliquables (`Tested`)<br>`ORF-QA-026` — Malicious module fixtures (`Tested`)<br>`ORF-QA-029` — Coverage Aurora-parity (`Tested`)<br>`ORF-QA-030` — Acceptance gates (`Tested`) |
| `FC-QUEST-P0-01` | QUEST | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-QST-001` — Catégorie de journal (`Tested`)<br>`ORF-QST-002` — Entrées numériques du journal (`Tested`)<br>`ORF-QST-003` — État courant par joueur (`Tested`)<br>`ORF-QST-004` — États terminé et échoué (`Tested`)<br>`ORF-QST-005` — Texte d’entrée localisé (`Tested`) |
| `FC-QUEST-P0-02` | QUEST | P0 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-QST-007` — Transitions d’état (`Tested`)<br>`ORF-QST-022` — Simulation de quête (`Tested`)<br>`ORF-QST-023` — Validation narrative (`Tested`) |
| `FC-SCRIPT-P0-01` | SCRIPT | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-SCR-001` — Langage proche du C (`Tested`)<br>`ORF-SCR-002` — Sources et bytecode (`Tested`)<br>`ORF-SCR-003` — Compilateur intégré (`Tested`)<br>`ORF-SCR-006` — Événements par objet (`Tested`)<br>`ORF-SCR-007` — Fonction d’entrée (`Tested`) |
| `FC-SCRIPT-P0-02` | SCRIPT | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-SCR-009` — API zones et objets (`Tested`)<br>`ORF-SCR-017` — API mouvement (`Tested`)<br>`ORF-SCR-021` — API math et chaînes (`Tested`)<br>`ORF-SCR-022` — Variables locales (`Tested`)<br>`ORF-SCR-027` — Sandbox sans fichier/réseau (`Tested`) |
| `FC-SCRIPT-P0-03` | SCRIPT | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-SCR-028` — Handles opaques (`Tested`)<br>`ORF-SCR-029` — Budget d’instructions (`Tested`)<br>`ORF-SCR-030` — Budget mémoire (`Tested`)<br>`ORF-SCR-031` — Déterminisme (`Tested`)<br>`ORF-SCR-036` — Version d’API (`Tested`) |
| `FC-SCRIPT-P0-04` | SCRIPT | P0 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-SCR-039` — Permissions par module (`Tested`) |
| `FC-SHELL-P0-01` | SHELL | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-SHELL-001` — Disposition multi-panneaux du Toolset (`Tested`)<br>`ORF-SHELL-002` — Explorateur hiérarchique du projet (`Tested`)<br>`ORF-SHELL-003` — Vue 3D principale (`Tested`)<br>`ORF-SHELL-004` — Palette contextuelle (`Tested`)<br>`ORF-SHELL-005` — Palettes Standard et Custom (`Tested`) |
| `FC-SHELL-P0-02` | SHELL | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-SHELL-006` — Sélection et inspection (`Tested`)<br>`ORF-SHELL-007` — Navigation caméra d’édition (`Tested`)<br>`ORF-SHELL-010` — Retour visuel de placement invalide (`Tested`)<br>`ORF-SHELL-011` — Transformation avec gizmos (`Tested`)<br>`ORF-SHELL-013` — Undo et redo complets (`Tested`) |
| `FC-SHELL-P0-03` | SHELL | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-SHELL-014` — Copier, couper, coller et dupliquer (`Tested`)<br>`ORF-SHELL-015` — Recherche globale (`Tested`)<br>`ORF-SHELL-016` — Find references (`Tested`)<br>`ORF-SHELL-017` — Renommage sûr (`Tested`)<br>`ORF-SHELL-018` — Aperçu et mode test (`Tested`) |
| `FC-SHELL-P0-04` | SHELL | P0 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-SHELL-019` — Build du module (`Tested`)<br>`ORF-SHELL-020` — Messages, erreurs et statut (`Tested`) |
| `FC-TRIGGER-P0-01` | TRIGGER | P0 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-TRG-001` — Création polygonale (`Tested`)<br>`ORF-TRG-002` — Trigger générique (`Tested`)<br>`ORF-TRG-003` — Trigger de transition (`Tested`)<br>`ORF-TRG-006` — OnEnter (`Tested`)<br>`ORF-TRG-007` — OnExit (`Tested`) |
| `FC-TRIGGER-P0-02` | TRIGGER | P0 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-TRG-020` — Validation de trigger (`Tested`) |
| `FC-WAYPOINT-P0-01` | WAYPOINT | P0 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-WPT-001` — Waypoint nommé (`Tested`)<br>`ORF-WPT-005` — Point de départ (`Tested`)<br>`ORF-WPT-006` — Destination de transition (`Tested`)<br>`ORF-WPT-017` — Validation (`Tested`) |
| `FC-AI-P1-01` | AI | P1 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-AI-001` — Assistant intégré au Toolset (`Tested`)<br>`ORF-AI-003` — Index de lore (`Tested`)<br>`ORF-AI-012` — Fournisseurs interchangeables (`Tested`)<br>`ORF-AI-014` — Création de plan de zone (`Tested`)<br>`ORF-AI-015` — Construction de zone assistée (`Tested`) |
| `FC-AI-P1-02` | AI | P1 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-AI-016` — Création de dialogue (`Tested`)<br>`ORF-AI-017` — Création de quête (`Tested`)<br>`ORF-AI-018` — Création de ForgeScript (`Tested`)<br>`ORF-AI-020` — Création de capacité (`Tested`)<br>`ORF-AI-021` — Création 3D via Blender (`Tested`) |
| `FC-AI-P1-03` | AI | P1 | À livrer | 1/5 `Tested` | `DELIVER` | `ORF-AI-025` — Agent de test (`Researched`)<br>`ORF-AI-026` — Simulation de dialogue (`Researched`)<br>`ORF-AI-027` — Simulation de quête (`Researched`)<br>`ORF-AI-029` — Contrôle de cohérence narrative (`Researched`)<br>`ORF-AI-030` — Prompts versionnés (`Tested`) |
| `FC-AREA-P1-01` | AREA | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-AREA-006` — Groupes de tuiles (`Implemented`)<br>`ORF-AREA-007` — Effaceur et remplacement (`Implemented`)<br>`ORF-AREA-009` — Ajustement aléatoire compatible (`Implemented`)<br>`ORF-AREA-010` — Propriétés de tuile (`Implemented`)<br>`ORF-AREA-011` — Redimensionnement de zone (`Implemented`) |
| `FC-AREA-P1-02` | AREA | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-AREA-012` — Copie de zone (`Implemented`)<br>`ORF-AREA-013` — Zones intérieures/extérieures (`Implemented`)<br>`ORF-AREA-014` — Cycle jour/nuit (`Implemented`)<br>`ORF-AREA-015` — Lumière ambiante et directionnelle (`Implemented`)<br>`ORF-AREA-016` — Brouillard de zone (`Implemented`) |
| `FC-AREA-P1-03` | AREA | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-AREA-021` — Audio de zone (`Implemented`)<br>`ORF-AREA-024` — Événement utilisateur de zone (`Implemented`)<br>`ORF-AREA-029` — Carte et brouillard de guerre (`Implemented`)<br>`ORF-AREA-033` — Occlusion et visibilité (`Implemented`)<br>`ORF-AREA-034` — LOD par zone et asset (`Implemented`) |
| `FC-AREA-P1-04` | AREA | P1 | À compléter et prouver | 0/1 `Tested` | `COMPLETE` | `ORF-AREA-035` — Couches et groupes d’édition (`Implemented`) |
| `FC-AUDIO-P1-01` | AUDIO | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-AUD-003` — Recherche et aperçu des ressources (`Implemented`)<br>`ORF-AUD-004` — Ordre séquentiel ou aléatoire (`Implemented`)<br>`ORF-AUD-006` — Intervalle et variation (`Implemented`)<br>`ORF-AUD-007` — Volume relatif (`Implemented`)<br>`ORF-AUD-009` — Son partout (`Implemented`) |
| `FC-AUDIO-P1-02` | AUDIO | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-AUD-011` — Portée audible (`Implemented`)<br>`ORF-AUD-012` — Activation par script (`Implemented`)<br>`ORF-AUD-013` — Ambiance jour/nuit (`Implemented`)<br>`ORF-AUD-014` — Musique jour/nuit/combat (`Implemented`)<br>`ORF-AUD-018` — Mixage et bus (`Implemented`) |
| `FC-AUDIO-P1-03` | AUDIO | P1 | À compléter et prouver | 0/1 `Tested` | `COMPLETE` | `ORF-AUD-019` — Sous-titres de sons importants (`Implemented`) |
| `FC-BUILD-P1-01` | BUILD | P1 | À compléter et prouver | 2/5 `Tested` | `COMPLETE` | `ORF-BUILD-007` — Import/export en masse (`Implemented`)<br>`ORF-BUILD-012` — Merge assisté (`Implemented`)<br>`ORF-BUILD-017` — Packaging Windows (`Implemented`)<br>`ORF-BUILD-020` — Documentation générée (`Tested`)<br>`ORF-BUILD-021` — Task manifests (`Tested`) |
| `FC-BUILD-P1-02` | BUILD | P1 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-BUILD-025` — Crash artifacts (`Tested`) |
| `FC-COMPANION-P1-01` | COMPANION | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CMP-001` — Créature recrutabile (`Implemented`)<br>`ORF-CMP-002` — Conversation avant recrutement (`Implemented`)<br>`ORF-CMP-003` — Conversation avec son maître (`Implemented`)<br>`ORF-CMP-005` — Recrutement et renvoi (`Implemented`)<br>`ORF-CMP-006` — Plusieurs compagnons (`Implemented`) |
| `FC-COMPANION-P1-02` | COMPANION | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CMP-007` — Propriété du compagnon (`Implemented`)<br>`ORF-CMP-008` — Ordres de déplacement (`Implemented`)<br>`ORF-CMP-009` — Ordres de combat (`Implemented`)<br>`ORF-CMP-010` — Comportement autonome (`Implemented`)<br>`ORF-CMP-012` — Équipement du compagnon (`Implemented`) |
| `FC-COMPANION-P1-03` | COMPANION | P1 | À compléter et prouver | 0/3 `Tested` | `COMPLETE` | `ORF-CMP-017` — Mort, inconscience et résurrection (`Implemented`)<br>`ORF-CMP-018` — Téléportation de rattrapage (`Implemented`)<br>`ORF-CMP-022` — Interface de groupe (`Implemented`) |
| `FC-CONTENT-P1-01` | CONTENT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CONT-004` — Import ERF en masse (`Implemented`)<br>`ORF-CONT-005` — Export de blueprints (`Implemented`)<br>`ORF-CONT-008` — Manifeste d’assets (`Implemented`)<br>`ORF-CONT-012` — Police TrueType et UI scalable (`Implemented`)<br>`ORF-CONT-015` — Walkmeshes et collisions (`Implemented`) |
| `FC-CONTENT-P1-02` | CONTENT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CONT-016` — Animations (`Implemented`)<br>`ORF-CONT-018` — 2DA-style data tables (`Implemented`)<br>`ORF-CONT-021` — External edit round-trip (`Implemented`)<br>`ORF-CONT-022` — Blender bridge (`Implemented`)<br>`ORF-CONT-025` — Compatibilité de plateforme (`Implemented`) |
| `FC-CONTENT-P1-03` | CONTENT | P1 | À compléter et prouver | 1/2 `Tested` | `COMPLETE` | `ORF-CONT-026` — Migrations de package (`Implemented`)<br>`ORF-CONT-027` — Désinstallation sûre (`Tested`) |
| `FC-CREATURE-P1-01` | CREATURE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CRE-001` — Assistant de créature (`Implemented`)<br>`ORF-CRE-004` — Rôle et défi (`Implemented`)<br>`ORF-CRE-005` — Race, espèce et sous-type (`Implemented`)<br>`ORF-CRE-008` — Apparence et modèle (`Implemented`)<br>`ORF-CRE-011` — Animation set (`Implemented`) |
| `FC-CREATURE-P1-02` | CREATURE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CRE-012` — État initial et pose (`Implemented`)<br>`ORF-CRE-013` — Attributs (`Implemented`)<br>`ORF-CRE-015` — Défenses et sauvegardes (`Implemented`)<br>`ORF-CRE-021` — Capacités spéciales (`Implemented`)<br>`ORF-CRE-022` — Inventaire de créature (`Implemented`) |
| `FC-CREATURE-P1-03` | CREATURE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CRE-025` — Faction de créature (`Implemented`)<br>`ORF-CRE-026` — Perception (`Implemented`)<br>`ORF-CRE-028` — Conversation associée (`Implemented`)<br>`ORF-CRE-031` — Plot, immortel et interruptibilité (`Implemented`)<br>`ORF-CRE-034` — OnConversation (`Implemented`) |
| `FC-CREATURE-P1-04` | CREATURE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-CRE-035` — OnPerception (`Implemented`)<br>`ORF-CRE-037` — OnPhysicalAttacked (`Implemented`)<br>`ORF-CRE-038` — OnSpellCastAt (`Implemented`)<br>`ORF-CRE-040` — OnBlocked (`Implemented`)<br>`ORF-CRE-046` — Intelligence artificielle par profil (`Implemented`) |
| `FC-DIALOGUE-P1-01` | DIALOGUE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DLG-007` — Tokens de texte (`Implemented`)<br>`ORF-DLG-008` — Participants multiples (`Implemented`)<br>`ORF-DLG-010` — Évaluation dans l’ordre (`Implemented`)<br>`ORF-DLG-012` — Assistant de scripts de dialogue (`Implemented`)<br>`ORF-DLG-013` — Commentaires de nœud (`Implemented`) |
| `FC-DIALOGUE-P1-02` | DIALOGUE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DLG-017` — Scripts de fin et d’abandon (`Implemented`)<br>`ORF-DLG-018` — Recherche dans les conversations (`Implemented`)<br>`ORF-DLG-019` — Mise en évidence et filtres (`Implemented`)<br>`ORF-DLG-022` — Mémoire de conversation (`Implemented`)<br>`ORF-DLG-024` — Checks de compétence (`Implemented`) |
| `FC-DIALOGUE-P1-03` | DIALOGUE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DLG-026` — Graphes imbriqués (`Implemented`)<br>`ORF-DLG-028` — Relations et conséquences (`Implemented`)<br>`ORF-DLG-030` — Couverture de branches (`Implemented`)<br>`ORF-DLG-031` — Édition collaborative (`Implemented`)<br>`ORF-DLG-032` — Génération IA contrôlée (`Implemented`) |
| `FC-DM-P1-01` | DM | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DM-002` — Authentification GM (`Implemented`)<br>`ORF-DM-011` — Examiner (`Implemented`)<br>`ORF-DM-025` — Liste des joueurs (`Implemented`)<br>`ORF-DM-031` — Journal d’audit GM (`Implemented`)<br>`ORF-DM-034` — Chat et canaux GM (`Implemented`) |
| `FC-DOOR-P1-01` | DOOR | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DOOR-004` — Verrou et clé (`Implemented`)<br>`ORF-DOOR-005` — Échec d’ouverture (`Implemented`)<br>`ORF-DOOR-006` — Piège de porte (`Implemented`)<br>`ORF-DOOR-011` — OnAreaTransitionClick (`Implemented`)<br>`ORF-DOOR-012` — OnOpen et OnClose (`Implemented`) |
| `FC-DOOR-P1-02` | DOOR | P1 | À compléter et prouver | 0/1 `Tested` | `COMPLETE` | `ORF-DOOR-013` — OnLock et OnUnlock (`Implemented`) |
| `FC-EE-P1-01` | EE | P1 | À compléter et prouver | 1/5 `Tested` | `COMPLETE` | `ORF-EE-001` — Transformations visuelles runtime (`Tested`)<br>`ORF-EE-003` — Plusieurs onglets de zone (`Implemented`)<br>`ORF-EE-005` — Rendu PBR (`Implemented`)<br>`ORF-EE-006` — Tone mapping et HDR (`Implemented`)<br>`ORF-EE-007` — Éclairage par pixel (`Implemented`) |
| `FC-EE-P1-02` | EE | P1 | À compléter et prouver | 3/5 `Tested` | `COMPLETE` | `ORF-EE-008` — Nombre accru de lumières dynamiques (`Implemented`)<br>`ORF-EE-010` — LOD (`Implemented`)<br>`ORF-EE-011` — Règles plus data-driven (`Tested`)<br>`ORF-EE-012` — NUI scriptable (`Tested`)<br>`ORF-EE-013` — Événements GUI (`Tested`) |
| `FC-EE-P1-03` | EE | P1 | À compléter et prouver | 4/5 `Tested` | `COMPLETE` | `ORF-EE-015` — JSON runtime (`Tested`)<br>`ORF-EE-016` — Sérialisation d’objet avec état (`Tested`)<br>`ORF-EE-019` — Copie dynamique d’objet (`Tested`)<br>`ORF-EE-022` — Indicateur de zone d’effet (`Tested`)<br>`ORF-EE-023` — Chargement de zone amélioré (`Implemented`) |
| `FC-EE-P1-04` | EE | P1 | À compléter et prouver | 3/4 `Tested` | `COMPLETE` | `ORF-EE-024` — Améliorations NUI (`Tested`)<br>`ORF-EE-026` — TrueType et scaling UI (`Implemented`)<br>`ORF-EE-027` — Interpolation de transforms (`Tested`)<br>`ORF-EE-031` — Améliorations compilateur (`Tested`) |
| `FC-ENCOUNTER-P1-01` | ENCOUNTER | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-ENC-001` — Assistant de rencontre (`Implemented`)<br>`ORF-ENC-004` — Difficulté de rencontre (`Implemented`)<br>`ORF-ENC-005` — Nombre minimum et maximum (`Implemented`)<br>`ORF-ENC-006` — Sélection selon Challenge Rating (`Implemented`)<br>`ORF-ENC-007` — Rencontre unique ou continue (`Implemented`) |
| `FC-ENCOUNTER-P1-02` | ENCOUNTER | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-ENC-009` — Points de spawn manuels (`Implemented`)<br>`ORF-ENC-011` — Restriction joueurs (`Implemented`)<br>`ORF-ENC-012` — OnEnter et OnExit (`Implemented`)<br>`ORF-ENC-013` — OnExhausted (`Implemented`)<br>`ORF-ENC-015` — Vagues et phases (`Implemented`) |
| `FC-ENCOUNTER-P1-03` | ENCOUNTER | P1 | À compléter et prouver | 0/1 `Tested` | `COMPLETE` | `ORF-ENC-018` — Prévisualisation statistique (`Implemented`) |
| `FC-FACTION-P1-01` | FACTION | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-FAC-001` — Création et suppression de faction (`Implemented`)<br>`ORF-FAC-003` — Matrice de réputation (`Implemented`)<br>`ORF-FAC-004` — Relations asymétriques (`Implemented`)<br>`ORF-FAC-005` — Seuils hostile/neutre/allié (`Implemented`)<br>`ORF-FAC-006` — Réputation personnelle (`Implemented`) |
| `FC-FACTION-P1-02` | FACTION | P1 | À compléter et prouver | 0/4 `Tested` | `COMPLETE` | `ORF-FAC-007` — Réputation globale (`Implemented`)<br>`ORF-FAC-011` — Tags de faction (`Implemented`)<br>`ORF-FAC-012` — Interface de matrice (`Implemented`)<br>`ORF-FAC-013` — Réaction au joueur (`Implemented`) |
| `FC-GAMEPLAY-P1-01` | GAMEPLAY | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-GAME-002` — Vol libre (`Implemented`)<br>`ORF-GAME-008` — Projectiles (`Implemented`)<br>`ORF-GAME-009` — Physique 3D (`Implemented`)<br>`ORF-GAME-014` — Ciblage (`Implemented`)<br>`ORF-GAME-017` — Temps de monde (`Implemented`) |
| `FC-GAMEPLAY-P1-02` | GAMEPLAY | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-GAME-021` — Sauvegarde par joueur/partie/serveur (`Implemented`)<br>`ORF-GAME-025` — Checkpoints et respawn (`Implemented`)<br>`ORF-GAME-026` — Carte, journal et HUD (`Implemented`)<br>`ORF-GAME-027` — Cinematics and dialogue control locks (`Implemented`)<br>`ORF-GAME-029` — Replay deterministic test (`Implemented`) |
| `FC-ITEM-P1-01` | ITEM | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-ITEM-001` — Assistant de création d’objet (`Implemented`)<br>`ORF-ITEM-005` — Apparence personnalisable (`Implemented`)<br>`ORF-ITEM-006` — Poids et encombrement (`Implemented`)<br>`ORF-ITEM-008` — Piles (`Implemented`)<br>`ORF-ITEM-009` — Charges et utilisations (`Implemented`) |
| `FC-ITEM-P1-02` | ITEM | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-ITEM-013` — Objet plot (`Implemented`)<br>`ORF-ITEM-014` — Indroppable et transfert (`Implemented`)<br>`ORF-ITEM-015` — Propriétés magiques (`Implemented`)<br>`ORF-ITEM-016` — Pouvoir unique (`Implemented`)<br>`ORF-ITEM-018` — Équipement et slots (`Implemented`) |
| `FC-ITEM-P1-03` | ITEM | P1 | À compléter et prouver | 0/3 `Tested` | `COMPLETE` | `ORF-ITEM-024` — Loot tables (`Implemented`)<br>`ORF-ITEM-030` — Inspecteur d’inventaire (`Implemented`)<br>`ORF-ITEM-031` — Événements d’objet (`Implemented`) |
| `FC-MERCHANT-P1-01` | MERCHANT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-MER-001` — Objet magasin indépendant (`Implemented`)<br>`ORF-MER-002` — Store Setup Wizard (`Implemented`)<br>`ORF-MER-003` — Inventaire du magasin (`Implemented`)<br>`ORF-MER-004` — Markup et markdown (`Implemented`)<br>`ORF-MER-008` — Restrictions whitelist/blacklist (`Implemented`) |
| `FC-MERCHANT-P1-02` | MERCHANT | P1 | À compléter et prouver | 0/3 `Tested` | `COMPLETE` | `ORF-MER-009` — Stock infini (`Implemented`)<br>`ORF-MER-011` — OnOpen et OnClose (`Implemented`)<br>`ORF-MER-017` — Interface de boutique (`Implemented`) |
| `FC-MULTI-P1-01` | MULTI | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-MP-001` — Serveur dédié (`Implemented`)<br>`ORF-MP-002` — Serveur autoritaire (`Implemented`)<br>`ORF-MP-007` — Parties et groupes (`Implemented`)<br>`ORF-MP-011` — Réplication d’entités (`Implemented`)<br>`ORF-MP-012` — Interest management (`Implemented`) |
| `FC-MULTI-P1-02` | MULTI | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-MP-013` — Synchronisation de contenu (`Implemented`)<br>`ORF-MP-015` — Version locking (`Implemented`)<br>`ORF-MP-017` — Rôles et permissions (`Implemented`)<br>`ORF-MP-020` — Chat (`Implemented`)<br>`ORF-MP-027` — Bus d’événements serveur (`Implemented`) |
| `FC-MULTI-P1-03` | MULTI | P1 | À compléter et prouver | 0/3 `Tested` | `COMPLETE` | `ORF-MP-029` — Logs structurés (`Implemented`)<br>`ORF-MP-031` — Anti-cheat par conception (`Implemented`)<br>`ORF-MP-036` — Tests réseau déterministes (`Implemented`) |
| `FC-PALETTE-P1-01` | PALETTE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-PAL-004` — Mise à jour du blueprint (`Implemented`)<br>`ORF-PAL-005` — Mise à jour des instances (`Implemented`)<br>`ORF-PAL-008` — Catégories hiérarchiques (`Implemented`)<br>`ORF-PAL-010` — Miniatures et prévisualisation (`Implemented`)<br>`ORF-PAL-011` — Recherche avancée d’assets (`Implemented`) |
| `FC-PALETTE-P1-02` | PALETTE | P1 | À compléter et prouver | 0/3 `Tested` | `COMPLETE` | `ORF-PAL-013` — Variantes de prefab (`Implemented`)<br>`ORF-PAL-014` — Compositions et collections (`Implemented`)<br>`ORF-PAL-018` — Budget d’asset (`Implemented`) |
| `FC-PLACEABLE-P1-01` | PLACEABLE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-PLC-004` — Conteneur (`Implemented`)<br>`ORF-PLC-007` — État initial d’animation (`Implemented`)<br>`ORF-PLC-008` — Destructibilité (`Implemented`)<br>`ORF-PLC-009` — Protection plot (`Implemented`)<br>`ORF-PLC-012` — Verrouillage (`Implemented`) |
| `FC-PLACEABLE-P1-02` | PLACEABLE | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-PLC-014` — Piège (`Implemented`)<br>`ORF-PLC-015` — Trésor initial (`Implemented`)<br>`ORF-PLC-016` — OnOpen et OnClose (`Implemented`)<br>`ORF-PLC-017` — OnLock et OnUnlock (`Implemented`)<br>`ORF-PLC-019` — OnDisturbed (`Implemented`) |
| `FC-PLACEABLE-P1-03` | PLACEABLE | P1 | À compléter et prouver | 0/3 `Tested` | `COMPLETE` | `ORF-PLC-020` — OnDamaged et OnDeath (`Implemented`)<br>`ORF-PLC-025` — Sockets et assemblage (`Implemented`)<br>`ORF-PLC-026` — Interaction multi-verbes (`Implemented`) |
| `FC-PROJECT-P1-01` | PROJECT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-PROJ-007` — Événements de connexion client (`Implemented`)<br>`ORF-PROJ-008` — Événements d’objet acquis/perdu (`Implemented`)<br>`ORF-PROJ-009` — Événements d’utilisation d’objet (`Implemented`)<br>`ORF-PROJ-010` — Événements de mort et respawn (`Implemented`)<br>`ORF-PROJ-011` — Événements d’équipement et niveau (`Implemented`) |
| `FC-PROJECT-P1-02` | PROJECT | P1 | À compléter et prouver | 0/3 `Tested` | `COMPLETE` | `ORF-PROJ-014` — Événement utilisateur global (`Implemented`)<br>`ORF-PROJ-016` — Temps et calendrier du monde (`Implemented`)<br>`ORF-PROJ-022` — Description et instructions joueur (`Implemented`) |
| `FC-QA-P1-01` | QA | P1 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-QA-006` — Validation de localisation (`Tested`)<br>`ORF-QA-009` — Tests ForgeScript (`Tested`)<br>`ORF-QA-011` — Tests de dialogue (`Tested`)<br>`ORF-QA-012` — Tests de quête (`Tested`)<br>`ORF-QA-013` — Tests de rencontre (`Tested`) |
| `FC-QA-P1-02` | QA | P1 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-QA-014` — Tests réseau (`Tested`)<br>`ORF-QA-016` — Tests visuels (`Tested`)<br>`ORF-QA-017` — Benchmarks de zone (`Tested`)<br>`ORF-QA-018` — Budgets par plateforme (`Tested`)<br>`ORF-QA-019` — Profilage intégré (`Tested`) |
| `FC-QA-P1-03` | QA | P1 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-QA-022` — Rapport de santé du projet (`Tested`)<br>`ORF-QA-023` — Accessibilité de l’éditeur (`Tested`)<br>`ORF-QA-024` — Accessibilité du runtime (`Tested`)<br>`ORF-QA-025` — Security fuzzing (`Tested`)<br>`ORF-QA-028` — Crash recovery (`Tested`) |
| `FC-QUEST-P1-01` | QUEST | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-QST-006` — Objectifs explicites (`Implemented`)<br>`ORF-QST-008` — Récompenses (`Implemented`)<br>`ORF-QST-010` — Commentaires auteur (`Implemented`)<br>`ORF-QST-011` — Plot Wizard (`Implemented`)<br>`ORF-QST-012` — Cast du scénario (`Implemented`) |
| `FC-QUEST-P1-02` | QUEST | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-QST-013` — Props du scénario (`Implemented`)<br>`ORF-QST-014` — Étapes de scénario (`Implemented`)<br>`ORF-QST-015` — Quêtes cachées ou révélées (`Implemented`)<br>`ORF-QST-019` — Chronologie des événements (`Implemented`)<br>`ORF-QST-020` — Pins de carte (`Implemented`) |
| `FC-QUEST-P1-03` | QUEST | P1 | À compléter et prouver | 0/2 `Tested` | `COMPLETE` | `ORF-QST-024` — Tests générés (`Implemented`)<br>`ORF-QST-025` — Génération IA de quête (`Implemented`) |
| `FC-SCRIPT-P1-01` | SCRIPT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-SCR-004` — Éditeur de scripts (`Implemented`)<br>`ORF-SCR-005` — Script Wizard (`Implemented`)<br>`ORF-SCR-008` — API d’actions différées (`Implemented`)<br>`ORF-SCR-010` — API combat (`Implemented`)<br>`ORF-SCR-011` — API effets (`Implemented`) |
| `FC-SCRIPT-P1-02` | SCRIPT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-SCR-012` — API inventaire (`Implemented`)<br>`ORF-SCR-013` — API journal et quêtes (`Implemented`)<br>`ORF-SCR-014` — API dialogues (`Implemented`)<br>`ORF-SCR-015` — API factions et réputation (`Implemented`)<br>`ORF-SCR-016` — API perception (`Implemented`) |
| `FC-SCRIPT-P1-03` | SCRIPT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-SCR-019` — API son et musique (`Implemented`)<br>`ORF-SCR-020` — API temps (`Implemented`)<br>`ORF-SCR-023` — Événements custom (`Implemented`)<br>`ORF-SCR-032` — Hot reload (`Implemented`)<br>`ORF-SCR-033` — Débogueur (`Implemented`) |
| `FC-SCRIPT-P1-04` | SCRIPT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-SCR-034` — Tests unitaires ForgeScript (`Implemented`)<br>`ORF-SCR-035` — Profilage de scripts (`Implemented`)<br>`ORF-SCR-037` — Documentation générée (`Implemented`)<br>`ORF-SCR-038` — Analyse statique (`Implemented`)<br>`ORF-SCR-040` — Sérialisation d’état script (`Implemented`) |
| `FC-SCRIPT-P1-05` | SCRIPT | P1 | À compléter et prouver | 0/1 `Tested` | `COMPLETE` | `ORF-SCR-041` — Interop éditeur/MCP (`Implemented`) |
| `FC-SHELL-P1-01` | SHELL | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-SHELL-008` — Filtres d’affichage (`Implemented`)<br>`ORF-SHELL-009` — Placement répété (`Implemented`)<br>`ORF-SHELL-012` — Édition multi-zones par onglets (`Implemented`)<br>`ORF-SHELL-021` — Aide contextuelle (`Implemented`)<br>`ORF-SHELL-023` — Profils d’éditeur (`Implemented`) |
| `FC-SHELL-P1-02` | SHELL | P1 | À compléter et prouver | 0/1 `Tested` | `COMPLETE` | `ORF-SHELL-024` — Sauvegarde automatique et récupération (`Implemented`) |
| `FC-TRIGGER-P1-01` | TRIGGER | P1 | À compléter et prouver | 0/4 `Tested` | `COMPLETE` | `ORF-TRG-004` — Trigger de piège (`Implemented`)<br>`ORF-TRG-011` — Filtre de cible (`Implemented`)<br>`ORF-TRG-017` — Volumes de gameplay (`Implemented`)<br>`ORF-TRG-019` — Visualisation d’occupation (`Implemented`) |
| `FC-WAYPOINT-P1-01` | WAYPOINT | P1 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-WPT-002` — Apparence d’éditeur (`Implemented`)<br>`ORF-WPT-003` — Map note (`Implemented`)<br>`ORF-WPT-004` — Map note activable (`Implemented`)<br>`ORF-WPT-007` — Points de patrouille ordonnés (`Implemented`)<br>`ORF-WPT-008` — Patrouille forward/reverse (`Implemented`) |
| `FC-WAYPOINT-P1-02` | WAYPOINT | P1 | À compléter et prouver | 0/4 `Tested` | `COMPLETE` | `ORF-WPT-009` — Orientation au waypoint (`Implemented`)<br>`ORF-WPT-014` — Ancres audio/VFX (`Implemented`)<br>`ORF-WPT-015` — Groupes et requêtes (`Implemented`)<br>`ORF-WPT-016` — Visualisation des routes (`Implemented`) |
| `FC-AI-P2-01` | AI | P2 | À livrer | 2/3 `Tested` | `DELIVER` | `ORF-AI-019` — Création de boss (`Tested`)<br>`ORF-AI-022` — Fournisseurs text/image-to-3D (`Tested`)<br>`ORF-AI-028` — Simulation de combat (`Researched`) |
| `FC-AREA-P2-01` | AREA | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-AREA-004` — Terrain de base (`Implemented`)<br>`ORF-AREA-005` — Features de tileset (`Implemented`)<br>`ORF-AREA-017` — Vent (`Implemented`)<br>`ORF-AREA-018` — Météo (`Implemented`)<br>`ORF-AREA-019` — Skybox et ciel (`Implemented`) |
| `FC-AREA-P2-02` | AREA | P2 | À compléter et prouver | 4/5 `Tested` | `COMPLETE` | `ORF-AREA-020` — Eau (`Implemented`)<br>`ORF-AREA-023` — Événement heartbeat de zone (`Tested`)<br>`ORF-AREA-025` — Modificateurs de perception (`Tested`)<br>`ORF-AREA-026` — Interdiction de repos (`Tested`)<br>`ORF-AREA-027` — Mode PvP de zone (`Tested`) |
| `FC-AREA-P2-03` | AREA | P2 | À compléter et prouver | 1/2 `Tested` | `COMPLETE` | `ORF-AREA-028` — Écran de chargement (`Tested`)<br>`ORF-AREA-032` — Navigation volumétrique (`Implemented`) |
| `FC-AUDIO-P2-01` | AUDIO | P2 | À compléter et prouver | 3/4 `Tested` | `COMPLETE` | `ORF-AUD-010` — Position aléatoire (`Tested`)<br>`ORF-AUD-015` — Réverbération (`Implemented`)<br>`ORF-AUD-016` — Voix de créature (`Tested`)<br>`ORF-AUD-017` — Événements audio (`Tested`) |
| `FC-BUILD-P2-01` | BUILD | P2 | À compléter et prouver | 0/2 `Tested` | `COMPLETE` | `ORF-BUILD-016` — Build multi-plateforme (`Implemented`)<br>`ORF-BUILD-024` — Release channels (`Implemented`) |
| `FC-COMPANION-P2-01` | COMPANION | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CMP-004` — Conversation avec non-maître (`Tested`)<br>`ORF-CMP-011` — Formation (`Tested`)<br>`ORF-CMP-013` — Progression et level-up (`Tested`)<br>`ORF-CMP-014` — Relation et loyauté (`Tested`)<br>`ORF-CMP-015` — Banter et interjections (`Tested`) |
| `FC-COMPANION-P2-02` | COMPANION | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CMP-016` — Quêtes personnelles (`Tested`)<br>`ORF-CMP-019` — Gestion des pièges (`Tested`)<br>`ORF-CMP-020` — Utilisation de sorts/capacités (`Tested`)<br>`ORF-CMP-021` — Contrôle direct (`Tested`)<br>`ORF-CMP-023` — Séparation et réunion du groupe (`Tested`) |
| `FC-CONTENT-P2-01` | CONTENT | P2 | À compléter et prouver | 1/4 `Tested` | `COMPLETE` | `ORF-CONT-009` — Synchronisation automatique (`Tested`)<br>`ORF-CONT-017` — VFX et emitters (`Implemented`)<br>`ORF-CONT-028` — Signatures (`Implemented`)<br>`ORF-CONT-029` — Delta updates (`Implemented`) |
| `FC-CREATURE-P2-01` | CREATURE | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CRE-006` — Sexe, genre et pronoms (`Tested`)<br>`ORF-CRE-007` — Portrait et représentation UI (`Tested`)<br>`ORF-CRE-009` — Parties du corps (`Tested`)<br>`ORF-CRE-010` — Couleurs, ailes et queues (`Tested`)<br>`ORF-CRE-016` — Classes et niveaux (`Tested`) |
| `FC-CREATURE-P2-02` | CREATURE | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CRE-018` — Compétences (`Tested`)<br>`ORF-CRE-019` — Dons et talents (`Tested`)<br>`ORF-CRE-020` — Sorts et pouvoirs préparés (`Tested`)<br>`ORF-CRE-023` — Objets droppables (`Tested`)<br>`ORF-CRE-029` — Voix et soundset (`Tested`) |
| `FC-CREATURE-P2-03` | CREATURE | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-CRE-032` — Cadavre et loot corpse (`Tested`)<br>`ORF-CRE-039` — OnCombatRoundEnd (`Tested`)<br>`ORF-CRE-041` — OnDisturbed (`Tested`)<br>`ORF-CRE-042` — OnRested (`Tested`)<br>`ORF-CRE-045` — OnUserDefined (`Tested`) |
| `FC-CREATURE-P2-04` | CREATURE | P2 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-CRE-047` — Animations ambiantes (`Tested`)<br>`ORF-CRE-048` — Auto-level et templates (`Tested`) |
| `FC-DIALOGUE-P2-01` | DIALOGUE | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-DLG-014` — Animations de dialogue (`Tested`)<br>`ORF-DLG-015` — Sons et voix (`Tested`)<br>`ORF-DLG-016` — Caméra de conversation (`Tested`)<br>`ORF-DLG-020` — Bookmarks (`Tested`)<br>`ORF-DLG-023` — Interjections de compagnons (`Tested`) |
| `FC-DIALOGUE-P2-02` | DIALOGUE | P2 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-DLG-027` — Variantes procédurales (`Tested`) |
| `FC-DM-P2-01` | DM | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DM-001` — Client maître de jeu séparé (`Implemented`)<br>`ORF-DM-003` — Vue Creator (`Implemented`)<br>`ORF-DM-004` — Création d’une ou plusieurs instances (`Implemented`)<br>`ORF-DM-005` — Vue Chooser (`Implemented`)<br>`ORF-DM-006` — Aller à un objet (`Implemented`) |
| `FC-DM-P2-02` | DM | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DM-007` — Faire venir ou téléporter une cible (`Implemented`)<br>`ORF-DM-008` — Tuer ou détruire (`Implemented`)<br>`ORF-DM-009` — Soigner (`Implemented`)<br>`ORF-DM-010` — Limbo ou désactivation de l’IA (`Implemented`)<br>`ORF-DM-012` — Posséder une créature (`Implemented`) |
| `FC-DM-P2-03` | DM | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DM-013` — Impersonner une créature (`Implemented`)<br>`ORF-DM-014` — Invulnérabilité GM (`Implemented`)<br>`ORF-DM-016` — Donner ou retirer or (`Implemented`)<br>`ORF-DM-017` — Donner ou retirer XP (`Implemented`)<br>`ORF-DM-019` — Verrouiller/déverrouiller porte (`Implemented`) |
| `FC-DM-P2-04` | DM | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DM-020` — Déclencher entrée/sortie trigger (`Implemented`)<br>`ORF-DM-021` — Sélection multiple (`Implemented`)<br>`ORF-DM-023` — Ordre de déplacement de groupe (`Implemented`)<br>`ORF-DM-024` — Ordre d’attaque de groupe (`Implemented`)<br>`ORF-DM-026` — Téléportation vers joueur (`Implemented`) |
| `FC-DM-P2-05` | DM | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-DM-027` — Recherche par nom et suivant (`Implemented`)<br>`ORF-DM-028` — Exécuter un script (`Implemented`)<br>`ORF-DM-029` — Modifier variables (`Implemented`)<br>`ORF-DM-032` — Macros et scènes préparées (`Implemented`)<br>`ORF-DM-033` — Brouillard de guerre GM (`Implemented`) |
| `FC-DM-P2-06` | DM | P2 | À compléter et prouver | 0/2 `Tested` | `COMPLETE` | `ORF-DM-035` — Gestion des quêtes en direct (`Implemented`)<br>`ORF-DM-036` — Gestion de rencontres en direct (`Implemented`) |
| `FC-DOOR-P2-01` | DOOR | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-DOOR-007` — Destructibilité (`Tested`)<br>`ORF-DOOR-010` — Écran de chargement spécifique (`Tested`)<br>`ORF-DOOR-014` — OnDamaged et OnDeath (`Tested`)<br>`ORF-DOOR-015` — OnPhysicalAttacked (`Tested`)<br>`ORF-DOOR-016` — OnSpellCastAt (`Tested`) |
| `FC-DOOR-P2-02` | DOOR | P2 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-DOOR-018` — Conversation et faction (`Tested`)<br>`ORF-DOOR-020` — Portes doubles et complexes (`Tested`) |
| `FC-EE-P2-01` | EE | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-EE-009` — Eau améliorée (`Tested`)<br>`ORF-EE-014` — Requêtes de périphérique (`Tested`)<br>`ORF-EE-017` — Resource manager VM commands (`Tested`)<br>`ORF-EE-018` — Création dynamique de zone (`Tested`)<br>`ORF-EE-020` — Contrôle de groupe (`Tested`) |
| `FC-EE-P2-02` | EE | P2 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-EE-021` — Jusqu’à huit classes (`Tested`)<br>`ORF-EE-025` — Métadonnées NWSync (`Tested`)<br>`ORF-EE-029` — Profiler Tracy (`Tested`)<br>`ORF-EE-030` — MSAA et filtrage anisotrope (`Tested`) |
| `FC-ENCOUNTER-P2-01` | ENCOUNTER | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-ENC-008` — Respawn (`Tested`)<br>`ORF-ENC-010` — Faction de spawn (`Tested`)<br>`ORF-ENC-016` — Renforts et alertes (`Tested`)<br>`ORF-ENC-017` — Spawn hors vue (`Tested`)<br>`ORF-ENC-019` — Nettoyage (`Tested`) |
| `FC-FACTION-P2-01` | FACTION | P2 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-FAC-002` — Faction parente (`Tested`)<br>`ORF-FAC-008` — Propagation par témoins (`Tested`)<br>`ORF-FAC-009` — Membership multiple (`Tested`) |
| `FC-GAMEPLAY-P2-01` | GAMEPLAY | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-GAME-003` — Nage et plongée (`Tested`)<br>`ORF-GAME-004` — Escalade (`Tested`)<br>`ORF-GAME-011` — Menu radial (`Tested`)<br>`ORF-GAME-015` — Pause tactique (`Tested`)<br>`ORF-GAME-016` — Modes de combat (`Tested`) |
| `FC-GAMEPLAY-P2-02` | GAMEPLAY | P2 | À compléter et prouver | 2/3 `Tested` | `COMPLETE` | `ORF-GAME-018` — Météo runtime (`Implemented`)<br>`ORF-GAME-019` — Cutscenes (`Tested`)<br>`ORF-GAME-024` — Difficulté et mutateurs (`Tested`) |
| `FC-ITEM-P2-01` | ITEM | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-ITEM-007` — Valeur et coût (`Tested`)<br>`ORF-ITEM-010` — Niveau ou Lore requis (`Tested`)<br>`ORF-ITEM-011` — Objet identifié (`Tested`)<br>`ORF-ITEM-012` — Objet volé (`Tested`)<br>`ORF-ITEM-017` — Effets On Hit (`Tested`) |
| `FC-ITEM-P2-02` | ITEM | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-ITEM-019` — Armes et combat (`Tested`)<br>`ORF-ITEM-020` — Armures et protection (`Tested`)<br>`ORF-ITEM-023` — Conteneurs imbriqués (`Tested`)<br>`ORF-ITEM-025` — Trésor par contexte (`Tested`)<br>`ORF-ITEM-026` — Objets uniques (`Tested`) |
| `FC-ITEM-P2-03` | ITEM | P2 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-ITEM-027` — Butin instancié (`Tested`)<br>`ORF-ITEM-028` — Artisanat et matériaux (`Tested`)<br>`ORF-ITEM-029` — Génération procédurale d’objet (`Tested`) |
| `FC-MERCHANT-P2-01` | MERCHANT | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-MER-005` — Trésorerie (`Tested`)<br>`ORF-MER-006` — Prix maximal d’achat (`Tested`)<br>`ORF-MER-007` — Biens volés (`Tested`)<br>`ORF-MER-010` — Réapprovisionnement (`Tested`)<br>`ORF-MER-013` — Monnaies multiples (`Tested`) |
| `FC-MERCHANT-P2-02` | MERCHANT | P2 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-MER-016` — Services (`Tested`)<br>`ORF-MER-018` — Audit économique (`Tested`) |
| `FC-MULTI-P2-01` | MULTI | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-MP-003` — Découverte et connexion (`Implemented`)<br>`ORF-MP-004` — Lobby et sélection de personnage (`Implemented`)<br>`ORF-MP-005` — Création de personnage (`Implemented`)<br>`ORF-MP-006` — Personnages locaux ou serveur (`Implemented`)<br>`ORF-MP-008` — PvP et friendly fire (`Implemented`) |
| `FC-MULTI-P2-02` | MULTI | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-MP-009` — Instances de zones (`Implemented`)<br>`ORF-MP-010` — Création dynamique de zone (`Implemented`)<br>`ORF-MP-016` — Authentification et comptes (`Implemented`)<br>`ORF-MP-018` — Administration à distance (`Implemented`)<br>`ORF-MP-019` — Modération (`Implemented`) |
| `FC-MULTI-P2-03` | MULTI | P2 | À compléter et prouver | 0/5 `Tested` | `COMPLETE` | `ORF-MP-022` — Persistance de monde (`Implemented`)<br>`ORF-MP-023` — SQL sécurisé (`Implemented`)<br>`ORF-MP-028` — Métriques et health checks (`Implemented`)<br>`ORF-MP-030` — Profiler serveur (`Implemented`)<br>`ORF-MP-032` — Reconnexion (`Implemented`) |
| `FC-MULTI-P2-04` | MULTI | P2 | À compléter et prouver | 0/2 `Tested` | `COMPLETE` | `ORF-MP-033` — Sauvegarde/rollback serveur (`Implemented`)<br>`ORF-MP-034` — Capacité et files d’attente (`Implemented`) |
| `FC-PALETTE-P2-01` | PALETTE | P2 | À compléter et prouver | 0/2 `Tested` | `COMPLETE` | `ORF-PAL-009` — Favoris et récents (`Implemented`)<br>`ORF-PAL-017` — Compatibilité artistique (`Implemented`) |
| `FC-PLACEABLE-P2-01` | PLACEABLE | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-PLC-010` — Faction (`Tested`)<br>`ORF-PLC-011` — Conversation (`Tested`)<br>`ORF-PLC-013` — Clé consommée (`Tested`)<br>`ORF-PLC-021` — OnPhysicalAttacked (`Tested`)<br>`ORF-PLC-022` — OnSpellCastAt (`Tested`) |
| `FC-PLACEABLE-P2-02` | PLACEABLE | P2 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-PLC-024` — Physique dynamique (`Tested`) |
| `FC-PROJECT-P2-01` | PROJECT | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-PROJ-012` — Événements de repos (`Tested`)<br>`ORF-PROJ-013` — Événement heartbeat global (`Tested`)<br>`ORF-PROJ-015` — Abort de cutscene (`Tested`)<br>`ORF-PROJ-017` — Échelle de progression (`Tested`)<br>`ORF-PROJ-018` — Film ou séquence de démarrage (`Tested`) |
| `FC-PROJECT-P2-02` | PROJECT | P2 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-PROJ-023` — Configuration de difficulté (`Tested`) |
| `FC-QA-P2-01` | QA | P2 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-QA-027` — Soak tests (`Tested`) |
| `FC-QUEST-P2-01` | QUEST | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-QST-009` — Priorité du journal (`Tested`)<br>`ORF-QST-016` — Quêtes répétables (`Tested`)<br>`ORF-QST-017` — Quêtes de groupe (`Tested`)<br>`ORF-QST-018` — Choix exclusifs (`Tested`)<br>`ORF-QST-021` — Notifications (`Tested`) |
| `FC-SCRIPT-P2-01` | SCRIPT | P2 | Clos | 3/3 `Tested` | `MAINTAIN` | `ORF-SCR-018` — API caméra et cutscenes (`Tested`)<br>`ORF-SCR-025` — Module switches (`Tested`)<br>`ORF-SCR-026` — AI behavior switches (`Tested`) |
| `FC-SHELL-P2-01` | SHELL | P2 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-SHELL-022` — Raccourcis configurables (`Tested`) |
| `FC-TRIGGER-P2-01` | TRIGGER | P2 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-TRG-005` — Trigger secret (`Tested`)<br>`ORF-TRG-008` — OnClick (`Tested`)<br>`ORF-TRG-009` — OnHeartbeat (`Tested`)<br>`ORF-TRG-010` — OnUserDefined (`Tested`)<br>`ORF-TRG-012` — Clé requise (`Tested`) |
| `FC-TRIGGER-P2-02` | TRIGGER | P2 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-TRG-013` — Curseur et surbrillance (`Tested`)<br>`ORF-TRG-014` — Trigger de repos sûr (`Tested`)<br>`ORF-TRG-016` — Trigger d’interjection (`Tested`)<br>`ORF-TRG-018` — Déclenchement manuel par GM (`Tested`) |
| `FC-WAYPOINT-P2-01` | WAYPOINT | P2 | À compléter et prouver | 2/3 `Tested` | `COMPLETE` | `ORF-WPT-010` — Patrouille inter-zones (`Tested`)<br>`ORF-WPT-011` — Routes aériennes (`Implemented`)<br>`ORF-WPT-013` — Ancres de caméra (`Tested`) |
| `FC-AI-P3-01` | AI | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-AI-024` — Génération de VFX/audio/images (`Tested`) |
| `FC-CONTENT-P3-01` | CONTENT | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-CONT-010` — Packs curatés (`Tested`) |
| `FC-CREATURE-P3-01` | CREATURE | P3 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-CRE-017` — Alignement (`Tested`)<br>`ORF-CRE-024` — Pickpocket (`Tested`)<br>`ORF-CRE-030` — Divinité et informations de rôle (`Tested`)<br>`ORF-CRE-044` — OnHeartbeat (`Tested`) |
| `FC-DIALOGUE-P3-01` | DIALOGUE | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-DLG-025` — Choix temporisés (`Tested`) |
| `FC-DM-P3-01` | DM | P3 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-DM-015` — Repos forcé (`Tested`)<br>`ORF-DM-018` — Ajouter ou retirer niveau (`Tested`)<br>`ORF-DM-022` — Groupes sur raccourcis (`Tested`)<br>`ORF-DM-030` — Changer apparence (`Tested`)<br>`ORF-DM-037` — Enregistrement de session (`Tested`) |
| `FC-DOOR-P3-01` | DOOR | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-DOOR-017` — OnHeartbeat et OnUserDefined (`Tested`) |
| `FC-ENCOUNTER-P3-01` | ENCOUNTER | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-ENC-014` — OnHeartbeat et OnUserDefined (`Tested`) |
| `FC-FACTION-P3-01` | FACTION | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-FAC-010` — Factions dynamiques (`Tested`) |
| `FC-GAMEPLAY-P3-01` | GAMEPLAY | P3 | Clos | 4/4 `Tested` | `MAINTAIN` | `ORF-GAME-005` — Apesanteur (`Tested`)<br>`ORF-GAME-006` — Véhicules (`Tested`)<br>`ORF-GAME-007` — Montures (`Tested`)<br>`ORF-GAME-028` — Achievements and milestones (`Tested`) |
| `FC-ITEM-P3-01` | ITEM | P3 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-ITEM-021` — Munitions et projectiles (`Tested`)<br>`ORF-ITEM-022` — Usure et durabilité (`Tested`) |
| `FC-MERCHANT-P3-01` | MERCHANT | P3 | Clos | 2/2 `Tested` | `MAINTAIN` | `ORF-MER-014` — Négociation (`Tested`)<br>`ORF-MER-015` — Marchés dynamiques (`Tested`) |
| `FC-MULTI-P3-01` | MULTI | P3 | Clos | 5/5 `Tested` | `MAINTAIN` | `ORF-MP-014` — Curated content (`Tested`)<br>`ORF-MP-021` — Voix (`Tested`)<br>`ORF-MP-025` — Hot reload de contenu serveur (`Tested`)<br>`ORF-MP-026` — Extensibilité serveur (`Tested`)<br>`ORF-MP-035` — Cross-platform protocol (`Tested`) |
| `FC-PLACEABLE-P3-01` | PLACEABLE | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-PLC-023` — OnHeartbeat et OnUserDefined (`Tested`) |
| `FC-SCRIPT-P3-01` | SCRIPT | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-SCR-024` — Tag-based scripting (`Tested`) |
| `FC-TRIGGER-P3-01` | TRIGGER | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-TRG-015` — Trigger de pistes (`Tested`) |
| `FC-WAYPOINT-P3-01` | WAYPOINT | P3 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-WPT-012` — Routes de véhicules (`Tested`) |
| `FC-MULTI-P4-01` | MULTI | P4 | Clos | 1/1 `Tested` | `MAINTAIN` | `ORF-MP-024` — Sharding et zones serveur (`Tested`) |

## Règle de complétude à 100 %

La cible fonctionnelle initiale est atteinte uniquement lorsque les 717 lignes sont `Tested`,
les 187 lots sont `Clos`, le tracker et ce fichier sont frais, et le portail intégré reste vert.
Un nombre de gates fermés, de classes, de marqueurs ou de tests ne remplace pas cette condition.
