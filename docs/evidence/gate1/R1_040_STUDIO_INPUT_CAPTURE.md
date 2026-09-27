# R1-040 — État de capture des entrées Studio

2026-09-06, Rust, local non publié. Gate 1 ouverte ; CI NOT RUN.

## Implémenté

`apps/forge_studio/src/input_capture.rs` observe le focus effectif du moteur et l'événement
document `pointerlockerror`. La vue affiche un statut accessible : entrées libres, fenêtre non
focalisée, exploration active, ou capture refusée. Un nouveau clic réinitialise le refus ; seule
une capture réellement active peut afficher Exploration active. Le listener est retiré au
démontage du composant. Les messages identiques ne republient pas le signal à chaque image.

Aucun assouplissement de `PlayerInputFocus` : focus document ET pointer lock sur forge-canvas
restent requis. Aucun déplacement, autorisation navigateur, document projet ou renderer modifié.
Le refus n'est pas avalé ni transformé en succès. La possibilité de réessayer dans une fenêtre
active n'est pas une garantie de compatibilité de toutes les surfaces navigateur.

## Contrôles

Clippy Studio wasm32/web PASS ; avertissement amont de compatibilité Rust future conservé
(`proc-macro-error2 2.0.1`). Format et diff PASS. Build Trunk : code 0.

## Preuve navigateur

Build `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-input-capture-20260906/dist`, page
WorldSketch, navigateur intégré. État initial observé : Entrées libres. Après clic canvas,
événement réel WrongDocumentError et statut visible Capture refusée par le navigateur.
Capture inspectée : message lisible sous le viewport, scène toujours affichée. Le refus reste
présent dans les logs ; ce n'est pas un essai zéro erreur. Le monde est initialisé une seule fois.
Viewport 768×606, pas Full HD ni Tauri. Les états capture active et fenêtre non focalisée n'ont
pas été exercés dans cette session ; aucune validation du déplacement n'est déduite.
Onglet et serveur de preuve fermés. Il reste à rejouer sur une surface acceptant le pointer lock.
SHA-256 WASM : `f3a23cc88f0f0482eda9da0e2082e4e03315c9bd57d057b531a3b15717b5fd81`.

## Chrome au premier plan : capture active confirmée

Même build, nouvel onglet Chrome dédié, aucun changement du code ou des permissions.
Le premier clic reproduit WrongDocumentError. Après mise au premier plan de l'onglet
(`Page.bringToFront`, action de focus normale), le clic canvas réussit : Exploration active
observé. Cela réfute un blocage systématique de l'application ou une incompatibilité générale
de Chrome. Le succès n'a pas encore été rejoué dans le navigateur intégré.

Brume High activée avant capture. Touche V : première personne, capsule masquée et point de
vue changé dans la capture réelle. Nouvelle touche V : capsule et troisième personne restaurées.
Échap : Entrées libres confirmé. Ledger volumétrique actif à 64 étapes/128 m pendant l'essai.
Une seule erreur dans les logs, celle du premier clic ; aucune nouvelle erreur après focalisation.

Vingt impulsions W ont été envoyées, mais aucun déplacement continu ni distance n'est prouvé :
ne pas les compter comme validation de marche. Captures inspectées dans la conversation,
sans PNG exporté. Viewport 1408×741 après focalisation (1408×797 initialement), pas Full HD.
Les métriques de cadence incluent le séjour en arrière-plan et sont impropres au benchmark.
La brume et l'overlay restent expérimentaux ; aucun verdict artistique n'est déduit.

Onglet de test fermé après Échap et serveur arrêté ; autres onglets utilisateur laissés intacts.
