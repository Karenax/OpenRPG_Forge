# Ressources de ciel/nuages transportables vers Studio

Le paquet d'environnement est indépendant des 23 assets de bâtiments. Il contient
266 fichiers (2 677 437 octets) : shaders d'atmosphère et de ciel, entrées nuageuses,
bruits, paramètres globaux acquis, cube HDR, éclairage du village et licence MIT.
Les sorties GPU de nuages et LUT atmosphériques ne sont pas embarquées : elles
restent calculées par le moteur. Le cube HDR est encore celui de l'acquisition.
Les ombres de terrain/objets et la scène complète ne font pas partie de ce paquet.

`package-environment-assets.py` vérifie les hashes des acquisitions avant copie.
Les exports `environment-package-001` et `002` produisent le même manifeste :
`6d278d406a34eccf06fae4cd6d4a68647d5988f620761524ed03c41a97e795d1`.
Racine locale : `E:/OpenRPG_Forge/.tidewater-integration`.

`forge_assets::tidewater_environment::EnvironmentPackage` charge les bytes sans
filesystem et sans renderer, en synchrone ou asynchrone. Le manifeste est épinglé
par l'appelant, borné à 256 Kio ; une liste exacte de noms ferme les dépendances.
Chaque fichier est borné à 2 Mio, l'ensemble à 16 Mio et tous les hashes sont
vérifiés avant exposition. La qualification sémantique/GPU reste séparée.

La candidate Studio peut désormais épingler `environment_sha256` en plus du paquet
de bâtiments. Les deux chargements sont vérifiés avant adoption en mémoire ;
un échec conserve les dernières ressources valides. Le contrôle de génération
interdit l'adoption d'un ancien chargement. L'interface annonce uniquement les
ressources chargées et garde l'aperçu/placement explicitement indisponibles.

Staging local `building-library-site-002` : bâtiment paquet 003, environnement 001.
Aucune publication ni modification de la candidate précédente.

## Vérification

- Exemple `verify_tidewater_environment` : chargements sync/async et 266 fichiers
  vérifiés ; hash erroné, fichier corrompu/manquant, chemin hors paquet, taille
  excessive et dépendance absente rejetés. Index invalide rejeté avant lectures ;
  première réponse asynchrone corrompue arrête les suivantes.
- Clippy strict ciblé forge_assets : passe.
- Check Studio WASM/web : passe après correction d'une variable UI inutilisée.
- Clippy Studio strict : échoue uniquement sur le problème préexistant
  `atelier_ui.rs:122`, `bind_main` 103/100 lignes ; fichier non modifié ici.
- Le chargement navigateur, le rejet d’un fichier altéré et le réessai sont
  désormais vérifiés : voir `2026-09-27-studio-environment-transport.md`.
- Aucune nouvelle parité graphique revendiquée : raccordement des bytes au
  renderer Studio, outils de placement et scène complète restent ouverts.
