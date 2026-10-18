# Intégration Tidewater

Composant facultatif de carte, séparé du moteur générique.

```text
Studio / application de carte
  ├─ forge_engine : capacités génériques
  └─ forge_tidewater : projections et extensions de la carte
       ├─ forge_engine
       └─ forge_tidewater_assets : acquisitions et recettes
            └─ forge_assets : contrats génériques d'assets
```

Le moteur et les contrats d'assets n'importent et ne réexportent aucun de ces
deux composants facultatifs. Les appels aux API moteur sont explicites ; aucune
façade de compatibilité ne réexporte le moteur depuis cette crate.

Ce composant possède les projections de bâtiments, végétation, océan, atmosphère,
matériaux et rendu temporel historiquement intégrées au moteur, ainsi que les
ports de référence et leurs notices. Les données et acquisitions restent dans
`forge_tidewater_assets` et les paquets externes gouvernés. Les shaders et caches
GPU ne deviennent pas des documents auteur.

L'extension de joueur emploie le contrôleur, la caméra, les collisions Rapier et
le contrat `forge_engine::player_motion_extension` du moteur. Le moteur ne lit
aucun état, nom ou type propre à la carte. Les caméras de capture utilisent le
marqueur générique `OffscreenSceneCamera`.

Les noms des exemples et leurs arguments sont conservés ; leur paquet Cargo a
changé :

```powershell
cargo run -p forge_tidewater --example tidewater_plant_probe --locked --offline -- <arguments>
cargo run -p forge_tidewater --example generic_scene_render_probe --features qualification --locked --offline -- output/optical-proof
```

Les notices des fichiers portés restent avec ces fichiers, hors du moteur.
Le texte de référence est dans [third_party/tidewater/LICENSE](../../third_party/tidewater/LICENSE).
Ces conditions concernent le composant correspondant ; elles n'ajoutent aucune
dépendance de carte au moteur. Les licences des dépendances génériques demeurent
propres à ces dépendances.

Le [guide des entrées](COMMON_SCENE_INPUTS.md) décrit les paramètres des ports.
L'[ancien README](PRE_SPLIT_ENGINE_README.md) conserve le détail historique des
projections ; ses commandes et emplacements antérieurs à la séparation sont datés.
