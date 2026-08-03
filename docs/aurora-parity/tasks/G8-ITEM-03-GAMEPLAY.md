# G8 ITEM-03 — Transferts, modificateurs, équipement et loot

`TransferPolicy` contrôle chaque destination et permission. Les modificateurs data-driven portent
paramètres, règle d’empilement, condition, résumé UI et hooks ForgeScript. Les capacités d’objet lient
une définition de capacité à son ciblage, ses coûts, charges, cooldown et événements typés.

Les slots sont définis par ruleset et rig avec multi-slot, couches, conflits et aperçu des déplacements.
Les tables de loot pondérées gèrent groupes, conditions, quantités, qualité, seed déterministe,
unicité et aperçu statistique.

Preuves : `ItemGameplayServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 ITEM-04**.
