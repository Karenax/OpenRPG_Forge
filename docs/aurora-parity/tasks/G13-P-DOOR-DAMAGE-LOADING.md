# G13-P — Portes : dégâts et chargement

Features : `ORF-DOOR-007`, `ORF-DOOR-010`, `ORF-DOOR-014`, `ORF-DOOR-015`.

## Livré

`DoorWorldProfile` porte santé, dureté, protection plot, visuels endommagé/brèche, débris, service de
réparation et mise à jour navigation. L'écran de chargement possède image, clés localisées, durée et
fallback de destination. Les événements dégâts, mort et attaque sont autoritaires.

## Preuves

- test : `G13P_DoorDamageLoading_ProducesBreachAndNavigationUpdate` ;
- Toolset/runtime : `G13P_DOOR_DAMAGE_LOADING_PASS` et `G13P_DOOR_DAMAGE_PASS`.

Limite : les assets de débris et chargement restent soumis au Vault et à la licence du module.
