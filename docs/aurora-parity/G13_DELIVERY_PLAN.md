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
- [x] **G13-I** — `ORF-DLG-025`, `ORF-DLG-027` : choix temporisés accessibles et variantes
  conditionnelles/pondérées déterministes.
- [x] **G13-J** — `ORF-QST-009`, `ORF-QST-016`, `ORF-QST-017`, `ORF-QST-018`, `ORF-QST-021` :
  priorité du journal, répétition, partage de groupe, choix exclusifs et notifications accessibles.
- [x] **G13-K** — `ORF-ITEM-007`, `ORF-ITEM-010`, `ORF-ITEM-011`, `ORF-ITEM-012`, `ORF-ITEM-017` :
  économie, prérequis, identification, propriété/vol et effets On Hit déterministes.
- [x] **G13-L** — `ORF-ITEM-019`, `ORF-ITEM-020`, `ORF-ITEM-021`, `ORF-ITEM-022`, `ORF-ITEM-023` :
  armes, armures, munitions, durabilité et conteneurs imbriqués.
- [x] **G13-M** — `ORF-ITEM-025`, `ORF-ITEM-026`, `ORF-ITEM-027`, `ORF-ITEM-028`, `ORF-ITEM-029` :
  loot contextuel, unicité transactionnelle, propriété instanciée, artisanat et génération par seed.
- [x] **G13-N** — `ORF-PLC-010`, `ORF-PLC-011`, `ORF-PLC-013` : faction, conversation et
  identifiants consommables/à charges des placeables.
- [x] **G13-O** — `ORF-PLC-021`, `ORF-PLC-022`, `ORF-PLC-023`, `ORF-PLC-024` : événements
  physiques, capacités, heartbeat/custom budgétés et physique dynamique répliquée.
- [x] **G13-P** — `ORF-DOOR-007`, `ORF-DOOR-010`, `ORF-DOOR-014`, `ORF-DOOR-015` : dégâts,
  brèche/navigation, chargement contextualisé et événements de porte.
- [x] **G13-Q** — `ORF-DOOR-016`, `ORF-DOOR-017`, `ORF-DOOR-018`, `ORF-DOOR-020` : capacités,
  heartbeat/custom, dialogue/faction et assemblages de portes complexes.
- [x] **G13-R** — `ORF-TRG-005`, `ORF-TRG-008`, `ORF-TRG-012`, `ORF-TRG-013`, `ORF-TRG-014` :
  découverte, interaction, accès, retour visuel et repos sûr.
- [x] **G13-S** — `ORF-TRG-009`, `ORF-TRG-010`, `ORF-TRG-015`, `ORF-TRG-016`, `ORF-TRG-018` :
  automatisation, pistes, narration et déclenchement GM audité.
- [x] **G13-T** — `ORF-ENC-008`, `ORF-ENC-010`, `ORF-ENC-014` : respawn borné, faction de spawn
  et événements de rencontre budgétés.
- [x] **G13-U** — `ORF-ENC-016`, `ORF-ENC-017`, `ORF-ENC-019` : renforts, spawn hors vue et
  nettoyage déterministe.
- [x] **G13-V** — `ORF-WPT-012` : routes de véhicules à points, voies, arrêts et synchronisation.
- [x] **G13-W** — `ORF-MER-005`, `ORF-MER-006`, `ORF-MER-007`, `ORF-MER-010` : trésorerie,
  plafonds, biens volés et réapprovisionnement.
- [x] **G13-X** — `ORF-MER-013`, `ORF-MER-014`, `ORF-MER-015`, `ORF-MER-016`, `ORF-MER-018` :
  monnaies, négociation, marchés, services et audit économique.
- [x] **G13-Y** — `ORF-CMP-004`, `ORF-CMP-011`, `ORF-CMP-013`, `ORF-CMP-014`, `ORF-CMP-015` :
  permissions sociales, formation, progression, relation et banter.
- [x] **G13-Z** — `ORF-CMP-016`, `ORF-CMP-019`, `ORF-CMP-020`, `ORF-CMP-021`, `ORF-CMP-023` :
  quête personnelle, pièges, capacités, contrôle direct et regroupement.
- [x] **G13-AA** — `ORF-SCR-018`, `ORF-SCR-024` : caméra/cutscenes et routage explicite par tag.
- [x] **G13-AB** — `ORF-SCR-025`, `ORF-SCR-026` : flags de module documentés et réglages IA typés.
- [x] **G13-AC** — `ORF-GAME-005`, `ORF-GAME-006`, `ORF-GAME-007` : apesanteur, véhicules et montures.
- [x] **G13-AD** — `ORF-GAME-028`, `ORF-CONT-010` : succès et packs sélectionnés gouvernés.
- [x] **G13-AE** — `ORF-DM-015`, `ORF-DM-018`, `ORF-DM-030` : repos, niveau et apparence Live Director.
- [x] **G13-AF** — `ORF-DM-022`, `ORF-DM-037` : groupes sauvegardés et enregistrement de session.
- [x] **G13-AG** — `ORF-MP-014`, `ORF-MP-021` : contenu réseau sélectionné et voix optionnelle.
- [x] **G13-AH** — `ORF-MP-024`, `ORF-MP-025` : shards et hot reload atomique.
- [x] **G13-AI** — `ORF-MP-026`, `ORF-MP-035` : extensions serveur isolées et protocole compatible.
- [x] **G13-AJ** — `ORF-EE-009`, `ORF-EE-014`, `ORF-EE-017`, `ORF-EE-018`, `ORF-EE-030` : outils Creator techniques.
- [x] **G13-AK** — `ORF-EE-020`, `ORF-EE-021`, `ORF-EE-025`, `ORF-EE-029` : RPG, dépôt et observabilité.
- [x] **G13-AL** — `ORF-AI-024`, `ORF-QA-027` : génération média gouvernée et soak tests.

G13 est fermé après **G13-AL** : ses 135 feature IDs disposent d'une implémentation et de preuves
référencées. Aucun lot postérieur n'est ouvert par cette fermeture.

## Gate de fermeture

G13 sera fermé lorsque les 135 lignes ciblées auront une décision vérifiable (`Tested` ou report
documenté), que les modules de démonstration resteront jouables, que le runtime sans IA restera
fonctionnel, et que build, tests, graphe, licences, captures et documentation seront cohérents.
