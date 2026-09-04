# Gate 2 — Modules, zones et métadonnées source

Date : 4 septembre 2026. Branche : `Rust`.
Code qualifié : `1e151b22e529899c590e32675992e7176909f475`.
Arbre : `42f48fcc28751d7f13677f0175a05e9ea5067f80`.

**R2-002, R2-003 et R2-004 PASS pour la création et l'édition des sources.** Gate 2 reste
ouverte à **4/43**. Le point de départ est une métadonnée explicite, pas un placement de personnage
ni une preuve de navigation. Onglets, projection 3D, picking et hiérarchie des objets restent dus.
Gate 1 reste à 43/55 sous ADR-0011, CI distante `NOT RUN — infrastructure billing` sous ADR-0006,
PresentMon suspendu sous ADR-0010. Aucune route générale R2-F n'est fermée par ce lot.

## Contrat livré

- Workspace v2 : collections de modules et zones, IDs typés stables, modules vides autorisés,
  au plus 64 modules et 256 zones, budget JSON complet de 1 Mio conservé.
- Migration stricte du v1 : enveloppement des anciens objets en collections, sans modifier les
  identités, révisions, terrains, noms, graines ou points de départ. Les octets originaux restent
  intacts ; le hash canonique v2 n'est pas présenté comme le hash historique v1.
- Commandes pures partagées : ajout module/zone, renommage projet/module, édition nom/départ zone.
  Révision de base, paramètres typés, validation, document candidat, hashes et snapshot inverse.
- Session native autoritaire, historique borné à 32 opérations, undo/redo monotones ; un nouvel
  edit après undo invalide redo, un edit refusé conserve les deux piles. Aucun effacement disque.
- Projets conserve le bureau doré, ses menus et les mêmes actions. La sélection par IDs détermine
  le module destinataire et les métadonnées éditées. Pas de seconde copie canonique dans Leptos.

Les seules dépendances sont celles déjà présentes ; Cargo.lock, capacités Tauri et assets ne changent
pas. Aucun fichier Cadle ou média du site de référence n'est copié. Le fond CSS et les glyphes Forge
restent originaux. Le style demandé est celui du [simulateur Golden Gate](https://macos-27-simulator.mweinbach.chatgpt.site/),
pas une reprise de ses applications, comptes ou données.

## Vérification automatisée

Répertoire local ignoré : `artifacts/gate-2/modules-areas-2026-09-04/`.

| Contrôle | Résultat |
|---|---|
| `cargo test --workspace --all-targets --locked --offline -- --quiet` | 432 PASS, aucun échec/ignoré |
| `cargo clippy --workspace --all-targets --locked --offline -- -D warnings` | PASS |
| `cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web --locked --offline -- -D warnings` | PASS |
| `cargo fmt --all --check`, `git diff --check` | PASS |
| Trunk Release `--locked`, réseau Cargo offline | PASS, bundle optimisé produit |
| Studio Release `--features tauri/custom-protocol --locked --offline` | PASS, autonome |
| `scripts/Test-CadleGateC.ps1` | PASS, snapshot ea0da625 intact, frontière tracked=484, checkout présent |

Les deux builds portent `FORGE_SOURCE_COMMIT` au commit exact ci-dessus. Le warning de compatibilité
future de `proc-macro-error2 2.0.1` préexiste ; aucun warning projet supprimé pour passer les checks.
La frontière Cadle ne remplace pas une nouvelle baseline ou un nouvel audit licences. Aucune nouvelle
campagne supply-chain n'est revendiquée en l'absence de changement de dépendances.

Les dix nouveaux tests couvrent migration stricte/invariants, ajouts multiples, métadonnées et
préservation des voisins, commandes invalides/périmées/sans effet, quotas/IDs, historique 32,
révisions monotones, branchement après undo et sauvegarde/réouverture v1/v2 sur filesystem réel.
Les scénarios de crash et l'audit durable de toutes les futures commandes restent hors portée.

## Parcours natif réel, sans JSON manuel

Studio autonome, client 1600 × 900, captures inspectées par l'agent :

1. Ouvrir explicitement le snapshot v1 du bureau précédent, projet **Atelier doré de preuve**, r1,
   module **Chapitre doré**, zone **Clairière du bureau**. Noms, identités, graine et départ conservés.
2. Créer **Chapitre de la forêt** (r2), le sélectionner ; son état vide est visible.
3. Ajouter **Lisière boisée** (r3), graine `18446744073709551615`, départ (0,2,0), puis
   **Chemin des hauteurs** (r4), graine 73, même départ. Deux modules, trois zones.
4. Sélectionner Lisière boisée, saisir **Lisière profonde** et X=257 : commande refusée,
   `spawn x must be finite and inside [-256, 256] metres`, document toujours r4 et départ (0,2,0).
   Le texte non soumis est conservé. L'alerte accessible est en haut, hors cadrage après défilement :
   amélioration UX à prévoir (erreur au voisinage du champ / remise en vue), pas une acceptation visuelle finale.
5. Corriger X=12 et appliquer : r5, nom et départ (12,2,0) acceptés. Ctrl Z depuis le bouton
   restaure Lisière boisée et (0,2,0), r6 ; Ctrl Maj Z rétablit Lisière profonde et (12,2,0), r7.
6. Ctrl S, destination explicite puis Enregistrer : nouveau snapshot v2 r7, statut enregistré.
   Fermer le Studio, vérifier sa disparition, relancer le même exécutable.
7. Ouvrir explicitement le nouveau fichier : deux modules/trois zones, r7, mêmes noms/IDs,
   graine u64 exacte et départ (12,2,0). Historique vide après réouverture, pas de restauration
   automatique revendiquée. Nouvelle fermeture propre confirmée.

Un premier ciblage automatique d'un champ hors écran a été refusé par l'outil avant saisie ;
le parcours a continué après mise en vue. Ce n'est pas un échec d'ouverture du produit.

| Objet | Identité |
|---|---|
| Projet | `9d72c00d-02a6-50c2-87b3-411787233851` |
| Module ajouté | `1bb3c448-4fc8-5f5e-b75f-2e909fe0ccf4` |
| Lisière profonde | `580d7438-73f1-560a-aeda-015967e7540f` |
| Chemin des hauteurs | `e7f44300-f2fb-5a7e-9ad8-fcaf1b617af6` |

Fichier produit : `snapshots/9d72c00d-02a6-50c2-87b3-411787233851-r7.forge.json`, **5 542 octets**,
SHA-256 `297035827452c15186b9f17840784d575b98f660ddcfb8984102d10c8de61ae4`, identique après réouverture.
Contrôle du JSON : le premier module est exactement conservé hormis le tampon de révision terrain
restauré à 7 par undo/redo ; graine u64 maximale exacte et structure 2 modules/3 zones vérifiées.
Le fichier v1 de `desktop-shell-2026-09-04/snapshots/`, **1 771 octets**, reste inchangé :
`44a59f46476cfbf5d2a1523790512c6f35adcdabf2f6072f08324f9cf23e747b`.

Navigateur 1280 × 720 : rendu doré inspecté, brouillon **Brouillon conservé** intact après masquage
et réouverture de Projets. Sans Tauri : création désactivée, révision 0, document nul, dirty false ;
aucun faux backend de persistance. Ce contrôle n'est ni une preuve disque ni un audit console complet.

## Empreintes des preuves locales

| Fichier | SHA-256 |
|---|---|
| `01-v1-opened.png` | `21765ca72f2566f59f98509e74fbfa7a6dab4e1f2c71e37c14051b6478381d93` |
| `02-multiple-areas.png` | `860c05a9e3bf0d21fce5e25f148ab5303a5b320db9f27718fa73fc28645bdba2` |
| `03-undo-metadata.png` | `47600b94f328046c6cbb07461d0bf37ac3450fc3841cc142b30be6940194a2e8` |
| `04-redo-metadata.png` | `589c679e8efdace0ca323c6d564f66dfc412d67e1a866e8688c7a1777f176a62` |
| `05-saved.png` | `39dba63f8c9b926bb76a43cf69de1c1fb8e182dbc57c965472c15b4155fda2d5` |
| `06-reopened-desktop.png` | `869b3a915a252c57cd41699cd457aaced3ee5461db3fe77dbfa083b259f9766c` |
| `workspace-tests.txt` | `e9f685a5a2842d143c62a604c76a8993eb74be91c7e131e193920a34867d5a45` |
| `clippy-native.txt` | `da6b41b2642966e4a849b87301a92b9da876a97410ebc6041cabc1ee0695c714` |
| `clippy-wasm.txt` | `0516d022514c95f7ddc3b08e802a657d61c3eedf23e425f8cf5dd4d78f873d3d` |
| `trunk-release.txt` | `3d63061f144a85f2cf9c6ee3268ceb961126d8231191ce1d392afdd15307e211` |
| `studio-release.txt` | `82869a8d9589a1dd75cf816581d317545b10592dae04dbfb6f180e8a1f6fcb67` |
| `cadle-boundary.txt` | `79c6a66557bf977e2bf2ecafaf9ef79a42248de24a15f439dc20c012b4fdf967` |

| Binaire/bundle | Octets | SHA-256 |
|---|---:|---|
| `target/release/forge-studio.exe` | 33 600 512 | `d8d0a5077813205074d06333017ebf93bf84af4ae425b7785cfe03f1de2d10d0` |
| `apps/forge_studio/dist/forge_studio_bg.wasm` | 39 940 785 | `f7cef4ce327a3330ca464b147b8cfb8bb4c05ab4d0188531bef7bf6578f2a3ce` |
| `apps/forge_studio/dist/forge_studio.js` | 187 769 | `1df120203268b41651ffca0ffb9376a3df66b0812b67473a90dc26284a9b91cb` |
| `apps/forge_studio/dist/styles.css` | 8 346 | `a2b47503fd98d3156d5a1d5d986abe88dd568af09dd14cbddd55f32fcd3ef76a` |
| `apps/forge_studio/dist/desktop.css` | 21 911 | `10c38316245c120fe679e20bdd6de6d15e1ee96dc859b4f574c217c7ddcd4dad` |

Paquet humain Gate 1 inchangé : `6d1219d19748f9e90d49e6431e17f160e65c90e7483b4d38a388e801ad754ac8`.
Ces observations ne sont ni une validation humaine du propriétaire, ni une mesure GPU/cadence.
Prochain travail métier : onglets et viewport du document réel, puis édition du terrain ;
Diagnostics reste une scène technique indépendante et les futures applications du dock indisponibles.
