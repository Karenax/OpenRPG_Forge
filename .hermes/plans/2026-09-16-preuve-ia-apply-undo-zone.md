# Preuve IA bornée — proposition, Apply et Undo sur une zone

**Statut : conception uniquement ; aucun test exécuté, aucune fonctionnalité implémentée, aucune gate ouverte ou clôturée par ce document.**

**Objectif :** vérifier qu'une proposition IA peut déplacer un placement existant par les commandes normales du Studio, après aperçu et approbation, puis être annulée sans perte des autres retouches.

**Architecture :** l'IA propose une intention bornée ; un adaptateur déterministe la valide et prépare une commande Forge. Le Studio conserve l'approbation et appelle le protocole existant. Ni accès fichier, ni shell, ni écriture directe de la tête de zone par le modèle.

**Pile :** Rust, forge_project, Studio Tauri/Leptos, viewport Bevy/WebGPU.

## 1. Portée et contexte

Référence : `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, branche Rust avec changements locaux préexistants. Ce document seul est ajouté ; aucun code, document canonique ou travail Codex ne doit être modifié pour sa rédaction.

Le contexte canonique consulté le 16 septembre indique R2-020 terminé localement sur candidat 1303A840 ; Gate 2 demeure ouverte. Cette preuve future ne requalifie pas R2-020 et ne constitue pas la Gate 6 complète.

On réduit volontairement le scénario « clairière, trois maisons et chemin » à UNE mutation observable. Création de monde, narration, multijoueur, recherche distante d'assets et orchestration multi-commandes sont exclus.

### Deux niveaux de preuve à ne pas confondre

- **A — contrat déterministe :** proposition fixe utilisée pour tester validation, aperçu, application et annulation. Ce niveau ne prouve aucune capacité d'IA.
- **B — parcours IA réel :** un fournisseur réellement appelé produit la proposition depuis le brief ; même chaîne de validation et même parcours Studio. Modèle, fournisseur, entrées autorisées, latence et coût connu sont consignés. En cas d'indisponibilité, verdict BLOQUÉ, jamais une réponse synthétique présentée comme réelle.

Le présent livrable spécifie ces niveaux ; leur implémentation exige une autorisation distincte, notamment si elle anticipe Gate 6.

## 2. Sources vérifiées et limites

| Source existante | Ce qui a été constaté en lecture |
|---|---|
| `crates/forge_project/src/authoring.rs` | `AuthoringRequest::Inspect`, `PreviewZone`, `ApplyZone`, `Save`, `Open`, `StartPlaytest` sont déclarés. |
| `crates/forge_project/src/zone_authoring/session.rs` | `ZoneRequest` porte command_id, expected_revision, expected_hash et action ; `ZoneAction` expose Edit, Regenerate, ReconcilePromotions, Undo et Redo. Les reçus portent inverse, révision et hash résultants. |
| `crates/forge_project/src/zone_authoring/placements.rs` | Le symbole `prepare_placement` existe ; sa signature et les préconditions détaillées devront être vérifiées au moment de l'implémentation. |
| `PROJECT_CONTEXT.md` | L'état récent sépare Gate 2 active, R2-020 terminé localement et gates ultérieures non ouvertes. |

Ces constats ne démontrent ni un adaptateur IA disponible, ni une API MCP Rust exposée, ni les comportements d'exécution du candidat courant. Les outils MCP Solaren historiques ne sont pas utilisés.

## 3. Fixture minimale

Préparer ultérieurement par les outils gouvernés du Studio une copie isolée d'une zone de test, sans modifier un module utilisateur :

- deux placements existants A et B avec IDs et assets valides ;
- A sélectionné, non verrouillé, sur une surface plane, destination libre ;
- B déjà retouché, servant de témoin de conservation ;
- transform de A, corrections de B, recette, terrain, références d'assets et verrous enregistrés comme référence sémantique S0 ;
- archive initiale sauvegardée et empreinte de ses octets notée D0 ;
- hash de tête H0 et révision R0 inspectés depuis l'autorité Forge.

Les IDs, coordonnées et hashes sont acquis à l'exécution, jamais inventés dans le rapport. La translation demandée doit rester dans la zone, sans changement implicite de Y, rotation ou échelle.

## 4. Brief et proposition bornée

Brief : « Déplace uniquement l'objet sélectionné de deux mètres vers +X. Ne change ni sa hauteur, ni son orientation, ni son échelle, ni les autres objets. Montre-moi le changement avant de l'appliquer. »

L'IA reçoit seulement le résumé nécessaire : sélection A, unité métrique et axes, transform actuel, limites de la zone, capacités permises et identité du contexte. Aucune clé, aucun chemin utilisateur ni bibliothèque entière n'est transmis.

Proposition conceptuelle attendue : cible A, opération translation, delta X +2 m, delta Y/Z 0. Ce n'est pas le schéma JSON actuel de Forge ni une nouvelle commande canonique.

L'adaptateur ne demande pas au modèle de générer `ZoneCorrections` complet. Il part de la tête autoritaire, utilise le préparateur de placement, conserve toutes les autres corrections et lie la requête à H0/R0. L'ID de commande est géré par l'application. Une proposition hors périmètre est refusée, pas réparée silencieusement.

## 5. Un scénario de bout en bout

1. Ouvrir la copie isolée dans un candidat Studio identifié ; relever S0, H0, R0 et D0.
2. Soumettre le brief : proposition fixe au niveau A, appel fournisseur réel au niveau B.
3. Valider la proposition et préparer la requête avec le mécanisme partagé de placement.
4. Appeler PreviewZone et afficher le diff : A seul, X initial → X initial + 2 m.
5. Vérifier qu'aucune tête acceptée ni archive n'a changé ; l'aperçu graphique reste temporaire.
6. Faire approuver explicitement ce diff dans le Studio, puis appliquer exactement la requête approuvée via ApplyZone.
7. Vérifier le déplacement, le reçu et l'absence de modification de B et des autres domaines. Sans Save, D0 doit rester inchangé.
8. Déclencher Undo par le parcours normal du Studio avec l'identité courante. Vérifier le retour sémantique à S0.
9. Sauvegarder, fermer et rouvrir la copie. Vérifier S0 à nouveau et la conservation des références.
10. Fermer uniquement les processus/onglets possédés par l'essai, puis vérifier leur terminaison.

**Attention :** Undo ajoute ou déplace l'état d'historique et peut faire évoluer la révision. On exige le retour au contenu sémantique S0, pas l'égalité aveugle de la révision, du hash de tête ou des octets de l'archive initiale après Save.

## 6. Critères d'acceptation exacts

- **AC-01 — Périmètre :** la proposition ne cible que A et ne demande que X +2 m ; tout autre ID, axe, attribut ou opération est refusé avant mutation.
- **AC-02 — Aperçu sans mutation :** après PreviewZone, la tête acceptée reste H0/R0 et les octets sauvegardés restent D0 ; aucun Apply implicite.
- **AC-03 — Consentement lié :** Apply n'est possible qu'après approbation du diff exact pour la requête et la tête examinées. Une nouvelle proposition ou tête invalide cette approbation.
- **AC-04 — Application exacte :** X(A) devient X0(A)+2 m ; Y, Z, rotation, échelle, ID et asset de A restent identiques. B, terrain, recette, verrous et autres corrections restent sémantiquement identiques.
- **AC-05 — Annulation complète :** un Undo restitue S0 pour le contenu auteur ; la révision et l'historique peuvent différer.
- **AC-06 — Persistance :** après Undo, Save, fermeture et réouverture, le contenu auteur est égal à S0 et les références restent valides.
- **AC-07 — Plan périmé :** si une commande humaine modifie la tête après Preview et avant Apply, l'ancienne requête est refusée sans mutation ; relecture, nouvel aperçu et nouvelle approbation sont requis.
- **AC-08 — Abandon :** annuler l'aperçu restaure la présentation acceptée, ne modifie ni tête ni archive et rend l'approbation précédente inutilisable.
- **AC-09 — Refus sûr :** cible inconnue, cible verrouillée, valeur non finie ou destination hors zone échouent explicitement sans écriture ; les erreurs restent lisibles dans le Studio.
- **AC-10 — Véritable preuve IA :** le niveau B consigne un appel fournisseur réel et sa sortie réellement reçue ; une fixture ne peut satisfaire ce critère.
- **AC-11 — Preuve visuelle :** captures inspectées avant/aperçu/après/Undo et réouverture dans la vraie WebView Tauri ; un test CPU ou Chrome seul ne suffit pas.

Tolérance numérique : les champs non modifiés sont comparés selon leurs valeurs canoniques exactes ; pour X calculé, utiliser la règle numérique déjà retenue par le domaine, documentée avant le test, sans tolérance ad hoc pour masquer un échec.

## 7. Dossier de résultat attendu lors de l'exécution future

- Candidat binaire/frontend et identités des entrées, contexte local et versions réellement utilisés.
- Commandes et reçus réels, identités de tête à chaque étape, différence sémantique S0/après/Undo/réouverture.
- Hash des octets sauvegardés avant toute écriture et après les étapes Save.
- Captures Tauri et observation humaine ou automatisée explicitement distinguées.
- Pour B : fournisseur/modèle, contexte transmis expurgé, sortie reçue, temps d'attente, usage/coût quand disponibles, erreurs et interventions manuelles.
- Tableau AC-01 à AC-11 : PASS, FAIL, BLOQUÉ ou NON EXÉCUTÉ, avec référence de preuve pour chaque verdict.

Aucun PASS global si un critère requis échoue ou manque. Aucun gain de productivité déduit d'un seul essai : une comparaison au déplacement manuel serait une campagne distincte.

## 8. Travail futur proposé, non lancé

1. Figer avec le propriétaire l'autorisation et le périmètre de la preuve ; ne pas interrompre ni modifier le lot Codex actif.
2. Vérifier le contrat du préparateur et le point d'entrée partagé Studio ; déterminer si un adaptateur suffit ou si une capacité produit manque.
3. Écrire les tests déterministes échouants pour AC-01 à AC-09, puis implémenter le minimum et rejouer les tests ciblés. Noms de tests et commandes exactes à fixer après cette inspection, pas de cible Cargo inventée ici.
4. Exercer A dans Tauri. Si ses contrats échouent, corriger avant tout appel IA.
5. Ajouter, avec autorisation, un seul fournisseur et un appel borné ; plafond de coût explicite, aucune boucle autonome, pas de retry payant silencieux.
6. Exercer B et remplir le dossier. Décider ensuite si la preuve justifie d'élargir à un chemin, une maison ou une région.

Fichiers de code candidats à examiner, pas modifications prescrites : `crates/forge_project/src/zone_authoring/placements.rs`, `crates/forge_project/src/zone_authoring/session.rs`, `crates/forge_project/src/authoring.rs`, `apps/forge_studio/src/authoring_preview.rs`, `apps/forge_studio/src/authoring_history.rs`. Le chemin d'un éventuel adaptateur IA sera décidé séparément ; ne pas créer un second bus de commandes.

## 9. Risques et choix restant à décider

- Le reçu existant ne prouve pas, à lui seul, la liaison UI approbation/diff : la vérifier explicitement.
- Une commande de déplacement ne valide ni la qualité artistique d'une génération ni la création d'un RPG complet.
- Ne pas ajouter une transaction multi-commandes, une marketplace ou un MCP complet pour cette preuve.
- Le choix du fournisseur, le consentement de transmission et le budget ne sont pas décidés par ce document.
- Si une lacune touche les fichiers du lot Codex actif, consigner le besoin ; aucune correction parallèle implicite.

**Livrable présent : cette spécification uniquement. Tous les critères AC restent NON EXÉCUTÉS dans ce tour.**
