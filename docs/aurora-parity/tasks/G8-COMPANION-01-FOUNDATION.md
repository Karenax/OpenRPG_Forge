# G8 COMPANION-01 — Fondation, recrutement et validation

`CompanionComponent` relie une créature à une politique de recrutement indépendante du dialogue,
mais expose les contextes NotRecruited, RecruitedBySpeaker, RecruitedByOther et Unavailable. La
politique porte conditions, offre, refus implicite, coût, réputation, conséquences et voies dialogue
ou script.

`CompanionService` recrute ou renvoie atomiquement en vérifiant capacité, propriétaire, fonds,
inventaire, position et persistance. Tout rejet restitue exactement l'état original. La validation
localise les chemins de recrutement, créature, commandes, inventaire, équipement, mort, dialogue et
monnaie.

R074 rattache désormais cette fondation au document de monde : identité UUID/schéma/provenance,
interface transactionnelle, commande unique, persistance exacte, ForgeScript typé, MCP filtré,
validation pré-build et adaptateurs moteur sont couverts. Le recrutement et le renvoi partagent le
même plan déterministe et atomique.

Preuves : `CompanionServiceTests`, `CompanionFoundationServiceTests`, smokes Toolset/runtime et
`P1-R074-COMPANION-FOUNDATION-ACCEPTANCE.md`.

La prochaine tranche unique est **R075 / G8 COMPANION-02**.
