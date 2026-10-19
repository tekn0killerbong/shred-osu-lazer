ALIGNEMENT DES COTES ENTRE LES DEUX RANGEES
AlignRows: true calcule le decalage horizontal selon angle et RowGap.
Les deux inclinaisons sont liees ; les picks suivent le decalage.
18 tests cibles passes, dont collinearite des vrais glyphes a trois angles.
Cette mise a jour ne contient pas encore l editeur Shred.
Recopier les trois dossiers, puis relancer TestSceneShredSong.

AJUSTEMENT VISUEL - RANGEES RAPPROCHEES
RowGap passe de 90 a 34, decalage horizontal conserve.
Contour fin autour des deux rangees et separateur frettes/picks,
sans nouveaux separateurs entre les cases. Les lignes de defilement restent.
Recopier les trois dossiers et relancer TestSceneShredSong.
Compilation verifiee ; rendu a verifier en jeu.

ETAPE 5 - HALOS, ANIMATIONS, ERREURS ET SKIN.INI
Copier maintenant TROIS dossiers : osu.Game.Rulesets.Shred,
osu.Game.Rulesets.Shred.Tests et ShredSkin, dans la racine du projet osu-shred.
Accepter les remplacements des sources. ShredSkin/skin.ini est nouveau.
Lancer TestSceneShredSong comme auparavant.
Lire SKIN-SHRED.txt pour les reglages et PNG optionnels.

- Halo dore autour du contour des H/P, avec pulsation legere.
- Reussite : contour qui s'agrandit et s'efface au recepteur.
- Pick avec mauvaise frette dans la fenetre : la note devient un Miss,
  le combo natif et la chaine de cette corde sont casses, la case secoue.
- Mauvaise frette sans pick : aucune penalite ; preparation libre.
- Aucun pick avant expiration : note figee puis fade out simple.
- Lumiere de touche sous les notes, active pendant l'appui puis fade au relachement.
- skin.ini local [Shred] : largeurs, espacements, angles, couleurs et durees.
- Angles trigonometriques : 0 droite, +90 haut, -90 bas.
- PNG transparents statiques ou animes optionnels dans ShredSkin.
- Exactement deux rangees et correction A/S/Z/X conservees.
- Compilation reussie et 50 tests passes. Rendu interactif a verifier sur votre PC.

CORRECTIF : PARALLELOGRAMMES ET BINDINGS A/S/Z/X
- Inclinaison des glyphes inversee horizontalement (-70/150).
- Actions Shred numerotees 100..117 pour eviter les anciens IDs du template.
- Anciens IDs ignores avant la suppression des doublons de touches.
- Nouveaux bindings personnalises conserves ; actions manquantes completees.
- Aucun effacement de la base de donnees ou des configurations de lazer.
- Compilation reussie et 4 tests de regression cibles passes.
- Cause probable du probleme A/S/Z/X : collisions avec les anciens bindings.
  Votre base locale n'a pas ete inspectee ; retester les quatre touches en jeu.
- Les eventuels bindings personnalises de la precedente numerotation 0..17
  ne sont pas migres : les 18 actions repartent avec leurs valeurs par defaut.

osu!shred - Etape 4 : premiere chanson dans le joueur natif de lazer

INSTALLATION DANS LE PROJET DE DEVELOPPEMENT UNIQUEMENT
Fermer le navigateur de tests.
Copier les trois dossiers de cette archive dans :
C:\Users\Utilisateur\source\osu-shred
Accepter les remplacements. Le bloc modifie maintenant le vrai ruleset.
VisualTestRunner.cs n'est PAS fourni : votre nom d'hote de developpement est conserve.
Aucun fichier de configuration de votre lazer officiel n'est modifie.
Les references NuGet restent ppy.osu.Game 2026.916.0 / .NET 10.

DEMARRAGE
Depuis le dossier du projet :
dotnet run --project .\osu.Game.Rulesets.Shred.Tests\osu.Game.Rulesets.Shred.Tests.csproj -c Debug
Choisir TestSceneShredSong (nouvelle scene) dans le navigateur de tests.
Le test charge un vrai Player de lazer avec la map et la piste embarquees.
Si besoin, cliquer son etape Load player for osu!shred pour relancer.
Cette scene utilise les commandes de pause/reprise du joueur lazer (Echap).
Les touches F5/F6 du Workbench ne pilotent pas cette nouvelle scene.
Ne pas choisir l'ancien TestSceneOsuPlayer pour cette demonstration specifique.

CONTROLES PAR DEFAUT, DEUX RANGEES FIXES
Haut : frettes A S D F G ; picks J K L point-virgule.
Bas : frettes Z X C V B ; picks M virgule point slash.
Ce sont maintenant des actions du RulesetInputManager de lazer.
Les changements de bindings faits dans Workbench ne sont pas transferes.
Le profil gaucher et la vitesse personnalisable du Workbench ne sont pas encore
branches au Player natif ; ils restent disponibles dans leur scene de diagnostic.
Les quatre picks sont des actions distinctes.

CE QUI EST INTEGRE
- Vrai DrawableRuleset / Playfield / DrawableHitObject, resultats et HUD de lazer.
- Jugements automatiques du template retires.
- Deux cordes, 5 frettes et 4 picks par corde, open et HOPO.
- Fenetres de precision de mania lazer 2026.916.0 selon l'OD.
  La demo est OD5. Ce n'est plus la fenetre artificielle +/-120 ms.
- Piste instrumentale originale synthetisee, embarquee en WAV : 36 s, 120 BPM.
- 26 notes, avec un depart a 4 secondes et une paire d'OPEN a 33,5 s.
- Les frettes elles-memes servent de recepteurs. Pas de ligne de jugement separee.
- Notes et recepteurs partagent la meme forme et les memes dimensions.
- Trajectoire du haut : de haut-gauche vers bas-droite (~65 degres).
- Trajectoire du bas : de bas-droite vers haut-gauche (~-115 degres).
- Frettes en parallelogrammes, picks en losanges, graphismes proceduraux.
- NoFail disponible et demande par la scene de test.
- Affichage du dernier jugement et de son ecart en ms.

MAP ET FORMAT
Lire FORMAT-SHRED.txt. La demo se trouve dans :
osu.Game.Rulesets.Shred\Resources\shred-demo.osu
La musique correspondante est shred-demo.wav.
Le lecteur experimental de cette scene comprend [ShredArticulations].
L'importeur habituel de lazer ne comprend PAS encore cette extension.
Les lignes HitObjects seules restent des lignes de circles au format mania.
Les maps mania 12K de notes simples peuvent etre converties en picks seulement.
Le support des holds est explicitement absent, comme celui des sliders/spinners.
Ne pas importer la demo dans votre installation officielle pour ce test.

CE QUI RESTE A FAIRE
L'import et l'edition completes des maps Shred, les replays/autoplay,
les bindings/parametres natifs pour gaucher et vitesse, l'integration finale a l'editeur de skins lazer,
les particules et les hitsounds detailles. La difficulte en etoiles est encore
celle du squelette (0), et le score n'est pas presente comme eligible aux pp.
Le lecteur experimental ne traite pas encore toute la richesse d'un .osu :
SV, breaks, effets, samples personnalises et autres metadonnees.
Ce lecteur sert a la demonstration embarquee, pas encore a une conversion generale.

VALIDATION EFFECTUEE
SDK 10.0.401 installe dans l'environnement de preparation.
Compilation des deux projets reussie.
32 tests passes, 0 echec : logique, bindings, lecteur, ressources, fenetres mania
et pipeline natif WorkingBeatmap.GetPlayableBeatmap.
Avertissement NU1903 AutoMapper 13.0.1 toujours present dans les dependances.
L'affichage et la lecture audio interactifs n'ont pas ete executes ici.
A confirmer sur votre PC : demarrage du Player, son, controles, alignement.

Commande des tests :
dotnet test .\osu.Game.Rulesets.Shred.Tests\osu.Game.Rulesets.Shred.Tests.csproj -c Debug --filter "FullyQualifiedName~ShredNativeTest|FullyQualifiedName~ShredPracticeSessionTest|FullyQualifiedName~ShredPracticeBindingsTest"
