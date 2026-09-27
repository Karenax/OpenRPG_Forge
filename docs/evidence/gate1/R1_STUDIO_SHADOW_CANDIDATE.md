# R1 — accès Studio au candidat d'ombres équilibrées

9 septembre 2026, Rust local, aucune publication, Gate 1 ouverte.

## Implémenté

Entrée de menu « Vallée avec ombres équilibrées · candidat expérimental »,
requête explicite `forge_shadows=balanced-preview`, libellé dans l'inspecteur.
Paramètre unique, strict, réservé aux vallées ; une requête invalide échoue
avant lancement du renderer. Sans paramètre, politique de référence inchangée.

Le Studio copie la politique chargée et change uniquement les ombres High :
atlas 2048 et trois cascades, candidat déjà étudié en natif. Portée, contact,
autres paliers, sources authorées et assets préservés. Validation avant et après
transformation. Tous les adapters qualité reçoivent la même politique candidate,
pas un override d'ombres divergent du plan global. Hash de politique distinct.
Les gardes forêt/qualification restent actifs. Aucun gain de FPS ou preset final
annoncé ; le refus du benchmark vivant précédent reste dans son rapport.

## Vérifié

56 tests Studio PASS : 52 bibliothèque et quatre shell, dont deux nouveaux tests
de requête et de politique. Les tests établissent absence d'effet par défaut,
refus Gate 0/doublons/valeurs inconnues, différence du hash et conservation du
document d'origine et des champs hors ombres High. Clippy strict Studio WASM
PASS ; avertissement futur préexistant proc-macro-error2 conservé. Fmt/diff PASS.
Aucun contrat partagé, shader ou source canonique modifié ; pas de nouveau
passage workspace revendiqué pour ce raccord de présentation.

## Assemblage et exécution terminés

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-shadow-candidate-20260909/`.
`Build.ps1` : Trunk release offline/locked, Binaryen deux threads, cache/temp sur
E:, puis constructeur Tauri existant avec kit gouverné actuel. Rust release
terminé en 21,87 secondes. Optimisation et constructeur Tauri ensuite terminés,
session 4359 sortie 0. Aucun second build lancé pendant l'attente.

- WASM : `D7D866491D9E45861B4C90C4178B38CC397743B909254F102561D1BD7D573A56`.
- EXE : `E6DAFDF9C3F41B75E82FCDA2BA2AA6412B72BC0787BBF6777B557088858D5574`.

Une instance Tauri lancée, route normale observée avec « politique de référence »,
puis menu réel « ombres équilibrées » suivi. Nouveau chargement observé, libellé
du candidat visible et renderer prêt. Les premiers clics pendant la préparation
n'ont pas immédiatement produit d'état observable ; l'état a été relu avant
navigation, sans injection DOM ni deuxième processus.

Diagnostic `forge-diagnostic-1788936667473.json` et PNG
`forge-viewport-1788936680260.png` exportés par les boutons, copiés sur E: avec
contrôle des hashes. PNG 1408×966 ouvert et inspecté : terrain, végétation,
personnage et ombre présents. Cette vue de départ n'est pas une comparaison
sous canopée ni une preuve de finesse ou de fluidité des ombres.

Le plan effectif indique 2048 / trois cascades / 128 m / contact shadows actifs.
Hash de politique `a79aceb7b8f68afa5d9376ceb1c1ffff10ef26c40ccb14a1619d66db824d23e3`,
identique au candidat natif ; plan
`ebd983d6868221422c0d7d13193b63a5cc54213bc5d4019199fbef3299252c18`.
Contrôle des valeurs et des gardes de qualification PASS. Les budgets sont
observés côté CPU et le rendu existe ; acquittement complet GPU absent,
`blockers=[vegetation]`, `full_preset_applied=false`, `human_acceptance=false`.
Ne pas assimiler le plan résolu partiellement à une qualification des ombres.

Kit 20/20 et 36 colliders prêts, 1662 arbres / 19944 primitives annoncés.
Préparation 20 608,6 ms, 98 pipelines, zéro pending/failed final ; pic de 19
erreurs shader transitoires conservé, pas de journal déclaré sans erreur.
Les 120 frames de démarrage donnent 25,51 ms moyens / p95 27,2 ms : ce relevé
n'est pas un benchmark comparatif ni une qualification de performance.

Fermeture Alt+F4 et absence de l'arbre de processus vérifiée : 20296, 23872,
27480, 28568, 31564, 6036, 32288, 9172. Aucun navigateur utilisateur arrêté.
SHA-256 JSON : `793B612388F89CC6DC0CAD01DFEA00506BFCA976CF9F4EFED2929EF5D0AF7229`.
SHA-256 PNG : `97FC836B90E354698D21D12F0EDB175A8C71B8FBDE50ADEF4E2B3FB045BA0AB7`.

Lot d'accès Studio terminé. Reste la qualification comparative du candidat en
jeu et l'acceptation artistique ; aucun profil canonique, forêt ou gate promu.
