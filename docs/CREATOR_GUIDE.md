# Guide créateur minimal — OpenRPG Forge

## 1. Créer ou ouvrir un module

Lancer l'application de création, ouvrir l'écran **Forge**, puis choisir **+ Nouveau module**. Donner
un titre, une description et le nom de la première zone. Choisir **Intérieur** pour une pièce ou un
donjon compact, ou **Extérieur** pour un terrain sculptable ; la sélection propose respectivement
32 × 32 m ou 128 × 128 m, mais la largeur et la profondeur restent modifiables avant la création.
Sélectionner ensuite un dossier parent vide et valider. Forge crée un workspace `1.22.0` contenant :

```text
mon-module/
├─ forge.project.json
└─ project.json
```

Un sol provisoire, un blueprint de point de départ et leur instance sont fournis. Une zone extérieure
active aussi immédiatement son heightmap canonique aux dimensions choisies. Un projet existant
s'ouvre avec **Choisir un dossier Forge**.

Depuis **Factions avancées**, définir les factions parentes, choisir précisément les tags,
politiques et relations hérités, puis renseigner témoins, affiliations et diplomatie dynamique dans
les onglets dédiés. **Prévisualiser** affiche diagnostics et diff sans mutation ; **Appliquer** crée
une commande annulable. Les transitions dynamiques restent autoritaires côté serveur.

## 2. Construire la zone

Choisir un blueprint dans la palette, saisir X/Z et la taille de grille, puis **Placer**. La sélection
permet de déplacer sur les deux axes, tourner de 90°, supprimer et annuler/rétablir chaque commande.
Les propriétés locales servent à spécialiser une instance sans dupliquer son blueprint ; les chemins
ForgeScript sont des chemins relatifs `.forge` tels que `scripts/interaction.forge`.

Pour une zone extérieure, ouvrir **Zones & transitions**, puis **Terrain & volumes…** et l’onglet
**Sculpture**. Choisir lever, abaisser, aplanir, lisser, peindre un matériau, ouvrir ou refermer,
régler le centre X/Z, le rayon et l’intensité, puis prévisualiser le nombre d’échantillons ou cellules
touchés. Le matériau « Automatique » rend la cellule aux recettes de biome. **Appliquer le trait** crée
une commande indépendante : les boutons Annuler/Rétablir restaurent le heightmap et ses trous, et le
viewport, les couleurs de matériau, les collisions et la navigation sont reconstruits depuis ce même
document canonique.

Dans l’onglet **Terrain & biomes**, les champs **Asset prairie** et **Asset roche** proposent les
modèles gouvernés installés depuis le Content Browser. Une cellule peinte avec la brosse
**Matériau / biome** force localement la recette correspondante et sa dispersion ; une cellule
automatique continue d’utiliser altitude, pente et exclusions. L’aperçu et le jeu utilisent le même
plan déterministe, le même manifeste et le même contrôle SHA-256.

Le kit Crypte (sol, mur, porte, pilier, personnages et triggers) reste disponible dans le module de
démonstration ; les outils de biome forestier et leurs assets CC0 restent disponibles comme briques
réutilisables pour créer un module extérieur.

Pour une créature, ouvrir **Atelier RPG Kit**, puis **Présenter une créature** pour les portraits,
parties du corps, couleurs, appendices et voix. **Progression & pouvoirs** ouvre ensuite les parcours
multiclasses, axes de disposition propres au projet, compétences, talents et loadout. Chaque écran
prévisualise la transaction, affiche les diagnostics et permet Annuler/Rétablir avant sauvegarde.
Les pouvoirs appris ou accordés par un talent doivent déjà exister dans le profil **Combat** du
blueprint ; le résumé latéral confirme le niveau total et la résolution des références.

**Butin, vol & cadavres** configure ensuite chaque objet droppable, son slot, sa quantité, sa
probabilité, son propriétaire et son instanciation. Les onglets suivants règlent le vol à la tire
(compétences, perception, consentement serveur, crime et délai) puis la dépouille (conteneur,
ragdoll, durée, résurrection et nettoyage). Un objet doit déjà figurer dans la table **Combat** de
la créature afin que la référence soit validée ; les compétences de vol doivent exister dans son
profil **Progression**.

**Rôle, activité & auto-level** complète ensuite la créature. L’onglet Rôle & lore conserve des
champs définis par le ruleset sans leur donner de bonus implicite. Activité ambiante combine sets
d’animations pondérés, horaires, interactions et interruptions. Auto-level affiche le diff produit
par une seed avant de l’appliquer ; activer le verrouillage manuel garantit qu’aucun rang ne change.

## 3. Écrire les dialogues et scripts

**Dialogues** ouvre l'éditeur de graphe simple : nœuds localisés, choix, conditions, actions et aperçu.
Les références manquantes et nœuds inaccessibles bloquent l'application. Un script ForgeScript
déclare `void main()` et n'accède qu'à l'API allowlistée documentée dans
`docs/FORGESCRIPT_API.md`. Les erreurs de compilation et de quota restent diagnostiquées sans arrêter
le runtime.

Le workspace **Scripts** ouvre l'analyse **Scripts & Build incrémental** : ressources, liens,
hashes, licences, packs externes et impact du prochain export. `REBUILD` désigne ce qui sera
recalculé, `CACHE` ce qui peut être réutilisé. L'analyse est en lecture seule et ne modifie jamais le
module.

**Monde vivant** ouvre le studio des comportements avancés de la carte. Sélectionnez d'abord le
blueprint ou la zone cible, puis utilisez l'onglet concerné : Placeables, Portes, Triggers,
Rencontres, Routes, Marchands, Compagnons ou Scripts. Les champs expliquent leur portée et le résumé
montre le contrat runtime. **Prévisualiser** calcule diagnostics et diff sans écrire ; **Appliquer**
crée une commande révisionnée que l'historique peut annuler/rétablir. Les anciennes cartes sans
catalogue restent compatibles. La Crypte charge un profil de démonstration uniquement lorsqu'elle
détecte son ancien catalogue vide ; tout contenu créé dans le studio est prioritaire.

Le workspace **Validation** ouvre **Validation & distribution**. Les cinq profils Editor, Client,
Serveur dédié, Test et Publication y sont persistés avec leurs réglages. Le même écran gère le
registre des dépendances (version, licence, responsable, justification, cadence et retrait), affiche
les diagnostics bloquants et choisit la cible du prochain export. Le champ « profil de secrets » est
une référence : aucune clé ou valeur secrète ne doit être saisie dans le projet.

## 4. Tester et sauvegarder

Choisir un point de départ puis **▶ Jouer depuis ici**. Le snapshot de playtest est temporaire et la
console Forge récupère les logs au retour. Dans la Crypte : ZQSD/flèches déplacent, E interagit,
ESPACE attaque, E fouille aussi les dépouilles à portée, F5 sauvegarde et F9 recharge le checkpoint rapide. Les fichiers `.orpsave` sont
atomiques, versionnés et liés à l'identité ainsi qu'au hash du module.

## 5. Exporter et valider

Sauvegarder puis cliquer **Exporter .orpmod**. Forge crée l'archive atomiquement et la valide avant de
confirmer. Équivalent en ligne de commande :

```powershell
dotnet run --project tools/Forge.Cli -- package export <workspace> <module.orpmod>
dotnet run --project tools/Forge.Cli -- package export <workspace> <module.orpmod> --target shipping
dotnet run --project tools/Forge.Cli -- package validate <module.orpmod>
dotnet run --project tools/Forge.Cli -- package analyze <workspace>
dotnet run --project tools/Forge.Cli -- package compliance <workspace> shipping
```

Le runtime accepte ensuite `--module-package <chemin-absolu.orpmod>`. Les chemins traversants,
extensions natives, ratios de compression suspects, doublons de données et hashes incohérents sont
refusés avant extraction.

Editor et Test restent utilisables pendant la création avec des métadonnées incomplètes. Client,
Serveur dédié et Publication refusent en revanche toute licence ou provenance inconnue avant de
créer l’archive. Chaque package contient un `THIRD-PARTY-NOTICES.txt` généré et revérifié.

## 6. Demander une modification IA

Lancer le host MCP :

```powershell
dotnet run --project apps/Forge.McpHost -- --workspace <workspace>
```

Le client commence par lire le résumé, la révision et les diagnostics. Pour un dialogue, il soumet un
plan : validation et dry-run produisent diff, inverse et `planId`; l'application n'est possible que si
la révision correspond encore. `revert_dialogue_plan` annule le dernier plan appliqué. Aucun outil de
shell ou d'écriture libre n'est exposé.

Les outils `query_world_features` et `preview_world_features` donnent le même accès filtré au Monde
vivant. Ils retournent des contrats typés et une prévisualisation ; l'application reste soumise à la
commande, au `planId` et à la révision du projet.

## 7. Gate reproductible

Depuis la racine :

```powershell
.\scripts\Verify.ps1
.\scripts\Measure-RuntimePerformance.ps1
```

Le premier contrôle restore, format, build, tests, graphe, branding, éditeur, export, MCP, runtime et
sauvegarde/reprise. Le second exécute plusieurs démarrages headless et écrit le rapport brut dans
`artifacts/performance/runtime-performance.json`.

## 8. Configurer la production post-lancement

Dans le RPG Kit, ouvrir **Production & expérience**. Les six onglets couvrent mobilité, succès et
contenu, opérations Live Director, réseau, plateforme et automatisation. Prévisualiser affiche le
diff sans écrire ; Appliquer sauvegarde une commande révisionnée, annulable et rétablissable.

Les champs réseau et production décrivent une politique, pas des secrets : licences autorisées,
capacités d'extension, versions de protocole, providers média et budgets de soak. Les services,
jetons et chemins propres à la machine restent hors du module.
