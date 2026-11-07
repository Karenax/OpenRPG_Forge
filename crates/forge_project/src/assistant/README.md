# Assistant gouverné

Les documents et reçus de zone restent canoniques. Ce module ne contient ni
réseau, credential, shell ni chemin fourni par un modèle.

Flux : sélection validée et région → contexte borné → corps fournisseur exact
approuvé pour partage → job enregistré avant envoi → proposition non fiable →
commandes existantes → diff approuvé → hôte commun → reçus et Save/Open/Undo.

- `selection` calcule les permissions depuis les IDs sélectionnés et borne les
  données partageables. Changer la révision invalide la sélection.
- `planning` compile une liste fermée d'opérations typées en une transaction :
  lieux, quête de collecte, contenu RPG additif, placement d'asset vérifié,
  nom de carte, altitude, déplacements et réplique. Le modèle ne reçoit pas
  de droit d'écrire un document arbitraire ; les identifiants nouveaux sont
  produits et validés par l'hôte.
- `provider` sérialise le protocole et contrôle les réponses. URL et réglages
  participent à l'approbation de partage. Aucun raisonnement brut n'est conservé.
- `jobs` rejoue un journal borné, avec réservations prudentes des appels incertains,
  limites et staging. Une archive de job n'accorde aucune approbation humaine.

Les adapters natifs du Studio assurent HTTP/TLS et persistance atomique des jobs.
L'état Applied d'un journal doit être vérifié contre un reçu canonique ; il ne
prouve pas seul qu'un projet a changé. Le panneau Studio couvre le parcours
fournisseur, diff, Apply et reprise, avec les limites persistées du job chargé.
Le transport MCP stdio du même processus expose inspection et proposition
seulement après autorisation native du périmètre dans Studio. Le panneau impose
la revue et l'approbation du diff avant les mêmes commandes ; aucun outil MCP
d'application directe ou de shell n'est exposé. Le staging durable n'enregistre
aucune autorité : une reprise exige un nouveau périmètre et une nouvelle revue.
Le raccordement est exercé sur renommage, reconnexion avec staging conservé et
nouvelle autorisation, puis refus d'un plan périmé par une retouche de terrain.
La replanification exige un nouveau contexte et une nouvelle revue ; la retouche
est préservée. Ces recettes emploient des propositions synthétiques sur le vrai
transport stdio, distinctes des générations Qwen. Les autres réserves restent
suivies dans le plan Gate 6 et son rapport d'avancement.
