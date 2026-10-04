# Icon

Icônes d'interface au trait (1.75 px, grille 24, bouts arrondis), en `currentColor` : elles prennent la couleur du texte qui les entoure.

- Tailles : 16 px dans le texte et les boutons, 14 px dans les boutons compacts, 12 px dans les puces.
- Elles servent aux actions et à la navigation. **Une ressource Azure ne se représente jamais avec une icône d'interface** : utiliser `ResourceIcon`.
- Décoratives par défaut (`aria-hidden`). Passer `label` quand l'icône porte seule une information (une coche « Disponible »).
- Ajouter une icône : un tracé au trait sur la grille 24, sans remplissage, dans `ICON_PATHS` du bundle.
