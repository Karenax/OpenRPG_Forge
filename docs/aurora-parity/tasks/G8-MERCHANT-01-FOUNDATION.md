# G8 MERCHANT-01 — Transaction, boutique et stock

`StoreDefinition` est indépendant des acteurs et peut être référencé par plusieurs vendeurs, canaux,
interactions et choix de dialogue. Son stock ordonné porte variantes, quantité, fournisseur,
réapprovisionnement, conditions masquées et politique d'approvisionnement.

`MerchantService` prévisualise puis applique l'assistant de boutique avec UUID déterministe et diff
sémantique. Buy, Sell et Barter valident parties, fonds, propriété, stock et capacité avant toute
mutation ; un rejet restitue la même instance d'état, tandis qu'un succès transfère atomiquement
fonds, objets et stock. La validation couvre vendeurs, monnaies, trésorerie, stock, localisation et
scripts avec chemins stables.

R091 relie désormais ces services purs à `MerchantFoundationProfile`, au Studio transactionnel, à la
persistance projet, à ForgeScript/MCP et au runtime. La preuve reproductible est détaillée dans
`P1-R091-MERCHANT-FOUNDATION-ACCEPTANCE.md` et la capture
`docs/aurora-parity/evidence/r091-merchant-foundation-ui.png`.

La fondation magasin, le wizard et l'inventaire sont clos. R092 relie en plus la politique infinie au
profil projet et prouve une transaction de 50 unités sans décrémenter la quantité configurée. La preuve
reproductible est détaillée dans `P1-R092-MERCHANT-EXPERIENCE-ACCEPTANCE.md`.
