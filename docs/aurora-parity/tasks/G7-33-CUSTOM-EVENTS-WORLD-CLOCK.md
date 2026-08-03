# G7-33 — Événements globaux et calendrier du monde

Le Core fournit un registre d'événements utilisateur namespacés, versionnés et munis d'un schéma
de payload strict. Les abonnements sont liés à l'identité stable du schéma ; une version ou un champ
inattendu est refusé par un code de diagnostic stable avant publication.

Le profil d'horloge complète les primitives temporelles par vitesse, pause, calendrier configurable,
minute initiale, fuseaux propres aux zones et minutes astronomiques. Le service calcule l'heure locale
et les franchissements d'aube, coucher, crépuscule, lune ou minuit de façon déterministe et sérialisable.

Preuves : `CustomWorldEventBusTests`, build et suite globale.

La prochaine tranche unique est **G7-34 — notes et routes de waypoint**.
