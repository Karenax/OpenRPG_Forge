namespace Forge.Scripting;

public static class ForgeScriptApiCatalog
{
    public static IReadOnlyList<ApiRegistration> Registrations { get; } =
    [
        Api("health", "int health(EntityRef target)", "Lit les points de vie courants.", "health(hero)", "server", "client"),
        Api("variable", "void variable(const string &in key, const string &in value)", "Modifie une variable autorisée.", "variable(\"door\", \"open\")", "server"),
        Api("say", "void say(const string &in text)", "Publie une réplique localisable.", "say(\"Bienvenue\")", "server", "client"),
        Api("cast_ability", "void cast_ability(EntityRef target, const string &in ability)", "Demande une capacité au service de combat.", "cast_ability(target, \"ash-nova\")", "server", "rpg-kit"),
        Api("spawn", "EntityRef spawn(const string &in blueprint)", "Crée une entité sous quota et autorité serveur.", "spawn(\"ash-guardian\")", "server"),
        Api("quest", "void quest(const string &in transition)", "Demande une transition de quête validée.", "quest(\"crypt.completed\")", "server", "rpg-kit"),
        Api("unlock", "void unlock(EntityRef target)", "Déverrouille une cible autorisée.", "unlock(door)", "server"),
    ];

    public static IReadOnlyList<string> Signatures { get; } = Registrations.Select(item => item.Signature).ToArray();

    private static ApiRegistration Api(string name, string signature, string summary, string example,
        params string[] availability) => new(name, signature, summary, "1.0",
            availability.ToHashSet(StringComparer.Ordinal), example, $"forge://api/{name}");
}
