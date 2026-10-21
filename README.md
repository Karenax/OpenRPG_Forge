<div align="center">

# OpenRPG Forge

### Créons ensemble les outils pour donner vie à nos mondes.

**Un projet communautaire de création d’un éditeur de jeux et d’un moteur RPG 3D sur mesure.**

Imaginez un territoire. Façonnez ses lieux, ses personnages et ses règles.
Construisons ensemble l’atelier qui permettra d’en faire une aventure jouable.

[**Site officiel — karenax.net/openrpgforge**](https://karenax.net/openrpgforge/) · [**Contribuer**](#rejoindre-la-forge) · [**Explorer la roadmap**](docs/planning/MIGRATION_ROADMAP.md) · [**Démarrer avec les sources**](docs/HANDOFF.md)

![Le véritable Studio OpenRPG Forge, avec la carte Tidewater, son inspecteur et sa bibliothèque de décors](https://karenax.net/openrpgforge/assets/captures/studio-preview.webp)

*Capture réelle du Studio — version en développement, 19 octobre 2026.*

</div>

## Votre monde. Vos règles. Notre Forge.

OpenRPG Forge porte une ambition : permettre aux créateurs de construire des jeux de rôle
3D, de façonner leurs univers et de partager leurs aventures. Un même parcours relie
l’édition des cartes, les données de jeu, les contenus et l’exécution dans un jeu séparé.

Nous voulons en faire **un projet communautaire**, auquel développeurs, artistes,
auteurs, concepteurs de règles et testeurs peuvent apporter leur savoir-faire.
L’objectif : des outils compréhensibles, des contenus retouchables et des mondes
qui restent entre les mains de leurs créateurs.

### Un moteur maison, une nouvelle base

Le moteur RPG et le Studio sont conçus pour OpenRPG Forge. La réécriture Rust est
**développée depuis une base neuve (« from scratch »)**, sur une branche indépendante
ouverte le 30 août 2026. Son architecture, ses formats de monde, ses règles métier,
ses outils d’édition et ses systèmes propres sont construits pour ce projet.

Cette création s’appuie sur **Bevy** pour le socle moteur et l’ECS, **wgpu/WebGPU**
pour l’accès graphique et **Rapier** pour la physique. **Tauri et Leptos** portent
l’interface du Studio. Ces fondations et les techniques tierces restent créditées :
« from scratch » désigne la nouvelle implémentation Forge, et ne signifie pas que
chaque bibliothèque, moteur physique ou couche graphique a été réécrit.

**Plus de 50 milliards de tokens mobilisés dans le travail de conception et de
développement assisté par IA**, selon le chiffre communiqué par le créateur du
projet le 21 octobre 2026. Ce volume déclaré décrit l’effort de développement ;
il ne mesure ni la taille du code, ni la qualité, ni le nombre de fonctionnalités livrées.

### De l’atelier au monde jouable

| Créer | Organiser | Jouer |
|---|---|---|
| Éditer les cartes, placer les décors, retoucher le monde et préparer ses données RPG dans le Studio. | Structurer des modules, gérer les dépendances et conserver la provenance et les droits des assets. | Lancer le jeu natif séparé, éprouver les parcours RPG documentés, sauvegarder et reprendre. |

Le parcours de création doit rester **réalisable sans IA**. L’ambition à terme est
aussi d’aider à décrire, générer et régénérer des contenus tout en conservant les
retouches de l’auteur. L’assistance IA complète et la narration avancée restent
des objectifs de la roadmap.

[![Tidewater : plage, village côtier et relief dans le moteur](https://karenax.net/openrpgforge/assets/captures/plage-preview.webp)](https://karenax.net/openrpgforge/#studio)

*Capture réelle de Tidewater — carte de démonstration en développement, 19 octobre 2026.
Les contenus de démonstration et leurs droits restent distincts du code du moteur.*

## Rejoindre la Forge

**Vous pouvez contribuer à la prochaine étape.** Pas besoin de travailler sur tout
le moteur pour rendre le projet meilleur.

- **Développement** : Rust, rendu, outils d’édition, formats, tests et ergonomie du Studio.
- **Création** : retours d’usage, conception de règles, documentation et exemples originaux.
- **Art et technique** : modèles, matériaux, animation et préparation d’assets dont les droits permettent la contribution.
- **Tests** : reproductions de bugs, parcours de création et retours précis sur votre configuration.

Pour commencer, consultez les [issues](https://github.com/Karenax/OpenRPG_Forge/issues),
ouvrez un sujet concret ou proposez une amélioration ciblée. Le [guide de reprise](docs/HANDOFF.md)
explique comment travailler avec les sources ; les [règles de contribution](AGENTS.md)
précisent l’architecture, les validations et les limites à respecter.
Vérifiez les licences applicables au code et aux contenus avant toute réutilisation
ou contribution ; les assets tiers et privés conservent leurs propres droits.

**Une étoile aide à faire découvrir le projet. Un retour concret aide à le construire.**

---

## Où en est le projet ?

Au **21 octobre 2026**, les étapes jusqu’à Gate 4 disposent de leurs clôtures
documentées, avec leurs périmètres et réserves. **Gate 5 est en cours** : elle
approfondit les règles et les mécanismes génériques du Creator et du runtime.
Le projet reste en développement ; la distribution générale, les performances
sur tous les matériels et la qualification multiplateforme restent à établir.

Les sections suivantes donnent les détails techniques et les preuves datées.
Le [contexte canonique](PROJECT_CONTEXT.md) et la [roadmap](docs/planning/MIGRATION_ROADMAP.md)
restent les références pour l’état courant.

Validation du 18 octobre : [rapport de séparation moteur/carte](docs/evidence/architecture/STRICT_MAP_SEPARATION_20261018.md) ;
14 contrôles réussis et 2 232 tests du workspace réussis. Les preuves de runtime
générique et des extensions optiques gardent des périmètres distincts.
Le dernier parcours natif instrumenté de la carte conserve une réserve de cadence
(44,81 FPS moyens pour un seuil de 60) ; ses contrôles fonctionnels passent.

OpenRPG Forge est un moteur et un atelier de création de RPG 3D en **Rust**, avec
**Bevy, wgpu/WebGPU et Rapier**. Le Studio utilise **Tauri 2 et Leptos/WASM**.
La branche maintenue et définie par défaut sur GitHub est `Rust`.

Le projet vise un parcours complet : créer un monde avec ou sans IA, retoucher
précisément cartes, personnages et histoire, conserver ces corrections lors des
régénérations, puis jouer, sauvegarder et distribuer une aventure. Cette vision
reste distincte des capacités actuellement qualifiées.

## État au 18 octobre 2026

| Étape | État documenté |
|---|---|
| Gate C — référence Cadle | Qualifiée selon son protocole et ses limites |
| Gate 0 — fondations natives, WebGPU et Studio | Passée sur preuves locales et décisions documentées |
| Gate 1 — rendu et Visual Valley | Clôturée localement sous dérogations |
| Gate 2 — édition de monde | Clôturée par validation propriétaire |
| Gate 3 — première boucle RPG | Clôturée le 15 octobre |
| Gate 4 — assets gouvernés, modules et installation | Clôturée localement sous Windows le 16 octobre |

Le lot Gate 4 est `0.1.0-preview.20261016.019`. Les rapports
[Gate 3](docs/evidence/gate3/GATE3_CLOSURE_20261015.md) et
[Gate 4](docs/evidence/gate4/GATE4_CLOSURE_20261016.md) précisent les versions,
critères, preuves et réserves. Gate 5 a ensuite été ouverte le 19 octobre 2026 et reste en cours ;
ces clôtures ne qualifient pas ses critères. Le [contexte canonique](PROJECT_CONTEXT.md) et la
[roadmap](docs/planning/MIGRATION_ROADMAP.md) portent le suivi détaillé.

La [reprise d’architecture du 18 octobre](docs/planning/ENGINE_RESERVES_20261018.md)
consigne la correction des diagnostics Studio WebGPU et les nouvelles API communes
de locomotion, animation et optique, avec captures natives et WebGPU indépendantes.
La réserve Clippy WASM est levée. La généricité globale reste à terminer ; les
contrôles et captures de cette reprise sont distingués des anciennes qualifications.

La [séparation stricte moteur/carte](docs/ADR/ADR-0026-strict-engine-map-separation.md)
retire ensuite de `forge_engine` et de `forge_assets` les ports, shaders,
acquisitions, fixtures et notices Tidewater. Ils appartiennent aux composants
facultatifs `forge_tidewater` et `forge_tidewater_assets`, qui utilisent les
contrats génériques. Le moteur n'en dépend pas et ne conserve aucun alias de
leurs anciennes API. Cette séparation est contrôlée automatiquement.

## Parcours et capacités actuels

- **Studio** : navigation par module et carte, édition du monde, retouches,
  sauvegarde et création des données RPG selon les périmètres qualifiés.
- **Jeu natif séparé** : dialogue, quête, combat et sauvegarde/reprise dans les
  parcours documentés. Le Studio conserve sa carte pendant la partie puis
  reprend son viewport au retour.
- **Assets gouvernés** : imports GLB, textures et audio, provenance, licences,
  empreintes, variantes et validation de publication.
- **Modules** : packages `.orpmod` v2, dépendances, contrôles d'archives,
  installation et régression avec un second module.
- **Livraison Windows locale** : un installateur principal, un launcher Aube,
  un gestionnaire de contenus, le Studio et le jeu, avec contenus séparables.

Ces capacités sont rattachées aux scénarios et composants des rapports de gate.
Elles ne constituent pas une qualification générale de tous les modules,
matériels ou usages futurs. L'assistant IA complet, la narration avancée,
le scripting et le multijoueur restent suivis par la roadmap.

## Limites de qualification

**La qualification complète des capacités reste ouverte.** Les ports de référence
sont maintenant hors du moteur, dans le composant de carte. Leur extraction ne
qualifie pas tous les effets sur tous les contenus. La clôture des parcours RPG
et de packaging ne solde pas cette exigence. Voir l'[analyse et les corrections de
dette d'architecture](docs/planning/ENGINE_ARCHITECTURE_DEBT_20261017.md), qui
distingue les extractions réalisées, les contrôles et le travail restant.

La clôture locale ne vaut pas qualification de distribution générale, de
Windows vierge ou de Linux. La réserve `RUSTSEC-2024-0429` concernant GLib
reste documentée ; aucune performance générale ou acceptation artistique n'est
inférée des seuls tests automatisés.

Le run Gate 3 du 15 octobre documenté pour cette clôture est
**NOT RUN — infrastructure billing**.
[ADR-0023](docs/ADR/ADR-0023-gate3-owner-removes-github-ci-criterion.md) retire
l'attente GitHub des critères de cette clôture Gate 3, sans produire de PASS
distant. Les anciennes décisions de CI gardent leur portée datée.

## Reprendre le développement

Commencer par le **[guide de reprise hors Codex](docs/HANDOFF.md)**. Il décrit
les prérequis, les procédures de compilation et validation, les données hors Git,
le lancement du poste actuel et le travail local à préserver.

Le développement ne nécessite ni Codex ni OpenViking. Un clone contient les
sources versionnées ; les cartes privées, le Vault, certains assets et les
preuves locales doivent être restaurés séparément pour reproduire leur parcours.
Les chemins du poste du propriétaire ne sont pas des valeurs universelles.

Documents de référence :

- [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) : vision et état canonique.
- [AGENTS.md](AGENTS.md) : architecture et règles de contribution.
- [Décisions d'architecture](docs/ADR) : choix et dérogations explicites.
- [Roadmap](docs/planning/MIGRATION_ROADMAP.md) : critères et progression.
- [Studio](apps/forge_studio/README.md) : parcours et détails du sous-système.
- [Livraison Windows](scripts/delivery/README.md) : reconstruction et reçus.

Les documents sont versionnés avec les sources ; les sections historiques ne
remplacent pas les états plus récents. Une modification locale doit être
commitée puis poussée pour apparaître sur GitHub.

## Architecture et historique

Les documents et règles métier Forge restent indépendants de Bevy, Tauri et
Rapier. Les adapters projettent les données versionnées vers le rendu, la
physique et l'interface ; les entités moteur ne sont pas le format canonique.

La réécriture Rust a commencé le 30 août 2026 sur une branche orpheline.
L'ancien projet C#/Godot est une référence historique, identifiée notamment par
le commit `a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`. Il ne constitue pas une
dépendance d'exécution du produit Rust. Toute reprise suit la
[matrice historique](docs/migration/LEGACY_REUSE_MATRIX.md).

Cadle sert de référence de techniques selon les ADR et leurs contraintes de
licence. Forge doit compiler et fonctionner sans son clone ; aucun asset Cadle
n'est publié dans le produit.
