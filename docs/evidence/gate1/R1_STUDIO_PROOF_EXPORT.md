# Exports locaux de qualification Studio — 2026-09-06

Statut : implémentation et contrôles automatisés réussis ; livraison des fichiers
par le navigateur non vérifiée. Aucun passage de gate.

## Périmètre implémenté

Deux boutons de l'inspecteur demandent un diagnostic JSON et une capture PNG du
viewport. Le JSON reprend les ledgers déjà observés (scène, qualité, pont, météo,
télémétrie) et les statuts affichés, sans inventer de succès pour une donnée absente.
Le schéma `forge.studio-diagnostic-export` v1 conserve explicitement
`human_acceptance=false`, `gate_passed=false` et `gpu_capture_pair_atomic=false`.
Il ne sauvegarde ni projet ni module. Entrées et sortie sérialisée sont bornées à
1 Mio ; erreurs JSON et doublons sont refusés.

Le PNG réutilise `Screenshot` / `save_to_disk` de Bevy. Une seule requête peut être
active, avec dimensions non nulles limitées à 3840 × 2160 et expiration après
15 secondes observées par `Time<Real>`. Une page suspendue peut retarder cette
observation. L'interface annonce une demande de téléchargement, jamais un fichier
reçu ou sauvegardé. Les erreurs d'encodage/téléchargement de l'observateur Bevy ne
sont pas retournées à ce composant ; cette limite interdit un verdict de livraison.

Aucune mutation des documents, nouvelle dépendance de version, seconde pile GPU,
copie d'asset Cadle ou fonction d'édition Gate 2.

## Vérifications réellement réalisées

Répertoire local hors Git :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-proof-export-20260906`.

- Tests workspace : **412 réussis**, dont cinq tests d'export et de limites.
- Clippy workspace tous targets/features, warnings projet refusés : PASS.
- Clippy Studio cible `wasm32-unknown-unknown`, feature `web` : PASS.
- Build Studio `trunk build` : PASS.
- Avertissement de compatibilité future conservé : `proc-macro-error2` 2.0.1.
- Chrome réel, build servi sur loopback 14642 : kit **20/20**, **36 colliders**.
- Clic réel JSON : demande annoncée ; attente d'événement download expirée.
- Clic réel PNG : callback GPU reçu **1408 × 741**, demande de
  `forge-viewport-1788672452305.png` annoncée. Ce n'est pas du Full HD.
- Aucun fichier correspondant retrouvé dans le dossier Downloads attendu.
  Aucun PNG de ce lot n'a donc été archivé ou inspecté.
- Journaux navigateur warning/error consultés : aucun retourné dans cette lecture,
  pas une garantie d'absence globale d'erreurs.

Le parcours automatique lancé pendant ce smoke a échoué au watchdog 30 secondes :
468/468 ticks au sol, 427 contacts, 38,491 m. Démarrage sans maîtrise du premier
plan ; ce run ne constitue pas une nouvelle preuve de traversée réussie. Le délai
n'a pas été modifié. La preuve précédente reste distincte dans
`R1_VEGETATION_WEB_REPLAY.md`.

## Limites et suite

SHA-256 du lot compilé et des journaux locaux :

| Fichier | SHA-256 |
| --- | --- |
| `dist/forge_studio_bg.wasm` | `e2db23d1ac0f15d327fec6d720ef487a54db0cdaa3053eb198c2dfd5ef445e1d` |
| `dist/forge_studio.js` | `d17582f23cc92ab3c5129fb318059ad3aae17cb53b804ae9a33de771cb584a43` |
| `tests.log` | `97748123e396a7b26717fd5907ace2b831f9d746888b0c6f85d560178158065e` |
| `clippy.log` | `5e316669e42e0703c763359b2d80d5494bc9c168067c0585f2e65b70b7e1c02a` |
| `clippy-web.log` | `d1fcaefc10ece54bc4f548e40310c937b31f949ab1ba383f2c38c4a95dee2f2a` |
| `build.log` | `799c983043bb35cc8db0582312d919e3455d8123b00212c25ce0d54a2fb21c01` |

La consultation de la page interne de téléchargements a été refusée par la
politique de l'outil navigateur. Aucun contournement ni changement de sécurité
n'a été effectué. La cause de non-réception n'est pas établie : ne pas attribuer
arbitrairement l'échec au navigateur ou à la durée de vie du Blob.

La réception et la lisibilité des deux fichiers restent à qualifier dans un
environnement autorisé. La capture GPU reçue ne prouve pas la livraison PNG.
Les preuves sont non atomiques, ne qualifient ni Tauri, ni performances, ni
acceptation artistique/humaine. Gate 1 reste ouverte, CI distante NOT RUN sous
ADR-0006, arbre de développement modifié préservé, aucune publication Git.
