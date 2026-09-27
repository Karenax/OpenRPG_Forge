# Gate 1 — Lissage temporel des reflets, correction des variantes de shaders

2026-09-05, Rust local non publié. Gate 1 reste ouverte.

## Implémenté

Les caméras auxquelles Forge applique `ScreenSpaceReflections` reçoivent maintenant
`TemporalAntiAliasing`, déjà fourni par Bevy 0.19.1 et ses plugins existants. MSAA reste Off.
Aucune dépendance, texture, changement de source WorldSketch ou désactivation de reflets.
Les caméras sans SSR ne reçoivent pas ce composant par ce raccord.

L'inspection du code local épinglé Bevy explique le choix : `ssr/ssr.wgsl` échantillonne une
direction spéculaire aléatoire par pixel/frame ; l'exemple `examples/3d/ssr.rs` active TAA.
Le composant accumule les images successives, avec un compromis de netteté et de traînées
potentielles. La présence du composant ne suffit pas à prouver son exécution GPU.

## Échecs réellement rencontrés et corrigés

Le premier runtime a échoué (code 1) avec `invalid field accessor previous_world_position`
dans les shaders eau et végétation. Leur branche `MOTION_VECTOR_PREPASS` s'activait aussi
avec `forward_io::VertexOutput`, qui ne contient pas ce champ. Les écritures sont maintenant
gardées par **PREPASS_PIPELINE et MOTION_VECTOR_PREPASS**. Le calcul des positions précédentes
déformées reste disponible dans la vraie prépasse.

Le deuxième passage clair réussissait, mais la pluie échouait avec la même erreur. Le shader
pluie est forward uniquement, avec alpha blending : son bloc de sortie précédente invalide
est supprimé. Ce bloc ne fonctionnait pas auparavant ; la correction ne prétend pas ajouter
des vecteurs de mouvement à la pluie transparente. Son comportement temporel reste à qualifier.

Les deux dossiers d'échec sont conservés : `r1-water-taa-20260905` et
`r1-water-taa-20260905-fixed` sous le cache externe habituel. Aucun échec n'est remplacé par
un manifeste de succès.

## Vérification native finale

Collecteur `scripts/Capture-WorldSketch.py`, source révision 3, kit authoré inchangé, filtre
`--camera human_bridge`, sortie
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-water-taa-20260905-final`.
Deux lancements code 0, clair/pluie, deux PNG 1920 × 1080 ouverts et inspectés, sans ERROR.
Sources, pins, caméra, terrain, kit (20 instances/36 colliders), pont et météo vérifiés par le
collecteur. La compilation et les tests étaient terminés avant ces captures finales.

Le grain ponctuel de l'eau et du tablier est réduit par rapport aux images révision 3 de
`R1_065_HUMAN_CAMERA_HEIGHT.md`. Les détails fins sont plus doux ; l'eau n'est pas déclarée
artistiquement terminée. Les vagues animent la surface, donc deux PNG ne sont pas un test
d'identité de pixels ni une mesure de bruit temporel.

Preuve GPU séparée dans les métriques : `render/taa/fragment_shader_invocations=2073600`
et `render/taa/elapsed_gpu=0.13088 ms` (clair), `0.131 ms` (pluie). Ce sont les relevés
diagnostiques de la passe, pas une moyenne de surcoût A/B. SSR reste présent et mesuré.
Smokes RX 7800 XT/Vulkan : 109,74 / 110,35 FPS moyens et p95 9,8495 / 9,7548 ms.
Les captures précédentes avaient une compilation concurrente : aucune comparaison de FPS
avant/après ni qualification GPU milieu de gamme n'est revendiquée.

SHA-256 octets :

- runtime : `15726c2e57543de3a24a6bd9796f8533a48368563380ad89896fb2e6f0b486e5` ;
- manifeste final : `fdd68df31537af1bc72f95c6903b354924a92a872844992c2638b1b10cf649e0` ;
- PNG clair : `89aede0f58738775d511cf8957b79ead4b7aa5a9d9156b9a758857ce9860e3b9` ;
- PNG pluie : `d9d94af603b1198638083772e1fbe502d75dc8f188c52b6f55dd9f37ca468981`.

## Autres contrôles et suite

103 tests moteur PASS après toutes les corrections ; le test caméra vérifie le composant TAA.
Les tests de contrat shader vérifient les deux gardes, sans être présentés comme un compilateur
WGSL. Les deux lancements GPU finaux sont la preuve de compilation des variantes exercées.
Build runtime, Clippy moteur toutes cibles/features strict et Clippy Studio web/WASM strict PASS.
Formatage et `git diff --check` PASS. Avertissement amont `proc-macro-error2 2.0.1` conservé.
La suite workspace de 318 tests du lot précédent n'est pas présentée comme rejouée ici.

Restent à vérifier : traînées en déplacement, pluie transparente, changements de caméra et
Chrome/WebGPU réel avec ce shader. Les défauts de composition et de matériaux, douze vues,
parcours vidéo, comparaison Cadle et acceptation humaine restent ouverts. Aucun commit/push,
nouvelle gate ou asset Cadle.
