# ResourceRow

Une ressource dans la liste d'un composant ou d'un groupe de ressources : icône Azure, nom logique, type, nom généré, détail, état par rapport à la dernière révision.

- `generatedName` est **le nom Azure exact** pour l'environnement choisi au-dessus de la liste. Jamais une approximation.
- Quand une contrainte Azure ajuste le nom (pas de tirets pour un compte de stockage ou un registre), `generatedNote` le dit en clair.
- L'état compare à la dernière révision : `success` À jour, `ember` Modifiée / Nouvelle. Un constat de validation passe par les constats de la ressource, pas par la ligne.
- La ligne entière est un lien vers l'écran de la ressource (`href`).
- Une ressource enfant (base SQL sous son serveur) est décalée de 40 px à gauche.
- Une ressource existante (référencée par son identifiant Azure) porte la puce `Existante`.
