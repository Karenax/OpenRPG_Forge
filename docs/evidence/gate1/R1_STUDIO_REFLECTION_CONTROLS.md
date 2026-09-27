# R1-040 — Contrôles Studio des reflets, qualification partielle

Date : 7 septembre 2026. Branche Rust, base HEAD
`f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre de travail modifié et conservé.
Gate 1 reste ouverte. Aucune publication, aucune capacité Web promue.

## Implémenté

- Trois aperçus transitoires : profil, environnement seul (SSR désactivé), sans reflets.
- Retour au profil réinitialisant les deux drapeaux ; priorité à la désactivation totale
  lorsque les observations contiennent les deux drapeaux.
- État sélectionné calculé depuis les observations moteur, pas depuis le seul clic.
- État de l'adaptation dans « Profil effectif et limites », avec distinction entre attente,
  application, acquittement et arrêt sans retour arrière garanti.
- Avertissement explicite : adaptation Web non qualifiée, réglages manuels conservés.
  Aucun bouton d'activation automatique ajouté et aucune sauvegarde de document.

Ces contrôles sont génériques à l'adaptateur d'eau ; ce n'est pas une retouche ponctuelle
des données de Visual Valley. La qualification optique Web reste indépendante du natif.

## Vérifié

`cargo test --workspace` : **537 PASS, 0 échec, 1 test de timing préexistant ignoré**.
Deux nouveaux tests couvrent les modes et le texte d'adaptation sans faux acquittement.
Clippy workspace/all-targets/all-features avec `-D warnings`, check Studio WASM/Web
et build Trunk **dev** réussis. Ce build n'est pas une qualification release/Tauri.

Une lecture réelle de l'arbre d'accessibilité de Chrome sur
`http://127.0.0.1:1427/?forge_scene=visual-valley` montre les trois contrôles et le mode
« Suivre les reflets du profil » sélectionné. Le texte indique « Reflets du profil ·
réflectance 0.360 · appliquée · observation CPU, rendu à vérifier ».
Le monde est initialisé une fois, High est affiché comme partiel, et la présentation GPU
reste explicitement non observée. Cela prouve le chargement des contrôles, pas leur cycle.

## Première tentative interrompue

Le contrôle navigateur s'est interrompu avant les clics (`Debugger unattached`, puis
variable d'onglet absente après réinitialisation). Aucun cycle interactif, aucune capture
visuelle ni comparaison optique Web ne sont déclarés PASS. Les relevés de frames de cet
onglet ne constituent pas un benchmark (environ 1 s, onglet non qualifié/focus non établi).

À reprendre : profil → environnement seul → sans reflets → profil, vérifier les observations
et les sources constantes, ouvrir le détail d'adaptation, capturer et inspecter une fois.
Puis qualifier séparément teinte/IBL/SSR Web avant toute promotion de capacité ou activation
de l'adaptation Studio. Acceptations humaines, matériel cible et passage Gate 1 inchangés.

## Reprise fonctionnelle du 7 septembre — PASS borné

La session Chrome reste inaccessible au contrôle. Le même bundle, sans recompilation ni
modification du code, a été servi dans le navigateur intégré Codex (onglet local 3).
Le cycle est cette fois exécuté et observé après chaque action :

| Action | État sélectionné observé | Réflectance CPU |
| --- | --- | ---: |
| État initial High | Suivre les reflets du profil | 0.360 |
| Environnement seul | Reflets d'environnement seuls ; écran désactivé | 0.360 |
| Désactivation totale | Aperçu sans reflets | 0.000 |
| Retour au profil | Suivre les reflets du profil | 0.360 |

Un second cycle a comparé exactement les cinq `source_bindings` du ledger DOM avec
le relevé initial : identiques en environnement seul, désactivation totale et retour
au profil (15 comparaisons). Monde initialisé une fois. Aucun message de niveau error
dans les logs console retournés pour cette session. Le détail d'adaptation affiche
« Mode fixe · choix manuels prioritaires · 0 proposition(s) », puis l'avertissement Web
non qualifié. `full_preset_applied=false`, `application=null`, `gpu_ready=false`,
`adaptation.status=disabled` : aucun acquittement GPU inventé par l'interface.

Capture de l'interface inspectée dans la session : vue 3D visible, panneau de diagnostics
lisible mais étroit/défilant dans le navigateur intégré. Capture conservée dans la trace
de conversation, pas d'artefact PNG local revendiqué. Résolution du canvas 448 × 798 ;
les 120 échantillons affichés (moyenne 10.63 ms, p95 12.40 ms) ne qualifient ni le 1080p
ni le matériel cible, et ne sont pas comparables au précédent onglet Chrome en arrière-plan.

Cette reprise lève uniquement le manque de preuve du cycle UI et des sources constantes.
La qualification visuelle isolée teinte/IBL/SSR Web reste à faire : le passage des boutons
et une image du panneau ne prouvent pas les effets optiques. Aucune case de Gate 1 fermée.

## Artefacts locaux et SHA-256

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-reflection-controls-20260907`.
Les fichiers restent locaux ; le WASM dev mesure 149302720 octets.

| Artefact | SHA-256 |
| --- | --- |
| tests.log | 481675949B3B84E9685E96A387A33D5F03F3F9AEBBBCFAB4F31F3918187B415E |
| clippy.log | 420625DD1CAA394AC2C212130803C2BF40AEE86DC0F5397FBE4C59FD3E44A019 |
| wasm.log | 38A2A5835C53BCFCF21F5478399692053B0552C9E8D1EF805C18DFB1E7C3D780 |
| web-build.log | A708B45D5A47FF3EAFEF0F963EC821AB791740B7AFEFDE16990AA3173FE3EC95 |
| web/forge_studio_bg.wasm | E596E759F42E5AA0FD9FDC8FF418662DE43DEE66FF2C4682E8C972264085CC99 |

CI distante **NOT RUN** sous ADR-0006. Aucun asset Cadle utilisé.
