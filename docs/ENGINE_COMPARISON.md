# Comparaison des moteurs — synthèse

## Décision

Le MVP retient **Godot 4.7.1 .NET + C#** : c’est le meilleur compromis entre liberté, éditeur 3D existant, runtime/headless, rendu moderne et maintenabilité du toolset.

## Alternatives évaluées

| Moteur | Atouts | Motif de non-sélection pour le MVP |
|---|---|---|
| Unreal Engine | Rendu, outils de monde, réseau, écosystème | EULA et conditions commerciales incompatibles avec l’objectif d’un socle libre |
| O3DE | Libre, renderer et réseau puissants | Pile C++/CMake/Qt lourde et lente à maintenir pour ce projet |
| Stride | MIT, C#, rendu personnalisable | Éditeur plus limité et actuellement centré Windows, écosystème réduit |
| ezEngine | Éditeur, plugins, AngelScript, compilation rapide | Pas de multijoueur intégré et base C++ plus coûteuse |
| Bevy | Rust moderne, renderer en progrès | Imposerait de construire l’éditeur nécessaire au toolset |
| Fyrox | Rust et éditeur intégré | Plafond graphique et écosystème moins rassurants |
| Wicked Engine | Renderer moderne et libre | Trop proche d’un framework graphique ; trop de toolset reste à créer |

Un changement de moteur exige un blocage Godot reproductible, des mesures et un prototype comparable. Stride et O3DE ne sont pas réévalués sur simple hypothèse.
