# G7-31 — Session gameplay, sauvegarde et replay

Le Core possède désormais des contrats canoniques pour les sauvegardes par joueur, partie ou
serveur, avec autorité, révision, politique de conflit et options de confidentialité. Le même lot
ajoute les checkpoints et règles de respawn, le shell carte/journal/HUD adaptable, une pile de
contextes de contrôle qui restaure toujours le contexte précédent, et un replay déterministe.

Le replay enregistre tick, entrée, événement, seed et hash d'état. Son exécution headless produit
au premier écart un diagnostic et un paquet de reproduction minimal borné au tick divergent.

Les profils sont validés par des codes d'erreur stables et peuvent être persistés comme propriétés
typées par la commande transactionnelle existante. Les écrans spécialisés seront fournis par les
kits concernés, sans créer une seconde voie de mutation.

Preuves : `GameplaySessionServicesTests`, build sans avertissement et suite globale.

La prochaine tranche unique est **G7-32 — configuration et maintenance de projet**.
