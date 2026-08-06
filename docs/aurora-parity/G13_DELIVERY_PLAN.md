# G13 — Extensions post-1.0

## Objectif

G13 ferme les 135 capacités P2/P3/P4 que la matrice Aurora 2026 a explicitement placées après les
gates produit G7 à G12. Elles enrichissent un prototype déjà jouable ; elles ne rouvrent pas les
frontières Core/RPG Kit/Network/éditeur et ne remplacent pas la source de vérité canonique.

Chaque tranche traite une à cinq feature IDs, interroge le graphe avant modification, livre un
workflow réel plutôt qu’une classe vide, puis apporte tests, preuve UI/runtime pertinente, tracker,
documentation et portail global vert.

## Inventaire verrouillé

| Domaine | Exigences G13 |
|---|---:|
| Shell | 1 |
| Créature | 21 |
| Faction | 4 |
| Dialogue | 7 |
| Quête | 5 |
| Objet | 15 |
| Placeable | 7 |
| Porte | 8 |
| Trigger | 10 |
| Rencontre | 6 |
| Waypoint | 1 |
| Marchand | 9 |
| Compagnon | 10 |
| Script | 4 |
| Gameplay | 4 |
| Live Director | 5 |
| Multijoueur | 6 |
| Contenu | 1 |
| Expérience éditeur | 9 |
| IA | 1 |
| QA | 1 |
| **Total** | **135** |

## Ordre de livraison

1. **Finition auteur** : raccourcis, représentation des créatures, détails d’équipement et retours
   visuels ; cette piste produit immédiatement une interface et des cartes plus agréables.
2. **Profondeur RPG** : progression des créatures, objets, factions, dialogues, quêtes, marchands,
   compagnons et rencontres.
3. **Monde réactif** : placeables, portes, triggers, waypoint, gameplay et scripts avancés.
4. **Production et exploitation** : expérience éditeur, contenu, QA, IA, Live Director et extensions
   multijoueur qui ne compromettent pas l’expérience solo locale.

Les dépendances Network restent autoritaires, les extensions IA restent optionnelles et aucune
capacité G13 ne peut imposer un fournisseur, un abonnement ou un asset propriétaire.

## File active

- [x] **G13-A** — `ORF-SHELL-022` : keymap Standard/Aurora/Blender, recherche, conflits,
  persistance, exécution et undo/redo.
- [x] **G13-B** — `ORF-CRE-006`, `ORF-CRE-007`, `ORF-CRE-009`, `ORF-CRE-010`, `ORF-CRE-029` :
  identité, portrait, parties du corps, couleurs/ailes/queues et voix d’une créature.
- [x] **G13-C** — `ORF-CRE-016`, `ORF-CRE-017`, `ORF-CRE-018`, `ORF-CRE-019`, `ORF-CRE-020` :
  classes/niveaux, alignement libre, compétences, talents et pouvoirs préparés data-driven.
- [x] **G13-D** — `ORF-CRE-023`, `ORF-CRE-024`, `ORF-CRE-032` : objets droppables,
  pickpocket et cadavres/conteneurs de butin dans un workflow cohérent.
- [x] **G13-E** — `ORF-CRE-030`, `ORF-CRE-047`, `ORF-CRE-048` : informations de rôle,
  animations ambiantes et auto-level/templates de créature.
- [x] **G13-F** — `ORF-CRE-039`, `ORF-CRE-041`, `ORF-CRE-042`, `ORF-CRE-044`, `ORF-CRE-045` :
  événements avancés de créature (fin de round, perturbation, repos, heartbeat et événement utilisateur).
- [x] **G13-G** — `ORF-FAC-002`, `ORF-FAC-008`, `ORF-FAC-009`, `ORF-FAC-010` : factions parentes,
  propagation par témoins, memberships multiples et factions dynamiques.
- [x] **G13-H** — `ORF-DLG-014`, `ORF-DLG-015`, `ORF-DLG-016`, `ORF-DLG-020`, `ORF-DLG-023` :
  animations, voix, caméra, bookmarks et interjections de dialogue.
- [ ] **G13-I et suivants** — développement volontairement arrêté après G13-H ; aucune nouvelle
  tranche ne démarre sans demande explicite de l’utilisateur.

## Gate de fermeture

G13 sera fermé lorsque les 135 lignes ciblées auront une décision vérifiable (`Tested` ou report
documenté), que les modules de démonstration resteront jouables, que le runtime sans IA restera
fonctionnel, et que build, tests, graphe, licences, captures et documentation seront cohérents.
