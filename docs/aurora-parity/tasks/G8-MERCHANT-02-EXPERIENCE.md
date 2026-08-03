# G8 MERCHANT-02 — Prix, restrictions, sessions et interface

`PricingPolicy` calcule achat et vente par monnaie, réputation, rareté, état et négociation, avec
bounds et détail explicable. `ItemQuery` filtre type, tags, qualité, provenance, état, propriétaire et
règle sandbox. Les six `SupplyPolicy` couvrent infini, fini, généré, fabriqué, serveur partagé et
personnel.

`StoreSessionOpened/Closed` conservent client, vendeur, contexte de prix et bilan transactionnel.
`StoreUiConfiguration` exige navigation clavier/manette, labels lecteurs d'écran, comparaison,
filtres, panier, confirmation et colonnes configurables ; la validation localise les contrats
économiques, requêtes et interfaces incohérents.

Preuves : `MerchantExperienceServiceTests`, suite RPG Kit et solution globale.

La famille Merchant sélectionnée pour G8 est complète. La prochaine tranche unique est
**G8 COMPANION-01**.
