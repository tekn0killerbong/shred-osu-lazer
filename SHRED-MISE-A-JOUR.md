# Shred — colonnes jusqu'aux bords, agrandissement et picks à vide

Le playfield, ses cases et ses notes sont agrandis de 30 % dans le gameplay
et dans l'éditeur. Les lignes des colonnes atteignent maintenant le premier
bord de la zone de jeu rencontré selon leur angle. Les notes sont affichées
dès leur entrée dans cette zone, sans l'ancienne coupure à ScrollLength.
Leur vitesse de déplacement est conservée : la plus grande distance visible
donne davantage de temps de lecture, selon la colonne et le format de l'écran.
Les commandes F3/F4 restent disponibles.

Un pick avant la fenêtre de miss de la prochaine note brise le combo sans
consommer cette note ou cet accord. À OD 5 et sans multiplicateur, cette limite
est 173,5 ms avant la note. Entre cette limite et le début de la fenêtre du 50,
le pick trop tôt reste un miss de la note, comme auparavant. Un pick sans note
à venir brise aussi le combo. Les picks à vide ne modifient ni la précision,
ni le nombre de notes jugées, ni la détection de fin de map. La logique de
tolérance aux frettes supplémentaires et de hammer-pulls est conservée.

Les notes et les extrémités des notes longues restent pleines; les récepteurs
conservent leur contour évidé. La sélection
éclaircit les notes elles-mêmes, sans rectangle blanc. Les notes OPEN déjà
blanches deviennent légèrement translucides pendant leur sélection.
Le correctif du crash de création de notes est conservé.

Dernière mise à jour : la sélection par rectangle conserve les notes rencontrées
lorsque le défilement au bord commence, même après leur sortie de l’écran.
Les zones OPEN sont présentes à gauche et à droite de chaque rangée.
Les whams se créent directement dans leur bande avec Pick ou Hammer-pull;
leur outil séparé et le raccourci d’édition 4 ont été retirés.
La touche Espace du gameplay est conservée.

Le wham est une barre indigo translucide uniforme, sans tête claire. Son premier
bord atteint la face visible de la ligne à son temps de début, son dernier bord
à son temps de fin. La petite branche décorative ne compte pas dans la durée.
La ligne est à la même distance de la dernière colonne que l’écart entre les
frettes. Sa grille partage les subdivisions et couleurs lazer de la grille des
frettes, y compris les huitièmes et seizièmes.

## Installation

Fermer le jeu, puis remplacer les sources du dossier :
`C:\Users\Utilisateur\source\osu-shred\osu.Game.Rulesets.Shred`
par le dossier du même nom dans cette archive. Le projet conserve sa référence
vers `C:\Users\Utilisateur\source\osu-shred-lazer\osu.Game\osu.Game.csproj`.

Extraire aussi le dossier `lazer-patches` dans
`C:\Users\Utilisateur\source\osu-shred`. Le correctif du démarrage au curseur
concerne `EditorPlayer.cs` dans le dépôt lazer et doit être appliqué une fois
seulement. Si le test démarre déjà au curseur, cette étape est inutile :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Utilisateur\source\osu-shred\lazer-patches\Apply-ShredTimelineCursor.ps1"
```

Le script utilise `C:\Users\Utilisateur\source\osu-shred-lazer` comme cible.
Il sauvegarde le fichier original, modifie uniquement le bloc de démarrage
attendu et s'arrête sans changement si ce bloc est différent. Il peut être
relancé : un correctif déjà présent est reconnu. Aucun autre script de patch
ou d'annulation n'est nécessaire.

Puis lancer le lazer de développement :

```powershell
cd "C:\Users\Utilisateur\source\osu-shred-lazer"
dotnet run --project ".\osu.Desktop\osu.Desktop.csproj"
```

Cette archive contient les sources du ruleset, pas une installation de lazer.
Elle n'effectue aucune migration de maps ou modification des dossiers AppData.

## Démarrage des tests

Lazer conserve la position précise du curseur audio à l'ouverture du test.
Le correctif applique cette position à l'horloge du test même à 0 ms ou avant
la première note, au lieu de conditionner le saut à la présence d'une note
antérieure. Les jugements des notes antérieures restent gérés par lazer.

## Gameplay

- Les notes au même instant sur une rangée forment un accord. Un pick de cette
  rangée juge toutes ses notes ensemble. Les deux rangées restent indépendantes :
  pour un accord sur deux rangées, piquer les deux.
- Toutes les frettes demandées doivent déjà être maintenues au pick. Des frettes
  supplémentaires arment ce pick mais retardent son jugement jusqu'à leur
  relâchement. La précision est celle de ce dernier instant.
- Un pick avant le début de la fenêtre 50 rate l'accord. Une mauvaise frette qui
  reste maintenue après cette fenêtre fait aussi rater l'accord.
- Une frette appartenant à une note longue déjà en cours reste autorisée pendant
  un nouvel accord. Les hammer-pulls conservent la chaîne et les attaques par
  pression ou relâchement d'une frette supérieure.
- Les notes longues ont deux jugements : attaque et relâchement. Une libération
  trop tôt ou un maintien trop tard est raté. Aucun maintien sur les notes open.
- L'extension est plus sombre, le maintien n'a pas d'animation de réussite;
  l'effet de réussite intervient au relâchement. Une longue ratée reste sombre
  jusqu'à sa fin, puis disparaît.

## Touches

- Whammy : **Espace**.
- Deuxième finger pick du bas : **Shift droit**, remplace l'ancien défaut virgule.
- Deuxième finger pick du haut : touche physique produisant **è** sur la
  disposition Windows active. Le code traduit cette position pour les bindings
  physiques de lazer; il n'attend pas la saisie d'un caractère pendant le jeu.
  Si Windows ne fournit pas de touche directe pour è, le défaut reste K :
  réassigner alors Row1Pick2 dans les réglages Shred. Cette détection doit encore
  être vérifiée sur ton clavier.
- Les raccourcis personnalisés sont conservés; seuls les anciens défauts K et
  virgule de ces deux actions sont remplacés.
- F3/F4 conservent le réglage de vitesse de défilement existant.

## Éditeur

- Pick / Hammer-pull : raccourcis **2 / 3**. Cliquer dans la bande wham
  crée un wham avec l’un ou l’autre outil.
- Cliquer pour une courte; glisser pour une longue. Open reste courte.
- Wham est toujours longue : un clic lui donne au minimum une subdivision.
  Son temps dépend de la position horizontale; tirer vers la droite pour
  allonger. La petite branche inclinée indique sa fin et n'ajoute pas de durée.
- Le glisser garde la frette et la rangée choisies au départ. Le défilement
  commence seulement lors du glisser près du bord et accélère progressivement.
- Le placement n'avance plus automatiquement l'horloge vers la note créée.
- Les notes simultanées de frettes différentes ne se remplacent plus.
- Toute la partie visible de la grille est utilisable, avec le zoom de la timeline.
- Notes et cases utilisent des formes centrées avant inclinaison.

## Skin

Ajouter au besoin dans la section `[Shred]` du `ShredSkin/skin.ini` existant :

```ini
WhamPressAngle: 240
WhamPressDistance: 12
WhamScrollLength: 350
```

L'angle part de l'axe X positif selon la convention trigonométrique.
La distance est exprimée dans les unités de base du playfield; l'animation
translate l'ensemble du playfield pendant l'appui, puis le ramène au relâchement.
Le trait de jugement est placé dans l'espace entre les deux sections, entre les
lignes des cordes. La barre indigo translucide traverse cet espace depuis la droite.

Dans l'éditeur, un clic droit sur toute la partie visible d'un wham, y compris
son manche incliné, supprime ce wham et peut être annulé. Le manche commence
maintenant sous l'extrémité de la barre pour que la jonction soit continue,
dans l'aperçu et pendant le jeu. La bande de création et le calcul du snap
des whams restent identiques.

## Sauvegarde et audio

Le format Shred passe à `Version:2` et conserve les durées et le whammy. Les maps
`Version:1` restent lisibles. Utiliser ce nouveau ruleset pour rouvrir les maps
sauvegardées avec des maintiens ou des whams.

Le chemin audio complet est maintenant conservé dans les données Shred,
car l'encodeur legacy de lazer réduit `AudioFilename` au seul nom du fichier.
Cela corrige la perte d'un sous-dossier audio lors d'un aller-retour de sauvegarde.
La conservation du chemin et des métadonnées a été testée; la disparition du son
signalée sur ta map n'a pas pu être reproduite sans la map et le jeu en marche.
Si elle persiste, joindre la map exportée en .osz et le runtime.log du profil de
développement après un test permettra de poursuivre le diagnostic.

## Vérifications effectuées

Correctif du crash à la création d'une note : le blueprint de sélection reçoit
directement le playfield de son compositeur. Il ne demande plus une dépendance
`ShredEditorPlayfield` absente du contexte d'injection des blueprints de lazer.
Cette transmission directe est conservée avec le nouveau rendu de sélection.

Compilation .NET 10.0.401 réussie contre le `osu.Game.dll` modifié fourni dans
l'archive initiale et les versions de dépendances indiquées par celle-ci.
106 vérifications automatisées réussies : masques d'accords, tolérance de pick,
précision différée dans le code du playfield, jugements natifs de relâchement,
format et anciennes maps, chemin audio avec sous-dossier, placement aux bords à
deux niveaux de zoom, remplacement limité à une même frette, sélection cumulative,
zones OPEN à droite, création automatique de whams, subdivisions partagées et
correspondance des bords visibles avec les temps de jugement, jonction et zone
cliquable des whams. Deux vérifications supplémentaires injectent les dépendances
de blueprints de frette et de wham sans playfield enregistré, comme dans le
contexte du crash signalé (sans rendu graphique).
Les nouvelles vérifications couvrent également l'application et le retrait
de la sélection, le remplissage des notes et le cas des notes OPEN blanches.
Les tests couvrent également les picks éloignés répétés, l'accord toujours
jouable ensuite, la limite exacte de la fenêtre de miss, le combo natif,
le nombre de notes jugées, la précision et le retour arrière à travers un
pick à vide. La géométrie vérifie les dix intersections des colonnes avec
les bords, le facteur 1,3 et la visibilité avant l'ancienne limite.

Le remplacement du bloc d'EditorPlayer a été vérifié avec les expressions
régulières .NET sur le code fourni et sur l'ancien correctif partiel. La méthode
modifiée compile dans un test isolé d'horloge et démarre aux positions 0, 250,
1000 et 10000 ms pour une première note à 1000 ms. Ce test isolé ne constitue
pas un essai du client lazer complet sous Windows.

Le dossier `osu.Game.Rulesets.Shred.Verification` contient ces vérifications :

```powershell
dotnet run --project .\osu.Game.Rulesets.Shred.Verification\osu.Game.Rulesets.Shred.Verification.csproj
```

Ces vérifications ne remplacent pas un essai visuel et sonore sur ton poste.
Le code des anciens écrans de pratique reste présent pour compatibilité; cette
mise à jour cible le gameplay et l'éditeur intégrés à lazer.
