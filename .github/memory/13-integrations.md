# 13 — Intégrations externes

> Cible : `docs/technique/07-integrations.md`, décisions DT-11 à DT-13, DT-33 et DT-35.

## Faits vérifiés

- J0-03 implémente `IEmailSender` : MailKit/SMTP vers MailPit avec `ConnectionStrings:email` en local, ACS Email via `IfsAzureCredential` avec `Azure:email:Endpoint` en Azure. Les gabarits français et anglais sont envoyés par le canal e-mail du registre de notifications et la file `notifications`.
- J0-26 implémente `IGitProvider` et `GitProviderRegistry` derrière le port unique : `AzureReposProvider` et `GiteaProvider` sont disponibles en développement; les entrées GitHub/GitLab restent non livrées. Les connexions sont limitées à une organisation; leurs dépôts sont ouverts à tous les projets ou à une liste de projets.
- Azure Repos prend en charge le principal fédéré « IFS Git » sans secret client, avec assertion issue de `IfsAzureCredential`, et le PAT de repli à expiration obligatoire. Le PAT passe par `ISecretStore`, n'est pas enregistré dans PostgreSQL ni dans les journaux; le test d'infrastructure vérifie sa non-divulgation.
- Le fournisseur Gitea de développement utilise l'endpoint local configuré par l'AppHost. `tools/dev/gitea-init.ps1` initialise les dépôts privés `contoso/shop`, `contoso/shop-infra` et `contoso/shop-app`.
- Chaque connexion est testée à l'ajout et lors d'une modification. Le Worker planifie les alertes d'expiration à J-15 et J-3 et les remet au canal e-mail; la tâche peut aussi être déclenchée dans la galerie de développement.
- La recette J0-26 a réussi en local sur bureau et mobile : trois dépôts Gitea listés, seul `shop` autorisé au projet `shop`, connexion Azure DevOps PAT testée et alerte reçue par MailPit. Une régression de grille a été reproduite en rouge puis corrigée; captures dans `docs/plan/revues/captures/R-07/`.
- Le principal Entra « IFS Git » et son inscription dans l'organisation Azure DevOps restent des prérequis hors dépôt pour éprouver le mode fédéré. La recette locale J0-26 a utilisé le mode PAT.
