# osu!shred — correctif cumulatif v18.1 vers v21

Cette archive regroupe les versions finales de TOUS les fichiers modifiés ou
ajoutés par les correctifs v19, v20 et v21, à partir de v18.1. Elle convient aussi
si seulement une partie des correctifs intermédiaires a été installée.
Il n'est pas nécessaire de réappliquer les trois archives séparément.

Fermer le jeu, puis fusionner les DEUX dossiers `osu-shred` et `osu-shred-lazer`
dans `C:\Users\Utilisateur\source`, en remplaçant tous les fichiers fournis.
Ne pas créer de second dossier osu-shred à l'intérieur du premier.

Le cumul inclut notamment `ShredPlayfieldGeometry.FirstPickCentre` et
`DrawableShredHitObject.MissHead(double?)`, requis par ShredPlayfield.cs.
Leurs absences dans des fichiers restés à une version antérieure provoquaient
les erreurs CS0117 et CS1501 signalées. Les autres dépendances v19, les tests
v19/v20/v21 et le changement du moteur pour le retour du mode Test sont inclus.

Il s'agit d'un regroupement des correctifs existants, sans nouvelle règle de jeu.
Compilation toujours à confirmer sur le PC de l'utilisateur.

## Généralisation

Les groupes ne sont pas codés pour un seul exemple : ils peuvent utiliser
n'importe quels picks distincts parmi les huit, répartis sur une ou deux rangées.
Chaque pick réactivé fournit toujours une frappe sur sa rangée (sauf Band Hero,
qui réunit les rangées). Un test parcourt les 6 050 paires possibles de groupes
non vides et disjoints parmi ces huit picks.

## Nouveau tampon

La v20 jugeait le relâchement immédiatement. Un appui reçu quelques millisecondes
après pouvait alors viser une autre note. La v21 reporte uniquement le jugement
du hammer-pick, afin de donner la priorité à un vrai appui proche.

- Plafond : 20 ms réelles, converties dans l'horloge de la map selon sa vitesse.
- Réduction automatique : au maximum un quart de l'intervalle distinct le plus
  court autour des notes ciblées, sur leurs rangées, parmi les notes présentes
  dans le playfield. Pour 40 ms entre notes, le tampon est donc de 10 ms ; pour
  60 ms, il est de 15 ms ; pour 100 ms ou plus, il reste de 20 ms à vitesse 1x.
- Un appui réel dans le tampon avant OU après le relâchement supprime le
  hammer-pick de ce geste. La borne exacte appartient au tampon.
- La priorité du vrai appui s'applique entre les deux rangées : un geste mêlant
  un appui réel et un retour synthétique très proches est traité comme un geste
  physique. Pour un accord simultané de ce type, fournir les vrais picks requis
  sur chaque rangée, ou séparer les gestes au-delà du tampon.
- Les vrais appuis ne sont jamais fusionnés entre eux : deux ou trois picks
  simultanés restent deux ou trois frappes. Des appuis réels espacés de seulement
  5 ms sont aussi conservés.
- Si aucun appui ne remplace le hammer-pick, sa note, son compte par rangée et
  la validité des frettes sont ceux capturés au relâchement. Son jugement utilise
  cet instant initial : le délai ne s'ajoute pas à l'erreur de timing.
- Le retour visuel/score des hammer-picks peut donc attendre le tampon plus le
  passage à l'image suivante. Les appuis physiques restent immédiats.
- Les cibles sont conservées explicitement : un hammer-pick en attente ne cherche
  pas une autre note si sa cible a déjà été jouée. La pause et un retour arrière
  annulent les retours encore en attente.

20 ms est un choix de conception à éprouver en jeu, pas une frontière humaine
universelle. Avec plusieurs doigts, un appui et un relâchement intentionnels
peuvent être très proches. Agrandir uniformément cette fenêtre absorberait plus
facilement de vraies alternances rapides ; la réduction selon la densité limite
ce compromis sans pouvoir identifier avec certitude l'intention du joueur.

Le plafond se trouve dans `ShredPickBuffer.MaximumRealMilliseconds` ; aucun
réglage d'interface supplémentaire n'est ajouté dans ce correctif.

## Tests

9 scénarios ajoutés : toutes les paires de groupes ; permutations appui/relâchement
et bornes temporelles ; gestes distincts au-delà du tampon ; non-consommation de
la note suivante ; accords et frappes physiques rapides ; passages denses ;
timing/frettes capturés ; priorité physique entre rangées ; pause/retour arrière.
Les tests v20 attendent maintenant explicitement la confirmation différée.

La suite contient 121 scénarios nommés : 107 éditeur/gameplay et 14 recorder.
Compilation et tests C# NON exécutés ici : aucun SDK .NET disponible.
La relecture et la vérification de l'archive ne remplacent pas l'exécution.

Depuis `C:\Users\Utilisateur\source\osu-shred` :

```powershell
dotnet run --project .\Shred.Editor.Tests\Shred.Editor.Tests.csproj
dotnet run --project .\Shred.StepRecorder.Tests\Shred.StepRecorder.Tests.csproj
```

Puis ouvrir le jeu et vérifier particulièrement les transitions rapides :

```powershell
dotnet run --project ..\osu-shred-lazer\osu.Desktop\osu.Desktop.csproj -c Debug
```
