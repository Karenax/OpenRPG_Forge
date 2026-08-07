# G13-W — Marchands : économie de base

Features : `ORF-MER-005`, `ORF-MER-006`, `ORF-MER-007`, `ORF-MER-010`.

## Livré

Le marchand possède trésorerie par monnaie, crédit/dette/recharge/plafond, limites d'achat par item,
transaction et catégorie, relation minimale et contrôle des fonds. Biens volés, juridictions,
preuve/fence, risque et événement de crime sont explicites. Le réapprovisionnement suit temps monde,
événement, route fournisseur, demande et persistance.

## Preuves

- test : `G13W_MerchantCore_EnforcesTreasuryCapsStolenGoodsAndRestock` ;
- Toolset/runtime : `G13W_MERCHANT_CORE_PASS` et `G13W_MERCHANT_ECONOMY_PASS`.

Limite : l'inventaire de stock reste géré par le catalogue d'items.
