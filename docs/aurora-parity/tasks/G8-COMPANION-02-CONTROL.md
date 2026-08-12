# G8 COMPANION-02 — Groupe, propriété, ordres et IA

`PartyPolicy` borne compagnons, rôles, invocations, familiers, alliés temporaires et population
serveur. `CompanionOwnership` couvre individuel, groupe, partagé, IA seule et MJ ; chaque transfert
vérifie les cibles permises et émet un audit révisionné.

Les ordres typés Move, Follow, Hold, Rally, Guard et Interact partagent une file priorisée avec les
ordres Attack, Defend, FocusFire, Protect, Disengage et UseAbility. L'autorisation provient du même
ownership. `CompanionAiProfile` borne autonomie, risque, usage de ressources, tir allié, rôle,
distance de suivi et capacités autorisées.

R074 rattache `PartyPolicy` au document de monde pour les quotas compagnons, invocations, familiers,
alliés temporaires, rôles et population serveur. R075 rattache maintenant propriété, transferts,
ordres, autonomie et équipement au même document canonique, avec UI transactionnelle, persistance,
ForgeScript, MCP, diagnostics pré-build et adaptateurs moteur.

Preuves : `CompanionControlServiceTests`, `CompanionFoundationServiceTests`,
`CompanionControlFoundationServiceTests`, `P1-R074-COMPANION-FOUNDATION-ACCEPTANCE.md` et
`P1-R075-COMPANION-CONTROL-ACCEPTANCE.md`.

La prochaine tranche unique est **R076 / FC-COMPANION-P1-03**, limitée à cinq fonctions.
