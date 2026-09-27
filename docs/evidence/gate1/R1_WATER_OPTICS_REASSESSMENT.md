# R1-022/023 — réorientation du rendu de rivière

Date : 2026-09-08. Branche `Rust`, base HEAD
`f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié préservé.
Statut : **diagnostic de code et recherche ; aucun nouveau rendu implémenté ou qualifié**.
Gate 1 ouverte. Cette note ne remplace pas les preuves antérieures.

## Retour et décision de travail

Le propriétaire refuse encore l'aspect de l'eau et demande des méthodes éprouvées.
Les petites rides précédemment capturées ne constituent pas une acceptation.
Ne plus traiter la clôture comme une succession de réglages de sinusoïdes.
Traiter ensemble contact avec les berges, profondeur optique, courant et reflets.
Conserver le raccord géométrique sous les berges ; aucune retouche ponctuelle de
`visual_valley`, aucun asset Cadle ou ressource tierce copié.

## Références consultées

- [NVIDIA, Effective Water Simulation from Physical Models](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models) : séparation du relief géométrique et des normales de détail.
- [Valve, Water Flow in Portal 2, SIGGRAPH 2010](https://advances.realtimerendering.com/s2010/index.html) : direction/vitesse locales pilotant l'advection des détails ; piste adaptée aux rivières plutôt qu'un océan spectral.
- [Alex Tardif, Water Walkthrough](https://alextardif.com/Water.html) : exemple pratique, explicitement non entièrement physique, combinant réfraction, reflets et transition courte aux intersections. Son coût mesuré n'est pas une mesure Forge.
- [Filament, Materials](https://google.github.io/filament/Materials.md.html) : transmission et absorption selon la distance optique. Référence de principe, pas dépendance à intégrer.

Aucun code ou texture des articles repris. Une reprise substantielle ultérieure
nécessiterait sa propre vérification de licence et attribution.

## Constats reproductibles dans le candidat

Entrées inspectées :

- `crates/forge_engine/src/water.rs`, SHA256
  `088F2A2AE6438CE11909F51077E554270D30EFBF2459190B387587D463DB20F1` ;
- `crates/forge_engine/src/shaders/visual_valley_water.wgsl`, SHA256
  `ED777A24622822CB054FF469B5DEFAA9CB05BB4F8274322D3531D33E94318E9F`.

1. Le matériau est `Opaque`/`Deferred`, avec transmissions diffuse et spéculaire
   à zéro. La caméra reçoit `ScreenSpaceTransmission { steps: 0, ... }`.
2. Les deux branches fragment remplacent la couleur PBR par une palette et un
   alpha de 1. La couleur rouge des sommets encode une profondeur de lit sur
   trois bandes transversales ; ce n'est pas la distance optique par pixel.
3. `bevy_water_optics_capabilities` déclare explicitement la réfraction écran
   indisponible pour garder l'eau dans le G-buffer requis par SSR.
4. `water/quality.rs` rapporte honnêtement un profil partiel ; ce diagnostic
   ne permet pas de changer `full_preset_applied` en vrai.

## Limite du raccourci « activer la transmission »

Source primaire locale inspectée : crate épinglée `bevy_pbr-0.19.1`, sous
`C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/`.

- `src/pbr_material.rs`, documentation `specular_transmission` : effet écran,
  copies/draws supplémentaires selon le nombre de couches ; les matériaux
  alpha-blend rendus ensuite ne sont pas visibles à travers cette transmission.
- `src/material.rs`, sélection `render_phase_type` : les matériaux lisant la
  texture de transmission sont classés `Transmissive`, avant le cas opaque.
- `src/ssr/ssr.wgsl` : reconstruit l'entrée PBR depuis profondeur et G-buffer
  différé. Le shader contient déjà une contribution de sonde d'environnement ;
  ne pas présenter l'ajout d'un fallback de sonde comme entièrement absent.
- `src/transmission/transmission.wgsl` : direction réfractée propagée par une
  épaisseur fournie, puis échantillonnage du fond. La profondeur sert aussi à
  rejeter les objets devant l'eau ; elle ne calcule pas automatiquement
  l'épaisseur variable de la rivière à partir du terrain.

Conclusion : une simple modification de `specular_transmission` ne réalise ni
une eau de rivière à profondeur correcte ni la conservation des reflets SSR
actuels. C'est une contrainte d'intégration identifiée, pas la preuve qu'une
solution Bevy est impossible. Aucun défaut du SSR Bevy n'est établi ici.

## Prochain lot d'implémentation — non exécuté

Construire une voie d'eau cohérente dans l'adapter Bevy existant, sans second
moteur : accès à la couleur du fond et à sa profondeur avant l'eau, épaisseur
optique bornée, rejet des échantillons devant la surface, absorption et
transition courte aux berges. Choisir explicitement le chemin de réflexion
compatible avec cette composition ; ne pas annoncer SSR conservé sans preuve.
Réutiliser les fonctions/passes publiques Bevy avant toute extension bas niveau.

Le premier candidat doit couvrir fond proche visible, profondeur plus opaque,
deux berges et obstacle au premier plan. Il ne doit pas devenir simplement
une vitre bleue, ni un contour d'écume masquant la couture. Garder les vagues
existantes comme témoin jusqu'à ce que la composition soit inspectable.
Ensuite seulement : courant dérivé du tracé et détails moins périodiques.
Les paramètres et éventuelles retouches restent moteur-neutres, déterministes
et compatibles avec conservation des retouches ; aucune seconde carte canonique.

## Critères de validation du lot

- Tests de profondeur nulle/forte, valeurs invalides, premier plan, résolution
  et viewport décalé ; conversions de profondeur conformes au reversed-Z Bevy.
- Captures avant/après à caméra et phase identiques : proche, rive opposée,
  angle rasant ; courte séquence pour répétition/scintillement.
- Preuve native puis WebGPU/Studio ; profils et fallbacks observables sans
  annoncer des champs non appliqués. Ne pas assimiler compilation à pixels.
- Coût avant/après sur mêmes entrées, pas d'optimisation fondée sur un FPS isolé.
- Fermer et vérifier les processus propres aux essais ; préserver la session
  propriétaire. Pas de publication Git.

## Vérifications de cette note

Lecture du code Forge et du code Bevy local, hashes et branche vérifiés.
Pas de modification Rust/WGSL, pas de build ou processus graphique lancé.
La recherche précédente et ce diagnostic changent la prochaine action ; ils
ne fournissent aucune nouvelle preuve visuelle ou de performance.

## Suite — calcul optique WGSL préparé, non raccordé

`crates/forge_engine/src/shaders/water_optical_depth.wgsl` contient maintenant
les fonctions originales de reconstruction de distance sur le rayon de vue
(viewport décalé compris, profondeur inversée, fond absent borné) et d'absorption
exponentielle à partir d'une transmittance à distance de référence.
La distance calculée est celle du rayon non réfracté, pas une marche de rayon
ni une profondeur verticale. L'appelant doit encore choisir la texture avant
l'eau et rejeter les échantillons réfractés au premier plan.

Le module n'est **pas encore chargé ni appelé par le shader de rendu**. Aucun
changement de pixels, de profil ou de capacité déclaré. Cette séparation permet
de valider la brique commune avant de modifier l'ordre de composition/reflets.

Test réel du texte WGSL par Naga 29.0.4 : parsing et validation dans deux
consommateurs (compute et fragment), un test ciblé PASS. Ce n'est ni une exécution
GPU ni une preuve numérique. Naga est une dépendance de développement seulement,
version exacte déjà présente dans Cargo.lock via Bevy, licence MIT OR Apache-2.0.
Aucune nouvelle version de package verrouillée ; lien de test ajouté à forge_engine.

Preuves locales :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-optical-depth-20260908/`.
Shader SHA256 `B987BAE04872CBF6375437BCB7EF85BF41DA3B24E9C810666A4FB978B625F6C3`.
Tests SHA256 `2D3E7A620CBDCEEA9B1513093B05EE89C2CE7927A0289A3E3773C62D551516F9`.
Avant activation : lecture GPU numérique, composition et captures natives/Web,
mesure et qualification des fallbacks restent requises.

Validation terminée le 2026-09-08 à 19:42:27 UTC : `Verify.ps1` exit 0,
fmt PASS, workspace **638 PASS / 0 échec / 1 ignoré**, Clippy workspace strict
toutes cibles/features PASS. Logs `fmt.log`, `tests.log`, `clippy.log` dans
le dossier de preuve ci-dessus. Aucun nouveau build/capture du candidat de jeu :
la brique n'est pas encore intégrée et aucun succès GPU n'est revendiqué.

## Suite — exécution GPU numérique, 19:50:51 UTC

Sonde explicite `crates/forge_engine/examples/water_optical_depth_probe.rs`.
Elle utilise le même wgpu 29.0.4 déjà verrouillé, comme dépendance de développement
uniquement ; ni seconde pile de rendu du produit ni nouveau package/version.
Le WGSL original est inclus directement. Les données de cas sont envoyées dans
un buffer storage à l'exécution, puis les sorties sont copiées et mappées sur CPU.
Pas de substitution par un calcul CPU ou par des constantes de shader.

Neuf cas PASS sur **AMD Radeon RX 7800 XT / Vulkan / DiscreteGpu**, tolérance
absolue 0,0001 : perspective 2 m, viewport décalé, rayon oblique sqrt(5) m,
premier plan, contact nul, ciel borné, grande profondeur bornée, pixel hors
viewport et viewport vide. Les quatre composantes (transmittance RGB et distance)
sont comparées ; le cas oblique retourne 2,2360680103302 m, pas 2 m.

Cette preuve utilise des profondeurs synthétiques chargées depuis le CPU, **pas
une texture de profondeur de la scène**. Le cas premier plan prouve un chemin
optique nul ; il ne prouve pas encore le rejet d'un prélèvement de couleur
réfractée. Réflexions, composition, WebGPU navigateur et rendu restent ouverts.

Premier build de sonde refusé (`InstanceDescriptor::default` absent de wgpu 29),
corrigé par le constructeur headless public. Premier Clippy refusé, corrections
effectuées (type nommé, lecture par tableaux, défaut explicite), avec une
expectation de longueur locale justifiée par le cycle de possession GPU unique.
Clippy ciblé strict et fmt PASS. Rebuild final et replay de la sonde exit 0.

Fichiers dans le dossier de preuve précédent : `RunGpu.ps1`, `gpu-build.log`,
`gpu-results.json`, `gpu-stderr.log`. Aucun navigateur ouvert ; processus terminé,
buffer unmapped et device détruit explicitement avant sortie.

- Exécutable SHA256 : `0B9E495E8C1D3A4979DA3915484C91C8A93A5DF17480CCC1A3581E7B1C81B3FA`.
- JSON SHA256 : `198867A393864433778BF6921CCF95857347CA1A27FA12A0EA43FA8AFDDBDD5E`.

Les 638 tests workspace mentionnés plus haut précèdent l'ajout de cette sonde ;
ne pas les présenter comme un nouveau replay global. Le shader optique et le
renderer produit sont inchangés par ce dernier ajout. Prochain travail : brancher
la texture de fond et la composition, avec chemin de réflexion explicitement
résolu et captures avant/après. Gate 1 toujours ouverte.
