# Transport des placements vegetaux canoniques

Date : 2026-09-27. Portee : protocole domaine et commande native Studio.

`authoring::vegetation_source` fournit des fragments de 256 KiB maximum,
lies au hash exact de la tete de zone, a l'index du bloc, a son hash source
et a sa taille. L'assemblage exige l'ordre exact et verifie le SHA-256 final
avant de rendre les octets disponibles. Aucun chemin ni hash de fichier
arbitraire ne vient de l'appelant : la tete ouverte autorise le bloc demande.

`AuthoringHost::read_vegetation_source` lit la fermeture de sources de la zone
libre ou du workspace gere. Le cache ne retient qu'un bloc, lie a son autorite,
son hash et sa taille ; chaque demande reverifie la tete ouverte. Il est vide
a la fermeture de la derniere zone. Studio expose ReadVegetationSource et
VegetationSource dans son protocole JSON existant et route la commande native.

## Preuves

- Test `vegetation_source_transport` : PASS (1 test). Lecture en plusieurs
  fragments depuis une zone enregistree, etat auteur inchange. Refus sans zone,
  source absente, hash de tete incorrect, index absent, offsets invalides,
  identites/tailles/ordre incoherents, doublon, reponse apres assemblage termine,
  assemblage incomplet et corruption finale. Apres fermeture/reouverture, un
  fichier corrompu est refuse et le cache ne masque pas la corruption.
- Le premier essai avait une fixture invalide (filtre de materiau incompatible
  avec le terrain synthetique) ; correction de cette fixture, sans changement
  des regles produit. Le second essai passe.
- Probe reel `verify_vegetation_transport` : PASS sur le workspace importe.
  10 blocs, 65 383 instances decodees, 48 fragments, 10 461 280 octets.
  Tete f05a68acc294b586940237565c77fe2f7679ae9d2b77f4596f0380680665d50f.
  Demandes perimees, index absent et session fermee refuses ; etat canonique
  identique avant/apres lecture. Aucun enregistrement de la carte.
- Compilation Studio native PASS (16,39 s), WASM PASS (44,92 s).
- Clippy strict bloque par deux diagnostics preexistants needless_pass_by_value
  dans zone_authoring/session.rs:349-350. Controle supplementaire PASS avec
  ce seul lint autorise sur la ligne de commande ; ce n'est pas un strict PASS.
  Aucun attribut de suppression ajoute au code produit.

Logs locaux dans E:/OpenRPG_Forge/.tidewater-integration :
vegetation-transport-test-001/002.log, vegetation-transport-real-001.log,
vegetation-transport-clippy-001/002.log,
vegetation-transport-native-check-001.log et vegetation-transport-wasm-check-001.log.

## Limites

La commande est compilee mais aucun appel GUI Tauri n'est qualifie ici.
Le consommateur asynchrone Studio, l'invalidation des chargements en vol dans
le navigateur, la projection des plantes, le picking et les gestes d'edition
restent a implementer. Le test reel utilise le host directement, pas un IPC
simule presente comme parcours utilisateur. Pas de nouveau rendu, de mesure
FPS ou de verdict de parite upstream. Aucun commit, push ni cloture du chantier.
