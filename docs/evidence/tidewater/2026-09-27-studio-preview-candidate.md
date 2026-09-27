# Preparation du candidat Studio pour revue de l'apercu

La surface computer-use disponible dans cette session expose uniquement le
navigateur integre (aucune fenetre native). Le transport navigateur sans zone
ne prouve pas le parcours Tauri avec renderer auteur. Pas de substitution par
une zone simulee ni de verdict UI produit sans observation.

Staging local : E:/OpenRPG_Forge/.tidewater-integration/building-library-site-003.
Paquet batiments : a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6.
Paquet environnement : 6d278d406a34eccf06fae4cd6d4a68647d5988f620761524ed03c41a97e795d1.
Les 821 ressources batiments et 266 ressources environnement sont verifiees
avant copie par prepare-building-library-site.py. index.html et styles.css copies.
Le build WASM release et wasm-bindgen ont termine avec succes. Les 544 fichiers
source epingles avant compilation sont encore identiques apres generation.
WASM distribue : 89f71c49770a8130b1e5bdbfa6e17e6553755c3924609156da625ceef9bc0375
(103872104 octets). Rapport local : studio-preview-wasm-proof-001.json.
Le build Tauri Authoring a termine avec succes dans studio-preview-candidate-001.
Les 544 sources et 1107 fichiers frontend sont verifies apres assemblage.
Executable : 4b67cf68794beebf20e4f9a0579c24762b811f0b08353ba7da7cf081d7c93ae2.
Rapports locaux : build-proof.json et studio-preview-candidate-proof-001.json.
Aucun verdict runtime ou UI n'est acquis.

Une copie de la fixture auteur existante est preparee sous
E:/OpenRPG_Forge/.tidewater-integration/studio-preview-workspace-001.
Les 15 fichiers (95615 octets) sont compares par SHA256 apres copie ; les sources
sont restees identiques. Rapport studio-preview-workspace-proof-001.json.
Zone cible : 03992311-9297-5219-8aa3-ed561640a312, R2-004 Final B.
Source authoring.json : 89e0dcb892a81b9589a604459de1aa1f97690cd502737f81ecae0782e84118d7.
Cette zone sert a qualifier le bouton et le calcul dans un vrai viewport auteur ;
elle n'est pas la carte Tidewater et ne doit pas etre presentee comme telle.
La copie human-review plus ancienne n'est pas utilisee ni modifiee.

Assemblage termine avec Build-StudioCandidate.ps1, Authoring et kit Forge gouverne.
Launch-Review.ps1 fixe la racine auteur et le profil WebView2 dans des dossiers
de test sur E:, verifie le hash de l'executable et restaure l'environnement appelant.
Son mode ValidateOnly passe ; il ne lance pas l'application. Le protocole est
dans studio-preview-review-001.md. Revue interactive non executee. Aucune publication. Le lancement/revue UI
et la qualification native/WebGPU de la carte complete restent ouverts.
