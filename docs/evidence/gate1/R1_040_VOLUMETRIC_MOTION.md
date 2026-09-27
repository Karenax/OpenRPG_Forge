# R1-040 — Coexistence avec le déplacement et suivi CPU

2026-09-06, Rust, local non publié. Gate 1 ouverte, CI NOT RUN.

## Exécution native

Commande : `forge_runtime --world-sketch-visual-valley --quality high --volumetric-preview
--player-camera-probe`. Ce scénario existant est auto-borné et exclut les captures/metrics CLI.
Pas de kit externe. Journal :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-volumetric-motion-20260906.log`.

Code 0, événement `player_camera_probe_completed`, success=true, 232 ticks fixes.
Marche : 150/150 ticks au sol, passage de la couture centrale, z=8.0001 à -3.7422 m.
Saut : 38 ticks en l'air, apex +1.2667 m, retour au sol confirmé.
Caméra : obstruction puis récupération monotone sur 20 échantillons, transition troisième →
première → troisième personne validée. 64 bindings terrain et bindings joueur conservés.
Le scénario ne mesure pas directement les sommets du volume ni les artefacts visuels de brume.
Il démontre la coexistence du chemin expérimental avec ces opérations, pas sa qualification artistique.

- SHA-256 binaire : `a91d75cbc00fe336b732a902f95d176d121acd9836aee697b089b356a3bb7e78`.
- SHA-256 journal : `d671400f5a24bb575bd5e39b5c6c44268a5b58eb40dab765844dc84d122e6732`.

## Contrôle CPU ciblé ajouté

`propagated_camera_and_volume_stay_in_the_same_frame` utilise le vrai TransformPlugin et
PostUpdate après propagation, pas seulement un appel direct du préparateur. Déplacements
alternés de 400 m, sortie à (4000,4000,4000), puis retour dans le volume authoré.
Contrôles : caméra globale actualisée, Transform/GlobalTransform du volume concordants,
huit sommets dans la borne 128 m de la caméra courante, suppression hors zone et restauration.
Ces sauts artificiels sont un test de retard de schedule, pas un comportement de déplacement produit.
Test ciblé PASS, code 0 ; Clippy moteur toutes cibles/features PASS, format et diff PASS.

Pas de changement de production dans ce lot. La qualité visuelle en mouvement reste ouverte :
aucune vidéo ou capture temporelle n'est produite par ce scénario. Les preuves statiques et le
cycle qualité précédents restent séparés ; aucun passage de gate déduit.

## Tentative interactive Studio : non validée

Même build WebGPU `10f2df12…` que la preuve Studio, réutilisé sans recompilation. Activation
de la brume puis clic canvas et touche V dans le navigateur intégré : aucune transition de
caméra observable. Une erreur navigateur est ensuite relevée :
`WrongDocumentError: The root document of this element is not valid for pointer lock.`

Le code actuel `update_viewport_input_focus` exige document.has_focus() et pointer_lock_element
égal à forge-canvas. L'erreur empêche donc de considérer cette session comme une preuve de
déplacement avec entrées actives. Elle n'invalide pas les captures statiques ni le cycle qualité
sans pointer lock, mais **le mouvement Web interactif reste non vérifié**. Aucun contournement
de la sécurité du navigateur n'est tenté. La tentative d'entrée clavier maintenue via CDP n'est
pas supportée par l'outil et ne prouve aucun déplacement non plus.

Suite : exposer l'état de capture des entrées et l'échec de verrouillage dans le Studio, puis
rejouer dans une surface qui accepte le verrouillage du pointeur. Ne pas attribuer l'absence
de mouvement à la volumétrie sans cette distinction. Onglet et serveur local de preuve fermés.
