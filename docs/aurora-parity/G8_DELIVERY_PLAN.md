# Plan de livraison G8 — RPG Kit

Date de référence : 3 août 2026.

## Audit initial

G8 assigne 184 exigences, toutes encore `Researched` : 39 P0 et 145 P1. Elles couvrent douze
domaines : créatures (28), placeables (20), scripts (21), objets (17), dialogues (15), compagnons
(14), rencontres (14), portes (13), quêtes (12), factions (10), marchands (10) et triggers (10).

Le Core G7 fournit déjà UUID, propriétés typées, commandes transactionnelles, zones, blueprints,
dialogues, quêtes, transitions, ForgeScript, persistance, recherche, validation et playtest. G8 doit
les spécialiser en documents RPG : une propriété générique ou un catalogue de recherche ne suffit
pas à faire passer une exigence au statut `Implemented`.

## Module de preuve

Le module G8 sera **La Veillée des Éclats** (`la-veillee-des-eclats`), distinct de la Crypte et des
Îles Brisées. Dans un village côtier assiégé par des créatures de verre, l'auteur doit pouvoir :

- recruter Ilyne, une éclaireuse dont la loyauté dépend des choix de dialogue ;
- négocier avec deux factions asymétriques et un marchand à stock limité ;
- acquérir, équiper et utiliser la Lanterne des Marées ;
- désamorcer un piège, ouvrir une porte verrouillée et traverser un trigger polygonal ;
- affronter une rencontre en deux vagues, puis choisir le sort du phare ;
- sauvegarder, recharger, exporter, installer et jouer sans modifier manuellement le JSON.

La fixture finale doit être créée par les services/commandes d'auteur G8 dans un test de scénario,
puis sauvegardée par `ProjectSession`; les fichiers JSON obtenus sont des preuves générées, pas une
voie d'édition parallèle.

## Lots exécutables

Chaque lot contient au plus cinq exigences. À l'intérieur d'un domaine, les P0 passent en premier.

| Lots | Exigences |
|---|---|
| CREATURE-01 | `CRE-002`, `003`, `014`, `027`, `033` |
| CREATURE-02 | `CRE-036`, `043`, `049`, `001`, `004` |
| CREATURE-03 | `CRE-005`, `008`, `011`, `012`, `013` |
| CREATURE-04 | `CRE-015`, `021`, `022`, `025`, `026` |
| CREATURE-05 | `CRE-028`, `031`, `034`, `035`, `037` |
| CREATURE-06 | `CRE-038`, `040`, `046` |
| FACTION-01 | `FAC-014`, `001`, `003`, `004`, `005` |
| FACTION-02 | `FAC-006`, `007`, `011`, `012`, `013` |
| ITEM-01 | `ITEM-002`, `003`, `004`, `032`, `001` |
| ITEM-02 | `ITEM-005`, `006`, `008`, `009`, `013` |
| ITEM-03 | `ITEM-014`, `015`, `016`, `018`, `024` |
| ITEM-04 | `ITEM-030`, `031` |
| PLACEABLE-01 | `PLC-001`, `002`, `003`, `005`, `006` |
| PLACEABLE-02 | `PLC-018`, `027`, `004`, `007`, `008` |
| PLACEABLE-03 | `PLC-009`, `012`, `014`, `015`, `016` |
| PLACEABLE-04 | `PLC-017`, `019`, `020`, `025`, `026` |
| DOOR-01 | `DOOR-001`, `002`, `003`, `008`, `009` |
| DOOR-02 | `DOOR-019`, `021`, `004`, `005`, `006` |
| DOOR-03 | `DOOR-011`, `012`, `013` |
| TRIGGER-01 | `TRG-001`, `002`, `003`, `006`, `007` |
| TRIGGER-02 | `TRG-020`, `004`, `011`, `017`, `019` |
| ENCOUNTER-01 | `ENC-002`, `003`, `020`, `001`, `004` |
| ENCOUNTER-02 | `ENC-005`, `006`, `007`, `009`, `011` |
| ENCOUNTER-03 | `ENC-012`, `013`, `015`, `018` |
| MERCHANT-01 | `MER-012`, `019`, `001`, `002`, `003` |
| MERCHANT-02 | `MER-004`, `008`, `009`, `011`, `017` |
| COMPANION-01 | `CMP-024`, `001`, `002`, `003`, `005` |
| COMPANION-02 | `CMP-006`, `007`, `008`, `009`, `010` |
| COMPANION-03 | `CMP-012`, `017`, `018`, `022` |
| DIALOGUE-01 | `DLG-007`, `008`, `010`, `012`, `013` |
| DIALOGUE-02 | `DLG-017`, `018`, `019`, `022`, `024` |
| DIALOGUE-03 | `DLG-026`, `028`, `030`, `031`, `032` |
| QUEST-01 | `QST-006`, `008`, `010`, `011`, `012` |
| QUEST-02 | `QST-013`, `014`, `015`, `019`, `020` |
| QUEST-03 | `QST-024`, `025` |
| SCRIPT-01 | `SCR-004`, `005`, `008`, `010`, `011` |
| SCRIPT-02 | `SCR-012`, `013`, `014`, `015`, `016` |
| SCRIPT-03 | `SCR-019`, `020`, `023`, `032`, `033` |
| SCRIPT-04 | `SCR-034`, `035`, `037`, `038`, `040` |
| SCRIPT-05 | `SCR-041` |

## Ordre de livraison

1. vertical RPG minimal : CREATURE-01/02, FACTION-01, ITEM-01, PLACEABLE-01, DOOR-01,
   TRIGGER-01 et ENCOUNTER-01 ;
2. boucle jouable : ITEM-02/03, PLACEABLE-02/03, DOOR-02, ENCOUNTER-02/03, MERCHANT-01 ;
3. narration et groupe : COMPANION, DIALOGUE, QUEST et FACTION-02 ;
4. complétude auteur : lots restants de chaque domaine, puis SCRIPT-01 à 05 ;
5. génération de `la-veillee-des-eclats`, round-trip, export, runtime et revue visuelle.

## Gate de fermeture

G8 ne sera fermé qu'après :

1. 184 lignes justifiées `Implemented` ou `Tested`, avec chemins de preuve existants ;
2. écrans spécialisés utilisables sans édition JSON et mutations via commandes avec dry-run/undo ;
3. module La Veillée des Éclats généré par le parcours auteur, exporté et joué ;
4. tests des règles, transactions, validations, sérialisation, événements et scripts ;
5. revue visuelle nominale/maximisée, `Verify.ps1` vert, tracker frais et graphe déterministe.
