# R1-022/023 — contrat d'observation du candidat optique

2026-09-08, Rust ; suite du premier échec Studio `R1_STUDIO_WATER_OPTICS.md`.

## Cause et correction

L'observateur exigeait zéro couche de transmission dans tous les modes. Il
utilisait aussi les overrides de qualité pour comparer un candidat qui les
ignore explicitement. Le refus Studio était donc réel et reproductible via son
menu, pas une preuve de panne du shader.

Le mode par défaut conserve ses exigences. Le candidat requiert exactement une
couche de transmission pour chaque caméra, DepthPrepass, TAA et zéro caméra SSR,
en plus des exigences communes (maillages, matériau, uniforms, DeferredPrepass,
MSAA Off). Son matériau doit être forward avec transmission spéculaire 1.
L'observation des profils non appliqués reste absente (`quality: null`) ; les
contrôles Studio correspondants ne doivent plus annoncer un profil appliqué.
Le contrôle d'erreur du Studio n'a pas été supprimé ni assoupli.

Le test initialise le vrai plugin avec un profil High, accepte la configuration
correcte, vérifie que l'override de teinte reste ignoré, puis retire une couche,
la profondeur, TAA, ajoute SSR et retire la transmission du matériau : chaque
configuration incorrecte doit rester refusée.

## Preuves

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-optics-observation-20260908/` :
premiers tests workspace PASS ; Clippy échoue sur longueur de fonction et
comparaison flottante. Fonction de contrôle caméra extraite, comparaison exacte
de la valeur configurée via ses bits ; aucun lint désactivé.
Rejeu conservé dans `replay/` : **643 tests PASS, zéro échec, un ignoré**,
fmt, Clippy workspace strict, check WASM et build natif PASS.
Binaire SHA256 `9407B12CF789D93354DC6F3F52E31B3543105EE03C0A7B6332AB3FB481D37486`.

Lancement natif du binaire exact, capture `hero_reference_match.png` inspectée,
SHA256 `7ACCE4CABFCBB21AA226B6DBE915B939275DFBEE9D0697EA296359C41C9C360B`.
L'overlay indique maintenant `ObservedMainWorldComponents`, sans retour de la
rupture sombre. Capture vérifiée, aucun ERROR, exit 0 ; PID 22716 terminé.
Overlay non benchmark : moyenne 22,63 ms, p95 29,11 ms, 1 % low 23,0 FPS.
Aucun gain ou qualification de performance déduit.

Pas de nouveau bundle Studio corrigé, de qualification de pixels ou de mouvement.
Gate 1 ouverte ; CI distante NOT RUN ; aucune publication.
