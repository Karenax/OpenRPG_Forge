# Prompt Codex — epic de domaine

Remplacer les champs entre chevrons.

```text
Lis AGENTS.md, le MEGACONTEXT, AGENTS_AURORA_PARITY.md et le document
`docs/aurora-parity/docs/requirements/<DOMAIN_FILE>.md`.

Features autorisées : <FEATURE_IDS>
But : <ONE_SENTENCE_GOAL>

Avant de modifier :
- inspecte Git et le code existant ;
- résume les contrats déjà présents ;
- identifie les tests ;
- n’élargis pas la portée.

Livrables :
- modèle de domaine canonique minimal ;
- commandes/handlers ;
- persistance ou adaptateur seulement si nécessaire ;
- validation structurée ;
- undo/transaction si édition ;
- tests correspondant aux critères d’acceptation ;
- mise à jour du tracker avec evidence.

Interdit :
- toute feature hors liste ;
- nouveau framework générique sans deux usages ;
- dépendance de production non approuvée ;
- changement de moteur/format canonique ;
- faux statut Tested.

À la fin, liste fichiers, tests, résultats, feature IDs et prochaine tâche unique.
```
