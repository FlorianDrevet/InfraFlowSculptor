# GoldenPathRail

L'avancement d'un projet ou d'une révision sur le chemin du produit, en quatre étapes reliées.

- Deux usages : en haut de la vue projet (Modéliser → Générer → Publier → Déployer) et en haut de l'écran d'une révision (Valider → Générer → Relire → Publier).
- États : `done` (coche verte), `attention` (ambre : fait mais périmé, partiel ou incomplet), `error` (rouge), `current` (étape où l'on est, nœud `signal`), `pending`.
- `meta` dit le fait (« 2 pull requests fusionnées sur 2 »), `hint` dit ce qui demande une action (« prd : partiellement appliquée ») ; le hint peut être un lien vers l'endroit où corriger.
- Toujours quatre étapes, dans cet ordre. Ne pas y ajouter d'étape décorative.
