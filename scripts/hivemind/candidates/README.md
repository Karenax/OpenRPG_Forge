# Candidats Hivemind : raccordement commun des scènes

Le code de ce dossier est préparé et contrôlé hors des modules canoniques. Son
existence ne prouve ni son chargement dans Studio ni un rendu fidèle.

`source_graph_scene_geometry.rs` prépare les primitives de la scène sélectionnée,
conserve les identités des nœuds et matériaux et décode une seule fois chaque
géométrie partagée. Les primitives glTF ordinaires restent explicites dans les
scènes mixtes. L'absence d'une tangente source est conservée et signalée.

Le banc `verify_scene_geometry_candidate.py prepare` utilise les 57 GLB existants
du reçu `source-graph-scene-native-tests-v3/CANDIDATES.v1.json`. Il épingle les
sources et les GLB, compile le candidat avec Bevy, compare les attributs aux bits
des buffers et teste les instances partagées, matériaux mixtes et refus. Ses
fichiers, journaux et dérivés de compilation restent sur F: :

- `F:\OpenRPG_ImportWork\source-graph-scene-geometry-adapter-v1`
- cache existant `source-graph-private-boundary-v1\target-rust198-explicit-domain-v8`

`record_scene_geometry_progress.py` reporte le reçu dans L01 et vérifie les
identités des processus Windows lorsqu'il est en cours. Il conserve les grands
entiers des reçus et ne transforme pas un contrôle CPU en validation visuelle.

## Travail restant avant intégration

1. Lire le verdict natif et corriger ses erreurs éventuelles sans effacer le reçu.
2. Renforcer l'admission : la liste des indices SourceGraph doit correspondre aux
   extensions déclarées dans le document, même si un appelant a modifié la liste
   publique des contrats après lecture. Aucun tel matériau ne peut passer par
   `HostGltf`. Rejouer les contrôles concernés après cette correction.
3. Intégrer le module avec une garde des sources, après les contrôles BC4/GLB
   actuels. Préserver leurs fichiers épinglés et les travaux étrangers.
4. Raccorder les primitives à la racine commune de placement et d'aperçu dans
   Studio ; conserver les primitives ordinaires, la sélection et les éditions.
5. Fournir les images capturées, repères source/cible, valeurs par instance,
   temps, caméra et routage des passes réellement requis. Ne pas inventer de
   matrices, images, tangentes ou comportement pour permettre le chargement.
6. Vérifier rendu, placement, modification, sauvegarde et réouverture dans le
   produit. Les contrôles CPU de ce dossier ne clôturent pas ces exigences.
