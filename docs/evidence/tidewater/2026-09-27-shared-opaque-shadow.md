# Preparation partagee des ombres opaques

Le module forge_engine::tidewater_object_shadow contient maintenant la preparation
GPU des casters opaques : vertex shader, pipeline Depth32Float, buffers de positions
et indices, uniformes de transformation et bind groups par cascade. Il utilise les
types exposes par Bevy et le RenderDevice existant, sans nouvelle dependance wgpu.

OpaqueDraw valide geometrie et matrice avant conservation. La preparation refuse
un nombre de cascades hors 1..4, des matrices non finies, plus de 4096 dessins ou
plus de 256 Mio de geometrie. Les limites par dessin et les controles d'indices
reprennent ceux de l'adapter du prototype. Les parametres de rasterisation et
la disposition des uniformes sont conserves.

Le probe utilise cette implementation pour ses casters du village. Les branches
propres aux feuillages et au terrain restent dans le probe ; la passe complete,
son adoption dynamique et son raccordement Studio restent a partager.
Aucune mutation de document auteur ni qualification de placement Studio.

Clippy moteur --lib --no-deps -D warnings : PASS (session 73117).
Diff --check et formatage cibles passes. Rejeu de scene native termine dans la
session 67393 (exit 0) : native-shared-opaque-scene-001.png et journal associe
sous E:/OpenRPG_Forge/.tidewater-integration. Comparaison a
native-shared-hill-scene-001 : 0 pixel different. Les 2097152 octets des ombres
de relief et les 50331648 octets des ombres d'objets sont strictement identiques.
Rapport : shared-opaque-scene-proof-001.json.
PNG SHA256 : 243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
Executable SHA256 : 271cb387decadb48f96e2ef3a6abe21e2d17ee371b05d101739039f6b46f3be5.
Ce resultat qualifie la non-regression du prototype a vue/heure fixes, pas la
parite globale upstream ni l'edition Studio. Check WASM Studio final PASS (session 69140), avec le seul avertissement
proc-macro-error2 de compatibilite future deja connu.
