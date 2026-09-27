# R1 — transition de contact eau/berge, essai candidat

9 septembre 2026, Rust, Gate 1 ouverte, aucune publication.

## Diagnostic et implémentation

Le maillage `AuthoredSurfaceV1` est déjà étendu sous les berges ; sa silhouette
visible résulte du test de profondeur contre le terrain canonique. Ce terrain
est échantillonné tous les deux mètres. Ajouter des sommets à la seule surface
d'eau ne lisse donc pas cette intersection. Les cartes, hauteurs, collisions et
meshes ne sont pas modifiés dans cet essai.

Le candidat optique reçoit une transition de contact `smoothstep(0,0.2,path)`.
Il s'agit de 0,2 m de **trajet de vue**, pas d'une largeur horizontale de berge,
ni d'une profondeur verticale : l'épaisseur apparente dépend de l'angle de vue.
Le résultat d'eau rejoint l'image opaque non déformée au contact. Cette image
provient de la texture de transmission Bevy existante (pleine cible), lue au
pixel absolu borné, y compris pour un viewport décalé. Aucun nouveau buffer,
maillage ou render pass ; au plus une lecture par fragment de contact.

Le mélange intervient après le post-éclairage de l'eau : ne pas réappliquer
exposition/brouillard à l'image opaque déjà traitée. Fond alpha nul : conserver
le rendu d'eau. Les trajets supérieurs à 0,2 m sont inchangés. Le mode opaque
normal n'utilise pas cette branche. Cette approximation de couverture n'est
pas une simulation physique de la rive ni une correction de la grille terrain.

## Vérifié

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-contact-20260909/Validate.ps1`
exécute tests workspace, Clippy, probe GPU étendu à 11 cas, build natif et check
WASM. **655 tests PASS / un ignoré**, Clippy strict moteur/projet/runtime
tous targets/features PASS, fmt et diff check PASS. Build natif et check WASM
Studio web/lib PASS ; avertissement futur `proc-macro-error2 2.0.1` conservé.
Pas de rebuild ni de lancement Studio dans ce lot.

**11 cas GPU PASS**, RX 7800 XT/Vulkan : contact nul, mi-transition, fin de
transition, intervalle désactivé, profondeurs et limites existantes. Tolérance
absolue 0,0001. Ce probe valide les fonctions réelles WGSL, pas la texture de
transmission ou son adressage en viewport décalé : ce dernier est fondé sur
l'inspection de Bevy 0.19.1, pas sur une capture offset dédiée.

Avant réutilisé sans relancer : `r1-water-medium-integration-20260909/clear-medium.png`,
SHA-256 `A82792F92306A4D9F18722532BEFEBBC4F4B312290E7986EC018154853DAC319`.
Après exécuté : même caméra `water_close`, profil clair, scène, résolution
1920×1080 et temps fixes ; capture frame 300, sortie frame 420.
Ce n'est ni un benchmark, ni une validation humaine, ni une preuve WebGPU réelle.

Capture inspectée : rive moins tranchée sous/près du pont et à droite, fond
toujours visible, sans halo manifeste sur cette vue. Amélioration limitée
conservée. La géométrie demeure anguleuse : ne pas annoncer un lissage du relief
ou la résolution générale des rives. Un gain visuel dans une vue fixe ne prouve
pas la stabilité temporelle en déplacement ni la qualification de l'eau.
Cadence glissante lue dans l'image : moyenne 10,07 ms, p95 16,53 ms, 1% low
50,8 FPS ; ne pas en déduire une optimisation comparative.

`capture_verified` présent une fois, aucun événement ERROR, sortie zéro.
PID 15168 terminé et confirmé absent ; le probe GPU n'est plus présent non plus.
Les avertissements du chargeur Vulkan restent conservés dans le journal.

SHA-256 :

- EXE : `45E35C7CCE5D68A807B54294474CA3CA9A94A1B2C947F1BEA352388D4A5A9A9F`
- contact.png : `356A8EF3D3A0E4F757B4B5F2C03A75640421B1F2296CEC8C54AB3F8F3B73749D`
- shader principal : `7357861664D01B1701995730CFCAB4CEAB1F5575F44402F8D6853AA025C228FC`
- helper optique : `5383B20C4B081E3C2D39CB985F07731CECF77F864A5DEE46BD9709EB2138E147`

## Suite de Gate 1

Rassembler milieu optique, détail sans étirement et contact dans un candidat
Studio accessible. Ne pas répéter une série de réglages/captures de cette vue
fixe. Qualifier ensuite le résultat intégré, les limites optiques et le mouvement,
en conservant les validations humaines requises. Aucun passage R1-022/023.
