# Point 4 — Natif et Studio

Complément ultérieur du 11 septembre : Medium/Ultra réellement sélectionnés et
exportés dans le même Studio épinglé, plans comparés au natif avec reçus GPU
valides, deux PNG inspectés et huit processus fermés. Les quatre niveaux sont
maintenant couverts fonctionnellement sur cette paire. Les réserves optiques,
temporelles et de révision finale demeurent ; détails et hashes dans
`R1_043_CURRENT_NATIVE_STUDIO.md`.

Actualisation du 11 septembre : `R1_043_CURRENT_NATIVE_STUDIO.md` ajoute un runtime
reconstruit sur les 283 sources du Studio actuel. Cinq étapes natives acquises,
comparaison détaillée High/Low avec les trois enregistrements mobiles Studio,
deux PNG inspectés et processus fermés. Portée fonctionnelle seulement ; les
références suivantes restent la paire historique du 10 septembre.

10 septembre 2026. **Parité fonctionnelle du candidat acquise**, portée bornée
ci-dessous. Gate 1 reste OUVERTE. Pas de benchmark PASS, de qualification
temporelle supplémentaire ni de validation humaine implicite.

## Implémenté

Le packaging `Build-StudioCandidate.ps1 -CloudPreview -WaterReflectionPreview`
sélectionne ensemble les routes de nuages et d'eau existantes. L'ancien candidat
du point 3 ne sélectionnait pas les nuages dans Studio. Aucun shader, profil,
document de carte ou asset n'est modifié par ce correctif. Les réglages restent
des aperçus sans sauvegarde ; aucun asset Cadle, commit ou push.

## Candidat et preuves exactes

Racine locale ignorée :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-parity-point4-20260910`.

- Natif réutilisé du point 3 : `r1-motion-final-20260910/runtime-shadow.exe`,
  SHA-256 `50991d48b8b00716847f805aa821f72fdd5ded9e9bb52b5d2dfba3f4d69e5e8f`.
- Studio recompilé pour le packaging seulement : `studio/forge-studio-candidate.exe`,
  SHA-256 `eb75ecef416944c4ec8ead6d88d450a71603280e9ea06679f05223f1eabceffd`.
- WASM inchangé du point 3 :
  `5cfc60f7cfbb389dd09a9c94056ef2660270b63a8ae44ca6fc390b9350da1007`.
- Les neuf fichiers du kit distribués dans Studio (six GLB et trois manifestes)
  correspondent exactement aux pins natifs. Tous les fichiers de `native/pins.json`
  ont été rehashés, sans changement. Kit avec normales corrigées, non promu.
- `studio/build-proof.json` épingle le frontend, le kit et l'URL intégrée.
  Ce rapport de build reste BUILD_ONLY : les preuves runtime sont distinctes.
- `native/runtime.jsonl` :
  `7fad2b5a7d7216838e3841bb3f45a8ab8f2856e5a7175d216ea6e3fbf68d3eaf`.
- `studio/stage-0.json` : `951ed74420bf23c2aa4c0cac89fd7e7e5fd47412598535c8a4dcc7a410cdc76c`.
- `studio/stage-1.json` : `1db28e76773277047dc83d2a115010d8a6ef21158cf9b6ca800146936aa86e19`.
- `studio/stage-2.json` : `5feb4488cd5750064569f72bd47b9bfa303141be066efed3aad8f36526e08d39`.
- `studio/stage-3.json` : `9dd4a1380b139f2f2fa8fdb7653d09fa8aec9eb4c9569f6edd668b2b0851f415`.
- `studio/stage-4.json` : `94d27d0cce5b7a00b155811585ef7d28832e090e7d1f65e06d5b3894fadc1dee`.

Les binaires natif et WASM ne sont pas prétendus byte-identiques ni issus du
même hash de tous les fichiers : la séparation du diagnostic de préchauffage
Studio du point 3 est postérieure au binaire natif. Les entrées et paramètres
de rendu sont comparés directement dans les observations runtime.

## Réellement vérifié

| Profil | Séquence native | Séquence Studio / PID | Plans effectifs et reçus GPU |
|---|---:|---|---|
| High | 1 | 1 / 31168 | MATCH / prêts |
| Low | 3 | 3 / 31168 | MATCH / prêts |
| Medium | 4 | 2 / 25884 | MATCH / prêts |
| Ultra | 5 | 3 / 25884 | MATCH / prêts |
| High restauré | 6 | 5 / 25884 | MATCH / prêts |

Comparaison exacte, sans tolérance numérique : sept domaines effectifs,
cinq sources terrain/végétation/environnement/eau/météo, hash de politique,
contraintes de berge et substitutions ADR-0011. Aucun blocker. Chaque reçu
correspond à l'identité du plan installé, à une file GPU terminée, aux assets
et effets prêts ; zéro pipeline en attente/en erreur. High retrouve ses valeurs.
La scène Studio conserve son manifeste attendu et une seule caméra active.

Nuages Studio : hash de réglages constant
`380e97d60b8f95e7b814c81ab3aebba7926d14c45e6c79923880ce1a1d3d6182`,
budgets High/Low/Medium/Ultra/High = 24/0/12/32/24 pas ; dôme masqué uniquement
en Low. Présence High/Ultra et absence Low aussi constatées dans les captures
natives. Le ledger nuages reste une observation CPU, pas une preuve GPU isolée.

Captures natives `native/cycle/stage-0.png`, `stage-1.png`, `stage-3.png`
inspectées : même vallée, pont, forêt, eau ; changements Low/High/Ultra visibles.
Studio lancé et manipulé réellement : quatre profils affichés, exports JSON
vérifiés sur disque, PNG High `studio/stage-0.png` et Ultra `stage-3.png` inspectés.
Les exports proviennent respectivement des téléchargements 1789047364569,
1789047556800, 1789048033026, 1789048098360 et 1789048186686.

Le natif réalise un cycle continu complet. Studio réalise High→Low, puis
High→Medium→Ultra→High dans une seconde session : la première a atteint le
watchdog externe de 600 secondes après une difficulté de ciblage UI. Ce n'est
pas un cycle Studio continu H/L/M/U/H ni un crash renderer. Les reçus ne sont
pas artificiellement renumérotés. Le test refuse une fusion en session unique.

## Validation reproductible

`scripts/Compare-NativeStudioProfiles.py` lit le JSONL natif et les cinq exports
Studio, avec `--studio-sessions 31168 31168 25884 25884 25884`.
Résultat exécuté : **SEMANTIC_PROFILE_PARITY_PASS**.

`scripts/Test-NativeStudioProfiles.py <racine locale>` : **4 tests PASS**,
dont huit altérations refusées (source/budget/fallback/tier, reçu obsolète,
GPU non prêt, pipeline en attente, effet absent), stage absent et faux cycle
continu. Tests de packaging `Test-StudioCandidateKit.ps1` PASS, route intégrée
incluse ; build Tauri PASS. Pas de nouvelle compilation Rust moteur/WASM inutile.

## Limites conservées et nettoyage

Les vues ne sont pas synchronisées ni au même cadrage : natif 1920×1080 en vue
de preuve, Studio viewport 1408×966 avec joueur. La fenêtre Studio a également
été maximisée pendant le premier essai. Pas de comparaison pixel à pixel ni de
mesure de performance comparable ; JSON/PNG non atomiques. Les plans de pluie
et brouillard sont cohérents, mais cette campagne claire ne requalifie pas leur
animation : conserver les preuves et réserves du [point 3](R1_MOTION_POINT3.md).
`full_preset_applied=false` reste exact (substitutions documentées), de même que
les limites de télémétrie Studio. Le point 5 et le dossier final restent dus.

PID natif 29936 terminé, exit 0, cinq stages vérifiés, aucun ERROR. PID Studio
31168 arrêté par le watchdog avec son arbre ; seconde session 25884 et descendants
24172/23748/5428/18816/28692/4436/28456 arrêtés et absence vérifiée. Aucune commande
de processus restant ne référence cette racine de preuve. Aucun onglet créé.
