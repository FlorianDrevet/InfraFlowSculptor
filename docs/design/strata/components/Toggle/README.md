# Toggle

Interrupteur pour un réglage binaire (présence dans un environnement, identité système, retour arrière automatique).

- Libellé à droite, formulé comme l'état activé (« Retour arrière automatique », pas « Désactiver le retour arrière »).
- État activé : piste `signal`, curseur `signal-ink`. Désactivé : piste `line-strong`, curseur `text`, bien visible.
- Rendu en `button role="switch"` avec `aria-checked`.
- Dans un formulaire qui s'enregistre en une fois, la valeur ne part qu'avec « Enregistrer » ; l'écran le montre par l'état « modifié » de la page.
