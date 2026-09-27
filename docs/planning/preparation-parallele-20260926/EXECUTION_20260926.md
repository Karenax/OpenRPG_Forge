# Exécution des lots indépendants — 26 septembre 2026

Périmètre : poursuivre les préparations P01–P12 autorisées, sans attendre les
preuves humaines de Gate 2 et sans modifier en concurrence le Studio/Tidewater.
La consigne « lance tout » permet l'exécution ; elle ne transforme pas les
prérequis non acquis en gates qualifiées. Ce rapport complète le premier dossier
documentaire, il ne remplace pas la roadmap.

## Livrables exécutables et données

[Corpus de recette](../../../tools/gate_preparation/README.md), autonome, sans
dépendance installée ni ajout au workspace Cargo :

| Préparation | Résultat de cette passe | Limite conservée |
|---|---|---|
| P01 Contrats | Raccordements existants conservés ; format du corpus explicitement non produit | Aucun nouveau schéma métier adopté |
| P02 Aventure | Deux dialogues originaux, références locales ; neuf checkpoints et deux parcours pour l'aventure A | Données d'oracle, pas modules jouables |
| P03 Sauvegardes | Huit mutations d'auteur et politiques attendues ; branches A/B distinctes jusqu'à la remise | Pas de migration ni sauvegarde runtime exécutée |
| P04 Navigation | Audit actuel du laboratoire et des conditions ADR-0009 | Aucun fournisseur sélectionné implicitement |
| P05 Assets | 17 GLB rehashés et revalidés ; cohérence catalogue/détails/profils contrôlée | Pas d'import complet, droits/publication et rendu non qualifiés |
| P06 Packages | 12 cas PK01–PK12 ; 12 entrées hostiles de chemins/types | Données jamais extraites ; aucun installateur testé |
| P07 Règles/Creator | 25 vecteurs de référence du manuel, sans seconde implémentation des formules | Pas d'adoption d'un profil Rust ni de preuve d'équilibrage |
| P08 Deux modules | Aventure A et enquête B définies ; 8 recettes M01–M08 | Pas deux packages installables |
| P09 IA/MCP | 8 recettes officielles et 4 contenus hostiles traités comme données | Aucun fournisseur, transfert, serveur MCP ou job lancé |
| P10 Distribution | 10 recettes D01–D10 couvrant les domaines préparés | Pas de mesure hardware, installateur ou preuve Linux inventée |
| P11 Réseau | 11 recettes N01–N11 avec autorité et oracles | Aucun serveur ou test multiréseau exécuté |
| P12 Intégration | Validateur du corpus et tests négatifs ; séparation explicite des catégories de preuves | Une réussite du corpus ne qualifie aucune gate |

Total : **65 cas d'acceptation produit préparés**, 12 chemins adverses, 4 contenus
non fiables, 8 classes de compatibilité et 25 vecteurs numériques/de classement.
Les 65 cas ne sont **pas exécutés contre Forge**. Les identifiants D01–D10 sont
locaux à cette préparation, sans ajout de critères officiels à la roadmap.

## Contrôles

Résultats exécutés par Luna max : **20/20 tests du corpus**, **13/13 tests du
manuel**, **4/4 contrôles des sources JSON du bestiaire**, codes de sortie 0.
La commande du corpus vérifie aussi la correspondance des IDs avec les quatre
documents de préparation : une omission ou un ajout non reporté est détecté.
Une première passe avait 18 tests réussis ; la revue a trouvé deux validations
trop permissives (lien sortant incomplet et oracle hostile inversé). Elles ont
été corrigées et couvertes par deux tests négatifs supplémentaires, puis rejouées.
Contrôle final documentaire : 22 liens locaux résolus et neuf fichiers contrôlés
avec `git diff --no-index --check`. Un espace de fin de ligne du rapport assets a
été corrigé avant le contrôle final réussi ; un simple `git diff --check` ne
couvre pas ces nouveaux fichiers encore non suivis.

```powershell
python -B tools/gate_preparation/test_corpus.py
python -B scripts/Test-PlayerHandbookRules.py
```

Le test des vecteurs appelle les fonctions de référence du script du manuel,
pas des fonctions de gameplay Rust. Les tests négatifs corrompent uniquement des
copies en mémoire : références absentes, doublons, dialogue sans sortie,
récompense doublée, perte de conséquence, mauvaise gate, oracle vide et fausses
déclarations de qualification sont refusés.

[Preuve d'intégrité des assets](../../evidence/assets/ASSET_INTEGRITY_20260926.md) :
17/17 SHA concordants, validation glTF Khronos rejouée avec zéro erreur et zéro
avertissement. Les métadonnées conservent `full_import=false` et les qualifications
Studio/runtime absentes. Aucun asset n'a été copié dans le dépôt.

[Audit navigation](../../evidence/navigation/NAVIGATION_READINESS_20260926.md) :
versions et capacités amont vérifiées ; aucune mise à jour simple identifiée
levant les blocages. Le hash du rapport historique et celui du lockfile ont été
recontrôlés. Aucun benchmark ancien n'est présenté comme nouveau replay.

La preuve Khronos a été conservée durablement sous
`output/gate-preparation-20260926/asset-integrity-rerun.json`, SHA-256
`8964996e133a4cbd14df26f30a8a3d44ef1dabe4ef5ced979a938f5bba27942f`.
Il s'agit du rapport, pas des modèles privés. La petite copie temporaire initiale
sur C: est conservée ; aucun nettoyage destructif n'a été effectué.

## Intégration et vrais points d'arrêt

Les builds observés au début appartenaient aux autres chantiers actifs ; aucun
build concurrent n'a été ajouté par cette passe. Aucun fichier des crates, du
Studio, du runtime, de la roadmap, de Cargo ou du Vault n'est modifié ici. Branche
`Rust`, HEAD observé `f786a38aca2023f08d708687f99a47d14f34e6d4` ; travail préexistant
modifié conservé. Aucun commit, push ou changement de branche.

Le [dossier Gate 2](../../evidence/gate2/GATE2_CLOSURE_WORK_20260923.md) conserve
R2-050 et R2-052 ouverts : refus Studio sur projection périmée/performance et
mesure réelle de l'effort humain. La qualification transversale doit ensuite
porter sur un candidat exact avec les contrôles ADR-0006 applicables. Les
évolutions Atelier ultérieures ont leurs propres preuves ; un ancien replay ne
qualifie pas automatiquement un nouveau candidat.

La revue de capture déjà acceptée n'est pas un essai humain chronométré. La
dispense d'écran 4K physique déjà acceptée n'est pas remise en cause. La CI
distante n'est pas exécutée par cette tâche et n'est pas déclarée verte.

Les futures implémentations Gates 3–8 restent dépendantes de l'ordre et des
contrats du plan. Le corpus permet de commencer leurs tests plus vite lorsque
leurs prérequis sont acquis ; il ne les contourne pas.
