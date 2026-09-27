# Tidewater — terrain effectif dans les simulations côtières couplées

Le chargement de la scène transmet désormais aux calculs de ressac,
déferlement et intégration des embruns le même champ côtier que le matériau
optique. Les trois noyaux utilisent également les corrections de hauteur
aux coins et l'élévation globale, distinctes du raster source.

L'adaptateur vérifie les fixtures acquises et leur correspondance avant toute
substitution en mémoire. Il refuse un raster de simulation différent du hash
canonique, un domaine autre que celui de Tidewater ou un champ invalide.
Le slot de texture de détail inutilisé par chaque noyau accueille les deltas
R32Float, avec liaison non filtrable ; l'adaptation refuse un shader qui utilise
déjà ce slot. Les trois noyaux restent sur le device Bevy existant.

## Vérifications

Racine `E:/OpenRPG_Forge/.tidewater-integration/`.

- Compilation et Clippy strict : scène, spray, ShoreSim et ombres d'objets.
- Binaire `dbb68a1e9e620cfe7dae21f829c2184189c4bbc7f382998e4d726b43b042ff66`.
- `relief-coupled-terrain-proof-001` : les deux chargements couplés annoncent
  le hash du champ local attendu et les mêmes deltas. Rendu achevé à 120 pas.
  Undo restitue la référence pixel-identique, SHA-256
  `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
- `native-global-coupled-terrain-001.png` inspectée : la bande de plage
  réagit au niveau de terrain +3 m. Aucun message ERROR dans les captures.
- `verify-authored-coupled.py`, rapport `authored-coupled-comparison-001.json` :
  après modification locale, 11109 valeurs du ressac changent et 612 pixels
  du rendu changent par rapport au lot limité au matériau optique.
  Après +3 m, 678346 valeurs du ressac, 11050 valeurs de crêtes et 271 cellules
  de dépôt changent. 6072 particules restent actives, contre 7014 auparavant.
  Toutes les données flottantes vérifiées sont finies ; les ombres proches et
  lointaines restent identiques. L'image globale change sur 276003 pixels.
- Undo retrouve exactement l'état du ressac, les crêtes et les dépôts ;
  l'ensemble des particules actives est identique indépendamment de l'ordre
  des slots atomiques. Le vérificateur ne confond pas cet ordre avec l'identité.

## Limites et suite

Cette preuve établit la réaction native et la non-régression après annulation,
pas une comparaison GPU complète des retouches avec le moteur source.
Les stations de déferlement sont encore générées sur le terrain initial :
leur reconstruction depuis le terrain effectif est la prochaine dépendance.
Les directions filtrées, raccordements des modes de diagnostic non couplés,
bâtiments, édition interactive et qualification WebGPU restent ouverts.
Les simulations s'exécutent encore sur une fenêtre fixe de 120 pas.
Aucun commit ni publication ; parité complète non acquise.
