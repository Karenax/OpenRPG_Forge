# R1-064 — kit gouverné dans le Studio Web

2026-09-06, Rust local modifié, aucune publication. Gate 1 reste ouverte.

## Implémenté

Le paramètre optionnel `forge_kit=governed`, avec la vallée WorldSketch, déclenche un préchargement
des rapports et six GLB du candidat déployés sous `./forge-kit/`. Les mêmes contrats de provenance,
pins et validations GLB sont utilisés. Les SHA-256 sont recalculés sur les octets reçus.
Les chemins validés sont encodés composant par composant pour HTTP ; les redirections sont refusées.
Une erreur bloque le lancement et est affichée, sans fallback silencieux.

Après contrôle complet, Bevy reçoit une source mémoire `forge-kit://` qui conserve les octets
vérifiés ; aucune seconde ouverture HTTP des GLB n'est requise. Les chemins persistants, rapports
et placements ne sont pas réécrits. La caméra joueur reste unique, et la disponibilité physique
du kit conditionne les entrées. Le statut visible indique instances/colliders/prêt/échec.

`forge_assets` et `sha2` réutilisent les versions du workspace, déjà présentes dans la pile ;
aucun nouveau fournisseur, format canonique, moteur, police ou asset externe ajouté.

## Vérifications exécutées

- Compilation et Clippy strict Studio WASM/Web PASS ; Trunk build PASS.
- Test moteur `adapter_binds_report_preview_world_sketch_terrain_and_camera` PASS, étendu pour
  contrôler que la source mémoire ne change ni rapport, ni preview, seulement le chemin dérivé.
- Formatage et diff-check PASS. Les 495 tests workspace du lot précédent ne sont pas présentés
  comme rejoués après ce raccord frontend.
- Chrome réel : `Kit : 20/20 instances · 36 colliders · prêt`, un seul monde initialisé,
  caméra 1/1 et hashes de scène alignés. Capture inspectée : scène et éléments du kit présentés.
  `dev.logs` warn/error retourne une liste vide pour les lancements valides vérifiés.
- Mode sans kit rejoué sur la construction finale : un démarrage, `Kit externe non demandé`,
  hashes alignés, aucune erreur retournée.
- Paramètre invalide rejoué sur la construction finale : zéro démarrage, erreur de configuration
  et `Kit refusé` visibles, boutons de qualité/météo désactivés.

## Essais négatifs et corrections conservés

1. Construction initiale : le remplacement d'un GLB dans la copie déployée a réutilisé les bons
   octets du cache HTTP. Ce n'était pas un contournement du hash, ni une preuve de rejet.
2. Préchargement passé en `RequestCache::NoStore`. Même essai : hash incorrect sur
   `vv-rock-moss-a`, zéro démarrage du monde. Le test a révélé un intitulé d'erreur météo inadapté.
3. Message généralisé à la configuration de démarrage et statut du kit passé à refusé. Dernière
   construction : lancement valide/capture et paramètre invalide vérifiés ; logique de hash
   inchangée depuis le test négatif de l'étape 2.

Les remplacements ont concerné uniquement les copies déployées de preuve. Tous les GLB sources
et les trois copies de déploiement ont été revérifiés conformes aux six hashes du rapport après
restauration. Aucun original Blender/GLB n'a été modifié.

## Artefacts locaux

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/` :

| Construction | Dossier | SHA-256 WASM |
| --- | --- | --- |
| Initiale, cache HTTP | r1-studio-kit-20260906/dist | f56480c7698769cf46611bd4b21281dd9862b4dc415e606c43f705b1ef6267cb |
| Sans cache, test hash négatif | r1-studio-kit-20260906-b/dist | 41bbfacb5f50fca8e0a974bd7813ec2d1501eb262a24488cfb9982cc8201ed95 |
| Finale, erreurs corrigées | r1-studio-kit-20260906-c/dist | 6cc98bfb6bec83907cbf2f5e44a638d014dd953f8198fd1136c524305d7b6ce0 |

Sources du kit : rapport `r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json`
et preview du dépôt `examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json`.
Le rapport est déployé sous `forge-kit/build.json`, le preview sous `forge-kit/preview.json`,
les GLB sous leurs chemins relatifs d'origine. Copie locale uniquement, pas une publication.

## Limites et suite

Viewport réel 1408×797 (sans kit 1408×741) : ni Full HD du canvas ni Tauri qualifiés. Captures
inspectées dans la conversation, non exportées en PNG durable. Fenêtres de navigateur en arrière-plan
avec cadence bridée : aucun chiffre de performance qualifiant. Traversée et contacts avec chaque
asset non testés ici. L'aspect reste prototypal ; aucune acceptation artistique revendiquée.

Chargement par URL de développement, pas encore choix de dossier dans le Studio, import éditable,
hot reload, sauvegarde ou export. Limites de taille vérifiées après réception du corps HTTP,
pas de quota streaming ni timeout explicite. Un déploiement local gouverné est requis.
L'ancien warning futur `proc-macro-error2 2.0.1` demeure, distinct des warnings projet.

Tous les onglets créés et les trois serveurs loopback de preuve ont été arrêtés. Les fichiers de
preuve sont conservés. CI distante NOT RUN, validations humaines et ordre des gates inchangés.

## Transport borné — complément du 2026-09-06

Le transport utilise maintenant un lecteur de flux et contrôle la limite de chaque fichier avant
d'ajouter un morceau au buffer WASM. `AbortController` annule la requête en cas d'échec ou après
un timer de 20 secondes couvrant en-têtes et corps ; le timer est libéré à la sortie, même sur erreur.
Pas de retry silencieux ou de monde partiellement lancé. Les buffers réseau propres au navigateur
ne sont pas inclus dans ce quota ; le timer reste soumis à l'ordonnancement du navigateur.

Vérification Chrome réelle avec `scripts/Serve-StudioKitFault.py`, serveur loopback qui ne modifie
aucun fichier :

- `--mode timeout` : en-têtes HTTP et premier octet reçus, corps bloqué 25 secondes. Le Studio
  affiche « délai de 20 secondes dépassé, téléchargement annulé », zéro démarrage.
- `--mode oversize` : corps de 2 Mio contre limite de rapport de 1 Mio. Refus explicite
  « limite de 1048576 octets dépassée », zéro démarrage, avant analyse JSON.
- Serveur normal : 20/20 instances et 36 colliders prêts, caméra 1/1, un démarrage,
  aucun warn/error retourné. Pas de nouveau verdict visuel ou de performance.

Compilation/Clippy strict WASM et Trunk PASS après correction d'un type d'erreur JS (`Object`
au lieu de `JsValue`) lors du cast du lecteur de flux. Formatage et diff-check PASS.
Construction : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-kit-bounded-20260906/dist`.
WASM SHA-256 `15e6d91570b3032eecc3e10a404b7ae22d4a6f4d82b3c08b8ba21e83d608e982`.
Trois onglets et trois serveurs de preuve fermés. La sélection de dossier et Tauri restent ouverts.
