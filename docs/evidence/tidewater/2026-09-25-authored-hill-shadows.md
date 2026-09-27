# Tidewater — ombres lointaines du relief édité

`SampledTerrainPlan::cell_center_heights` dérive des texels corrigés pour les
normales et les pyramides d'ombre. Les quatre corrections de coins sont moyennées
au centre de chaque cellule, puis ajoutées au texel source et à l'élévation.
La dernière ligne/colonne conserve ses vrais texels : elle n'est pas remplacée
par la sentinelle des requêtes hors domaine. Sans correction, chaque bit source
reste conservé. Ce dérivé ne remplace pas la représentation canonique à deux
grilles, ni l'évaluation exacte entre cellules utilisée par le relief GPU.

Le bake d'ombres de relief vérifie toujours le bloc source et sa pyramide
historique, puis reconstruit la pyramide à partir des texels corrigés lorsque le
document effectif comporte une retouche. Le calcul GPU existant consomme cette
pyramide. Le hash du document et celui du dérivé sont consignés dans le log.

## Vérifications

Cinq tests `sampled_authoring` réussis, dont conservation des bords, moyenne des
corrections de coins, identité sans édition et cohérence des échantillons internes
avec l'évaluateur monde. Clippy strict du prototype réussi ; compilation native
et deux captures exécutées.

Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/` :
`relief-shadow-render-proof-001` reprend les deux workspaces relief/Undo précédents.
Binaire SHA-256 : `a400ba45b79eab38358988ee4f28ad5a2eda4a09f22cb426ca8810cb07051598`.

- Calcul indépendant Python/NumPy depuis les corrections sauvegardées et les
  hauteurs source : les 699050 octets de pyramide donnent exactement le hash
  annoncé par Rust, `3bc31795f5a40d053b141f6c16647286a93512ed9546756faaf4c2ddba89820d`.
- La sortie GPU d'ombre lointaine diffère de la capture précédente sur 885 valeurs
  f16 ; les valeurs restent finies. La retouche atteint donc effectivement ce
  consommateur. `shadow-verification.json` conserve les métriques.
- L'annulation restaure exactement la texture d'ombre historique et l'image FHD
  de référence (SHA-256 `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`).
- Les 203 plantes suivent toujours le relief modifié dans les deux passes.

En préparation de la suite, la formule source `TerrainBake.js` reproduit
exactement les canaux X/Z des 4194304 normales source :
`terrain-normal-formula-verification-001.json`. Les mipmaps ne sont pas reproduits
exactement par une simple moyenne CPU arrondie ; ce contrôle reste distinct
(`terrain-normal-mip-formula-001.json`). Aucun raccordement des normales éditées
n'est revendiqué ici.

Normales, AO, matériaux/masques, eau, herbe, bâtiments, collisions et parcours
Studio restent incomplets. L'essai ne qualifie pas encore toute la chaîne de
retouche, toutes les caméras ou la parité complète.
