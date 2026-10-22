# Gate 5 — qualification locale du 21 octobre 2026

**Verdict technique : PASS local pour les treize domaines et R5-P01/P02.**
**Clôture locale acquise le 22 octobre**, selon
[le rapport final](GATE5_CLOSURE_20261022.md) et ADR-0034. Le propriétaire
retire les contrôles GitHub des critères des gates et demande leur arrêt.
Ce rapport conserve les preuves techniques et les réserves historiques sans
qualification distante ni publication. La campagne porte sur les fonctionnalités
de base permettant de construire du contenu ultérieurement. Les parcours
narratifs utilisent les données de test existantes sans enrichissement.

## Candidat et sources exactes

Le candidat commun `visual-model-controls-candidate-e-001` termine le
21 octobre à 18:56:57 UTC : 2 498 tests workspace réussis, zéro échec,
56 ignorés, 262 cibles ; douze tests natifs de présentation, formatage,
Clippy strict natif/WASM et constructions frontend, jeu et Studio réussis.
Son reçu historique `BUILD_PASS_RUNTIME_NOT_VERIFIED` reste inchangé ; les
parcours graphiques et rapports de jeu ultérieurs constituent leurs preuves
propres. Aucun nouveau build n'est nécessaire après les seules retouches
de données et de documentation.

| Artefact | SHA-256 |
| --- | --- |
| Studio natif | `e45af38254d4c1803dfb7ade9adf11aa527b5f8a18b8a80a3c185fd84b882291` |
| Frontend WASM | `aa46480bfc44210ab7844594a24b04533fa319d0d595316e35fe1d04102aa218` |
| Jeu natif | `65c238d191a27de28b5bfded50b2fe99159cb45331c5022c78ab40042c964c70` |

`gate5-final-inputs-audit-e-001.json` vérifie 5 148 entrées compilables : aucune
différence depuis le manifeste final de construction et trois binaires exacts.
Le HEAD du checkout a avancé de `e310d9b3` à
`6934eb80842dc6f9670b2b9ddde39e7503f2a430` pendant un travail concurrent ;
ce HEAD seul ne décrit pas l'arbre modifié qualifié. Le manifeste final conserve
les entrées suivies, modifiées, supprimées et non suivies, ainsi que les anciens
chemins historiques inaccessibles signalés par Git.

Les preuves antérieures identifient leurs candidats exacts. Le rapprochement
avec `event-quota-remedy-candidate-e-001` trouve uniquement trois différences
compilables : `authoring_gameplay_visuals.rs`, `gameplay_visual_transform.rs`
et la déclaration correspondante dans `lib.rs`. Il s'agit des contrôles de
placement visuel du Studio. Les entrées moteur, gameplay et persistance restent
identiques ; les anciens parcours sont conservés comme preuves de ces contrats,
sans prétendre que leurs binaires ont le même hash que le nouveau jeu.

## Rapprochement des critères

Tous les reçus ci-dessous sont dans la campagne centrale
`E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate5/20261019/`, physiquement sur F:.
La [matrice détaillée](GATE5_QUALIFICATION_STATUS_20261020.md) conserve aussi
les essais refusés, les correctifs et les réserves historiques.

| Critère | Résultat local et reçus principaux |
| --- | --- |
| UI01 — règles | PASS : retouche commune, Undo/Redo avec projection acquittée, contraste réel 4/5 m, réouverture des deux zones à 5 m ; `ui01-projection-history-replay-e-001*`, `ui01-range-4m-e-001*`, `ui01-range-5m-e-001*`, `ui01-ui07-r5-persistence-e-001.receipt.json`. |
| UI02 — blueprints | PASS : héritage v2 à 50, override local 60/fuite conservé, réouverture ; `ui02-final-reopen-e-001.receipt.json`. |
| UI03 — acteurs | PASS : contacts/dégâts antérieurs, puis feu sans dégâts, sentinelle éloignée de 3,497 m et pillard immobile malgré sa perception ; reprise durable ; `ui03-danger-runtime-e-001.receipt.json`. |
| UI04 — conteneurs | PASS : transferts, refus filtre/capacité sans perte et reprise ; `ui04-ui05-final-replay-e-001.receipt.json`. Le tonneau réellement rendu conserve le transfert d'items. |
| UI05 — dialogues | PASS : variable typée, choix conditionnel, conservation de l'identité et reprise ; `ui04-ui05-final-replay-e-001.receipt.json`. |
| UI06 — transitions/journal | PASS : deux solutions préexistantes dans deux parties fraîches distinctes, consommation, condition de défaite, récompense unique et reprise ; `ui06-ui09-ui10-runtime-e-001.receipt.json`, `ui06-two-paths-runtime-e-001.receipt.json`. Le choix de récompense disparaît ; aucun rejeu invalide artificiel n'est présenté comme une manipulation UI. |
| UI07 — factions | PASS : groupes/appartenances/relation orientée, même témoin masqué/visible/masqué par la porte, conséquences personnelles avec causes distinctes, reprise et définitions relues ; `ui07-perception-runtime-e-001.receipt.json`, `ui01-ui07-r5-persistence-e-001.receipt.json`. |
| UI08 — effets | PASS : coût atomique, refus de cumul, protection dans les dégâts, expiration, reprise d'un effet actif sans nouveau coût ; `ui08-runtime-e-002.receipt.json`. |
| UI09 — calendrier | PASS : sauvegarde avant 100 s, reprise et activation durable unique ; `ui06-ui09-ui10-runtime-e-001.receipt.json`. |
| UI10 — rencontres | PASS : apparition, deux défaites, délai bloquant à 600 s, réarmements à 500/700 s, identités/vitalités et reprises sans doublon ; même reçu. |
| UI11 — cinématiques | PASS : ordre/segments retouchables, fin naturelle, saut, interruption et reprise en cours ; matrice détaillée et `ui11-independent-e-002.receipt.json`. |
| UI12 — scripts | PASS : permissions, quotas CPU/événements et refus atomiques, corrections, répétitions/messages, ouverture de porte et franchissement physique complet, reprise ; `ui12-ui13-diagnostic-e-001.receipt.json`, `ui12-cpu-e-001.receipt.json`, `ui12-event-e-002.receipt.json`, `ui12-door-crossing-e-002.receipt.json`. |
| UI13 — diagnostics | PASS : cause/instruction/remède, sélection du nœud fautif, correction et replay ; reçus UI12/UI13 et quota d'événements. |
| R5-P01/P02 | PASS : profils communs 5 m/3 m et aide concordants, deux configurations de surfaces, masquage/Save/Open/réaffichage sans perte, capacités requises conservées, même Studio/runtime A→B→A, sauvegardes isolées et deux réexports admis ; `ui01-ui07-r5-persistence-e-001.receipt.json`, `r5-two-module-reexport-e-001.receipt.json`. |

Les derniers rapprochements conservent 30 fichiers de persistance vérifiés
(3 139 197 octets) et 17 fichiers de réexport vérifiés (141 067 605 octets).
La première configuration commune de B fait passer le gameplay de v1 à v2
et ajoute sa configuration Creator ; les autres données sont inchangées.
Le refus de reprise stricte de A après changement de source est conservé.
La commande normale « Reprendre après des retouches compatibles » réussit
avec la même empreinte fonctionnelle, sans éditer la sauvegarde ni les gardes.

## Réexports privés et présentation

| Module | Contenu vérifié | SHA-256 du `.orpmod` |
| --- | --- | --- |
| Les Trois Cairns | 2 zones, 8 assets, profil 5 m | `4c77ca47d92d389af1a2d3960b3b2aa8c34f0d6c81ce1b0d03377d29ce5a994f` |
| Le Relais des Lanternes | 1 zone, 2 assets, profil 3 m | `c09cc2da1d6e693210f9a445d5a852719727458afad11b23f34dc3bee62313fd` |

Les trois zones sont rouvertes et admises par `forge_delivery module-check`.
Les archives canoniques, profils, hashes d'assets et documents d'origine sont
rapprochés avec le workspace enregistré. Les deux modules restent
`private_local_only`. La porte Villa Forge et le tonneau Dungeon Builder
gardent leur provenance Hivemind, leur licence Unity Asset Store et leur
interdiction de redistribution. Aucun upload ni publication n'est effectué.

Les deux modèles sont ajustés, sauvegardés, rouverts et réellement chargés
dans le jeu. Leurs contrôles génériques refusent l'échelle nulle et ne cumulent
pas les aperçus de rotation ; la physique reste indépendante du placement visuel.
Les acteurs, cairns et l'objet témoin de feu ont encore des représentations
génériques. Aucun accord esthétique du propriétaire n'est acquis ; le résultat
qualifie des fonctionnalités de base et ne constitue pas une aventure livrée.

## Historique de la réserve CI — levée le 22 octobre par ADR-0034

### Actualisation du 22 octobre — résultat distant sur le commit publié

Le run [39664002787](https://github.com/Karenax/OpenRPG_Forge/actions/runs/39664002787),
créé le 21 octobre à 19:42:41 UTC sur `6934eb80842dc6f9670b2b9ddde39e7503f2a430`,
est terminé en échec. WASM et supply-chain réussissent. Windows passe formatage,
frontières d'architecture et Clippy, puis échoue pendant l'étape Test : le journal
atteste `There is not enough space on the disk. (os error 112)`, puis des échecs
de liaison/PDB. Les constructions natives suivantes sont sautées. Linux a passé
formatage, frontières et Clippy ; GitHub signale ensuite une perte de communication
avec le runner. Son journal renvoie `404 BlobNotFound` : la cause précise ne peut
pas être affirmée et les étapes internes restées `in_progress` ne sont pas une
preuve de processus encore vivant, puisque le job est terminal.

Ce run a réellement exécuté des étapes : il ne doit pas être classé en refus
de facturation. Il porte sur le commit publié, sans les modifications locales
Gate 5. Les limites locales du workflow (incrémental désactivé, deux jobs Cargo,
symboles dev/test retirés et nettoyage borné des intermédiaires d'installation
des outils sur le seul runner CI) sont déjà présentes dans l'arbre modifié,
mais absentes du workflow de ce run. Elles ne sont ni publiées ni qualifiées
par ce résultat. Aucun test n'est retiré et aucune nouvelle relance distante
n'est effectuée. L'examen statique du bloc PowerShell confirme sa syntaxe,
sans exécuter son nettoyage ni qualifier le workflow complet.

Les données API, annotations, extraits d'erreurs et deux versions du workflow
sont conservés dans `gate5-ci-review-20261022-e-001.zip` et son reçu vérifié,
dans la campagne centrale. Les preuves locales et la réserve de clôture restent
distinctes. Le paragraphe suivant concerne toujours le candidat local Gate 5.

La CI distante de ce candidat n'a pas été exécutée. Ne pas lui attribuer le
motif de facturation d'anciens runs sans nouveau résultat. ADR-0006 ne couvre
que les gates 0–4 ; ADR-0023 concerne seulement Gate 3. Le plan Gate 5 précise
qu'aucune exception distante n'est prolongée implicitement.

Le 22 octobre, le propriétaire retire les contrôles GitHub des critères des
gates et demande leur arrêt, GitHub servant de sauvegarde distante. ADR-0034
consigne cette décision ; la réserve administrative est levée et la Gate 5
est clôturée localement. Le workflow est désactivé sur GitHub et les lancements
automatiques sont retirés du fichier local. La CI du candidat reste NON EXÉCUTÉE, sans
qualifier Linux, Windows vierge, la distribution publique ou une release.
Les réserves générales de performances et de dépendances antérieures restent
dans leurs rapports propres. Aucune Gate 6 n'est ouverte.

Le rapport final référence l'archive ZIP64, son manifeste SHA-256, la lecture
intégrale de vérification et le catalogue de clôture. Aucun original n'est
supprimé et aucune purge par âge n'est prévue.
