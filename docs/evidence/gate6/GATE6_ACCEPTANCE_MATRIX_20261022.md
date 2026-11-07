# Gate 6 — rapprochement des critères et des preuves

Actualisation du 23 octobre 2026 : **clôturée localement**, selon
[le rapport final](GATE6_CLOSURE_20261023.md). Cette matrice ne remplace ni les
reçus ni leurs limites. Exigences reprises de
`docs/planning/MIGRATION_ROADMAP.md`, sections R6-A et R6-B, sans ajouter une
qualification réseau ou esthétique aux critères existants.

Les noms de données ci-dessous sont relatifs à la campagne centrale
`Preuves-et-analyses/actives/gate6-ia-mcp/`, physiquement sur F:.
`assistant-acceptance-audit-001.json` revérifie les reçus nommés, leurs captures,
l'archive de source, les résultats de tests et le binaire du jeu. Son pilote
exact est conservé à côté du reçu. Les manifestes 030 et 031 sont identiques.
L'audit final `assistant-final-acceptance-audit-001.json` étend ce rapprochement
aux 21 critères, au comparatif complet, aux 721 tests et aux entrées 038.
Le reçu central `rapports/gate6-archive-20261023.json` termine la vérification
d'archive après cet audit ; les indicateurs historiques d'attente restent datés.
Le [rapport chronologique](GATE6_PROGRESS_20261022.md) conserve aussi les essais
refusés et les corrections. Les observations UI sont celles de Codex ; elles ne
sont pas une validation humaine du propriétaire.

## Fondations R6-A

| Exigence explicite | Éléments inspectés | Portée et réserve |
| --- | --- | --- |
| Inspection du projet | `selection.rs`, contexte borné au head ; recettes fournisseur et `forge_inspect_selection` MCP | Sélection native autorisée, jamais une permission fournie par le modèle |
| Recherche d'assets | `assistant_assets_native.rs`, tests de recherche sans chemins privés, résolution de pins et refus de corruption ; Vault réel exporté | La recherche ne prouve pas à elle seule qu'un modèle est exportable ; les maisons refusées sont exclues du résultat qualifié |
| Proposition de carte | Création Qwen dans `gate6-content-studio-ui-001`, définitions canoniques sauvegardées | Quête, personnages, lieux et arbre réels ; les trois salles finales et la forêt retouchée sont manuelles |
| Génération de plan | Journaux fournisseur réels et schéma borné dans `assistant/provider.rs` et `planning.rs` | Les propositions MCP de recette sont synthétiques et identifiées comme telles |
| Dry-run | `AuthoringHost` et revue sans mutation ; phase 1 MCP approuvée sans application, workspace identique | Prévisualiser ou approuver ne crée pas de reçu de mutation |
| Approbation | Deux hashes distincts pour données sortantes et diff ; nouvelle revue MCP après redémarrage/conflit | Aucune approbation persistée ou outil MCP d'approbation |
| Application transactionnelle | Reçus canoniques des sources, tests host et coordinateur, sauvegardes UI | Aucun chemin de mutation spécifique au modèle |
| Undo | Tests du host et annulations UI des candidats de maisons puis pièce, retour exact au head antérieur | Les reçus de l'essai refusé restent historiques ; l'annulation ne transforme pas cet essai en réussite |
| Tests | `assistant-kit-export-regression-003.log` et reçu : 721 PASS, 0 échec, 11 ignorés / 78 suites ; Clippy natif 004 et WASM 001 | Ignorés non comptés comme succès ; fournisseur, Vault et UI ont leurs preuves séparées |
| Captures | Captures/arbre des parcours fournisseur, retouches, reprise, quota et MCP ; intérieur natif | Lisibilité fonctionnelle examinée, pas de qualification esthétique générale |
| Audit | Reçus des commandes et journaux jobs/staging liés aux hashes ; source finale et archives identifiées | Un journal Applied seul ne prouve pas une source modifiée ; rapprochement avec le reçu canonique |
| Quotas | Limites persistées, requêtes bornées, `assistant-quota-ui-001.closed.json` et tests | Résultat au plafond conservé, aucune nouvelle tentative/application ; zéro tarif local déclaré ne mesure pas l'électricité |
| Absence de shell arbitraire | `assistant_mcp.rs` : deux outils fermés, inspection et proposition ; `ToolCall` typé ; tests de refus | Pas d'outil shell, fichier, application ou permission ; `tools/call apply` est refusé |

## Parcours R6-B

| Critère | Preuves principales | Conclusion locale et portée |
| --- | --- | --- |
| R6-P01 : brief vers plan fondé sur le projet et les assets disponibles, capacités absentes signalées | Création réelle Qwen, contexte et catalogue bornés ; refus UUID et maisons incompatibles conservés ; `assistant_content`, `assistant_planning`, `assistant_assets` | Preuve réelle de création disponible ; ne pas présenter les refus comme une conformité fournisseur garantie |
| R6-P02 : commandes Studio communes, sélection/propriétés/région dans le diff | Tests `assistant_host`, `assistant_governance`, `assistant_planning` ; diff/UI/application du fournisseur et MCP | Les refus hors périmètre, révision et permissions sont atomiques ; une source de jeu indépendante ne prouve pas seule le parcours UI |
| R6-P03 : aventure de référence, porte et dialogue retouchés, variante locale sans perte après réouverture | Source finale `51a5a525…`, retouches UI puis variante Qwen sur l'arbre ; réouverture, rapports et reçus de conservation | Parcours réel disponible ; reprise et éditions finales de l'environnement hors modèle explicitement distinguées |
| R6-P04 : plan périmé refusé et conflit revalidé avec nouvelle approbation | `assistant-mcp-conflict-ui-001.result.json`, `assistant-mcp-ui-postclose-001.json` ; 115 hauteurs conservées, ancienne commande absente, nouvelle appliquée une fois | Recette Studio réussie sur le binaire `7a9667d5…` ; protocole et règles MCP inchangés depuis, régression finale sur les sources 038 réussie ; nouveaux changements bornés aux réglages manuels, export et fixture |
| R6-P05 : interruption/reprise sans doublon, staging puis promotion selon révision et permissions | `assistant-recovery-ui-002.result.json`, `assistant-recovery-ui-003.result.json`, MCP durable phases 1/2 ; tests `assistant_jobs`, `assistant_jobs_native`, `assistant_coordinator` | La faute contrôlée reprend un vrai résultat historique sans nouvel appel ; rapprochement perdu-ACK, refus de renvoi incertain, perte de source et nouvelle autorisation testés. Une coupure réseau fraîche n'est pas exigée en plus par le critère |
| R6-P06 : étape réelle, fournisseur/données/plafond/retries et arrêt conservant le résultat | UI fournisseur ; plafond historique 2 048/120 s relu malgré configuration 16 384 ; `assistant-quota-ui-001.result.json` et `.closed.json` ; relecture de `assistant_provider_native.rs` | Arrêt et conservation exercés par rejeu contrôlé. Transport sans proxy, redirection ou retry implicite ; délai de génération explicite de 2 à 16 min, connexion bornée séparément |
| R6-P07 : comparaison manuelle/IA en temps, corrections, coût, qualité | `assistant-comparison-review-001.json`, `assistant-kit-export-review-001.json` et [bilan](GATE6_COMPARISON_20261023.md) ; même état initial, signature fonctionnelle, deux solutions et reprises exactes | Comparaison locale réalisée : durées complètes observées, corrections, tokens connus/tarifs déclarés et qualité rapprochés. Modèles et défauts différents, latences/réparations incluses et coûts inconnus explicités ; aucun gain statistique démontré |
| R6-P08 : résultat éditable et jouable hors modèle | Retouches/réouverture, `assistant-reference-native-review-001.json`, `assistant-quest-settings-ui-review-001.json`, `assistant-kit-export-review-001.json` | Jeu et reprises hors modèle prouvés pour les deux parcours ; réglages manuels de récompense et export privé du kit livré qualifiés sans perte de source ni appel fournisseur |

## Candidats et réserves

Le dernier jeu natif qualifié a pour SHA-256
`7e200de1b9718daa10aa93e28ce4cc98a878b96acbeab77851288e3d61553797`.
Il joue le head `51a5a5257c7e6917ee231a0a8532252e7edcc25a52a9f80846f27f6958826aaa` :
trois salles/24 éléments enregistrés, six arbres épinglés, deux solutions,
récompense unique, porte correcte et sauvegardes reprises à l'intérieur. Les
déplacements de cette recette utilisent les commandes normales et le vrai KCC
via le protocole natif de qualification ; ils ne sont pas des marches par clics UI.

Le manifeste physique 030 décrit le correctif de réglages manuels : deux fichiers
nouveaux et quatre modifiés depuis 029, 2 157 entrées / 279 648 972 octets.
Clippy natif/WASM et tests passent. Le frontend 002 et le desktop 001 sont
reconstruits avec succès ; à cette étape, Studio porte le SHA-256
`2e16bbab5d0b89af88e2c0029c366a025ff8dd838a458d21e2d625862487cb40`.
Les manifestes 030 à 033 sont identiques. Le nouveau formulaire est qualifié
séparément par `assistant-quest-settings-ui-review-001.json` et trois couples
capture/arbre ; les deux processus de recette sont fermés normalement.

Le parcours comparatif complet, l'audit des 21 critères, le rapport de clôture
et l'archivage ZIP64 vérifié/catalogué sont achevés le 23 octobre. Le nouveau
Studio qualifié `f23467af…` et les manifestes 036/037/038 identiques couvrent le
résolveur du kit livré, son raccordement et les fixtures corrigées, avec le même
jeu natif. L'archive de 1 239 fichiers / 259 013 369 octets sources mesure
198 331 004 octets ; chaque hash et la source inchangée sont revérifiés, catalogues
JSON/TSV contrôlés, originaux conservés. L'indisponibilité Qwen
constatée à 21:45 UTC reste une limite actuelle de disponibilité ; elle n'annule
pas les appels réels antérieurs et ne permet pas d'inventer un nouvel appel réussi.
Ni publication Git, ni ouverture de gate suivante, ni qualification distante ne
sont impliquées.

L'inventaire séquentiel `assistant-campaign-volume-001.json` mesure 1 011 fichiers,
192 918 682 octets, sans lien à suivre. Il reste sous les budgets initial
2 Gio/2 000 fichiers et propriétaire 20 Gio/50 000 fichiers ; il précède les
derniers reçus et ne vaut pas inventaire final d'archive. Les chemins Windows
longs sont pris en charge après un premier refus de parcours WinError 3.
Les originaux restent conservés.

`assistant-comparison-provider-inputs-001.json` distingue les quatre journaux
Studio de la source de référence : les réponses stagées déclarent en tout
8 197 tokens d'entrée et 12 835 de sortie. Ces totaux ne comprennent pas des
tokens éventuellement consommés par des appels sans réponse comptabilisée.
Le lancement jusqu'à la première création sauvegardée est observé sur
768,538756 s, dont 318,634 s entre clic Générer et observation Staged ; les
retouches, la variante, les essais de bâtiments refusés à l'export et le monde
physique final sont des phases supplémentaires. Aucun de ces nombres ne mesure
à lui seul la durée complète du parcours IA assisté ou son gain sur le manuel.

Les contrôles `assistant-supply-audit-001` et `assistant-supply-deny-001` sortent
avec code 0, politique inchangée ; `assistant-supply-review-001.json` conserve
les détails. Audit rapporte zéro entrée dans sa liste de vulnérabilités, neuf
avis de maintenance et l'avis unsound GLib 0.18.5 `RUSTSEC-2024-0429`.
Ce dernier reste une réserve historique ; aucun « sécurité entièrement verte »
ni qualification de distribution n'est déduit du code de sortie. Le contrôle
deny valide licences, sources, bans et advisories selon la configuration actuelle,
avec des avertissements de versions multiples conservés.
