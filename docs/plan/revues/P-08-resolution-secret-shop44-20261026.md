# P-08 — résolution du prérequis secret shop44 — 2026-10-26

Blocage résolu : CONFIRMÉ

## Vérifications effectuées

- Florian a délégué le choix du mécanisme éphémère dans sa réponse du 2026-10-26 : « Prend le mécanisme que tu veux. »
- La release core #901 a prouvé que `MAIN_PAYMENTS_API_KEY` était vide dans l'exécution DEV. Les groupes `ifs-shop44-dev` (ID 12) et `ifs-shop44-prd` (ID 13) déclarent la variable comme secrète.
- Les permissions de pipeline lues pour les groupes 12 et 13 montrent une autorisation ciblée à la définition release 109 (`authorized=true`), sans autorisation globale de tous les pipelines. La définition n'a pas besoin d'une ouverture plus large.
- Le YAML du clone référence les groupes depuis les stages Preview et Deploy. La variable secrète sera donc fournie par les groupes, pas par une variable à la file qui serait moins prioritaire.
- Le témoin `SecretDependencyCheck` vérifie seulement que la valeur `Payments:ApiKey` n'est pas vide. Le secret est écrit dans le Key Vault de test par le pipeline ; aucun client de paiement externe n'est appelé.
- Mécanisme retenu : générer une valeur aléatoire non-production en mémoire et l'envoyer directement par l'API REST à la variable secrète du groupe DEV 12 pour un seul run de la release core 109 épinglé sur CI #900. La release vérifiera la valeur PRD déjà présente dans le groupe 13 ; ne mettre une valeur temporaire dans ce groupe que si le run confirme qu'elle est vide. Ne jamais afficher, écrire dans un fichier ou consigner la valeur.
- L'API REST a été vérifiée en remettant l'entrée DEV à une chaîne vide tout en conservant son nom et son indicateur secret. Après le run core terminal, remettre à vide uniquement les variables que ce mécanisme a modifiées et vérifier qu'elles restent secrètes ; la procédure T supprimera ensuite le Key Vault de test et son contenu. Aucun nouveau secret permanent ni ouverture des permissions à tous les pipelines n'est créé.

La priorité des variables YAML est décrite dans [la documentation Microsoft sur les variables](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/variables?view=azure-devops). Le corps de l'API REST pour mettre à jour un groupe de variables est décrit dans [la documentation Microsoft de Variablegroups - Update](https://learn.microsoft.com/en-us/rest/api/azure/devops/distributedtask/variablegroups/update?view=azure-devops-rest-7.1).
