# R2-049 — origine et portée des retouches

14 septembre 2026, Rust, modifications locales sans publication.

Implémenté dans les inspecteurs existants :

- Porte : valeur de recette, override absolu optionnel et valeur effective,
  mètres/axe X local, état du verrou du bâtiment entier (aucun verrou fin inventé).
- Placement : AssetId et hash épinglés, transformation absolue de cet EntityId,
  verrou du placement entier ; hash non assimilé à une preuve de licence.
- Chemin/rivière : nouveau brouillon distingué de la recette auteur acceptée,
  conservation lors d’une régénération de zone et portée du verrou explicitées.
- Champs identifiés comme brouillons jusqu’à Appliquer. Pas de nouveau writer,
  changement de format, modification de lock ou application implicite.

Test ciblé `door_origin_distinguishes_an_explicit_zero_from_inheritance` : PASS,
1 test, 0 s après compilation 50,23 s. Vérifie recette 1,5 m/override absent
contre override explicite zéro et libellé de verrou global.
Clippy Web/WASM strict final : PASS (6,86 s). Premier passage refusé pour la
taille de PlacedObjectControls ; extraction du calcul des réglages snap sans
changement de comportement, aucun lint supprimé. Avertissement tiers futur
proc-macro-error2 2.0.1 inchangé.

Texte réellement affiché et couverture de toutes les propriétés non qualifiés.
Aucun nouveau candidat compilé ou lancé ; F1C5DB66 ne contient pas ce lot.
R2-049 et Gate 2 restent ouverts.
