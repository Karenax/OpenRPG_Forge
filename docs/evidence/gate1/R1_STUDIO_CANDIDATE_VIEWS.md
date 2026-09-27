# Vues de preuve candidates dans le Studio — 7 septembre 2026

Branche Rust, base f786a38aca2023f08d708687f99a47d14f34e6d4, worktree préservé.
Pas de publication, de promotion d'asset ni de mutation de carte.

## Problème et implémentation

Le Studio ne connaissait que les caméras embarquées. La caméra `water_detail`, utilisée
par les preuves natives, était refusée avant démarrage du viewport. Les mesures depuis
`human_bridge` ne séparaient pas la teinte du bruit temporel de l'eau lointaine.

`forge_view_input=candidate`, uniquement avec une vue fixe WorldSketch valide, charge
`./forge-views/world-sketch.json`. Lecture en flux bornée à 512 KiB, délai de 20 s,
redirections refusées, aucune URL arbitraire. JSON UTF-8 et contrat WorldSketch validés.
Après remplacement de la liste des caméras par celle embarquée, les deux documents doivent
être strictement égaux. Seeds, verrous, terrain, placements, identités et révision ne peuvent
donc pas être changés par cet outil. Le hash des octets candidats est affiché et exporté
dans le statut de vue fixe. Aucun fichier source écrit et aucun nouvel état canonique.

Préparation avant démarrage, même plugin moteur de vue fixe et même garde-fou d'adaptation.
Sans option, comportement antérieur. Ce n'est pas un outil d'édition/sauvegarde de Gate 2.

## Vérifications

- 43 tests Studio PASS (40 bibliothèque + 3 shell), dont borne/UTF-8/contrat et refus d'une
  modification de seed ; document original conservé. Clippy natif strict et WASM strict PASS.
- Trunk build terminé, bundle initial de cette fonction SHA-256
  `4C4A22C6A55F862D793C40263FB8BED2F187823571755AB186B6DD0BC9050F24`.
- Chrome réel, caméra `water_detail` depuis le même document de preuve que le natif :
  `c815e2b9f23071390b69924ab3f4716499b1a8c33fdbb7bce62e14f7cda29fba`.
  Parité des six hashes observée, une caméra active, temps virtuel figé. PNG 1408 × 741
  récupéré et inspecté. Ce n'est ni Full HD ni une preuve Tauri/performance.
- Deux échecs de compilation WASM intermédiaires corrigés : alias Bevy ECS requis pour
  derive Resource, puis formatage du digest via ContentHash existant. Aucun nouveau package,
  warning supprimé ou assertion affaiblie. Warning futur proc-macro-error2 conservé.

Racine des commandes/logs/bundles :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907`.
Les séries optiques associées et leurs limites figurent dans
[le protocole optique](R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md). Gate 1 demeure ouverte.
