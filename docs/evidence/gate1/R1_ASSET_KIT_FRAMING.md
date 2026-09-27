# R1-063 — cadrage des aperçus DCC, 2026-09-06

## Implémenté

Le worker Blender calcule maintenant la caméra à partir des bounding boxes transformées des
meshes LOD0 pour toutes les poses demandées, et du champ réellement projeté (résolution et
aspect des pixels). Une marge de 8 % est réservée sur chaque bord. La caméra reste fixe pendant
le tour ; les rotations des objets sont restaurées, y compris en sortie exceptionnelle du rendu.
Les dimensions nominales de la recette ne sont plus prises pour les dimensions d'une retouche.
La géométrie, les matériaux, les placements et les fichiers sources ne sont pas modifiés.

## Vérifié

`Test-AssetKitFraming.py` exécuté avec Blender **4.5.10 LTS**, code **0** :

- 6 assets × 3 configurations (1920×1080, 1080×1920, pixels 1,5:1) × 8 angles ;
- une copie temporaire avec translation, rotation sur trois axes et échelle non uniforme,
  contrôlée sur 8 angles ; soit **19 cas, 152 poses** au total ;
- chaque coin projeté respecte les marges et les plans de clipping ; rotations restaurées ;
- deux entrées vides refusées ;
- deux rendus 1920×1080 par le chemin `render_turntable` du worker, inspectés : chêne et pin
  visibles de la base à la cime, sans coupe par le cadre ;
- SHA-256 inchangés du `.blend`, du rapport source et des six GLB.

Commande depuis la racine Rust (nouvelle sortie obligatoire) :

```powershell
& 'C:/Program Files/Blender Foundation/Blender 4.5/blender.exe' --background --disable-autoexec --python-exit-code 1 --python scripts/blender/Test-AssetKitFraming.py -- --base-build 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1' --output-dir 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-asset-framing-20260906-b'
```

Preuve externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-asset-framing-20260906-b/`.
Rapport `framing-validation.json` SHA-256 :
`2cfee11dda4cc30dbaec40dfd99129b71c4ff313ed3e6504960d1a1b31b5c334`.
Worker SHA-256 : `4554095a71f21c5eb59c9c2718a7fe5e288c0ee84a3fec93861c5521055fc48a`.
Le premier essai sans suffixe `-b` testait le fit puis rendait directement ; le second exerce
également le chemin de rendu du worker. Il ne s'agit pas de deux reconstructions d'assets.

## Limites et suite

### Vue groupée corrigée — 7 septembre

La reconstruction de conservation des retouches a montré un défaut distinct des
turntables : `render_lineup` conservait une position/cible fixes et tronquait les
arbres. Elle utilise maintenant le même ajustement aux bounds réels, appliqué
aux copies de présentation et au repère humain de 1,8 m, avec 8 % de marge.
Le nettoyage des copies est garanti par `finally`. Aucun modèle/placement source
ou rendu runtime modifié ; l'espacement existant du kit n'est pas redéfini.

`Test-AssetKitFraming.py` étendu et exécuté avec le Blender portable 4.5.10 :
19 contrôles individuels/152 poses, puis trois vues groupées avec 72 coins
projetés chacune (1920×1080, 1080×1920, pixels X=1,5). Tous les assets et le repère
sont exigés, marges et plans de clipping contrôlés, copies supprimées après rendu,
matrices sources et hashes des fichiers conservés. Sortie 0, cinq PNG générés.
Le premier essai utilisait par erreur le matériau du sol pour le repère de test ;
le second utilise `R1063_ScaleMarker`, comme le chemin de production.

Preuve retenue : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-lineup-bounds-20260907-b/`.
Planche paysage finale inspectée : les six assets, pieds et sommets des arbres
sont dans l'image. Portrait inspecté lors du premier essai de cadrage ; pas de
nouvelle qualification artistique du feuillage. Ce sont les arbres du générateur
de base, pas les dérivés rev.4 du Studio. Aucun rebuild complet ou Rust nécessaire
à ce changement de caméra d'aperçu ; diff vérifié. Le parcours de conservation
précédent reste lié au worker antérieur, pas rétroactivement rejoué ici.

SHA-256 : worker `48cf909bfd333a783a89fac33a620126245ad28d0aff2141905160fabb91ce3c` ;
test `255b8212468f08154a0ab2858e3ba9642d83b58ae6f44cf6f2b75c51dc975d04` ;
rapport `20a8780ec12d2c4ba73b3a1f1bcc6e584ad47f65df35e84ed83917cd87783c30` ;
`lineup-0.png` `ac12dc38c39263ef65f2263fa558cb31b1de8bcdaa08dda28d5ffd19f956ba15`.

Les vues complètes confirment un feuillage encore clairsemé et des jonctions de branches
artificielles. Le coût LOD0 existant est de 48 718 triangles pour le chêne, 62 944 pour le pin.
Ces assets ne sont donc pas promus automatiquement comme remplaçants des arbres procéduraux.
La correction porte sur l'aperçu DCC, **pas sur la forêt affichée dans le Studio**.
La prochaine amélioration artistique doit préserver les retouches et être mesurée avant intégration.

Aucun rebuild complet du kit, test Rust, benchmark runtime ou test d'erreur de rendu injectée
n'est revendiqué pour ce lot Python. Les anciennes captures restent des preuves de l'ancien worker ;
une future reconstruction devra produire de nouvelles références de cadrage, sans assouplir les
contrôles de reproductibilité. R1-063 et Gate 1 restent ouverts, CI distante `NOT RUN`,
revue humaine toujours requise. Aucun asset Cadle, changement de branche ou publication Git.
