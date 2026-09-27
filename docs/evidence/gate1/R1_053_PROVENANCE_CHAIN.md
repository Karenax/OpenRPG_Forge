# R1-053 — audit local de la chaîne de provenance

2026-09-06, branche Rust, lecture seule des sources et caches. Gate 1 ouverte.
Périmètre : références de Visual Valley et kit corrigé réellement utilisé par les smokes récents.
Ce rapport distingue intégrité, déclaration d'origine et autorisation de publication.

## Vérifié sur les octets présents

- Deux PNG de `examples/visual_valley/references` : tailles et SHA-256 concordent avec
  `provenance.v1.json`. Les prompts décrivent une création originale, non une capture runtime.
- Six GLB et 33 PNG de textures du cache
  `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1` :
  tailles et SHA-256 concordent avec le rapport. Chemins normalisés bornés à leurs racines.
- Quatre liens du rapport contrôlés contre leurs fichiers : manifeste du kit, script
  `Build-VisualValleyAssetKit.py`, descripteur `authored-source.v1.json` et `authored.blend`.
  Aucun lien périmé trouvé. Le descripteur et le Blend sont dans le parent de `rebuild-1`.
- Rapport audité SHA-256 :
  `02566c6ec217084e76fab4d741e62e93db0737e48a5f4ae3e107b924bb877631`.
  Recette : `c75ebf0183f6a30af6ed9c5f3190079102d76f7bf90887b201358027c8fcb920` ;
  générateur : `bd7142d94de071d6f14298310c07827a58dd3ecea845b8898e25eb81a15854d6` ;
  descripteur : `170e1e2edc95d3ce46610ee2fab7bad9fa6acc08a992636842f0d3c8aa941001` ;
  Blend authoré : `49966d9ce8a548d4540da42040f8d586d83965e73a68786f969eececc7b4ad18`.

## Comparaison Cadle bornée

Clone local au commit `ea0da62549980816a6a27e08b75808b1f56d70e7`, arbre propre avant/après.
Les 139 fichiers suivis sous `public/assets` et `docs/concepts` ont été inventoriés en lecture seule.
Comparaison des tailles avec les 41 fichiers Forge ci-dessus : aucun candidat de même longueur,
donc aucun fichier strictement identique possible dans cet ensemble. Aucun contenu Cadle copié.
Il n'a pas été nécessaire de calculer les hashes upstream après ce filtre exact de longueur.

Ce contrôle n'identifie **pas** un asset modifié, recompressé, recadré ou substantiellement dérivé.
Il ne remplace pas une revue des noms/lore/textes ni une analyse de droits. Les assertions
`cadle_content_used=false` sont des déclarations de provenance, pas une preuve indépendante.

## Droits déclarés et limites

Les références indiquent `LicenseRef-OpenAI-Generated-Content-Review-Required`, avec revue de
publication requise. Le kit indique `LicenseRef-OpenRPG-Forge-Project-Generated`,
`internal_gate_proof_only`, `human_publication_review_required=true` ; la retouche porte
`human_review=pending`. **Ne pas assimiler ces statuts à une autorisation de redistribution.**

Inspection ciblée du chargeur Blender : il lie le descripteur à la recette et au hash du Blend,
borne son chemin, puis importe les datablocks explicitement nommés. Cette inspection ne constitue
pas un audit exhaustif de Blender ou du script. Aucune reconstruction Blender relancée ici.

La recherche dans les manifestes Cargo et Cargo.lock ne relève pas de dépendance Cadle/upstream.
Ce constat statique ne valide pas R1-054 : aucun run avec clone absent n'a été effectué dans cet audit.
Pas de nouveau build, test, capture, changement de licence, commit ou push. CI distante NOT RUN.
R1-053 reste ouvert pour sa portée complète, notamment les contenus dérivés et la revue sémantique.

## Actualisation : forêt candidate réellement chargée — 2026-09-06

Le runtime des derniers smokes/mesures utilise maintenant
`r1-foliage-coherent-runtime-20260906-b`, kit candidat révision 2, et non directement les
GLB du rebuild Blender initial. Audit en lecture seule de cette chaîne actuelle effectué :

- six GLB concordent avec le rapport runtime, chemins résolus bornés à la fixture ;
- 45 occurrences d'images embarquées dans ces GLB ont des octets identiques à ceux des
  GLB de base correspondants ; aucun URI externe/data et aucun buffer image hors limites ;
- manifeste, script d'assemblage, rapport de base, rapport candidat, réimport et trois
  scripts de génération/validation concordent avec leurs hashes liés ;
- neuf fichiers source référencés par le candidat, dont le Blend et les GLB de base,
  ont les hashes attendus ; aucun rebuild Blender relancé ;
- la liaison du catalogue forêt vers le chêne correspond au GLB candidat, au kit et à sa
  révision ; cette vérification ne promeut pas le candidat ;
- noms de scènes, nœuds, meshes, matériaux et images GLB extraits et lus : identifiants
  techniques génériques Blender/Forge (roche, mousse, écorce, feuilles, maçonnerie), pas
  de nom Cadle observé dans cet ensemble. Dans les JSON de Visual Valley, les occurrences
  Cadle trouvées sont l'interdiction d'asset Cadle et `cadle_content_used=false`.

Le script d'assemblage courant a le hash
`d72cdf7e88f7b67968b0d6c2e9a31ffaefd61c528c3806ebae31a369381eb8c1` ;
le générateur de feuillage `0ef13ba8906155969a6ba2e3be596a39b3ea17947b8fe940e27de88061eaf58d` ;
son worker courant `4554095a71f21c5eb59c9c2718a7fe5e288c0ee84a3fec93861c5521055fc48a`.
Le hash du générateur cité dans l'audit initial décrit son état historique, pas ce worker
courant. La chaîne vers le rapport de base conservé est vérifiée, pas une reconstruction
indépendante du kit à partir du générateur historique.

Comparaison exacte renouvelée avec les 139 fichiers suivis des deux répertoires exclus
du grant Cadle, snapshot `ea0da62549980816a6a27e08b75808b1f56d70e7`, propre avant/après :
aucun candidat de même longueur parmi les six conteneurs GLB et les 45 images embarquées.
Donc aucune identité binaire dans ce périmètre. Aucun fichier upstream copié ou exécuté.
Une transformation, recompression ou dérivation n'est toujours pas exclue par ce test.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-current-provenance-20260906/`,
`audit.py` (lecture seule des sources), `audit.json` (PASS borné). SHA-256 du rapport :
`9eda124ea367e65d5eaa0a334af730b9b1b6a5c41fa617e03925f886ceff366f`.
Le contrôle bloque sur hash différent, URI inattendu, chemin hors racine ou identité Cadle.

**R1-053 reste ouvert** : les déclarations d'origine ne deviennent pas une preuve juridique
indépendante ; les dérivations/retouches et la revue humaine complète restent à qualifier.
`internal_gate_proof_only`, revue artistique/publication pending, publication non approuvée
et R1-063 incomplet sont conservés. Aucun changement de licence ou autorisation implicite
de redistribution, aucun build/capture, commit ou push dans cet audit.

## Liaison du candidat révision 4 — 7 septembre 2026

Le candidat actuellement chargé n'est plus la révision 2 ci-dessus. Contrôle
en lecture seule exécuté par
`E:/OpenRPG_Forge/.gate1-evidence-runtime/Check-CandidateRev4Links-20260907.ps1` :
sortie 0, `PASS_BOUNDED`. Sources : `r1-conifer-sprays-20260907/runtime` ; copies
Studio : `r1-cloud-quality-web-20260907/web/forge-kit`, sous la même racine de preuves.

- Onze pins source de l'assemblage concordent encore avec leurs fichiers.
- Rapports candidat et réimport concordent avec les hashes du build runtime.
- Six GLB runtime et leurs six copies Web concordent avec le manifeste rev.4.
- Les deux bindings du catalogue correspondent aux assets et hashes du build.
- Build et catalogue déployés sont identiques aux fichiers runtime.
- Restrictions `internal_gate_proof_only`, art pending, publication false conservées.

SHA-256 du build rev.4 :
`97afce2d85c36ebe25c3300af2461052c50453b7e8ec6f3a630ab935493e3f71`.
Ce contrôle actualise l'intégrité de la chaîne, pas son origine indépendante :
aucun nouveau comparatif des images embarquées avec Cadle, aucune reconstruction
indépendante ni revue de droits/dérivations. Ne pas transférer les conclusions
binaires de l'ancien audit rev.2 aux nouveaux dérivés. R1-053 reste ouvert.

## Kit colorimétrique actuellement livré — 9 septembre 2026

Contrôle exécuté, sortie 0 `PASS_BOUNDED`, sur le kit
`r1-texture-colorspace-20260908/runtime-kit` et sa copie réellement assemblée dans
`r1-studio-shadow-candidate-20260909/candidate/web/forge-kit`, sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/`.

- Neuf pins du kit source avant réparation vérifiés ; rapport source, script de
  dérivation, preuve de dérivation et rapport de validation liés par SHA-256.
- Inventaire exact de neuf fichiers dans chacun des deux kits : trois JSON et
  six GLB, aucun fichier supplémentaire ; copies déployées identiques octet par octet.
- Quatre GLB non modifiés ; deux arbres modifiés uniquement par remplacement de
  huit images base-color. Les anciens buffers binaires sont conservés en préfixe,
  les anciens bufferViews restent identiques ; structures hors images inchangées.
  Les images non concernées et leurs références sont identiques. Ce contrôle ne
  réexécute pas la conversion ni les mesures colorimétriques Blender déjà acquises.
- Les 45 images embarquées et les six conteneurs actuels ont été comparés par
  longueur/hash aux 139 fichiers suivis `public/assets` et `docs/concepts` du
  snapshot Cadle épinglé, propre avant/après : aucune identité binaire.
  Aucune URI externe dans les buffers/images GLB. Aucun contenu Cadle copié.
- Deux bindings catalogue et identités/révisions des trois documents concordants.
- Noms GLB extraits et inspectés : noms techniques Forge/Blender, pas de nom Cadle
  observé dans cet ensemble. Cela n'est pas un audit exhaustif du lore du projet.
- Restrictions internes, publication non approuvée et revue artistique pending
  vérifiées ; aucune modification des déclarations de droits.

Preuves sous la racine ci-dessus :

- `Check-CurrentKitProvenance-20260909.py`, SHA-256
  `d8cc705d9883599029ac19759d455b2bf9fdb927f33669bdf2a8e19258d8f53e` ;
- `current-kit-provenance-20260909.json`, SHA-256
  `acad81ea48e670a873e7762f1eae37df207346b8ccd8e0649b04bbe13371e5b0` ;
- `Test-CurrentKitProvenance-20260909.py` et
  `current-kit-provenance-guards-20260909.log` : quatre refus synthétiques PASS
  (hash incorrect, GLB tronqué, longueur incohérente, URI externe), en mémoire,
  sans altérer les sources. Ce ne sont pas des tests du moteur.

Le build courant audité est
`0f1304723bad88f4ab01d0710dc1d2b7f1b0ad073b2745ec39b90fd6943bbd48`.
L'intégrité de cette dérivation et de sa livraison est désormais établie sur les
octets courants, sans transférer les conclusions de la révision 2. Ne pas refaire
ce contrôle à fichiers inchangés. Les preuves antérieures d'origine, les textes
hors GLB et la détection de transformations de contenus tiers ne sont pas
requalifiés par ce résultat ; **R1-053 reste ouvert**. Aucun build, rendu,
publication ou changement de carte n'a été effectué.

## Textes source de Visual Valley — 11 septembre 2026

Inventaire exécuté par `scripts/Audit-VisualValleyText.py` : **16 fichiers JSON/Markdown,
357 chaînes**, dans `examples/visual_valley` uniquement. Tous les octets sont
hashés avant/après lecture ; fichiers binaires exclus, chemins symboliques refusés,
taille bornée et clés JSON dupliquées refusées. Sortie neuve hors des sources :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-text-inventory-20260911/visual-valley.json`,
SHA-256 `37e638beeb6175eaa3a32cd74b7429b4ba0411c7c3e8be459f83130cb5255b6d`.

Quatre tests synthétiques PASS : parcours imbriqué et échappement JSON Pointer,
refus des clés dupliquées, normalisation Unicode/casse des mentions et distinction
entre mention documentaire et verdict. Le script ne produit qu'un inventaire
`inventory_only_requires_review`, jamais une autorisation de publication.

Revue agent des valeurs textuelles, des identifiants distincts hors UUID/hashes,
du README du kit et des prompts de référence effectuée. Noms observés : terrain,
prairie, roche, rivière, village, pont, forêt et repères directionnels génériques.
Les descriptions concernent la direction artistique et les limites de
reconstruction ; aucun texte de quête/dialogue ou personnage narratif identifié
dans cet ensemble. Les prompts demandent une géographie originale et excluent
les symboles de franchises. Cela décrit leurs instructions, pas la preuve que
les images générées respectent juridiquement ces instructions.

Le repérage lexical des mots Cadle/Humpalumps trouve Cadle dans le manifeste du
kit et son README : interdiction d'asset, déclaration de non-consommation et
limite d'import. `cadle_content_used=false` reste une déclaration de provenance,
pas une preuve indépendante. Pas de nom propre upstream observé dans les labels
inventoriés ; aucun corpus Cadle extrait, copié ou exécuté pour cette revue.

**Portée bornée** : ni toutes les chaînes UI du Studio, ni le contenu des images,
ni les noms embarqués des nouveaux GLB, ni les dérivations ne sont qualifiés par
ce script. La revue n'est ni humaine ni juridique. Les audits binaires antérieurs
ne sont pas rejoués ou étendus implicitement au kit actuel. Aucun asset, licence,
fichier source Visual Valley, renderer ou branche modifié ; pas de publication.
Le skill de provenance a imposé la séparation inventaire/revue agent/droits,
sans lancer son workflow d'import ou Blender qui n'était pas nécessaire ici.
R1-053 reste ouvert sur la provenance finale complète et les revues requises ;
le manque ancien de revue des textes dans ce sous-dossier est désormais couvert
dans la portée décrite, pas à l'échelle de tout le produit.
