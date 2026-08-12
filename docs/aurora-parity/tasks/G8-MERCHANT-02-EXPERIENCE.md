# G8 MERCHANT-02 — Prix, restrictions, sessions et interface

`PricingPolicy` calcule achat et vente par monnaie, réputation, rareté, état et négociation, avec
bounds et détail explicable. `ItemQuery` filtre type, tags, qualité, provenance, état, propriétaire et
règle sandbox. Les six `SupplyPolicy` couvrent infini, fini, généré, fabriqué, serveur partagé et
personnel.

`StoreSessionOpened/Closed` conservent client, vendeur, contexte de prix et bilan transactionnel.
`StoreUiConfiguration` exige navigation clavier/manette, labels lecteurs d'écran, comparaison,
filtres, panier, confirmation et colonnes configurables ; la validation localise les contrats
économiques, requêtes et interfaces incohérents.

R091 persiste et expose désormais `PricingPolicy` et `ItemQuery` via le profil canonique, le Studio,
ForgeScript, MCP et l'adaptateur runtime qui réutilise les algorithmes RPG Kit. Les preuves sont
centralisées dans `P1-R091-MERCHANT-FOUNDATION-ACCEPTANCE.md`.

R092 persiste désormais les sessions OnOpen/OnClose, leur API et leur payload, ainsi que l'interface
complète. Le Studio transactionnel, ForgeScript/MCP et le runtime partagent ce profil ; le panneau
runtime expose réellement filtre, liste, comparaison, panier et confirmation avec navigation focusable
et métadonnées d'accessibilité. La preuve est centralisée dans
`P1-R092-MERCHANT-EXPERIENCE-ACCEPTANCE.md` et la capture
`docs/aurora-parity/evidence/r092-merchant-experience-ui.png`.

Les trois fonctions sélectionnées du domaine marchand P1 sont closes. La prochaine tranche
séquentielle est **R093** dans le domaine placeable.
