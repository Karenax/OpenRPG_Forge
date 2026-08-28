namespace Forge.Studio.Core.Navigation;

public static class StudioPageCatalog
{
    public static IReadOnlyList<StudioSectionDefinition> Sections { get; } =
    [
        Section("world", "Monde", "MONDE",
            Page("zones", "Zones & scène", "Retoucher la zone active sans quitter la 3D.",
                StudioPageLayout.Spatial, "Ajouter un objet", "ZONES ET CALQUES", "VUE 3D DE LA ZONE", "SÉLECTION"),
            Page("terrain", "Terrain & biomes", "Sculpter, peindre et relire le terrain dans son contexte.",
                StudioPageLayout.Spatial, "Choisir une brosse", "BROSSES ET BIOMES", "TERRAIN 3D", "BROSSE ACTIVE"),
            Page("world-generation", "Génération de monde", "Prévisualiser, exécuter et promouvoir une recette gouvernée.",
                StudioPageLayout.Specialized, "Préparer une recette", "RECETTE ET JOBS", "PIPELINE GOUVERNÉ", "PREUVE ET PROMOTION"),
            Page("placement", "Objets & interactions", "Placer portes, triggers, rencontres et décor par manipulation directe.",
                StudioPageLayout.Spatial, "Placer", "PALETTE CONTEXTUELLE", "SCÈNE ET INTERACTIONS", "OBJET SÉLECTIONNÉ"),
            Page("environment", "Environnement & audio", "Régler ciel, lumière, météo, eau et ambiance en direct.",
                StudioPageLayout.Spatial, "Prévisualiser", "PROFILS D'AMBIANCE", "APERÇU ENVIRONNEMENTAL", "RÉGLAGE ACTIF"),
            Page("navigation", "Navigation & transitions", "Voir les surfaces, routes, waypoints et passages entre zones.",
                StudioPageLayout.Spatial, "Ajouter un waypoint", "NAVIGATION", "SURFACES ET ROUTES", "ANCRE OU TRANSITION"),
            Page("living-world", "Monde vivant", "Composer rencontres, audio, repos et politiques propres à la zone.",
                StudioPageLayout.Spatial, "Ajouter une fonction", "FONCTIONS DU MONDE", "ZONE ACTIVE", "FONCTION SÉLECTIONNÉE")),

        Section("content", "Contenu", "CONTENU",
            Page("blueprints", "Bibliothèque & blueprints", "Trouver, comparer, prévisualiser et réutiliser le contenu.",
                StudioPageLayout.Hybrid, "Nouveau blueprint", "BIBLIOTHÈQUE", "APERÇU 3D", "BLUEPRINT"),
            Page("creatures", "Studio créature", "Une fiche cohérente pour apparence, statistiques, IA, loot et événements.",
                StudioPageLayout.Hybrid, "Nouvelle créature", "CRÉATURES", "APERÇU DE LA CRÉATURE", "FICHE CRÉATURE"),
            Page("items", "Studio objet", "Modifier l'objet sans perdre les propriétés avancées non affichées.",
                StudioPageLayout.Hybrid, "Nouvel objet", "OBJETS", "APERÇU DE L'OBJET", "FICHE OBJET"),
            Page("assets", "Assets & import", "Gouverner provenance, licence, variantes et destination avant import.",
                StudioPageLayout.Hybrid, "Importer un asset", "ASSETS", "COMPARAISON 3D", "PROVENANCE ET IMPORT"),
            Page("social-content", "Factions, marchands & compagnons", "Relier relations, inventaires, services et comportements sociaux.",
                StudioPageLayout.Specialized, "Ajouter", "RESSOURCES SOCIALES", "ÉDITEUR DIRECT", "DÉPENDANCES")),

        Section("story", "Histoire", "HISTOIRE",
            Page("dialogues", "Dialogues", "Écrire ligne par ligne ; garder le graphe comme vue avancée.",
                StudioPageLayout.Specialized, "Nouvelle ligne", "DIALOGUES", "LIGNES ET CHOIX", "CONDITIONS ET ACTIONS"),
            Page("quests", "Quêtes & journal", "Modifier objectifs, états, transitions et textes de journal nommés.",
                StudioPageLayout.Specialized, "Nouvel objectif", "QUÊTES", "OBJECTIFS ET ÉTATS", "TRANSITIONS"),
            Page("cinematics", "Cinématiques & caméras", "Monter la timeline en gardant le cadrage 3D visible.",
                StudioPageLayout.Hybrid, "Ajouter un plan", "TIMELINE", "CAMÉRA 3D", "PLAN SÉLECTIONNÉ"),
            Page("lore", "Lore & références", "Retrouver personnages, lieux, termes et usages sans parcourir les fichiers.",
                StudioPageLayout.Specialized, "Ajouter une entrée", "INDEX DE LORE", "CONTENU ET RÉFÉRENCES", "UTILISATIONS"),
            Page("localization", "Localisation", "Comparer les langues et traiter les manques dans leur contexte.",
                StudioPageLayout.Specialized, "Ajouter une langue", "LANGUES ET FILTRES", "TABLE DE TRADUCTION", "CONTEXTE")),

        Section("gameplay", "Gameplay", "GAMEPLAY",
            Page("rules", "Règles & difficulté", "Composer les règles et mutateurs sans exposer leur stockage.",
                StudioPageLayout.Specialized, "Nouveau profil", "PROFILS DE RÈGLES", "RÈGLES NOMMÉES", "IMPACT"),
            Page("abilities", "Capacités & progression", "Relier effets, progression, coûts et prévisualisation visuelle.",
                StudioPageLayout.Hybrid, "Nouvelle capacité", "CAPACITÉS", "APERÇU 3D", "FICHE CAPACITÉ"),
            Page("scripts", "ForgeScript", "Écrire, compiler, diagnostiquer et tester dans la sandbox.",
                StudioPageLayout.Specialized, "Nouveau script", "SCRIPTS", "CODE ET DIAGNOSTICS", "API ET PERMISSIONS"),
            Page("runtime", "Runtime, contrôles & extensions", "Configurer les comportements runtime et les contrôles du joueur.",
                StudioPageLayout.Specialized, "Tester le profil", "PROFILS RUNTIME", "RÉGLAGES", "COMPATIBILITÉ"),
            Page("network-director", "Réseau & Live Director", "Séparer configuration, assurance et opérations en direct.",
                StudioPageLayout.Specialized, "Vérifier", "SESSIONS ET PROFILS", "CONFIGURATION", "SÉCURITÉ ET ÉTAT")),

        Section("test", "Tester", "TESTER",
            Page("playtest", "Playtest", "Jouer un snapshot isolé et revenir exactement au contexte d'édition.",
                StudioPageLayout.Spatial, "Lancer depuis ici", "SCÉNARIOS", "RUNTIME 3D", "CONTRÔLES DU TEST"),
            Page("validation", "Validation & corrections", "Transformer chaque diagnostic en action localisée et prévisualisable.",
                StudioPageLayout.Specialized, "Tout vérifier", "FILTRES", "PROBLÈMES ACTIONNABLES", "CORRECTION"),
            Page("quality", "Laboratoire qualité", "Exécuter tests, budgets et captures sans bloquer l'interface.",
                StudioPageLayout.Specialized, "Lancer le profil", "PROFILS DE CONTRÔLE", "RÉSULTATS ET TENDANCES", "PREUVE"),
            Page("history", "Historique & récupération", "Comprendre les changements et restaurer dans une nouvelle révision.",
                StudioPageLayout.Specialized, "Créer un point", "SOURCES DE REPRISE", "TIMELINE", "RESTAURATION")),

        Section("publish", "Publier", "PUBLIER",
            Page("build", "Build & impact", "Expliquer ce qui sera reconstruit et pourquoi.",
                StudioPageLayout.Specialized, "Analyser le build", "CIBLES", "IMPACT DU BUILD", "TÂCHE"),
            Page("packages", "Packages & dépendances", "Contrôler contenu, versions, provenance et ordre de priorité.",
                StudioPageLayout.Specialized, "Ajouter une dépendance", "PACKAGES", "CONTENU ET RELATIONS", "PACKAGE"),
            Page("export", "Profils & export", "Résoudre les bloquants puis produire un paquet atomique vérifiable.",
                StudioPageLayout.Specialized, "Exporter .orpmod", "PROFILS", "CHECKLIST", "CIBLE"),
            Page("launcher", "Lanceur du jeu", "Configurer et prévisualiser l'unique exécutable destiné au joueur.",
                StudioPageLayout.Specialized, "Créer le lanceur", "ÉCRANS", "APERÇU DU LANCEUR", "RÉGLAGES")),

        Section("project", "Projet", "PROJET",
            Page("module", "Identité & configuration", "Modifier le module par des champs nommés et expliqués.",
                StudioPageLayout.Specialized, "Enregistrer", "CONFIGURATION", "FORMULAIRE DU MODULE", "AIDE CONTEXTUELLE"),
            Page("workspace", "Espace de travail & raccourcis", "Adapter la disposition sans modifier les données du module.",
                StudioPageLayout.Specialized, "Enregistrer mes préférences", "PRÉFÉRENCES", "CONFIGURATION PERSONNELLE", "VALEUR"),
            Page("security", "Sécurité & workers", "Voir exactement ce qu'une intégration peut lire et exécuter.",
                StudioPageLayout.Specialized, "Tester le worker", "INTÉGRATIONS", "PERMISSIONS ET ÉTAT", "RISQUE ET PORTÉE"),
            Page("production", "Production & plateforme", "Préparer les politiques de production sans secrets ni faux états.",
                StudioPageLayout.Specialized, "Prévisualiser", "PRODUCTION", "POLITIQUES", "IMPACT")),

        Section("assistant", "Assistant", "ASSISTANT",
            Page("contextual", "Assistant contextuel", "Proposer depuis la page active sans remplacer l'édition manuelle.",
                StudioPageLayout.Spatial, "Proposer un plan", "CONTEXTE AUTORISÉ", "PAGE DE TRAVAIL", "PROPOSITION"),
            Page("plans", "Plans & approbation", "Lire le diff, le dry-run et les tests avant toute application.",
                StudioPageLayout.Specialized, "Appliquer le plan validé", "PLANS", "DIFF SÉMANTIQUE", "DÉCISION"),
            Page("assisted-3d", "Création 3D assistée", "Comparer les variantes et gouverner l'asset avant son import.",
                StudioPageLayout.Hybrid, "Préparer l'import", "ÉTAPES", "COMPARAISON 3D", "PROVENANCE")),
    ];

    private static readonly IReadOnlyDictionary<string, StudioSectionDefinition> SectionById =
        Sections.ToDictionary(section => section.Id, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, StudioPageDefinition> PageById =
        Sections.SelectMany(section => section.Pages)
            .ToDictionary(page => page.Id, StringComparer.Ordinal);

    public static IReadOnlyList<StudioPageDefinition> Pages { get; } =
        Sections.SelectMany(section => section.Pages).ToArray();

    public static StudioSectionDefinition GetSection(string id) =>
        SectionById.TryGetValue(id, out var section)
            ? section
            : throw new KeyNotFoundException($"Unknown Studio section '{id}'.");

    public static StudioPageDefinition GetPage(string id) =>
        PageById.TryGetValue(id, out var page)
            ? page
            : throw new KeyNotFoundException($"Unknown Studio page '{id}'.");

    public static StudioSectionDefinition SectionForPage(string pageId) =>
        Sections.Single(section => section.Pages.Any(page => page.Id == pageId));

    private static StudioSectionDefinition Section(
        string id,
        string title,
        string shortTitle,
        params StudioPageDefinition[] pages) =>
        new(id, title, shortTitle, pages);

    private static StudioPageDefinition Page(
        string id,
        string title,
        string subtitle,
        StudioPageLayout layout,
        string primaryAction,
        string leftPanelTitle,
        string centerTitle,
        string rightPanelTitle) =>
        new(id, title, subtitle, layout, primaryAction, leftPanelTitle, centerTitle, rightPanelTitle);
}
