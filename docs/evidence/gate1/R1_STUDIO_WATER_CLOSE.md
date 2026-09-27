# R1-022/023 — observation rapprochée du candidat optique

9 septembre 2026, branche Rust. Gate 1 ouverte ; aucun passage artistique,
aucune publication. Rejeu du bundle existant, sans nouvelle compilation.

## Entrées et garde

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-water-close-20260908/corrected/`.
EXE SHA-256 `0E0D11C0FBB4D46168943C2F9E9A60BBB74A46769DB673C8C4DB5F5CA1B6174F`.
WASM `5EBE88604A98516E8AC86166F59B9DAE0A3125ECA5FDB6AB988120AD620A507B`.
Vue candidate `water_close`, position [-48,9,-145], cible [-55,2,-163].
Vue technique élevée, pas qualification à hauteur humaine. Carte canonique inchangée.

Le premier fichier de preuve avait été resérialisé par JavaScript : graine u64
6216453223746720837 arrondie à 6216453223746721000. Refus correct du garde
« différence hors caméras ». Le fichier corrigé préserve les tokens originaux ;
son hash accepté par le Studio est
`74E6EAAB1488C079B5FEFF228CFBE9ABBB24F695453D8F632B40E0919871507D`.
Le garde n'a pas été assoupli. Le premier essai reste conservé hors Git.

## Exports réellement acquis

Copies dans la racine locale ci-dessus ; originaux Downloads préservés.

| Temps virtuel observé | Fichier | SHA-256 |
| --- | --- | --- |
| 0 s | forge-diagnostic-1788905334164.json | 990B228E8BA804E40979B3F2274B145C4B3BF1EAC39E01320618F20937718029 |
| 0 s | forge-viewport-1788905345432.png | CC26E822682EC5B289961D9B2F3AD71E2E1A6E6E3DFD10F542FF904E23706618 |
| 0,250 s | forge-diagnostic-1788905377454.json | 38BA7245DAF530F92068EDA1476638C0D253356F2DD277160D82492393E92FD9 |
| 0,250 s | forge-viewport-1788905387777.png | 087EADA1B7537C50ADA3BEFCA9065B4B14EA90B951343683CFFA898DC402CEB6 |

Bouton existant « Avancer l'animation de 250 ms » exercé une fois ; horloge
passée de 0/32 à 1/32. Phase CPU exportée : 2,115455380293542 puis
2,32108690760866 radians. `observations.water_optics` réellement présent,
`optical_depth_preview=true`, observation main-world complète, SSR=0.
Cela clôt la limite « nouveaux champs non exercés dans un bundle » du rapport
`R1_STUDIO_WATER_OPTICS_DIAGNOSTICS.md`, pas la qualification optique.

## Verdict visuel et prochaine correction

Viewport 1408 × 966 inspecté. Fond vert très visible au premier plan ; transitions
géométriques nettes entre bandes vertes/brunes, surface encore striée au loin.
Le rendu reste insuffisant. L'origine exacte des bandes doit être isolée dans
le terrain et les bandes de berge avant de modifier le shader : ce constat
visuel ne prouve pas à lui seul une erreur de matériau ou de profondeur.
Priorité à une règle générique de lit/berge cohérente avec l'eau transmissive,
pas à une retouche locale de carte ni à de nouveaux réglages de rides.

Deux instants et une phase CPU différente ne prouvent pas la fluidité GPU.
Exports non atomiques, pas de benchmark, profils du candidat désactivés,
reflets écran absents. Acceptation humaine toujours requise.

PID d'essai 6116 terminé après vérification du chemin exact ; attente de sortie
et absence d'enfants directs vérifiées. Ancienne session utilisateur 39624 préservée.
