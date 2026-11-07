# ADR-0043 — Picking source et profondeur échantillonnable communs

Date : 25 octobre 2026. Statut : réalisation Gate 7 en cours.

## Décision

Le picking de végétation source, son shader ID et le passage hors écran
appartiennent à `forge_engine::source_vegetation::picking`. Les consommateurs
Studio et référence appellent ce propriétaire directement ; aucune façade
dans la crate de carte ne conserve une seconde implémentation.

Le snapshot d'occlusion est paramétré par les matériaux de terrain et de surface
fournis par l'intégration. Ces matériaux doivent respecter les contrats de
vertex/layout et d'alpha du shader source admis. Ce paramétrage ne déduit pas
une politique correcte pour un shader arbitraire ou une surface transparente.
Le matériau standard opaque conserve son chemin existant. Hiérarchie affine,
palette de joints, inverse bind poses, rejets alpha et identités source restent
ceux du monde projeté. Aucune forme approximative ne remplace les occluders.

Le marqueur moteur `OffscreenSceneCamera` remplace l'identité supplémentaire
de caméra d'aperçu de la carte. Tous ses anciens producteurs et filtres emploient
le même marqueur. Les outils de scène et lumières ignorent ainsi les passages
hors écran, y compris les images d'identifiants linéaires sans post-traitement.

`forge_engine::view_depth` possède l'exigence de texture profondeur
échantillonnable. Elle s'applique au rendu extrait avant préparation des
textures et conserve les autres usages auteur. AO et flare l'emploient
directement ; cela ne transfère pas encore le graphe d'ombres à cascades.

## Vérification et limites

Conserver les UUID, bindings, déformation, dithering LOD, alpha et IDs 24 bits.
Rejouer les contrôles d'adapter avec le shader épinglé et les trois images GPU
du passage existant : sans masque, masqué terrain/surface, puis masque standard.
Vérifier résolution des IDs vers les identités canoniques et conservation des
occluders animés et hiérarchiques. Les contrôles CPU de matériaux indépendants
ne prouvent pas un draw GPU ; le contrôle GPU de référence ne prouve pas à lui
seul le parcours auteur Studio, les retouches ou tous les profils.

Les révisions, caméra, readback et libération du passage gardent leur protocole
existant. Aucun format canonique, package ni ressource source n'est modifié.
Cette extraction sert Gate 7 et ne qualifie pas sa clôture générale.

## 27 octobre — Raccord au résident éclairé par le monde

Le rejeu Studio `studio-nature-observation-001.json` révèle une demande de clic
non consommée : le viewport canonique installe le host végétal commun, tandis
que les systèmes du sélecteur ne sont installés que dans l'ancien viewport.
Le chemin canonique ne possède pas le `State` de matériau capturé exigé par
ce sélecteur. Le clic DOM réel est vérifié ; la délégation des événements est
écartée comme cause et aucun correctif de délégation n'est appliqué.

Paramétrer le passage ID existant par son matériau résident. Le matériau de
sélection du monde dérive le shader `WorldPlantMaterial` existant, conserve
son vertex et son évaluateur de surface, puis écrit uniquement les IDs 24 bits.
Il réutilise les buffers de frame, de vent, d'instances et de modèles courants.
Le snapshot du host conserve les instances réellement publiées ; il ne relance
ni le générateur ni la sélection LOD. Tête, propriétaire, identité de projection,
génération et séquence lient la demande, avec la caméra, la bibliothèque et la
révision de module déjà contrôlées au retour GPU. Le chemin capturé conserve
son shader, son UUID et le même passage paramétré.

Installer le sélecteur dans le viewport canonique à partir des programmes admis
de son module. Les modèles opaques éclairés par le monde rejoignent les
occluders avec leurs matériaux, meshes, poses et éventuels joints réels ; leur
éclairage ne change pas la profondeur. Les surfaces transparentes gardent la
politique explicite existante. La qualification porte d'abord sur le module
API 8 et son terrain source admis ; elle ne qualifie pas automatiquement tous
les autres matériaux de terrain ou profils.

Sur les 2 528 entrées des sources 018, Clippy strict natif et Web, format et
25 frontières passent. La suite complète termine avec 2 780 PASS, 68 ignorés
et 144 suites ; le shader privé épinglé passe également. Les entrées restent
inchangées (`studio-source-terrain-code-checkpoint-004.json`). Le rejeu
interactif du candidat 006 reste en cours de préparation.
L'installation des systèmes ou un test CPU ne constitue pas une sélection GPU
réussie dans l'interface.
