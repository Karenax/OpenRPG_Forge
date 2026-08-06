# ADR-0038 — Dialogue adaptatif déterministe

Date : 6 août 2026
Statut : accepté

## Décision

Les choix temporisés et variantes procédurales étendent le `DialoguePresentationProfile` canonique
sans créer un second graphe de dialogue. Un timer référence un nœud et un choix existants, déclare
durée, politique de pause, autorité, révision serveur, hook ForgeScript de timeout, identité d’audit
et provenance. Un `VariantSet` référence un nœud et porte textes localisés, conditions typées,
poids, portée de seed et fenêtre d’anti-répétition.

`DialogueAdaptiveRuntimeResolver` reste dans `Forge.Domain`. Il reçoit explicitement temps écoulé,
locale, autorité/révision, demandes de pause, seed, session et historique récent. La sélection
pondérée dérive son tirage d’un SHA-256 ordonné ; elle ne dépend ni de l’horloge machine, ni de
Godot, ni de l’ordre d’un dictionnaire. Un client ne peut pas faire avancer une règle déclarée
serveur. Une pause n’est honorée que par la politique de l’auteur, y compris le mode accessibilité.

La mutation continue de passer exclusivement par `SetDialoguePresentationHandler`. Le même profil
alimente la fenêtre Toolset, la simulation, le handle ForgeScript, la ressource MCP filtrée et le
runtime. Le format projet passe en `0.6.30`, le workspace en `1.24.0`; les profils `0.6.29` et
`1.23.0` sont relus avec collections adaptatives vides.

## Conséquences

- un module ancien reste jouable sans timer ni variante ;
- timer, choix par défaut et hook de timeout partagent la révision serveur et l’audit du document ;
- une variante requise sans candidat éligible produit `dialogue.variant.required_missing` ;
- l’anti-répétition exclut l’historique récent lorsque plusieurs candidats restent disponibles ;
- l’interface peut évoluer vers une visualisation circulaire du temps sans déplacer la logique dans
  le moteur ou dupliquer le document canonique.
