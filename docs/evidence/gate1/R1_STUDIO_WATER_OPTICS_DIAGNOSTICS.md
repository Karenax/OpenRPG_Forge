# R1-022/023 — restitution du candidat optique dans le Studio

2026-09-08, Rust ; suite des limites observées dans `R1_STUDIO_WATER_OPTICS.md`.

## Implémenté

Le statut des commandes d'eau désactivées distingue désormais un candidat
optique épinglé d'un matériau non observé. Le booléen provient des statistiques
moteur, pas seulement de l'URL. Les commandes restent désactivées lorsque le
profil n'est pas appliqué ; aucun verdict de qualité n'est ajouté.

Le ledger `forge.studio-water-optics-observation` v1 contient les statistiques
`WaterProjectionStats`, la phase courante du runtime et le temps d'échantillonnage.
Publication bornée à une fois par seconde ; l'export JSON le reprend dans
`observations.water_optics` et ajoute le texte de demande à
`displayed_statuses.water_optics`. Le schéma enveloppe reste v1 : ses maps de
ledgers sont extensibles, aucun champ existant n'est supprimé ou réinterprété.

La résolution optique authorée reste une intention. Le booléen de candidat,
l'observation des composants, l'absence de profil projeté et les limites sont
exportés séparément. La phase est CPU ; elle ne prouve pas le mouvement GPU.
Les exports PNG/JSON restent non atomiques et non qualifiants.

## Vérifié / restant

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-water-optics-diagnostics-20260908/` :
fmt, **644 tests workspace PASS, zéro échec, un ignoré**, Clippy workspace strict,
check WASM et Clippy WASM strict PASS. Test des libellés candidat/absence ajouté. Aucun changement moteur ou
source de carte. Aucun nouveau build release, lancement ou export contenant ces
champs encore vérifié ; ne pas attribuer ces capacités au bundle du lot précédent.

Prochaine étape : intégrer ces diagnostics dans le bundle utilisé pour la vue
rapprochée et la preuve temporelle, en un seul rejeu. Gate 1 ouverte ; aucune
publication Git, aucun processus de validation encore actif.
