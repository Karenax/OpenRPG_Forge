# R2-017 — exclusions de végétation auteur

## Implémenté, non testé — 20 septembre 2026

`ZoneCorrections.vegetation_exclusions` est une substitution optionnelle de la
liste canonique `VegetationExclusion` de la recette. Son contrat est explicite :
`None` conserve les exclusions de la recette et `Some(vec)`, y compris
`Some([])`, les remplace. L'agrégat dérive ainsi le `VegetationDocument`
effectif utilisé par les projections, sans modifier la base générée ni créer une
seconde source éditable.

La tête passe en version 9 seulement lorsque cette substitution est présente.
Les têtes et archives historiques qui ne la portent pas conservent donc leur
version et leur forme. Une commande d'exclusion produit une requête typée du
bus de zone avec hash, révision et `CommandId`. Preview, Apply, retry
idempotent, inverse Undo/Redo, audit, Save/Open et les encodages normal,
compact et delta suivent le mécanisme unique déjà utilisé par les corrections de
zone. Les nouvelles archives portant ce champ utilisent respectivement les
versions 28, 29 et 30.

Le panneau Studio affiche la source effective et la source auteur, liste les
IDs effectifs, et prépare l'ajout, la modification ou le retrait d'un cercle ou
d'un segment corridor en mètres X/Z. Les modifications de champ, de type ou de
zone annulent l'aperçu. L'application ou l'abandon restent les actions centrales
existantes ; aucun writer Bevy ou document parallèle n'est introduit.

Tests ajoutés mais **non exécutés** à la demande : héritage, `Some([])`,
validation des doublons et du quota, preview/apply/retry, Undo/Redo, round-trip
des trois archives, et effet de l'exclusion effective sur la projection de
scatter. Aucun build, formatage, test, Clippy ou Computer Use n'a été lancé pour
ce lot. La qualification Luna et le parcours Studio restent requis avant tout
verdict R2-017.
