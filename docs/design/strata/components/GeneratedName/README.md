# GeneratedName

Le nom Azure qu'IFS calcule pour un environnement, en lecture seule, avec sa disponibilité.

- Bordure pointillée : c'est un résultat calculé, pas un champ éditable. On modifie le gabarit de nommage, le nom logique ou un nom forcé, pas ce nom.
- La valeur vient du même calcul que la génération : elle est écrite telle quelle dans le fichier de valeurs de la cible. Si les deux divergent, c'est un bug, pas un cas d'interface.
- `available` : coche verte si le nom est libre, croix rouge s'il est pris, rien si la vérification n'a pas eu lieu. Le texte voisin dit d'où vient l'information et quand (« Déployée (révision 14) », « vérifié il y a 2 h »).
- Le panneau « Pourquoi ce nom ? » qui l'accompagne donne le gabarit retenu, son origine et l'assainissement appliqué.
