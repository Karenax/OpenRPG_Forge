# Gate 7 — Progression et critères du 23 octobre 2026

## État courant au 7 novembre 2026

Gate 7 reste ouverte. La [matrice d'acceptation](#matrice-dacceptation)
intègre le candidat privé Windows 049, les essais partiels Linux 0837
et les réserves de redistribution. La [passation](../../HANDOFF.md#gate-7-etat-courant)
résume les travaux restants.

## Matrice d'acceptation

| ID | Critère canonique | Preuve exigée | État |
| --- | --- | --- | --- |
| R7-01 | Streaming de cellules | Chargement/déchargement borné réel, aller/retour, erreurs, identités et gameplay conservés | PASS local Windows du 5 novembre — région 2 km, trajet de 291 m, 29 transitions et 799 observations prêtes au sol ; 72 tests actuels des budgets/refus/identités, sources et persistance rapprochées. Reçu 015 `R7-01-streaming-assessment-015.json` ; aucun FPS, HLOD général ou autre OS déduit |
| R7-02 | HLOD | Représentation agrégée réellement rendue, continuité, coûts et limites mesurés | **Qualifié localement Windows.** Aller-retour 017 dans le module exporté, quatre profils, terrain et végétation 014–015, réduction texturée dans le cas dense ; aucun gain FPS établi. Runtime 017 exécuté séparément, cycle final d'installation distinct |
| R7-03 | Sauvegardes multi-zones | A→B→A, sauvegarde/reprise, progression et séparation des modules, écriture interrompue | PASS local Windows — A→B→A→B natif, états complets retrouvés ; refus intermodules et staging interrompu vérifiés par tests du stockage réel ; `R7-03-saves-assessment-001.json` |
| R7-04 | Packaging runtime | Exécution autonome sans sources, Vault ou IA ; intégrité et refus d'altération | PASS local Windows du 5 novembre — export 015, exécutions réelles et ressources fermées rapprochés ; chemins sans fallback dev/Vault/IA, 25 tests actuels de fermeture/corruption/identités étrangères. Reçu 015 `R7-04-runtime-packaging-assessment-015.json` ; OS propre, installation 015 et redistribution restent distincts |
| R7-05 | Profils graphiques | Low/Medium/High/Ultra appliqués, persistés, relus ; capacités effectives et refus visibles | PASS local Windows du 5 novembre — quatre profils réels, ressources, persistance/relecture, refus et défaut Medium rapprochés sur sources vérifiées ; reçu 015 `R7-05-graphics-profiles-assessment-015.json`. Les fenêtres mesurées courtes ne qualifient ni benchmark FPS ni parité Tidewater ; distribution/OS restent distincts |
| R7-06 | Lanceur | Launcher propre au jeu exporté, identité du projet, lancement/reprise, erreurs et retour | PASS local Windows du 5 novembre — parcours réels 008/011, identité du projet, reprise et retour ; sources/frontend 011–015 identiques, 32 tests actuels des refus/processus. Reçu 015 `R7-06-game-launcher-assessment-015.json`. Installation 015, OS propre et réglages indiqués non appliqués restent distincts |
| R7-07 | Builds Windows/Linux | Binaires issus des entrées qualifiées et contrôles locaux propres à chaque plateforme | **Volet Windows privé qualifié avec le 049.** Studio, kit, launcher et runtime rapprochés dans le bilan Windows 049. Le build Linux 0837 porte sur un snapshot antérieur. Le build Linux des sources courantes et sa qualification restent ouverts. |
| R7-08 | Licences agrégées | Notices et droits des composants réellement distribués ; réserves résolues | **Ouvert.** Notices intégrées aux paquets privés 049 et crédits Tidewater vérifiés via le service d'export natif. Attribution de `convert_case_extras`, `drain_filter_polyfill` et `erased` à résoudre. Droits de redistribution des assets et composants à établir. |
| R7-09 | Installateur | Installation, mise à jour/reprise, rollback, désinstallation préservant les données | **Volet Windows privé qualifié avec le 049.** Les cycles de maintenance du Studio et de Tidewater préservent les fichiers auteur et les sauvegardes contrôlés. Sous Linux 0837, installation et retrait vérifiés en conteneur. Mise à jour, restauration et reprise via le launcher restent ouvertes. |
| R7-10 | Machine propre | Installation et parcours sur OS propre, prérequis consignés, aucune dépendance de développement | **Volet Windows privé qualifié avec le 049.** Studio installé et deux exports dans un Sandbox hors ligne sans SDK, lancement et reprise des deux jeux dans un autre invité sans Studio. Sous Linux 0837, fenêtre Studio, lancement de diagnostic et sauvegarde du Grand Plateau observés en conteneur sans SDK. Parcours auteur et joueur de bout en bout sur OS propre à qualifier. |
