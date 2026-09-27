# R1-040 — Cycle volumétrique natif

2026-09-06, Rust, local non publié. Gate 1 ouverte ; CI NOT RUN.

Le harnais global exige maintenant, lorsque la prévisualisation volumétrique est activée,
zéro volume en Low et exactement un volume actif avec étapes et distance bornées pour les
autres niveaux. Le délai existant de 120 secondes reste bloquant. Ce contrôle CPU s'ajoute aux
captures réelles, sans convertir un domaine partiel en preset complet.

Exécution : mêmes scène WorldSketch, kit et caméra human_bridge que le smoke `ground-bounded`
de `R1_040_VOLUMETRIC_NATIVE.md`, avec `--quality high --volumetric-preview
--quality-cycle-proof E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-volumetric-cycle-20260906`.
Météo claire. Un seul processus, cinq transitions, code 0 et zéro ERROR. Chaque étape attend
180 observations concordantes puis un nouveau readback et fichier PNG. Les hashes des sources
terrain/végétation/environnement restent identiques selon le contrôle bloquant du harnais.

| Étape | Niveau | Volume actif | Étapes du volume | Distance effective |
|---|---|---|---|---|
| 0 | High | oui | 64 | 128 m |
| 1 | Low | non | aucune | aucune |
| 2 | Medium | oui | 32 | 96 m |
| 3 | Ultra | oui | 96 | 192 m |
| 4 | High | oui | 64 | 128 m |

Le journal Low conserve `volumetric_steps:64`, valeur authorée de l'observation historique,
mais `volumetric_applied:false`, zéro volume et maximum None : aucun ray-march de 64 étapes
n'est revendiqué en Low. Les autres valeurs sont les budgets projetés, pas un comptage GPU.

Cinq captures 1920×1080 inspectées : suppression de la brume en Low, réapparition aux autres
niveaux, retour High cohérent, pas de frontière dure visible sur cette vue. Végétation et eau
changent aussi avec la qualité globale ; ces images ne sont pas un A/B isolant la volumétrie.
Eau/environnement restent partiels, `full_preset_applied:false` aux cinq étapes.
Pas de validation artistique, benchmark, caméra mobile, WebGPU ou Tauri déduite.

SHA-256 :

- Binaire : `a91d75cbc00fe336b732a902f95d176d121acd9836aee697b089b356a3bb7e78`.
- Journal adjacent `.log` : `7bd26b861e74ae7a2588abb1dffc9aa51d3382eba5fe40067dd078f073752c15`.
- stage-0 : `4a61c249f082ef65127529bc304b8c1ac8c04b15a435994d5c4e1b838abe39e5`.
- stage-1 : `f4d6a8ce377fcd679a8f4eb4b3e02b5564ea3665506788c4ca419bf34cedcac2`.
- stage-2 : `376184f20087031ea7a77225d46455de25cafa73e5db2317717be8043f375ace`.
- stage-3 : `974f2081b9ef7332567d98c7a6e0c1a5e5516aa2aab146067ac1577e414dd5d7`.
- stage-4 : `e199b0958864a8b80a19b7351beb08e6ea610aec525ccce712842621f05ac1af`.

Clippy Runtime toutes cibles PASS. Aucun changement au renderer dans ce lot ; le harnais et
ses preuves sont renforcés. Effet toujours opt-in, qualification WebGPU restante.
Tests Runtime : 65 unitaires et une intégration PASS. Assertions sur l'ordre des cinq niveaux,
le retrait Low, le retour High, les cinq captures vérifiées et l'absence d'ERROR : PASS.
