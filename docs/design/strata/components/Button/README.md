# Button

Déclenche une action ; `primary` est réservé à l'action qui fait avancer sur le chemin modéliser → générer → publier → déployer, une seule par écran.

- **primary** (fond `signal`, texte `signal-ink`) : Générer la révision 16, Publier, Ajouter la liaison, Enregistrer.
- **secondary** : actions courantes (Ajouter un topic, Projet Foundry, Télécharger).
- **ghost** : actions de barre d'outils, Comparer, Archive, Dupliquer, Annuler.
- **danger** : suppression. Toujours suivie d'un dialogue d'impact qui liste ce qui disparaît.
- `size="sm"` (28 px) dans les panneaux et les lignes de tableau.
- `href` rend un `<a>` stylé en bouton pour la navigation.

Le libellé dit exactement ce qui se passe (« Publier dans 2 dépôts », pas « Valider »). L'icône précède le libellé, jamais seule sans `aria-label`.
