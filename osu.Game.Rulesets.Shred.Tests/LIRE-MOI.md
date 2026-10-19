> Documentation historique du prototype. Pour la version 16, lire `../LIRE-MOI.md` et exécuter `../Tester-v16.ps1`. Les 67 scénarios validés sont dans `Shred.Editor.Tests` et `Shred.StepRecorder.Tests`.

# osu!shred — step recorder et jugements

Mise à jour du 29 septembre 2026, préparée à partir du ZIP fourni dans cette conversation.

## Installer les sources

1. Fermer le jeu et garder une copie du dossier actuel `osu.Game.Rulesets.Shred`.
2. Copier le contenu du dossier `osu.Game.Rulesets.Shred` de cette archive dans le dossier homonyme du projet, en remplaçant les fichiers existants. Ne pas créer un sous-dossier du même nom à l'intérieur.
3. Depuis le dossier du ruleset, compiler :

```powershell
dotnet build .\osu.Game.Rulesets.Shred.csproj -c Debug
```

4. Utiliser ensuite la procédure habituelle pour charger ce ruleset dans le build de développement.

La référence existante vers `..\..\osu-shred-lazer\osu.Game\osu.Game.csproj` est conservée. Aucun patch du moteur osu!lazer n'est fourni ou nécessaire pour ces changements. L'archive contient les sources et ressources, sans anciens `bin`/`obj` ni DLL prétendument recompilée.

## Step recorder

Dans Compose, cliquer sur **Step recorder: OFF** pour l'activer. L'audio s'arrête et le curseur s'aligne sur la grille actuelle.

- Les frettes préparent l'accord : leur appui seul ne crée rien et ne démarre aucun délai. Un pick enregistre les frettes maintenues de sa rangée et démarre (ou redémarre) le délai commun de 500 ms. Chaque nouvelle note nécessite donc son pick.
- Le relâchement du pick ne termine pas les notes frettées : ce sont les relâchements des frettes enregistrées qui déterminent les notes courtes ou les maintiens.
- Relâcher toutes les nouvelles notes avant 500 ms les laisse courtes et avance d'un pas. Les notes d'un accord déjà relâchées restent courtes lorsque les autres atteignent le seuil.
- À 500 ms après le dernier pick, les frettes encore maintenues deviennent des LN et le curseur avance d'un pas. Les LN s'allongent ensuite automatiquement d'un pas toutes les 500 ms. Un nouveau pick ou un pas vide redémarre ce délai.
- Des notes courtes ou de nouvelles LN peuvent être saisies pendant les LN existantes. Chaque avancement prolonge les LN encore maintenues. Un nouveau pick sur une LN à une nouvelle position termine l'ancienne et commence une nouvelle note.
- **J** ou **N** : pas vide, qui prolonge aussi les notes déjà enregistrées et maintenues. Une frette préparée sans pick n'est pas enregistrée par le pas vide.
- Relâcher une LN termine la note à la position courante, sans avancer encore. Si une note courte vient de faire avancer la grille et que la LN est relâchée dans les 150 ms, sa fin revient à la position précédente.
- Un pick sans frette crée une OPEN courte. Le dernier relâchement des picks qui ont enregistré cette OPEN avance d'un pas. Les OPEN restent courtes : leur maintien ne devient pas une LN.
- L'appui wham est son propre pick, comme en gameplay. Il démarre le même délai de 500 ms et son relâchement termine la wham. Les whams suivent les règles de maintien des LN; leur durée minimale reste une subdivision pour un appui bref.

### Raccourcis configurables

Les nouveaux bindings sont déclarés dans la variante **Editor** du ruleset Shred, dans les réglages de touches osu!. L'ancien pas vide é/F6 de la variante gameplay est ignoré, sans modifier les bindings de jeu enregistrés.

| Action | Défaut |
| --- | --- |
| Step recorder: empty step 1 | J |
| Step recorder: empty step 2 | N |
| Decrease subdivision | I |
| Increase subdivision | O |
| Previous subdivision mode | P |
| Next subdivision mode | ^ |

I/O changent la subdivision dans la série native active; P/^ passent d'une série à l'autre (Binary/Common, Triplets). Cette DLL expose déjà Custom : lorsqu'une série Custom a été sélectionnée dans l'éditeur, elle est mémorisée dans le cycle jusqu'à la fermeture de l'éditeur. Aucun mode custom ni subdivision arbitraire ne sont inventés par le recorder. La touche physique ^ est recherchée dans la disposition Windows; si elle n'est pas identifiable, le défaut est la touche physique « [ » et peut être rebindé.

Les raccourcis de subdivision fonctionnent aussi recorder désactivé lorsque le focus n'est pas dans un autre contrôle. Les pas vides sont actifs seulement pendant l'enregistrement. Les actions d'éditeur ont priorité sur le gameplay pendant leur utilisation. **J est aussi Row1Pick1 par défaut** : utiliser un autre pick pendant le recorder (par exemple L si inchangé), ou reconfigurer le pick ou le pas vide. Cette priorité ne change pas les commandes pendant une partie.

- **Échap**, un changement de focus, une navigation dans la timeline, un raccourci avec Ctrl/Alt/Super ou la reprise de lecture désactive le recorder. Les notes déjà saisies sont conservées; le clic sur le bouton permet de reprendre.
- Les changements sont regroupés pour Undo jusqu'au relâchement des notes maintenues. Un objet déjà présent exactement à la même position et dans la même colonne est remplacé dans le même groupe d'annulation.

Le pas vient du BPM, de l'origine des timing points et de la subdivision courante de l'éditeur. La durée d'appui réelle ne détermine pas la durée musicale des LN : elle commande l'avancement par pas.

## Jugements

Le pick cible le prochain accord et conserve son instant. Une frette absente, incorrecte ou supplémentaire ne provoque plus immédiatement un Miss. Si l'accord devient correct avant la limite tardive Miss, son jugement utilise le décalage du pick initial. Sinon, le Miss normal survient à expiration. Un pick trop lointain casse le combo sans supprimer les prochaines notes.

Pour une LN, tenir jusqu'à la fin valide automatiquement la fin en Great. Aucun relâchement précis n'est requis. Un relâchement anticipé utilise une fenêtre 1,5 fois plus large que celle d'un appui, puis attribue la catégorie correspondante ou Miss. Cela ne remplace pas le jugement distinct de la tête.

Great (300) est le meilleur résultat autorisé. Perfect/rainbow est retiré des jugements et des catégories de résultat déclarées par le ruleset.

Les fenêtres sont symétriques, interpolées sans arrondi par palier :

| OD | Great 300 | Good | Ok | Meh | Limite Miss |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0 | ±200 ms | ±233 ms | ±263 ms | ±287 ms | ±324 ms |
| 5 | ±117,25 ms | ±150,25 ms | ±180,25 ms | ±204,25 ms | ±241,25 ms |
| 10 | ±34,5 ms | ±67,5 ms | ±97,5 ms | ±121,5 ms | ±158,5 ms |

L'extrémité OD 10 reprend les catégories de [ManiaHitWindows](https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Mania/Scoring/ManiaHitWindows.cs), sans sa catégorie Perfect. OD 0 interprète « 200 ms pour perfect » comme ±200 ms pour Great, meilleur jugement restant. Les autres fenêtres gardent le même écart entre catégories.

Le décalage affiché en jeu est celui du pick. Limite technique : le champ interne `JudgementResult.TimeOffset` de lazer reste l'instant de confirmation de la frette; un futur graphique statistique utilisant directement ce champ devra utiliser `ShredJudgementResult.PickOffset` pour afficher le pick initial.

## Visuels et bindings

- Les fins de LN n'ont plus de case ni de tête supplémentaire; l'extension sombre continue jusqu'à leur fin.
- Les bonnes frappes font grandir la forme pleine de la note. Les LN utilisent la couleur sombre de leur extension.
- Whams indigo opaques, avec deux segments de même épaisseur et une jonction ronde continue. La fin logique reste au bout de la partie horizontale; la poignée est décorative. Le même dessin est utilisé en gameplay et dans l'éditeur, y compris le placement.
- Les réécritures forcées de `Row2Pick2 = virgule` en Shift droit et de `Row1Pick2 = K` sont supprimées. Les réglages personnalisés sont prioritaires. Si l'ancien binding a déjà été enregistré différemment dans osu!, le remettre une fois sur la touche désirée.

## Vérification de cette révision

Relecture ciblée des changements et inspection des signatures exposées par la DLL osu.Game fournie. Aucune suite de tests exécutée, conformément à la préférence exprimée. Aucune compilation ni essai graphique/clavier ici : le SDK .NET et le projet moteur lié ne sont pas disponibles.

Le dossier Shred.StepRecorder.Tests conserve les 22 cas déjà fournis du moteur de notes. Ils restent optionnels et ne couvrent pas le routage clavier ajouté dans cette révision. Ils ne sont pas une preuve de compilation du ruleset.

Le dossier Windows `osu.Game.Rulesets.Shred.Tests\bin` contient normalement les sorties compilées des tests; ce n'est pas l'emplacement du SDK. `dotnet --info` permet de voir le SDK installé et son chemin. Pour compiler le ruleset, utiliser la commande indiquée au début de ce fichier depuis son dossier source.
