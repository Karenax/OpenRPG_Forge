# R1-040 — Préparation bornée de la volumétrie

État historique de préparation. Le raccordement CLI/soleil et le premier rejet visuel sont
décrits dans `R1_040_VOLUMETRIC_NATIVE.md`, qui fait autorité pour la suite de ce lot.

2026-09-06, branche Rust, modifications locales non publiées. Gate 1 ouverte.

## Implémenté

`crates/forge_engine/src/environment/volumetric.rs` prépare les composants publics Bevy 0.19.1
`VolumetricFog`, `FogVolume` et leur transformation à partir des profils validés.
Low ne produit aucun volume ; Medium/High/Ultra projettent 32/64/96 étapes.
Les paramètres physiques et ambiants proviennent du document, sans réécriture de celui-ci.
Le jitter reste explicitement à zéro avant qualification conjointe avec le rendu temporel.

Bevy n'expose pas de distance radiale maximale sur ce composant. La préparation intersecte donc
la boîte authorée avec un cube inscrit dans la sphère du budget caméra. Une marge flottante
conservatrice conserve les sommets à l'intérieur du budget. Ce n'est pas une coupure sphérique
du ray-march : des coins sont tronqués et une frontière mobile peut devenir visible.

## Vérifié

- Deux tests ciblés PASS : confinement des huit sommets dans les deux bornes, trois distances,
  trois positions caméra ; rejet hors volume, distance NaN et taille nulle.
- Projection des budgets et paramètres physiques, volume positif, hash source inchangé.
- Clippy workspace, toutes cibles et features, warnings comme erreurs : PASS.
- `cargo test --workspace --locked` : PASS, code 0.
- Check moteur WASM, sans features par défaut et avec `web` : PASS.
- Format Rust et `git diff --check` : PASS.

## Raccordement ECS ultérieur, expérimental

Le plugin environnement installe maintenant un système après propagation des transformations,
avant son observation. `VolumetricPreviewRequest` est public et désactivé par défaut. Activé avec
une politique environnement, il projette une brume ambiante uniquement, sans marqueur de lumière
volumétrique et sans supprimer le brouillard de distance. Aucun bouton Studio ou argument CLI
n'active encore cette prévisualisation.

La caméra conserve un marqueur privé ; le volume dérivé conserve son entité pendant le suivi.
Transform et GlobalTransform sont mis à jour ensemble. Low, désactivation, caméra hors volume
ou topologie invalide retirent les composants possédés. Un composant de brouillard étranger sur
la caméra bloque la projection ; les volumes étrangers restent intacts et restent signalés par
l'observation de topologie. Les métriques distinguent cette projection CPU expérimentale,
sans acquittement de preset complet ou adaptatif.

Douze tests environnement PASS après raccordement, dont suivi caméra, Low et composants étrangers.
Clippy workspace PASS. Le test ECS inclut également le retour High puis la désactivation explicite.
Check moteur WASM (`--no-default-features --features web`) PASS après raccordement.
SHA-256 du module `volumetric/projection.rs` :
`762ba12f904832ecfccc51d956210d572da26258daf2143497e5884ed20513a7`.

## Prévu, pas encore qualifié

Restent l'activation dans un scénario de preuve, le lancement/capture GPU, la coexistence
atmosphère/TAA, les rayons lumineux et la qualification native/WebGPU. Le cumul avec le brouillard
de distance peut surcharger l'image ; aucune qualité visuelle ni performance n'est annoncée.
CI distante NOT RUN ; ce raccordement ne ferme aucun critère visuel.
