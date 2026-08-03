# G8 ENCOUNTER-02 — Composition, cycle et points de spawn

`EncounterRuntimePolicy` décrit les bornes par groupe, budgets, sept politiques d'activation et les
ancres manuelles avec capacité, orientation, navigation, visibilité, fallback, poids et distribution.

`EncounterCompositionService` consomme un `PartyAssessment` fourni par le ruleset, sans formule RPG
historique codée en dur. Une seed donne une composition reproductible bornée par budget, limites de
candidat et cap serveur. `ActivatorQuery` filtre joueurs, invocations, compagnons, MJ, spectateurs,
tags et autorité réseau. Les erreurs de compte, timing et ancres sont localisées.

Preuves : `EncounterCompositionServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 ENCOUNTER-03**.
