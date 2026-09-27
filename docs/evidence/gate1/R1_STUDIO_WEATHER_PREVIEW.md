# Gate 1 — Aperçu météo vivant dans Studio

2026-09-05, branche Rust, local non publié. Gate 1 reste ouverte.

## Implémenté

Deux boutons `Aperçu clair` / `Aperçu pluie` réutilisent
`WeatherRuntimeState::request_state` dans le monde Bevy existant. Ils sont désactivés tant
que le contrat de scène n'a pas été observé. La requête typée transite dans une boîte à
une entrée, consommée en PreUpdate : le dernier choix avant consommation remplace le précédent.
Il s'agit d'une simulation d'aperçu non persistante, pas d'une commande d'édition de profil.
Ni fichiers, ni historique d'auteur, ni source canonique ne sont modifiés.

Le retour visible et `studio-weather-preview-ledger` lisent les `WeatherProjectionStats`
du moteur en Last, après ses projections PostUpdate, au plus quatre fois par seconde.
Ils distinguent observation CPU et exécution GPU non sondée. Le texte « météo au démarrage »
et la parité initiale ne sont pas présentés comme le nouvel état vivant.
L'inspecteur défile si nécessaire ; aucune recréation du viewport pour changer la météo.

## Vérifié

- `cargo test --workspace --locked -q` : **306 PASS**, dont les cycles moteur déjà existants ;
  pas de nouveau test unitaire revendiqué pour les boutons navigateur.
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo clippy -p forge_studio --no-default-features --features web --target wasm32-unknown-unknown --locked -- -D warnings` : PASS.
- Check Studio WASM, build Trunk, format et diff : PASS.
- Warning de compatibilité future `proc-macro-error2 2.0.1` toujours présent.

Target et variables Cargo identiques au lot `R1_STUDIO_EXPLORATION.md`.
Bundle : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-studio-weather-preview-20260905\dist`.
WASM SHA-256 : `e0ed53c65b63b27c24edf0335ff3b755375ddb689843278cbe1af4d40f120dda`.
Chrome, onglet explicitement au premier plan, URL
`http://127.0.0.1:1420/?forge_scene=visual-valley`.

Cycle par les boutons dans le même onglet, sans navigation ni rechargement :

| Observation | Clair initial | Pluie | Clair restauré |
| --- | --- | --- | --- |
| Horodatage UTC | 14:19:27.273 | 14:20:03.826 | 14:21:07.096 |
| Wetness réelle | 0 | 0,7799999713897705 | 0 |
| Lot pluie visible (ECS) | non | oui | non |
| Lots pluie | 1 | 1 | 1 |
| Uniformes terrain/végétation alignés | oui | oui | oui |

Le hash du profil météo reste identique sur les trois observations. Les objets sérialisés
de post-traitement initial/final sont exactement égaux. Le retour sec est explicitement
`clear_target_restored_exactly=true`. L'extraction des relevés est conservée dans
`R1_STUDIO_WEATHER_PREVIEW_CYCLE.json` ; elle est nommée comme extraction, pas comme ledger brut.

Deux captures inspectées dans la conversation : pluie visible et rendu mouillé, puis pluie
absente et rendu clair. Elles ne sont pas archivées comme fichiers durables. Un seul démarrage
du monde est observé ; CSS/buffer 2048 × 1101. Aucun log erreur/avertissement retourné par
la consultation finale, sans garantie exhaustive sur tous les messages du navigateur.

Le prélèvement initial de 120 frames au premier plan donne 11,1933 ms moyens, p95 13,6 ms,
1 % low 60,42 FPS sur 1,3432 seconde. Il précède la demande de pluie : ce n'est ni un benchmark
du cycle, ni une comparaison de coût, ni la qualification 1080p/High/GPU cible. Le placement
au premier plan compte ; ne pas comparer naïvement aux relevés précédents en arrière-plan.

## Limites et suite

La scène est la vallée embarquée, pas le candidat WorldSketch ou le kit Blender corrigé.
Le cycle UI est observé sur Chrome, pas WebView2 ; l'observation GPU automatique reste absente.
Revue artistique, animation temporelle normalisée, archivage des captures et benchmark de
qualification restent dus. Pas de nouveau code moteur natif dans ce lot ; sa preuve antérieure
reste distincte du cycle navigateur présent. CI distante NOT RUN ; aucun commit/push.
