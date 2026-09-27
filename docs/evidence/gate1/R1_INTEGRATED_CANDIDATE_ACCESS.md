# R1 — Candidat complet accessible et réévalué

7 septembre 2026, branche Rust, base f786a38aca2023f08d708687f99a47d14f34e6d4.
Worktree préservé ; aucun commit/push ni asset Cadle.

## Accès produit

Le menu « Scènes de preuve » propose désormais « Vallée avec kit et forêt · candidat
expérimental ». L'entrée sélectionne explicitement WorldSketch + kit gouverné + forêt
preview ; elle ne promeut aucun asset ni ne modifie les documents de carte. Les trois
scènes antérieures restent disponibles. L'interface prévient qu'un kit local déployé
est nécessaire ; les vérifications de hashes/révisions et erreurs de chargement existantes
restent actives. Le lien est testé contre les parseurs de scène et de kit existants.

42 tests Studio PASS, Clippy Studio WASM strict PASS, Trunk build exit 0. Fmt initialement
refusé sur une assertion, corrigé puis PASS ; aucun check supprimé. Warning futur
proc-macro-error2 conservé. Pas de nouveau total workspace revendiqué pour cette UI.

Bundle : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-candidate-menu-20260907/web`.
WASM SHA-256 `5B7C12600F1DC54251D99F03EE226A73D4708F4B14339E068A20B3C4D924BE4D`.
Neuf fichiers du kit déjà déployé dans `r1-studio-forest-wiring-20260906/web-dist/forge-kit`
copiés dans ce nouveau dossier ignoré, puis comparés par SHA-256 : identiques. Sources
et anciens déploiements préservés. Ce déploiement local n'est pas une publication.

### Parcours réellement exécuté dans Chrome

Le bundle ci-dessus a été servi sur `http://127.0.0.1:1433/`. Depuis la vallée sans
kit, ouverture du menu puis clic sur l'entrée du candidat : navigation vers
`?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=preview`.
Le Studio observe 20/20 instances, 36 colliders, 636 arbres/7632 primitives, six hashes
alignés et 1/1 caméra, une seule initialisation du viewport. Capture du navigateur
inspectée : arbres gouvernés effectivement visibles, canvas 2048 × 1157. Aucune erreur
console retournée lors de ce contrôle. Cette inspection n'est pas une capture viewport
archivée ni une preuve de qualité artistique, de fluidité, de parité optique ou de Tauri.
Le diagnostic conserve High partiel et GPU presentation non observée : aucun acquittement
ajouté sur la seule base de cette inspection.

## Nouveau replay natif du candidat

Build natif exécuté, binaire SHA-256
`249bbc6dc2b620648da75f8c2edf7ff12dc7c4f292406d224592ae537aa54cb7`.
`Capture-WorldSketch.py`, caméra human_bridge, High, forêt gouvernée, clair/pluie,
capture 300, sortie 420. Entrées exactes et commandes dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-integrated-candidate-20260907/manifest.json`.
SHA-256 manifeste : `8E0192374788DD9BD5A85627C2E277B90334E057FBEA14F547878ABB8D1853C7`.

Deux sorties 0, PNG 1920 × 1080 ouverts et inspectés, kit 20 instances/36 colliders,
forêt 636 arbres/7632 primitives LOD. Tous les pins d'entrée conservés. Profil météo
embarqué : aucune promotion implicite du profil soleil/pluie opt-in antérieur.

| Smoke RX 7800 XT/Vulkan | Moyenne FPS | p95 ms | 1 % low FPS |
|---|---:|---:|---:|
| Clair, 299 échantillons | 62,03 | 17,379 | 42,66 |
| Pluie, 299 échantillons | 62,18 | 17,256 | 41,93 |

Ces smokes courts comprennent des coûts de capture et ne qualifient pas le GPU cible.
Ils ne satisfont pas les seuils p95/1 % low de Gate 1 ; aucune moyenne seule utilisée
pour masquer cela. Les arbres gouvernés remplacent visiblement les silhouettes de secours,
mais sol uniforme, végétation encore clairsemée, silhouettes de feuillage artificielles
et brillance sous pluie restent visibles. Pas d'acceptation artistique acquise.

## PNG Web reçu et correction du diagnostic précédent

Le fichier fourni par l'utilisateur `forge-viewport-1788761091797.png` est présent,
ouvert et inspecté, 2048 × 1101, SHA-256
`A6878BECE49F780A3970CFBF7FBFCDCE931663584E0DFDFDF85EAF6FC4759489`.
Il démontre qu'un export aboutit ; le timeout d'observation du téléchargement ne prouve
pas un défaut produit. Aucune modification de l'export n'a été faite sur cette hypothèse.
La capture précédente fournie fait 2048 × 1157 : ne pas les traiter comme paire A/B
pixel comparable. La forêt y reste procédurale, contrairement au candidat natif ci-dessus.

Gate 1 ouverte. Matériel cible, qualités optiques Web, Tauri, revues humaines et critère
artistique restent dus. Ces preuves ne constituent pas une série finale complète de douze vues.
