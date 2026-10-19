# v21.1 — horloge du tampon hammer-pick

À appliquer sur la v21 (y compris l'archive cumulative v18.1 → v21).
Fusionner le dossier osu-shred dans C:\Users\Utilisateur\source,
en remplaçant les fichiers fournis. Aucun changement moteur.

Le résultat Windows fourni confirme la compilation, 14 tests recorder réussis
et 101 tests éditeur/gameplay réussis avant l'arrêt sur le scénario de double pick.
Les scénarios suivants n'avaient pas été exécutés. La v21 n'était donc pas
entièrement validée.

Modifications :
- Le banc de test initialise explicitement son horloge à 1×.
- Une vitesse nulle/non finie/négative utilise un repli à 1× pour le tampon.
  Auparavant, une vitesse nulle produisait 0,2 ms au lieu de 20 ms. La pause
  et le retour arrière restent gérés séparément par annulation des frappes.
- Le test d'intégration couvre les taux 0, 0,5×, 1×, 1,5× et 2×, les deux ordres
  d'événements et les écarts de 0, 1, 10 et 20 ms réelles (40 combinaisons).
- En cas d'échec, il affiche vitesse observée, délai, ordre des événements
  et résultats des deux notes. Les assertions n'ont pas été relâchées.
- Un test supplémentaire couvre les vitesses invalides et le respect du plafond
  adaptatif. La suite complète contient maintenant 122 scénarios nommés :
  108 éditeur/gameplay et 14 recorder.

Compilation et tests C# de CE correctif non exécutés ici, faute de SDK .NET.
Le défaut de calcul est corrigé dans le code, mais le résultat doit être confirmé
sur le PC Windows ; aucune réussite complète n'est annoncée à ce stade.

Depuis osu-shred :

```powershell
dotnet run --project .\Shred.Editor.Tests\Shred.Editor.Tests.csproj
```

Le recorder n'a pas changé et ses 14 tests ont déjà réussi dans la sortie jointe.
