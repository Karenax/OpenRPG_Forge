# ADR-0037 — Présentation canonique des dialogues

Date : 6 août 2026
Statut : accepté

## Décision

La mise en scène d’un dialogue appartient à `Forge.Domain.Dialogues` et reste une propriété
optionnelle de `DialogueDefinition`. Un `DialoguePresentationProfile` v1 porte UUID, provenance,
exigence runtime et autorité serveur des interjections. Il relie chaque réplique à des cues
d’animation, prises vocales localisées, plans de caméra, bookmarks de revue et règles
d’interjection. Les références utilisent les identités canoniques de nœud et de blueprint ; les
plans réutilisent `CameraFraming` et les ancres cinématiques existantes.

Une voix avec ressource exige un chemin relatif sûr, une licence SPDX et un SHA-256. Une voix sans
ressource n’est valide qu’avec repli sous-titre explicite. La caméra respecte le contrôle du joueur,
la réduction des mouvements et les marges sûres. Les interjections sont sélectionnées de façon
déterministe par présence, personnalité, relation, priorité, cooldown et politique de conflit ;
seul le serveur autoritaire peut les déclencher.

`SetDialoguePresentationHandler` est l’unique frontière de mutation : révision de base, validation
globale, dry-run, diff, inverse, audit et historique. Le Toolset et MCP l’appellent sans écrire le
JSON. ForgeScript ne reçoit qu’un handle typé en lecture/résolution. Le format projet passe en
`0.6.29`, le workspace en `1.23.0`; les lecteurs acceptent `0.6.28` et `1.22.0`, dont les dialogues
sans profil conservent un fallback optionnel jouable.

## Conséquences

- le graphe de dialogue et sa présentation partagent la même identité et le même cycle de vie ;
- aucune donnée Godot ne devient source de vérité et le domaine reste indépendant du moteur ;
- les séquences cinématiques existantes restent la source des ancres, cadrages et politiques
  d’accessibilité au lieu de créer un second système de caméra ;
- les modules historiques restent jouables, tandis qu’un runtime qui exige explicitement le profil
  reçoit le diagnostic stable `dialogue.presentation.required_missing` ;
- le futur transport multijoueur réutilisera l’autorité G9 pour les interjections sans modifier le
  format auteur.
