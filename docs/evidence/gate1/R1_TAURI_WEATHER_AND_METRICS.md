# Gate 1 — Météo et métriques réellement exercées dans Tauri

8 septembre 2026, branche Rust, aucun commit/push, sources authorées inchangées.

## Candidat et périmètre

Même candidat que la correction de continuité, sans nouvelle reconstruction :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tauri-lighting-continuity-20260908/`.
EXE SHA-256 `7355C486E5F50791CDBA691991B4774C4CE14485AD16E8A137BAEF1872F71788`.
Profil High, WorldSketch embarqué, kit gouverné rev.4 et forêt batches ; météo
embarquée, **sans activation du candidat optionnel d'atténuation solaire**.

Une seule session : ouverture/chargement, bouton métriques, pluie puis clair,
captures de fenêtre et deux exports JSON par les boutons du produit. Pas de
modification DOM, d'injection de valeurs ECS ni de rechargement entre les états.
Computer Use pilote le vrai shell Tauri, pas un onglet Chrome de substitution.

## Résultats observés

- Overlay lisible : histogramme, moyenne/p95/1 % low sur fenêtre glissante de
  240 frames ; « non benchmark » explicite. GPU opaque indisponible sur ce backend,
  pas de valeur zéro inventée. Voir `R1_035_036_LIVE_FRAME_OVERLAY.md` pour la
  clôture des capacités R1-035/036, distincte des seuils de performance.
- Clair initial → pluie : humidité 0 → 0,7799999713897705, 8 192 gouttes dans
  une entité, opacité 0,41999998688697815, uniformes terrain/végétation concordants.
  Image pluie inspectée : traits de pluie visibles et sol plus brillant/désaturé.
- Retour clair : humidité exactement 0, opacité exactement 0 et pluie masquée ;
  `clear_target_restored_exactly=true`, uniformes concordants sur deux matériaux
  terrain et neuf de végétation. Apparence sèche retrouvée dans la capture.
- Contraste/exposition/saturation projetés : pluie 1,08/-0,35/0,86, clair
  1,06/0/1,04, aux approximations f32 près. Présence CPU du bloom/color grading
  confirmée ; pas d'attribution optique isolée à chaque effet.
- Deux diagnostics : `target=tauri_studio`, une initialisation, mêmes six hashes
  source, kit 20/20 et 36 colliders, 650 arbres. Aucune source sauvegardée.

Le soleil reste à 55 000 lux et le ciel reste clair sous cette pluie, conformément
au profil embarqué sans `sunlight`. C'est une **limite visuelle réelle conservée**,
pas une météo cohérente artistiquement qualifiée. Le cycle ne promeut pas R1-019,
les assets, les profils complets ou l'adaptation. L'évolution temporelle des
gouttes et l'absence de stutter exigent autre chose que ces images discrètes.

## Artefacts et intégrité

Sous `weather-proof/` du candidat, images et arbres d'accessibilité conservés.
Les originaux des exports restent dans Downloads ; copies sans écrasement sur E:.

| Fichier | SHA-256 |
| --- | --- |
| clear-overlay.jpg | 2959E1AD04D9D57DD3B95B9197D56C26C85DB3475F7D398F71DBB73789748A0A |
| rain.jpg | 968023840B42423D6BBDEAAA47CFFC128D133CB1D41D113B31E4D37F5591BCD0 |
| clear-restored.jpg | 44512C7974AB56894719C16C9B89E4D94AF49F15018AA91622F0AF72353F22BB |
| rain.json (1788863549016) | 24792C923938476BDFE2D6796018A9227E3E88CA32122262F82A860D85AAE52F |
| clear-restored.json (1788863613701) | 412E05284AA2C979C94CF475BC840B15520889BB8745CAB86C28826222F15523 |

Images/JSON/accessibilité non atomiques. Vérification de cible, initialisation,
hash météo et hashes du manifeste runtime PASS ; valeurs des projections relues.
Série de démarrage 120 frames distincte de l'overlay et du cycle météo ; aucun
benchmark du cycle ni satisfaction de seuil de performance déduits.

Fermeture Alt+F4 et contrôle du chemin EXE exact : zéro processus restant.
Session Node réinitialisée, aucun navigateur de test ouvert, anciens artefacts
préservés. Gate 1 reste ouverte ; validations humaines et GPU cible non remplacés.
