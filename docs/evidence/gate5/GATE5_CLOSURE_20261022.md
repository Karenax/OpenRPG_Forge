# Gate 5 — clôture locale du 22 octobre 2026

**Verdict : CLÔTURÉE LOCALEMENT.** Gate 5, ouverte le 19 octobre avec objectif
de clôture, satisfait ses treize domaines et R5-P01/P02 sur les preuves locales
rapprochées dans le [rapport de qualification](GATE5_LOCAL_QUALIFICATION_20261021.md).
La décision propriétaire du 22 octobre retire les contrôles GitHub des critères
de gate et arrête leur suivi : [ADR-0034](../../ADR/ADR-0034-github-backup-local-gate-validation.md).
GitHub sert de sauvegarde distante. La qualification repose sur les validations
locales ; aucun verdict distant n'est transformé en succès.

## Critères et candidat

Le [rapprochement des treize domaines](GATE5_LOCAL_QUALIFICATION_20261021.md#rapprochement-des-critères)
associe à chaque exigence les reçus, captures, sources et rapports de jeu :
règles communes, blueprints et overrides, acteurs et fuite physique, conteneurs
et refus atomiques, dialogues conditionnels, transitions/journal/récompense
unique, perception des factions, capacités/effets, calendrier, rencontres,
cinématiques, scripts bornés et diagnostics corrigibles. Les deux modules
partagent le Studio et le runtime, avec profils/surfaces distincts, masquage
sans perte, Save/Open, sauvegardes isolées A→B→A et réexports privés admis.
Il s'agit des fonctionnalités de base permettant de créer des quêtes ensuite,
avec données de test préexistantes, sans livraison ni enrichissement d'aventure.

Le candidat `visual-model-controls-candidate-e-001` est construit depuis le
checkout commun : 2 498 tests workspace PASS, zéro échec, 56 ignorés, 262 cibles ;
douze tests natifs de présentation, Clippy strict natif/WASM, formatage et
constructions frontend/Studio/jeu PASS. Les preuves graphiques ultérieures
complètent le reçu historique de construction sans le réécrire.

L'audit `gate5-closure-inputs-20261022-e-001.json` vérifie à la clôture les
5 148 entrées compilables et les trois binaires contre le candidat acquis.
Le manifeste complet, le HEAD documentaire `6934eb80`, les modifications
locales et les preuves antérieures restent distingués. Les données testées
et les anciens candidats sont identifiés dans chaque reçu. Aucune publication
Git du lot n'est déduite de la clôture.

## Arrêt des contrôles GitHub

Le workflow `Rust gate`, seul workflow du dépôt, est désactivé dans l'interface
GitHub authentifiée. Le bandeau « This workflow was disabled manually. » est
capturé ; l'API confirme `disabled_manually` pour l'ID `346133112`.
La tentative CLI précédente, refusée avec HTTP 401, n'est pas présentée comme
une réussite. Les déclencheurs automatiques `push` et `pull_request` sont aussi
retirés du fichier local ; les tâches historiques sont conservées derrière un
déclenchement manuel, sans demande de nouvelle exécution.

Le candidat local Gate 5 n'a pas été exécuté en CI distante. Les résultats
historiques Windows/Linux/WASM/supply-chain restent décrits dans le bilan.
Ils ne bloquent plus cette clôture selon la décision du propriétaire.

## Conservation et périmètre

La campagne centrale est
`E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate5/20261019/`, physiquement
sur F:. Son archive de clôture est
`F:/OpenRPG_Forge/Preuves-et-analyses/archives/gate5-20261022-e-001.zip`.
Le reçu de vérification et catalogue
`E:/OpenRPG_Forge/Preuves-et-analyses/rapports/gate5-closure-20261022-e-001.json`
porte l'empreinte du ZIP, le manifeste SHA-256, la lecture intégrale de tous
les membres et le contrôle d'absence de changement des sources pendant
l'archivage. Les sources, scripts et documents de clôture sont inclus.
Les originaux restent conservés ; aucune suppression, purge, fermeture de
processus utilisateur ou modification des anciens reçus n'est effectuée.

Les assets privés de bibliothèque (porte et tonneau) sont réellement chargés,
ajustés et conservés dans les modules. Leurs droits de redistribution restent
interdits ; les réexports demeurent `private_local_only`. Les acteurs, cairns
et objet témoin restent génériques : aucune acceptation artistique n'est
acquise. La clôture ne qualifie ni une release, ni Linux, ni Windows vierge,
ni la distribution publique ou les performances générales. Les réserves
antérieures conservent leurs rapports propres.

**Gate 6 et la gate bonus restent non ouvertes.** Les travaux concurrents,
le checkout modifié, les caches partagés et les données d'assets sont préservés.
