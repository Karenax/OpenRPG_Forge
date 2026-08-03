# G8 MERCHANT-01 — Transaction, boutique et stock

`StoreDefinition` est indépendant des acteurs et peut être référencé par plusieurs vendeurs, canaux,
interactions et choix de dialogue. Son stock ordonné porte variantes, quantité, fournisseur,
réapprovisionnement, conditions masquées et politique d'approvisionnement.

`MerchantService` prévisualise puis applique l'assistant de boutique avec UUID déterministe et diff
sémantique. Buy, Sell et Barter valident parties, fonds, propriété, stock et capacité avant toute
mutation ; un rejet restitue la même instance d'état, tandis qu'un succès transfère atomiquement
fonds, objets et stock. La validation couvre vendeurs, monnaies, trésorerie, stock, localisation et
scripts avec chemins stables.

Preuves : `MerchantServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 MERCHANT-02**.
