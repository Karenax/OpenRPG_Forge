# G13-N — Placeables : social et accès

Features : `ORF-PLC-010`, `ORF-PLC-011`, `ORF-PLC-013`.

## Livré

`PlaceableWorldProfile` relie un blueprint à une faction, un dialogue et une politique d'identifiant
retenu, consommé, à charges, partagé ou virtuel avec audit. Le Studio Monde vivant configure la
cible et résume le résultat avant la commande transactionnelle.

## Preuves

- test : `G13N_PlaceableSocialAccess_ConsumesGovernedCredential` ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13N_PLACEABLE_SOCIAL_ACCESS_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13N_PLACEABLE_ACCESS_PASS`.

Limite : l'inventaire global reste propriétaire des quantités ; le résolveur retourne la décision de
consommation mais ne modifie pas directement l'acteur.
