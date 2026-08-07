# G13-Z — Compagnons : gameplay et contrôle

Features : `ORF-CMP-016`, `ORF-CMP-019`, `ORF-CMP-020`, `ORF-CMP-021`, `ORF-CMP-023`.

## Livré

Quête personnelle et conséquence de départ, détection/désamorçage de piège, capacités autorisées et
réservées, cible/threshold, contrôle direct autorisé et séparation/regroupement sont typés. Le
résolveur combine rôle, santé, risque, demande de contrôle et séparation en une décision.

## Preuves

- test : `G13Z_CompanionGameplayControl_HandlesTrapsAbilitiesPossessionAndRegroup` ;
- Toolset/runtime : `G13Z_COMPANION_GAMEPLAY_CONTROL_PASS` et
  `G13Z_COMPANION_CONTROL_PASS`.

Limite : transport multijoueur et possession restent soumis aux permissions serveur.
