# R1 — détail de courant sans étirement transversal imposé

9 septembre 2026, Rust, aucune publication, Gate 1 ouverte.

## Isolation actuelle

EXE de `R1_WEATHER_SHADOW_CONTRAST.md`, SHA256
`FCDBD29107C2AFA320877D63C71436240E2EA2CC3BF474143D930F0CF550B0D9`.
Comparaison `water_close`, clair, frame 300 : témoin `clear.png` de ce lot
contre `r1-water-current-normal-isolation-20260909/clear.png`, commande
`Capture.ps1 -Clear` avec `--water-optics-isolation normals-off`.
PNG isolé SHA256 :
`37786AA9B5BF7A00138675F05CA03882B101063552770DF42096D433E9B191C5`.
Seule la force des normales est neutralisée dans le document chargé ; aucun
fichier authoré changé. Relief conservé, stries largement atténuées : la
contribution normale reste déterminante après les corrections lumière/contraste.
Ce test ne sépare pas à lui seul chaque octave de normales.

## Modification candidate

`water_flow_detail.wgsl` supprimait 65 % de la fréquence transversale avant
échantillonnage, allongeant systématiquement les rides en travers du courant.
Le détail conserve désormais la même unité métrique dans les deux directions.
Advection locale, phase/crossfade, amplitude, filtres, palettes, relief,
absorption, ombres, berges et carte restent inchangés. Pas de nouveau paramètre,
texture, dépendance, asset ou passe GPU. Modification spécifique au candidat
optique, pas au chemin opaque par défaut.

SHA256 helper : `566F89342F16ACE379E5D47E408BE87AE2D9F3CF3302CF8731B1F7AD039827EF`.

## Preuve native

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-unstretched-detail-20260909/`.
Build natif PASS ; `Capture.ps1 -Clear`, même cadrage et profil que le témoin.

- EXE : `43F359F21CAFF596E44FE086467F05D51196C3935AEDF04590824070874E8574`.
- PNG : `C23C18631EE50B1F9EE960094A3B85A89A8F5A45FADF3E2B41D0E4F13CF91A7D`.

Sortie zéro, `capture_verified`, aucun ERROR. Inspection : détails moins
alignés sous le pont, ondulations toujours présentes. Gain local limité,
conservé sans revendiquer une eau réaliste. Fond brun et reflets peu variés
restent insuffisants ; ne pas transformer ce résultat en acceptation globale.
Cadence glissante : moyenne 9,98 ms, p95 16,01 ms, 1 % low 53,3 FPS.
Ce relevé court n'est pas un benchmark.

PID isolation 9512 et candidat 44768 fermés puis absence vérifiée. Aucun
processus utilisateur arrêté. Aucun bundle Studio reconstruit dans ce lot.
Tests workspace PASS : 649 réussis, zéro échec, un ignoré préexistant,
y compris validation du helper WGSL réel pour consommateurs compute/fragment.
Diff de format vérifié ; aucune modification Rust dans ce lot.
Mouvement et pixels WebGPU de cette modification non qualifiés.
