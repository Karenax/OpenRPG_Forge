# R1 — fondation des coefficients optiques, pas de nouveau rendu

9 septembre 2026, Rust, Gate 1 ouverte, aucune publication.

État historique de la fondation seule. Le raccord et ses preuves ultérieures
sont décrits dans `R1_WATER_MEDIUM_INTEGRATION.md` ; les limites ci-dessous
documentent cette première étape, pas l'état courant du renderer.

L'inspection confirme que le candidat actuel utilise une extinction RGB neutre
dérivée de l'opacité et une palette de diffusion approximative. Le maximum de
profondeur échantillonné exporté vaut 2,8 m ; aucune preuve d'un maximum aberrant
n'explique à elle seule le fond brun visible. Ne pas confondre fond peu profond
visible et défaut de couleur, ni interpréter arbitrairement la palette sRGB
comme des coefficients physiques.

## Implémenté

Type indépendant `forge_project::WaterMediumProfile` : absorption et diffusion
RGB en m^-1, validation stricte, transmittance homogène
`exp(-(absorption + scattering) * distance)`. Coefficients `[0,100]`, distance
`[0,100000]`, valeurs non finies/négatives refusées. Pas de dépendance ajoutée.
Il s'agit d'une approximation RGB, pas d'un modèle spectral mesuré.

Le type n'est **pas encore raccordé à WaterDocument**, au renderer ou au Studio.
Ce choix temporaire évite d'accepter un champ persistant que le moteur ignorerait.
Sources JSON, hashes des cartes, profils et pixels demeurent inchangés.

## Vérification

Deux tests ciblés PASS : limites transparentes, ordre d'extinction par canal,
composition de deux distances, round-trip et refus de champs/valeurs invalides.
Clippy projet strict PASS après correction d'un commentaire Markdown signalé.
Le test calcule une référence CPU ; il ne prouve pas l'exécution GPU.
Suite `cargo test -p forge_project` : 66 PASS, zéro échec ; format PASS.
Le contrôle workspace transversal reste à rejouer avec le raccord au renderer ;
les 649 tests antérieurs ne qualifient pas ce nouveau type public.

## Suite requise

Raccorder ensemble le champ optionnel (anciens hashes conservés), l'adapter
avec statut explicite, et la composition WGSL transmission/diffusion éclairée.
Exercer un profil de preuve explicite sans modifier la carte de référence,
puis comparer en natif/WebGPU. Ne pas présenter la brique actuelle comme un
nouveau matériau disponible ou une amélioration artistique. Aucun critère
de gate validé ; aucun essai graphique lancé pour ce lot sans effet visuel.
