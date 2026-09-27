# Chargement conjoint bâtiments/environnement dans Studio

La compilation release WASM du frontend passe en 5 min 29 s, puis wasm-bindgen
produit le frontend local. Hash `forge_studio_bg.wasm` :
`c9ac4f582756f9dd64393d807cb6a609506fe75de0fd49d385432c8af010fdfb`.
L'avertissement connu de compatibilité future `proc-macro-error2` subsiste.

La candidate `building-library-site-002` a été servie uniquement sur
`127.0.0.1:8747`. Elle épingle le paquet bâtiments 003
(`a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6`)
et l'environnement 001
(`6d278d406a34eccf06fae4cd6d4a68647d5988f620761524ed03c41a97e795d1`).

## Parcours observé

L'activation clavier des contrôles Objets, Fiche, Bibliothèque de bâtiments
importés puis Vérifier et charger lance le chargement. Le bouton devient
indisponible pendant la vérification puis redevient actif. Le statut confirme
23 bâtiments en mémoire et les ressources de ciel/nuages vérifiées.

Le journal HTTP confirme exactement 1090 requêtes attendues, toutes HTTP 200 :
candidate, deux manifestes, 821 fichiers bâtiment et 266 fichiers environnement.

Un seul octet de `sky.wgsl` a ensuite été altéré dans la copie du site de test,
sans toucher au paquet source ni à son manifeste. La nouvelle tentative affiche
`Bibliothèque indisponible : Environment payload size or pin mismatch`.
Elle s'arrête à la réponse corrompue après 1087 requêtes ; les trois dépendances
suivantes ne sont pas demandées. Après restauration depuis le paquet vérifié,
une nouvelle tentative réussit avec les 1090 réponses attendues. Les sources
originales sont conservées et le fichier de staging est restauré.

## Preuves et limites

Racine `E:/OpenRPG_Forge/.tidewater-integration` :

- `studio-environment-http-proof-001.json` : hashes, bornes des phases et audit HTTP.
- `building-library-http-002.log` : journal complet du serveur local.
- `studio-environment-loaded-001.png`, `studio-environment-rejected-001.png`,
  `studio-environment-recovered-001.png` : états UI observés.

Le mode navigateur autonome n'a pas de backend Tauri ni de zone ouverte. Le kit
GLB ordinaire n'est pas déployé dans ce staging et son panneau indique HTTP 404 ;
ce n'est pas un échec du paquet Tidewater. Les clics souris automatisés initiaux
n'ont pas changé le panneau ; le parcours est qualifié ici par activation clavier.
Aucun verdict sur la cause du comportement souris n'est posé.

La conservation de l'ancien cache sur erreur est visible dans le code d'adoption,
mais n'est pas prouvée par un rendu : aucun consommateur GPU n'utilise encore
ces deux caches dans Studio. Aperçu/placement restent explicitement indisponibles.
Ce test ne vaut donc ni scène Tidewater dans l'éditeur ni édition visuelle qualifiée.

Onglet de test fermé ; processus serveur interrompu et terminaison confirmée.
Aucune publication.
