# G8 COMPANION-01 — Fondation, recrutement et validation

`CompanionComponent` relie une créature à une politique de recrutement indépendante du dialogue,
mais expose les contextes NotRecruited, RecruitedBySpeaker, RecruitedByOther et Unavailable. La
politique porte conditions, offre, refus implicite, coût, réputation, conséquences et voies dialogue
ou script.

`CompanionService` recrute ou renvoie atomiquement en vérifiant capacité, propriétaire, fonds,
inventaire, position et persistance. Tout rejet restitue exactement l'état original. La validation
localise les chemins de recrutement, créature, commandes, inventaire, équipement, mort, dialogue et
monnaie.

Preuves : `CompanionServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 COMPANION-02**.
