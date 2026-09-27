# R1-022/023 — essai de macro-relief écarté

9 septembre 2026, Rust. Suite du lit minéral confirmé dans le Studio.

## Hypothèse et essai

Le candidat optique conserve deux sinusoïdes globales de faible amplitude.
Hypothèse : elles dominent les stries visibles. Essai original WGSL : les
remplacer uniquement dans le candidat par un bruit de valeur C1 advecté à
deux phases, amplitude toujours bornée à 15 % du paramètre, gradient analytique
issu du même champ que la hauteur. Petits détails, optique, ombres et lit inchangés.

36 tests eau PASS, validation Naga compute/fragment incluse, Clippy forge_engine
strict et build natif PASS. Pas de preuve numérique GPU des dérivées ou de
fluidité ; les tests ne prouvent pas l'amélioration artistique.

## Paire réellement exécutée

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-surface-20260909/`.
`Capture.ps1 -Variant before|after`, vue candidate water_close, temps figé,
frame 300, arrêt frame 420. Même terrain, pont, eau et végétation procédurale
de remplacement, **sans kit gouverné** : test isolé, pas la scène Studio complète.

- Témoin EXE `D8A2FD358D3060589352B7331CC1C58CBAF3A45CABF7606E95C139D6A09AC908` ;
  `before.png` : `57E555ED3D8309E2B3792D3749CCF6D92CA81367FE8E5D58BF87FCCD7DA99A7F`.
- Essai EXE `8865B1334092868802351EC74E687AB5080D6AC9F079D71A116926DEC654E16D` ;
  `after.png` : `E96764A932C17B985CD675B763FBA105BC2960E7F3583A823E0294A4B5935005`.
- PIDs 29548 et 45776 terminés, sorties 0, captures vérifiées, aucun ERROR.

Inspection : différence insuffisante, motifs gênants encore présents. Ne pas
conclure que les deux sinusoïdes étaient à elles seules la cause dominante.
Pas de gain artistique ou performance revendiqué ; overlays hors benchmark.
La rupture sous le pont reste compatible avec une ombre reçue, son caractère
défectueux n'est pas démontré. Ne pas supprimer les ombres pour la masquer.

## Décision exécutée

Essai retiré du code par patch inverse limité aux modifications de ce lot.
Le revêtement minéral confirmé est conservé. Hashes des shaders restaurés :

- principal : `C13048B9CEEE749B9BF353FFEA70957A9DC4FCB5CABD834C36550D1AFE59DE1B` ;
- détail : `A2269BF49A940773FBF75ED61D2ED1BB5E5F40F015317BE6D00AB4569281D6F2`.

Pas de rebuild Studio pour cet essai rejeté. Le bundle de référence reste
`r1-water-bed-20260909/candidate/`, avec sa preuve rapprochée et ses 645 tests.
Le cache natif debug peut encore contenir l'exécutable expérimental : ne pas
l'utiliser comme référence du code restauré sans reconstruire.

Prochaine investigation : isoler la contribution des normales de détail et
de la réfraction/composition sur une même vue avant toute nouvelle modification.
Pas de nouvelle série de variations du macro-relief. Gate 1 ouverte, aucun push.
