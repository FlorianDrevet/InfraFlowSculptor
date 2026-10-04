# Badge

Une à trois mots d'état à côté d'un nom ; la teinte a toujours un sens, jamais décorative.

| Ton | Sens | Exemples |
| --- | --- | --- |
| `success` | fait, vérifié | Déployée, Publiée, Fait, À jour, Trouvée |
| `ember` | a changé depuis la dernière révision, ou demande attention | Modifiée depuis la révision 14, Partiellement appliquée, À faire, Demande d'accès, Entra + mot de passe |
| `danger` | a échoué ou bloque | En échec, Erreur, Jeton expiré |
| `signal` | en cours, ou action attendue d'un tiers | En attente d'approbation, Génération en cours, Par défaut pour les liaisons |
| `neutral` | attribut ou état sans jugement | Container App, Bicep · Azure DevOps, Automatique, Inconnu |

`mono` pour les environnements, les noms Azure et les identifiants. `dot` quand la puce porte un état (colonne d'état, en-tête de ressource). Ne jamais distinguer deux états par la teinte seule : le texte dit toujours l'état. Les identités, rôles et la dérive utilisent `identity-soft` / `identity-text` en puce simple, avec la même géométrie.
