# Adaptation Web et fenêtre masquée — 7 septembre 2026

Gate 1 reste ouverte. Pas de changement des seuils, de timings injectés ou de
preuve de charge graphique déduite de ce lot.

## Essai Web avant correctif

Bundle du lot précédent, WASM
`37BADC41BFD32CBED2D018406A7EA1C79715585659EA6CBDC964A0B61121B3AA`,
Chrome, URL `/?forge_scene=world-sketch-visual-valley`, sans vue fixe ni kit/forêt
expérimentaux. Les anciens blocages optiques ne bloquent plus ce parcours.

- Low choisi manuellement, adaptation activée : pause de focus ; clic dans le
  viewport puis tracking. Diagnostic : 15 échantillons, 0 proposition, Low
  acquitté (séquence 2, génération 7), 7/7 domaines et `gpu_ready=true`.
- Ultra choisi manuellement puis adaptation relancée et viewport focalisé :
  tracking, 173 échantillons admissibles, 0 proposition, Ultra acquitté (séquence
  3, génération 11). Ces échantillons ne couvrent pas encore les 240 frames de
  cooldown plus 90 frames de pression nécessaires à une baisse.
- Retour au mode fixe exercé et confirmé avant compilation du correctif.

Il n'y a donc **aucun changement automatique de niveau qualifié** par cet essai.
Les clics manuels ne sont pas comptés comme propositions du contrôleur.
Les deux diagnostics sont archivés sous `adaptive-web-before/` :
`forge-diagnostic-1788784331583.json`, `forge-diagnostic-1788784594612.json`.
Pas de conclusion de performance sur un navigateur potentiellement ralenti en
arrière-plan. L'état d'occlusion n'était pas exporté : il ne peut pas être
reconstruit rétroactivement à partir du seul rythme des échantillons.

## Défaut et correctif

L'adaptation lisait le focus et les dimensions de la fenêtre, mais ignorait
`WindowOccluded`. Bevy 0.19.1 expose cet événement ; le backend Web de Winit
relaie notamment `visibilitychange` et l'intersection du canvas. Une fenêtre
signalée masquée ne doit pas alimenter la politique avec des frames ralenties.

Le suivi de visibilité est désormais indépendant de la réinitialisation du
contrôleur adaptatif. Il conserve l'état après rotation du buffer de messages,
retire les fenêtres détruites et exige une fenêtre primaire unique, visible,
non masquée, focalisée et de dimensions non nulles. L'état observé est exporté
dans `sampling_window` et dans les événements de diagnostic.

Le message Studio en pause invite à réafficher le Studio et cliquer dans le
viewport. Cela n'envoie pas un faux focus et ne force pas la visibilité.
La détection reste limitée aux informations fournies par le backend : absence
d'événement d'occlusion ne prouve pas à elle seule que la fenêtre est au premier plan.

## Validation

Huit tests adaptatifs ciblés PASS avant ajout du champ diagnostique : une fenêtre
masquée et pourtant focalisée reçoit 1000 observations de durée synthétique,
sans aucun échantillon admissible ni proposition ; réapparition, retrait du
flag visible et destruction de la fenêtre également couverts. Test du texte
Studio renforcé. Les scénarios existants d'acquittement, timeout, perte GPU et
priorité manuelle restent présents.

Validation finale : 579 tests workspace PASS / 1 timing ignoré, Clippy strict
workspace all-targets/all-features et Studio Web PASS, build Web PASS.
WASM SHA-256 `35011F075A6B95CFC6D40E31D3B57897F5F209C1B7454300A3C4713F57C2EFF8`.
Rejeu Chrome du nouveau bundle : le message de pause est affiché, puis clic dans
le viewport → `tracking/eligible`, clic dans les contrôles →
`waiting_focus/unfocused`, retour viewport → `tracking/eligible`. Diagnostics
150/151/167 échantillons admissibles, même High acquitté (séquence 1, génération
3), mêmes sources, 0 proposition. Retour manuel au mode fixe exercé ensuite.

L'onglet témoin créé par l'automatisation n'a pas produit d'état `occluded` :
l'échantillonnage est resté `eligible`. Il a été refermé et **ne compte pas comme
preuve de masquage réel**. Ce cas reste couvert par le test moteur, pas par une
interaction de navigateur ou un événement natif réel observé.

Deux erreurs navigateur `WrongDocumentError: The root document of this element
is not valid for pointer lock.` sont présentes (12:48:56 et 12:51:51 UTC).
La capture du pointeur a été refusée pendant les clics automatisés ; pas de
revendication d'exploration ou de console sans erreur. Le focus observé par
l'adaptation n'est pas une preuve de capture du pointeur ou de premier plan OS.

`Collect-Adaptive-Visibility.ps1` PASS pour les identités et la pause/reprise
de focus seulement. Diagnostics sous `adaptive-visibility-cycle/` :
1788785482080 / 1788785500303 / 1788785523008 (préfixe `forge-diagnostic-`).
Il reste à exercer le masquage réel, une baisse sous charge admissible et
l'interaction humaine. Aucune répétition du run lent précédent ni diminution
des seuils pour obtenir artificiellement une proposition.
Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907`,
script `Validate-Adaptive-Visibility.ps1`, log ciblé `adaptive-visibility-tests.log`.
