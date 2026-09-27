# R1 — Raccord des profils effectifs forêt/eau

9 septembre 2026, branche Rust, worktree préservé, sans publication.
Travail autorisé : terminer le premier des sept blocs de Gate 1 (profils),
sans reprise esthétique du ciel ni passage à Gate 2.

## Implémentation candidate

- Forêt gouvernée : reçu de projection lié au scatter courant, au profil et au
  cache de prototypes ; lots lointains de chunks exigés. Erreur, profil/catalogue
  périmé ou batch manquant interdisent la capacité. La génération d'adaptateur
  change aussi avec les bindings du catalogue, pas seulement les booléens de capacité.
- Eau forward : observation séparée des images IBL, intensité, teinte, viewport
  entier et budgets effectivement projetés. Les commandes temporaires optiques
  restent explicites et invalident le support automatique. Le plafond métrique
  réellement implémenté est conservé au lieu du fallback classique qui coupe le tracé.
- Le plan différencie le chemin forward du SSR classique. Les substitutions
  approuvées d'intensité des reflets, de brume et de protection des berges restent
  déclarées ; aucun preset complet annoncé par défaut.
- Acquittement GPU : une sous-vue que le shader forward refuse ne peut pas être
  déclarée prête sur la seule existence de textures allouées.
- Cycle natif : une seule demande par étape, car chaque nouvelle demande explicite
  réinitialise l'application. L'ancien envoi par frame empêchait la stabilisation.
  Les observations complètes sont journalisées aux étapes et en cas de timeout.
- L'accès CLI au cycle/adaptation accepte la forêt en batches ; la forêt individuelle
  reste refusée et le reçu moteur reste obligatoire. Aucun timeout assoupli.

## Contrôles du raccord

684 tests workspace PASS / un timing préexistant ignoré ; Clippy strict workspace,
toutes cibles/features PASS. Après la dernière correction des exclusions CLI :
97 tests runtime et un test de journalisation PASS. Builds natif, WASM et Tauri PASS.
Les journaux intermédiaires, y compris les refus de compilation/Clippy corrigés,
sont conservés sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-effective-profiles-20260909`.
Ces contrôles de code ne remplacent pas les essais réels ci-dessous.

## Portée du verdict

Bloc fonctionnel des profils terminé : forêt et eau raccordées, cycle des quatre
niveaux avec retour High en natif et Studio, adaptation montante native,
descendante Studio et priorité manuelle réellement vérifiées. Règles réutilisables,
aucune retouche ponctuelle de carte. Gate 1 reste ouverte. R1-040 conserve la réserve
de fluidité des transitions ; les six autres blocs et les validations humaines
restantes ne sont pas qualifiés par ce verdict fonctionnel.

## Cycle natif réellement passé

Binaire dev `4DF11013D9DAB2365C475E371E1E1FFA3833916690FD0495C8196CF414002BCB`,
AMD RX 7800 XT, 1920 × 1080, candidat intégré avec eau forward, forêt gouvernée
en batches et nuages existants. High → Low → Medium → Ultra → High : cinq
captures inspectées, cinq observations `plan_application_ready`, sept domaines
acquittés et inventaires GPU prêts. Séquences 1/3/4/5/6 ; aucune réutilisation
d'un reçu ancien. Les six hashes source restent identiques et les paramètres
effectifs High sont exactement restaurés. Processus 17332 fermé, sortie 0.

Le plafond des reflets est conservé : Medium 24 pas/64 m, High 48/96,
Ultra 64/128 ; réfraction High 24 pas/3,2 m et Ultra 40/4. Low utilise
l'environnement et la teinte. La portée végétation est 256/320/640/768 m.
Les écarts approuvés restent visibles : berges pleine résolution, intensité
des reflets non appliquée et, hors Low, brume de distance au lieu de volumétrie.
`full_preset_applied=false` à chaque étape ; ce sont des profils effectifs.

Les cinq vues montrent les réductions de végétation lointaine et d'effets en
Low/Medium, puis leur restauration. Les FPS d'overlay incluent chargement et
captures ; aucun benchmark ni gain revendiqué. Avertissement wgpu préexistant
sur `invariant` conservé, aucune correction esthétique engagée.

Preuve locale : `r1-effective-profiles-20260909/native-v3/summary.json`,
journal complet et cinq PNG voisins. `Summarize-Native.ps1` vérifie identités,
sources, paramètres restaurés et terminaison : PASS.
Essais antérieurs conservés : PID 29964 refusé par les anciennes exclusions CLI
(corrigées, 97 tests runtime et un test de journalisation PASS) ; PID 31200
sorti 0 avec cinq PNG mais collecteur refusé car `RUST_LOG=1` masquait les INFO.
Filtre corrigé à `info` pour le seul rejeu natif ci-dessus. Les trois PID sont fermés.
## Cycle Studio réellement passé

Tauri/WebView2, viewport 1408 × 966, candidat complet choisi depuis le menu du
produit (kit, forêt, eau et ciel). Binaire initial
`DCC62229C6AF6A1F960586B9F4CB77CC16253EC81B727AAAC26AD5E4A6F3D87A`.
High → Low → Medium → Ultra → High : cinq exports produits par les commandes UI,
sept domaines et GPU prêts, séquences strictement croissantes, aucun blocker,
sources identiques et retour High exact. Chaque niveau a été observé à l'écran ;
PNG High restauré exporté et inspecté. La caméra est différente du parcours natif :
preuve de commandes, pas comparaison optique pixel à pixel ni benchmark Full HD.

`Summarize-Studio.ps1` : STUDIO_CYCLE_PASS ; `studio-cycle/summary.json` et
`stage-0.json` à `stage-4.json`, `high-restored.png`. Le premier processus Studio
31828 a été fermé après interruption utilisateur. Le cycle est acquis ;
l'adaptation de cette première session interrompue n'est pas une preuve.

Le résumé CPU affichait encore « GPU non confirmé » malgré le reçu détaillé
valide. Libellé corrigé pour renvoyer au reçu du profil effectif, sans modifier
la logique moteur ni transformer un preset partiel en preset complet.

## Remontée automatique native

Même exécutable et sources épinglées que le cycle natif, démarrage Low,
`--adaptive-quality --exit-after-frames 4000 --no-vsync`, sans capture fixe ou
benchmark. Low acquitté à 2,731 s ; proposition automatique Medium à 14,854 s ;
nouvelle application Medium acquittée à 16,168 s (séquence 2, génération 7).
Aucune erreur ni oscillation supplémentaire sur cet essai. Le temps de transition
observé n'est pas une garantie de fluidité. Fenêtre native inspectée, sortie 0,
PID 23620 fermé ; sources rehashées identiques. `native-adaptive/changes.json`
et journal complet conservés.

## Descente automatique et priorité manuelle Studio

Binaire final `751486A9850B18B245B25DC849D3784DD9B9ED5EDED387A514C2E5C02434D245`,
recompilé avec la seule correction de libellé. Même candidat intégré ; adaptation
désactivée au démarrage puis activée par le bouton du produit. Après focus du
viewport : High → Medium → Low, deux propositions, Low acquitté (séquence 3,
génération 8), 2 157 échantillons admissibles au moment de l'export. L'export après
retour à l'inspecteur indique logiquement `waiting_focus`, sans perdre le reçu Low.
Ce n'est ni une adaptation injectée ni une modification des seuils de politique.

Choix manuel High ensuite : automatisme `disabled`, zéro proposition, High encore
actif après au moins 15 secondes dans le viewport, sept domaines et GPU prêts
(séquence 4, génération 10). Paramètres High et hashes source identiques au cycle
initial. `Summarize-Adaptation.ps1` vérifie reçus, identités, intégrité des entrées,
restauration et arrêt : **ADAPTATION_FUNCTIONAL_PASS**.
Exports `adaptation/studio-0.json`, `studio-1.json`, synthèse `adaptation/summary.json`.
Capture d'écran du Studio final inspectée ; PNG conservé du cycle distinct de ces
exports, sans prétention de paire atomique.

Studio 33732 fermé normalement, ainsi que ses enfants observés 20452, 33328,
26024, 33116, 32352, 32412 et 30100. Tous contrôlés absents après fermeture.
Aucun navigateur utilisateur arrêté, aucun essai graphique laissé ouvert.

## Contrôles finaux et limites conservées

55 tests Studio PASS. Clippy final a signalé `observe` à 105 lignes après les
derniers bindings ; identifiant d'adaptateur natif/Web mutualisé en constante
strictement équivalente, sans suppression de garde ni assouplissement du lint.
Clippy workspace strict final PASS (`clippy-final-v2.log`). Cette mutualisation
ne modifie pas les paramètres des candidats graphiques déjà vérifiés.
42 tests ciblés `global_quality` PASS après mutualisation (`quality-final-tests.log`),
dont perte de reçu, focus, reprise manuelle et adaptation ; `cargo fmt --all --check`
et `git diff --check` PASS. Les 684 tests workspace et les 98 tests runtime décrits
plus haut sont les passages antérieurs du raccord, pas des suites intégrales
réexécutées après cette seule factorisation constante.

Adaptation toujours opt-in ; pas de promesse de transitions imperceptibles, pas
de benchmark adaptatif. Les deux directions sont prouvées dans des environnements
distincts (montée native, descente WebView2), pas un cycle de pression identique
sur les deux plateformes. `full_preset_applied=false` conservé. Ni performances
RTX 3060/RX 6600, ni revue humaine des transitions, ni qualification optique exhaustive
acquises. Aucun push.
