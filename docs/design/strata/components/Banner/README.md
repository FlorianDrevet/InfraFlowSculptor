# Banner

Message en ligne qui concerne toute une section : avertissement de validation, révision périmée, publication partielle, explication d'une règle.

- `title` dit le fait en une phrase ; le corps dit la conséquence et quoi faire.
- `ember` : à regarder, ne bloque pas (révision périmée, données en production). `danger` : a échoué ou bloque. `success` : prérequis rempli. `signal` : explication d'un fonctionnement (« Qui écrit la configuration ? »), opération en cours.
- `action` : un bouton `sm` secondaire qui mène à la correction.
- Pas de bannière pour confirmer une action réussie banale : l'état visible suffit.
