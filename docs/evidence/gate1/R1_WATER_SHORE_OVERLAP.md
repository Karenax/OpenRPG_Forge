# Gate 1 — Surface d'eau prolongée sous les berges

8 septembre 2026, Rust local, aucune publication. Gate 1 ouverte.

## Défaut et correction

Le propriétaire refuse la bande verte/sombre longeant l'eau dans sa capture
`codex-clipboard-ebf778f6-31c4-484d-b0a5-01269be7fa62.png`.
Le carving conserve au bord nominal du chenal une profondeur de 32 % du lit,
puis remonte dans l'épaulement. Le mesh d'eau s'arrêtait au bord nominal,
avant l'intersection avec cette pente : sa silhouette restait suspendue.

Pour `AuthoredSurfaceV1` seulement, `water::build_geometry` étend les deux lanes
extérieures en XZ jusqu'au bord extérieur de berge déjà borné à la carte.
L'altitude authorée reste identique. Le terrain opaque masque la partie enfouie
et détermine le rivage visible. La politique historique de relèvement ne change
pas. Pas de nouvelle dépendance, shader, triangle, entité, requête terrain ou
modification des documents, berges, matériaux, colliders et retouches authorées.
La profondeur de couleur reste une approximation interpolée à trois lanes,
pas une bathymétrie ou une réfraction physique nouvellement qualifiée.

## Preuves

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-shore-overlap-20260908`.
`Verify.ps1` conserve les contrôles globaux ; `Capture.ps1` borne et termine
chaque processus dont il possède le PID. Cache/temp sur E:, deux jobs Cargo.

- 14 tests eau PASS, dont nouvelle régression sur l'extension, conservation de
  l'altitude/centre/comptage et de la voie historique. Test WorldSketch réel :
  positions dans la carte, winding positif, zéro relèvement. Berges : 40 369
  triangles, sept points par triangle à 0,015 m du terrain comme auparavant.
- Capture initiale `before.png` trop éloignée pour juger le raccord.
- Vue rapprochée `before-bridge.png` inspectée : bande verte et ombre nettement
  reproduites. SHA-256 `A85839426B2FA647107FBB1EF0B69DEA5BAD5E990413732DFD76B40EA9F57B61`.
  Ancien EXE `EE4E05CC2BFB5E57DC320F6C937394E1E5994D3B54D0BE95EF204393C3D0F25A`.
  Les deux runs sont terminés, PIDs 26524 et 6268.
- Essai rapproché intermédiaire refusé : argument `--bridge-placement` manquant,
  code 2, PID 32916 terminé ; journal `rejected-bridge-arguments.stderr.log`
  conservé. Le rejeu passe le document de pont rev.3 dont les hashes correspondent
  au terrain et à la WorldSketch courants. Aucun garde-fou assoupli.

Suite workspace : 636 PASS, un timing préexistant ignoré. Clippy a d'abord refusé
deux comparaisons de tableaux flottants dans le nouveau test ; remplacées par
égalité des bits, assertion plus explicite, sans changement de production.
Régression ciblée rejouée PASS (`regression-final.log`), Clippy strict workspace
tous targets/features PASS (`clippy-final.log`) et check Studio WASM PASS.
Warning futur préexistant `proc-macro-error2 2.0.1` conservé.
Build natif PASS. Nouveau binaire SHA-256
`F64C3DED3CC668736AE7B392AF76086AEC24D422F4944F6283CD2A1DF1501C05`.
Run rapproché 420 frames, capture frame 300 vérifiée à 17:33:38 UTC, sortie 0,
aucun ERROR. PID 17320 terminé. `after-bridge.png` SHA-256
`342CCD01360E607D8F67C88B65F2948EE4F0E9763400E61DB886B9E0296E35EC` ;
journal `after-bridge.stderr.log`
`474AFEA1B079EF882291295F703EED7644F581935F598DF0EAFB578B809DD6AA`.

Comparaison inspectée en 1920×1080 sur RX 7800 XT : même vue rapprochée et même
pont/terrain/kit/forêt. La bande verte et l'ombre de rive suspendue ont disparu ;
la surface rejoint le terrain aux deux côtés visibles du pont. Le bord conserve
des crénelures liées aux vagues ; ni finition complète, ni revue humaine acquise,
ni couverture de tous les angles et profils. Animation non synchronisée.
Overlay après : moyenne glissante 27,96 ms, p95 33,91 ms, 1 % low 26,6 FPS ;
compilation Web et Studio propriétaire concurrents, pas un benchmark ou un gain.
Le nombre de triangles est conservé, pas une promesse de coût de remplissage GPU identique.

Source WorldSketch inchangée SHA-256
`EA014301FB63835609FBF47494FD9FD87D135DB57AA82991B9C27194787AE95F` ;
`water.rs` final `088F2A2AE6438CE11909F51077E554270D30EFBF2459190B387587D463DB20F1`.
Le Studio PID 39624 laissé au propriétaire conserve l'ancien rendu et n'est pas
fermé automatiquement. Nouveau Studio construit avec code 0 ; WASM SHA-256
`01F87E9BBC68C3120C1D4D6FCBF9C327B7F350DD0B7DC20B248A56BA3F0F26E6`.
Candidat : `candidate/forge-studio-candidate.exe` dans la racine de preuve.
Lancé PID 45088, mais le propriétaire a interrompu Computer Use par Échap avant
la sélection de fenêtre : aucune capture ni validation WebView du nouveau raccord.
Le contrôle est arrêté, la session de test et ses enfants sont fermés par PID ;
la session propriétaire 39624 est préservée. Le build n'est pas une preuve GPU Web.
CI distante NOT RUN sous ADR-0006 ; Gate 1 ouverte, aucune publication.

## Reprise WebView — 8 septembre, 17:40 UTC

Le candidat exact est relancé et inspecté avec Computer Use : EXE SHA-256
`BC79897B2BDF0F7C06B13D1CD46ADF12F15C1AD5A3A13B4C1D92AF0E516A41B0`.
Écran de chargement puis scène colorée observés, exports réels via les boutons
du Studio : sources Downloads `forge-diagnostic-1788889251417.json` et
`forge-viewport-1788889264638.png`, conservées et copiées dans cette racine.

- `studio-diagnostic.json` SHA-256
  `E1799AA4BDC356EE60D950118BB35189D43D84BCA81A869F7F7FCFF24429A525`.
- `studio-viewport.png` SHA-256
  `F492B209BDD5CDED624FD9928CD162124B7A0B627FA4ED1F7D7732F0DDA3FCBA`.
- Ledger warmup : `prepared`, 98 pipelines, 0 attente/erreur fatale,
  8 observations stables sur 12, 15 645,4 ms observés, pic de 19 attentes shaders.
- Kit 20/20, 36 colliders, 1 662 arbres ; six sources alignées, un monde et
  une caméra. High partiel eau/environnement, pas une qualification globale.
- Viewport réel 1408×966, PNG inspecté : scène au spawn, pas une vue rapprochée
  de rive. Ne pas transférer la preuve native du raccord à cette vue WebView.
- 120 frames de démarrage : 29,87 ms moyenne / 33,70 ms p95 ; pas un benchmark.
- `gate_passed=false`, `human_acceptance=false`, couple JSON/PNG non atomique.

Le menu actuel ne propose pas de vue rapprochée du pont ; aucun changement de
carte ou contournement des commandes produit pour fabriquer cette preuve.
La qualification rapprochée Web reste à acquérir. Test fermé normalement ;
PIDs 37784 et 30476 absents après fermeture. Session propriétaire 39624 préservée.
Pas de nouveau build ou changement de code pendant ce contrôle.

## Vue rapprochée Web — 8 septembre, 18:11 UTC

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shore-fixed-view-20260908`.
Même WASM que le parcours corrigé (`R1_BRIDGE_PREPARATION_DEADLINE.md`) ;
seul assemblage Tauri supplémentaire, EXE SHA-256
`6230F53CBAAD5A008640205EB52B891DE3D04E9B5C3D5AD61B77D28C6EE73745`.
Options explicites `-FixedView human_bridge -CandidateView` ; tests d'entrées
`r1-shore-fixed-view-inputs-20260908` : quatre cas valides / sept refus PASS.
Le loader Rust existant accepte la caméra et confirme la carte inchangée.
Caméra candidate SHA-256
`DD8236F46752D5C904EBA3CF80CEB53ECC9AB80B50F49585C95CF0BB049A4F86` :
position [-27.36,15.655,-169.68], cible [-59.375,2.655,-169.5625].
Malgré l'identifiant et le rôle historiques `human_bridge` / `human_height`,
c'est une vue DIAGNOSTIQUE SURÉLEVÉE, pas une preuve à hauteur humaine.

Défaut distinct constaté : temps virtuel figé à 0, kit 20/20 mais zéro collider
enregistré ; warmup `preparing` à 95 206,9 ms, 89 pipelines, zéro attente/erreur,
zéro observation stable. Le cover reste visible. Le clic sur le pas d'animation
n'a pas fait avancer l'horloge. Aucun succès du chargement fixe revendiqué.
L'export PNG officiel du canvas fonctionne sous ce cover ; aucun masquage du
garde ou mutation de carte n'a été effectué pour l'obtenir.

Exports réels, sources Downloads conservées :

- `forge-diagnostic-1788891050110.json` → `studio-diagnostic.json`, SHA-256
  `72AC99A37176CAE6AAA7A8FCCA59B533DAEDB1AEF69F97D20F78550567B980E3`.
- `forge-viewport-1788891060165.png` → `studio-shore.png`, SHA-256
  `5109E52C683628A89E1D9BEE87F22247A45FD30C3E2242116CD8FFD1CA85D7B1`.

PNG 1408×966 inspecté : pont et rivière en gros plan, surface rejoignant les
deux berges, sans bande verte suspendue. Crénelures du bord et vagues très
régulières toujours présentes ; aucune finition artistique globale prétendue.
High partiel, un monde/une caméra, forêt 1 662 arbres. Les 120 premières frames
38,42 ms moyenne / 43,40 ms p95 ne sont pas un benchmark (temps figé et second
Studio propriétaire ouvert). JSON/PNG non atomiques, Gate 1 et revue humaine
ouvertes. Session PID 9568 fermée normalement et absence vérifiée ; seul le
Studio propriétaire PID 39624 reste ouvert. Aucun changement de branche/publication.
