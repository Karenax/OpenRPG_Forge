# G8 COMPANION-02 — Groupe, propriété, ordres et IA

`PartyPolicy` borne compagnons, rôles, invocations, familiers, alliés temporaires et population
serveur. `CompanionOwnership` couvre individuel, groupe, partagé, IA seule et MJ ; chaque transfert
vérifie les cibles permises et émet un audit révisionné.

Les ordres typés Move, Follow, Hold, Rally, Guard et Interact partagent une file priorisée avec les
ordres Attack, Defend, FocusFire, Protect, Disengage et UseAbility. L'autorisation provient du même
ownership. `CompanionAiProfile` borne autonomie, risque, usage de ressources, tir allié, rôle,
distance de suivi et capacités autorisées.

Preuves : `CompanionControlServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 COMPANION-03**.
