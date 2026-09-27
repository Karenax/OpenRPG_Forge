# Studio mapper — exécution de l’audit du 24 septembre

Portée : Rust, worktree existant préservé, sans sous-agent, commit ni push.
Référence : `docs/planning/STUDIO_MAPPER_RELEASE_AUDIT_20260924.md`.
Il s’agit d’une première vague d’améliorations, pas d’une release ni d’une clôture de Gate 2.

## Modifications de cette vague

- M01 partiel : croix / Alt-F4 de la fenêtre auteur ouvrent une confirmation modale. Continuer, quitter sans enregistrer ou enregistrer et quitter sont distincts. Le backend vérifie encore les retouches non sauvegardées avant une fermeture sans abandon explicite. Les rechargements navigateur, crash et récupération de brouillon ne sont pas couverts par cette correction.
- M04 : enregistrer refuse un aperçu en attente et le conserve ; bouton désactivé et explication pour Ctrl+S. Statut aperçu non appliqué séparé de carte non enregistrée. Aucun Apply implicite.
- M08 partiel : tiroir ouvert 256 px, fermé 44 px ; la différence est rendue au viewport et le statut se repositionne. Redimensionnement libre encore dû.
- M09 : Construire devient un outil distinct d’Objets ; hiérarchie de bâtiment dirigée vers Construire, sans empilement du catalogue.
- M10 : état vide Nature expliqué avec accès à la génération de végétation.
- M11 partiel : catalogue local chargé au montage, avec actualisation explicite conservée. Miniatures réelles encore dues.
- M13 partiel : duplication et suppression proposées dans l’inspecteur simple ; restent soumises à aperçu/application. Placement impossible sans référence sélectionnée ; liste d’objets sans UUID imposé.
- M14/M16 partiels : zoom molette ; navigation et sortie du canvas suspendent la brosse en effaçant un trait tenu, sans désarmer l’outil. Reprendre le dessin par points actif n’efface plus ses points ; commande distincte pour prolonger un brouillon existant.
- M15 partiel : poignées de points sélectionnées en espace écran, déplacement visuel transitoire au-dessus du terrain, coordonnées du brouillon mises à jour au relâchement seulement. Révision et brouillon contrôlés avant acceptation ; Échap/sortie annulent le glissement dans le code. Segment provisoire vers le pointeur pendant le dessin. Aucun Apply implicite ; déplacement et annulation du prolongement vérifiés graphiquement ci-dessous. Annulation au milieu d’un glissement, conflits et autres DPI encore à qualifier.
- M17 partiel : parcours rivière exposé directement, largeur/profondeur globales accessibles, provenance et creusement repliés, ajout de point dans l’édition précise. Niveau individuel et garanties de validation conservés.
- M21 partiel : pagination de hiérarchie cachée lorsqu’une seule page suffit.
- M22/M23 : une seule feuille Gold montée dans l’espace auteur, au lieu de deux couches auteur contradictoires. Palette et composants cohérents, texte utile 14 px, sections séparées sans multiplication des boîtes. Pas de déclaration de conformité contraste/DPI.
- M24 partiel : Ctrl+S reconnu depuis un champ ; historique textuel conservé pour Ctrl+Z/Y et Apply non capturé dans un champ.
- M28 partiel : le diagnostic layout distingue chrome et panneau, refuse le débordement, vérifie les actions principales déclarées et ne compte que les éditeurs de point visibles. Le grand replay historique doit encore être adapté à tous les nouveaux parcours.
- M06 partiel : liste des zones chargée au montage, noms lisibles sans UUID obligatoire. Accueil guidé et choix de workspace encore dus.

## Contrôles

- `cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web --offline --locked -- -D warnings` : PASS.
- `cargo test -p forge_studio --offline --locked` : 83 tests bibliothèque PASS, 1 benchmark ignoré ; 6 tests natifs PASS.
- `cargo clippy -p forge_studio --all-targets --offline --locked -- -D warnings` : PASS.
- `node --test scripts/Test-StudioGoldLayout.mjs` : 6 PASS, dont débordement, action hors écran et bibliothèque tronquée refusés.
- Build release WASM et candidat natif : PASS. La première reprise a produit le couple EXE `A46E81924B0D5F9A313FEBF5E07F7FADA6BD4CFAB672B23A990C890D43AE667E` / WASM `E8D763901B614F7DD86AD95477DA17EA79365A26C05294659C6D2C8D1D75BE6D`. Une correction de marge du catalogue suit ; identité finale à consigner après reconstruction.

## Parcours réel du 24 septembre — première reprise

Contrôle par Computer Use dans le candidat Tauri, carte isolée `R2-004 Final B`, surface CSS 1920 × 1080. Captures et mesures locales dans `output/studio-gold-20260924/mapper-proof/`. Ce sont des observations automatisées, pas une recette humaine ni un test de toutes les résolutions.

- Ouverture depuis la liste autochargée : terrain, chemin et chêne retrouvés.
- `paths-final` : panneau Chemins 927 px de contenu pour 927 px disponibles, action Examiner visible. Le précédent état mesurait 952 px, puis 1002 px après message. Aides regroupées, Terminer réservé au dessin, verrouillage/suppression repliés.
- `paths-drag-final` : poignée déplacée à la souris, ligne de brouillon déplacée et chemin accepté inchangé ; action Examiner toujours visible, panneau sans débordement.
- `path-resume-final` : prolongement de 2 à 3 points, sortie du canvas vers le panneau, retour et quatrième point ajouté sans réarmement. Échap affiche « Dessin annulé ; brouillon précédent restauré » et retrouve les deux points précédents. Aucun Apply pendant ce parcours.
- Aperçu du chemin puis Échap : message corrigé « Aperçu abandonné ; carte appliquée inchangée ». Le tour précédent avait aussi vérifié Ctrl+S depuis le champ Nom : refus expliqué, aperçu conservé ; Alt-F4 avec aperçu désactive Enregistrer et quitter ; Échap ferme le dialogue sans abandonner cet aperçu.
- `nature-final` : état vide explicite et bouton Préparer la végétation visibles. Construire possède bien son onglet séparé ; le parcours de création complet reste à qualifier.
- `objects-final` : huit cartes visibles, mais verdict layout FAIL conservé pour 261 px de contenu dans 255 px disponibles. Correction suivante : marge inférieure de 12 à 4 px, sans augmentation de la hauteur du tiroir.
- Enregistrer et quitter utilisé après les essais sans application. Hash de l’archive inchangé lors de la première réouverture : `62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`.

Les captures sans suffixe final proviennent du candidat précédent ; elles ne sont pas présentées comme des preuves du dernier binaire. `objects-open` ancien ne contrôlait pas encore le débordement propre à la bibliothèque : son PASS ne qualifie pas ce critère.

## Candidat final de cette passe

Reconstruction achevée le 24 septembre à 16:38:51 UTC. EXE SHA-256 `092754AEBC3BC0DB9467D72CD76EF6E499247D29FE61D765804234C7F07503EC`, WASM SHA-256 `7C1301775F368AD37F6DC7D558F8CEAAC26F2E1607710AC1764D52989F551E5B`. Pins complets dans `output/studio-gold-20260924/candidate/build-proof.json` ; ce reçu reste BUILD_ONLY, le présent rapport portant les observations d’exécution séparément.

La seule modification depuis le couple A46E8192 / E8D76390 est la marge inférieure CSS du tiroir. Clippy WASM strict et formatage repassés avec succès après ce changement.

- `objects-margin-final` : PASS, bibliothèque ouverte 256 px, contenu utile 255/255 px, les huit cartes entièrement visibles ; aucune barre verticale superflue.
- `objects-margin-closed` : PASS, bibliothèque fermée 44 px, canvas 1358 × 810,91 px contre 1358 × 598,91 px ouverte : 212 px restitués à la scène.
- `wheel-final` : geste de molette effectué dans le canvas, zoom observé sur le terrain, le chemin et l’arbre ; dimensions des panneaux inchangées. Le zoom reste centré sur le pivot, pas sous le curseur.
- Carte réouverte puis Enregistrer et quitter : fermeture effective du processus ; archive finale toujours `62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`.

Les captures ont été inspectées par l’agent. Aucun verdict de beauté utilisateur, aucune recette humaine et aucune clôture de gate ne sont déduits de ces résultats. Miniatures encore génériques, panneaux encore fixes ; la suite de l’audit reste explicitement ouverte.

Le warning de compatibilité future `proc-macro-error2 2.0.1` reste signalé par Cargo, sans être présenté comme corrigé ici.

## Stockage et fixture

Cache Cargo unique existant : `E:/OpenRPG_Forge/.build/cargo-target`, incrémental désactivé, temporaires dans `.build/cargo-temp`.
Un seul candidat remplaçable : `output/studio-gold-20260924/candidate` ; aucune copie d’historique de compilation.
Espace libre E: à la reprise : 187 020 111 872 octets ; après les derniers contrôles : 187 428 016 128 octets (environ 174,6 Gio). Ces relevés machine ne sont pas une mesure exclusive de l’espace consommé par cette tâche.
Avant essais, archive isolée `03992311-9297-5219-8aa3-ed561640a312/authoring.json` : SHA-256 `62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`.

## Reste obligatoire de l’audit

M02/M03/M05 : transitions et brouillons uniformes, protection des rechargements, récupération bornée après crash. Une confirmation de sortie n’équivaut pas à une récupération.
M06–08 : accueil/choix de workspace, organisation projet, panneaux ajustables.
M11–16 : miniatures vérifiées, sélection directe, fantôme, raccourcis de transformation, navigation complète, qualification complète des poignées de chemins et cohérence des autres états.
M17–21 : interaction eau homogène, brosses simplifiées, clairières dessinées, construction directe, scène organisée.
M24–30 : qualification accessibilité/DPI, retours de calcul, playtest, carte exemple représentative, recette transversale et livraison.
M29 conserve notamment la distinction auteur humain / automate ; aucune validation utilisateur n’est inventée.

Les références de Gate 2 et la CI distante restent inchangées. Cette vague ne démarre pas de nouvelle gate de distribution.
