# 09 — Keycloak : guide d'utilisation

> Pour apprendre à vous servir de Keycloak dans IFS, sans connaissance préalable. Décision :
> [DT-07](01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local). Keycloak n'existe **qu'en
> local** : en Azure, c'est Entra ID qui connecte les utilisateurs.

## 1. À quoi il sert ici

IFS connecte ses utilisateurs par **OpenID Connect** (OIDC). En production, le fournisseur d'identité est Entra ID
(comptes Microsoft). En local, on veut :
- se connecter sans compte Microsoft, sans réseau et sans rien configurer dans un tenant Entra ;
- tester **plusieurs organisations, plusieurs tenants et plusieurs rôles** en même temps (alice d'une organisation,
  david d'une autre, une lectrice, un opérateur IFS…) ;
- obtenir des jetons qui ressemblent à ceux d'Entra (`oid`, `tid`, `email`, `roles`…), pour que l'API ne fasse **aucune**
  différence entre les deux.

Keycloak est un serveur d'identité open source qui fait exactement cela. Aspire le lance dans un conteneur et y importe
la configuration d'IFS au démarrage.

## 2. Les cinq notions à connaître

| Notion Keycloak | Ce que c'est | Équivalent Entra | Dans IFS |
|---|---|---|---|
| **Royaume** (*realm*) | Un espace isolé : ses utilisateurs, ses applications, ses réglages | Un tenant | Un seul royaume : `ifs` |
| **Client** | Une application qui demande des jetons | Une inscription d'application | `ifs-web` (Angular), `ifs-api` (l'API, l'« audience »), `ifs-scalar` (l'interface de test de l'API) |
| **Utilisateur** | Un compte avec identifiant et mot de passe | Un utilisateur | alice, bob, chloe, david, emma, nina, ops |
| **Attribut d'utilisateur** | Une donnée libre posée sur un utilisateur | Propriétés de l'objet utilisateur | `oid`, `tid` (identités simulées d'Entra), `amr` |
| **Mapper** (*protocol mapper*) | Règle qui recopie une donnée dans le jeton | Revendications facultatives | Recopie `oid`, `tid`, `email`, `email_verified`, `name`, `roles`, `amr` dans les jetons |

Un jeton émis par Keycloak pour alice ressemble donc à un jeton Entra : même émetteur pour tous (le royaume), même
audience (`ifs-api`), mêmes noms de revendications.

## 3. Où est la configuration

- **Fichier** : `src/backend/InfraFlowSculptor.AppHost/Realms/ifs-realm.json` (créé à l'étape S-05). C'est la source :
  utilisateurs, clients, mappers. Il est **importé au démarrage** du conteneur.
- **Données** : un volume Docker garde l'état de Keycloak entre deux `aspire run`. Si le royaume existe déjà, Keycloak
  ne réimporte **pas** le fichier (voir § 7, « Repartir de zéro »).
- **Port** : `8080`, fixe et exposé en HTTPS par Aspire (l'émetteur des jetons est
  `https://localhost:8080/realms/ifs`).

## 4. Se connecter

| Pour… | Aller sur | Identifiants |
|---|---|---|
| Utiliser IFS | `http://localhost:4200` → bouton de connexion → page Keycloak | un utilisateur de démonstration, mot de passe `Ifs-Demo-2026!` |
| Voir son compte | `https://localhost:8080/realms/ifs/account` | idem |
| Administrer Keycloak | `https://localhost:8080/admin` (lien dans le tableau de bord Aspire) | `admin` / valeur du paramètre Aspire `keycloak-admin-password` (affichée dans le tableau de bord, ressource `keycloak`, onglet *Parameters*, ou `dotnet user-secrets list --project src/backend/InfraFlowSculptor.AppHost`) |

**Plusieurs utilisateurs à la fois** : une fenêtre de navigation privée (ou un profil de navigateur) par utilisateur.
La session Keycloak est un cookie : dans la même fenêtre, on est une seule personne.

## 5. Les tâches courantes

### Changer d'utilisateur
Se déconnecter dans IFS (menu utilisateur), ou ouvrir une autre fenêtre privée. Si la page Keycloak vous reconnecte
sans demander le mot de passe, c'est que la session Keycloak est encore ouverte : `https://localhost:8080/realms/ifs/account`
→ *Sign out*, ou vider les cookies de `localhost:8080`.

### Ajouter un utilisateur
1. Console d'administration → en haut à gauche, choisir le royaume **ifs** (pas `master`).
2. *Users* → *Add user* : *Username* = l'adresse e-mail, *Email*, *First name*, *Last name*, *Email verified* = Oui.
3. Onglet *Credentials* → *Set password* → `Ifs-Demo-2026!`, *Temporary* = Non.
4. Onglet *Attributes* : `oid` = un GUID nouveau (PowerShell : `[guid]::NewGuid()`), `tid` = le GUID du tenant simulé
   (Contoso `11111111-1111-1111-1111-111111111111`, Fabrikam `22222222-…`, comptes personnels
   `9188040d-6c67-4c5b-b112-36a304b66dad`).
5. Pour le garder après une remise à zéro : l'ajouter aussi dans `ifs-realm.json` (même structure que les autres
   utilisateurs) et commiter — sinon il disparaîtra au prochain réimport.

### Simuler une adresse non vérifiée
Utilisateur → *Email verified* = Non. L'API ne lui trouve alors aucune adresse vérifiée
([DT-07](01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local)) : utile pour tester le refus
d'une invitation (c'est le rôle de `nina`).

### Donner un rôle interne IFS (back-office)
Console → *Clients* → `ifs-api` → *Roles* : les rôles `Ifs.Support`, `Ifs.CatalogEditor`, `Ifs.PlatformAdmin` existent.
Utilisateur → *Role mapping* → *Assign role* → filtrer par client `ifs-api`. Le mapper les place dans `roles`. Le tenant
doit être celui d'IFS (`33333333-…`) et l'attribut `amr` contenir `mfa` (c'est le cas de `ops`).

### Voir ce que contient un jeton
1. Console → *Clients* → `ifs-web` → *Client scopes* → *Evaluate* → choisir un utilisateur → *Generated access token*.
   On voit exactement les revendications que l'API recevra.
2. Ou, dans l'application, outils de développement → *Network* → une requête vers l'API → en-tête `Authorization:
   Bearer …` → coller le jeton dans un décodeur local (par exemple `pwsh -c "($env:T -split '\.')[1]"` puis décoder le
   base64) ; ne jamais coller un jeton réel d'Entra dans un site en ligne.

### Rendre un jeton plus court (tester l'expiration)
Console → *Realm settings* → *Tokens* → *Access Token Lifespan* = 1 minute. L'application doit renouveler le jeton sans
que vous le voyiez ; remettre 5 minutes ensuite.

## 6. Ce qu'il ne faut pas faire

- Utiliser ces comptes ou ce mot de passe ailleurs qu'en local : ils sont publics dans le dépôt.
- Changer le port 8080 ou le nom du royaume : les jetons ne seraient plus acceptés par l'API ni par l'application.
- Modifier le royaume **master** : c'est l'administration de Keycloak lui-même.
- Exposer Keycloak sur le réseau : il tourne en mode développement (`start-dev`) ; Aspire fournit le HTTPS local, qui n'est pas une configuration de production.

## 7. Repartir de zéro

Quand `ifs-realm.json` a changé (nouvel utilisateur commité, nouveau mapper) ou que la configuration est cassée :
1. Arrêter `aspire run`.
2. `docker volume ls | Select-String keycloak` puis `docker volume rm <nom du volume>`.
3. Relancer `aspire run` : le royaume est réimporté depuis le fichier.

Pour **exporter** des modifications faites à la main dans la console vers le fichier : console → *Realm settings* →
menu *Action* (en haut à droite) → *Partial export* (cocher *Include clients*) — les utilisateurs ne sont pas exportés
par ce menu : les recopier à la main dans `users` du fichier.

## 8. Dépannage

| Symptôme | Cause | Remède |
|---|---|---|
| `401` sur toutes les requêtes de l'API | Émetteur différent (port, schéma ou nom d'hôte changé) | Vérifier `Auth__Authority` = `https://localhost:8080/realms/ifs` dans le tableau de bord Aspire, ressource `api` |
| « Invalid redirect uri » sur la page de connexion | L'application n'est pas sur `http://localhost:4200` | Lancer l'application par Aspire (port fixe), ou ajouter l'URL dans *Clients* → `ifs-web` → *Valid redirect URIs* |
| Le nouvel utilisateur du fichier n'apparaît pas | Le royaume existait déjà : pas de réimport | § 7 |
| `/v1/me` sans `tenantId` | Attribut `tid` absent ou mapper manquant | Vérifier les attributs de l'utilisateur et les mappers du client `ifs-api` |
| La connexion boucle | Cookies d'une ancienne session | Vider les cookies de `localhost:4200` et `localhost:8080` |

## 9. Pour aller plus loin

Documentation officielle : `https://www.keycloak.org/documentation` (Server Administration Guide : *Realms*, *Users*,
*Clients*, *Protocol mappers*). Intégration Aspire : `https://aspire.dev/integrations/security/keycloak/`.
