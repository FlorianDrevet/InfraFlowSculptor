# 13 — Intégrations externes

> **Au 2026-10-27, l'envoi d'e-mail est implémenté ; les intégrations métier Entra et Azure DevOps restent à faire.** Les émulateurs de développement et OIDC local ne constituent pas ces intégrations produit.
> Cible : `docs/technique/07-integrations.md`, DT-11 à DT-13. Étapes J0-03, J0-26, J0-28, J0-30, J0-31.

## Faits vérifiés

- J0-03 implémente `IEmailSender` : MailKit/SMTP vers MailPit avec `ConnectionStrings:email` en local, ACS Email via `IfsAzureCredential` avec `Azure:email:Endpoint` en Azure. Les gabarits HTML et texte existent en français et en anglais ; l'événement `InvitationCreated` passe par le canal e-mail du registre de notifications et la file `notifications`.
