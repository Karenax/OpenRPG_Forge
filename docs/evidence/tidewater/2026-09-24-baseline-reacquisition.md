# Tidewater — reprise de la référence globale

La première acquisition `baseline/` est rejetée : les six PNG répètent une
tuile de 1280×720 sur une image de 1920×1080. Les anciens fichiers sont conservés.
L'origine précise de ce défaut n'est pas établie. Il ne remet pas en cause les
oracles terrain, végétation et village, rendus sur des cibles explicites.

## Procédure corrigée

Le viewport du navigateur est fixé à 1920×1080 avant le chargement de l'overlay.
Chaque navigation réinitialise l'application source intacte, positionne la caméra
et exécute 120 pas de 1/60 s. La capture intervient après `ready_reference_only`,
sans redimensionnement de la scène figée. La capture CDP demande explicitement
PNG, `fromSurface: true`, `captureBeyondViewport: false`.

Un essai intermédiaire `baseline-002/` a produit six images visuellement correctes
mais encodées en JPEG par l'API générique du navigateur, malgré l'extension `.png`.
L'indexeur a refusé leur signature. Elles restent des traces de diagnostic,
exclues des comparaisons numériques. Aucun transcodage JPEG→PNG n'est utilisé
pour fabriquer une référence prétendument sans perte.

La reprise sans perte est enregistrée séparément dans `baseline-003/`.
Les paramètres de caméra restent ceux de l'acquisition initiale ; l'overlay
`scripts/tidewater/reference.html` et le code source n'ont pas été modifiés.

Les six PNG finaux ont été inspectés et indexés avec succès : village, côte,
île, récif, canopée et côte nocturne. Aucun des deux motifs de répétition n'est
présent. Index SHA-256 :
`a0f1854126895c28c681b116d2890fe7a96651d979cc2cb20a92dfc4b8bf5416`.
Overlay identique au fichier servi :
`de3e388fcb73b4270ab3477705a30fad3a488ad4154195b743027e44a9b68554`.
Le clone servi ne contient aucun changement suivi, seulement cet overlay non suivi.
Le viewport temporaire a été réinitialisé, l'onglet fermé et Vite arrêté.

## Limites conservées

Ces captures sont des références upstream, pas des captures Forge. Leur présence
ne prouve aucune parité native ou navigateur de Forge. L'heure et le temps fixe
ne qualifient pas les états asynchrones, l'historique temporel, les phases
aléatoires des objets ni la répétabilité entre GPU. La sérialisation initiale
de `adapter.info` donne `{}` : aucun matériel n'est déduit de ce champ.
Un relevé explicite de session dans `acquisition.json`, pendant la vue récif,
indique AMD / RDNA-3, Chrome 153, canvas et viewport 1920×1080, DPR 1.
Le navigateur masque les identifiants device/description ; ce relevé ne constitue
pas une identification complète ni une mesure indépendante par vue.
La vue aérienne est oblique, pas un relevé cartographique complet.
