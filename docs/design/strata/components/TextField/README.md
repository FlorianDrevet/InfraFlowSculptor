# TextField

Champ de saisie à une ligne avec libellé au-dessus, aide ou erreur en dessous.

- Le libellé est toujours visible (pas de placeholder qui sert de libellé).
- `mono` pour tout ce qui est un identifiant : noms, codes, chemins, CIDR, branches, variables.
- `changed` pour une **surcharge d'environnement** : la valeur diffère de la valeur de la ressource (bordure et fond ambre). Le texte d'aide dit la valeur héritée.
- `error` remplace l'aide : il dit ce qui ne va pas et comment corriger, sans excuse ni code technique.
- Une valeur qui ressemble à un secret est refusée avant enregistrement, avec la conversion en secret de pipeline proposée.
- Pour une adresse IP ou un CIDR, l'application garde un champ segmenté par octet (`app-ds-ip-input`) ; ce composant en est l'enveloppe visuelle.
