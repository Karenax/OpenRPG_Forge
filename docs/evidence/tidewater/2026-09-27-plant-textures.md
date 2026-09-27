# Textures vegetales partagees et preparation Studio

Date : 2026-09-27. Portee : preparation des ressources et non-regression native.

Le module moteur `tidewater_plants::textures` prepare les cinq images du paquet
vegetal verifie : detail, feuilles, deux atlas imposteurs et masque d'herbe.
Les octets des mipmaps sont conserves en RGBA8 lineaire, avec les reglages source
de repetition, filtrage et anisotropie. Les dimensions, niveaux et tailles sont
valides avant insertion dans les assets Bevy. Chaque remplacement produit de
nouveaux handles et conserve les images encore referencees par l'ancienne scene.

Studio prepare ces images avant publication de la nouvelle bibliotheque, puis
installe le shader et les ressources dans son monde Bevy. Le materiau partage
est enregistre. Ceci ne constitue ni un acquittement GPU ni une projection de
la population dans le viewport : aucun arbre selectionnable n'est qualifie ici.
Le probe natif utilise les memes fabriques pour detail, feuilles et imposteurs.

## Verifications

- Clippy moteur et deux probes, strict `--no-deps -D warnings` : PASS.
- Build natif : PASS, 7 min 41 s.
- Probe du paquet reel : PASS. Les cinq images ont les octets, dimensions,
  formats et nombres de mipmaps attendus ; repetition/anisotropie des feuilles
  et filtrage/adressage des atlas verifies. Deux generations isolees conservees.
  Dimensions invalides, nombre de mipmaps invalide et buffers tronques refuses.
- Execution native avec le workspace canonique et les batiments : PASS.
  Capture inspectee. Les trois SHA-256 correspondent a native-shared-plants-001 :
  - PNG : 243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
  - Ombres relief : a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
  - Profondeurs objets : 0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.
- `cargo check` Studio WASM : PASS, 39,81 s. Avertissement de compatibilite future
  de proc-macro-error2 2.0.1 conserve.
- Clippy Studio strict : echec uniquement sur le diagnostic preexistant
  `atelier_ui.rs:122`, bind_main 103 lignes pour une limite de 100.

Preuves locales sous E:/OpenRPG_Forge/.tidewater-integration :
plant-textures-proof-001.json, plant-textures-run-001.log,
plant-textures-clippy-003.log, plant-textures-build-001.log,
studio-plant-textures-check-001.log, studio-plant-textures-clippy-001.log,
native-plant-textures-001.log et ses trois sorties.

## Limites et suite

Le probe de textures lit le nouveau paquet portable ; la capture native lit
encore le fixture complet et le workspace canonique. Elle prouve une
non-regression Forge a camera/heure fixes, pas la parite complete avec upstream.
Le transport des blocs de population canonique vers Studio, leur projection,
les identites de picking, les retouches et leur sauvegarde/reouverture restent
a raccorder et qualifier. Les ombres vegetales dynamiques et l'herbe partagee
restent ouvertes. Aucun parcours GUI, nouveau frontend distribue ou benchmark
interactif ne sont revendiques. Aucun commit ni push. Objectif global ouvert.
