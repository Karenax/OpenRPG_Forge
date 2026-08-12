# G8 ITEM-03 — Transferts, modificateurs, équipement et loot

`TransferPolicy` contrôle chaque destination et permission. Les modificateurs data-driven portent
paramètres, règle d’empilement, condition, résumé UI et hooks ForgeScript. Les capacités d’objet lient
une définition de capacité à son ciblage, ses coûts, charges, cooldown et événements typés.

Les slots sont définis par ruleset et rig avec multi-slot, couches, conflits et aperçu des déplacements.
Les tables de loot pondérées gèrent groupes, conditions, quantités, qualité, seed déterministe,
unicité et aperçu statistique.

Preuves : `ItemGameplayServiceTests`, suite RPG Kit et solution globale.

R089 relie les transferts, modificateurs, capacités et slots au blueprint objet canonique. Le Studio P1
expose les huit destinations, permissions, paramètres, stacking, conditions, hooks, coûts, charges,
cooldown, événements et conflits en direct. ForgeScript, MCP, persistance et runtime partagent le même
profil optionnel/requis via `ItemGameplayRuntimeAdapter`.

Preuves R089 : `ItemGameplayRuntimeAdapterTests`, `P1-R089-ITEM-GAMEPLAY-ACCEPTANCE.md`, capture
1440×920 et portail intégré.

R090 persiste les tables pondérées dans le blueprint canonique et expose groupes, conditions,
quantités, qualité, seed, unicité, tirage reproductible et aperçu statistique dans le Studio objet.
Le runtime continue d'utiliser `ItemGameplayService` via l'adaptateur R090.

Preuves R090 : `ItemOperationsRuntimeAdapterTests`, `P1-R090-ITEM-OPERATIONS-ACCEPTANCE.md`, capture
1440×920 et portail intégré.
