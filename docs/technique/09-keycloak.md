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

Le royaume déclare ses scopes standards (`web-origins`, `acr`, `roles`, `basic`, `profile`, `email`) et les applique
par défaut aux clients Keycloak intégrés. C'est nécessaire à l'Account Console : sans le scope `roles`, son jeton ne
contient pas `resource_access.account.roles` et `/realms/ifs/account/` répond 403. Les clients IFS reçoivent aussi
`ifs-claims` explicitement; ce scope n'est pas global, afin de ne pas ajouter les revendications propres à l'API aux
jetons des clients d'administration Keycloak.

Un import vierge Keycloak 26.6 confirme ces scopes par défaut et les revendications des comptes Alice et Nina.
`offline_access` est un scope intégré de Keycloak : il n'est ni redéfini ni ajouté à `clientScopes` dans
`ifs-realm.json`. Pour émettre des jetons hors ligne, le client `ifs-web` le lie comme scope **optionnel**
(`optionalClientScopes: ["offline_access"]`). Il n'est émis que si le client le demande, ce que fait la configuration
Angular (`openid profile email offline_access`) pour obtenir un jeton de rafraîchissement hors ligne. `phone` et `address` ne
font pas partie du jeu de scopes de ce royaume ; les ajouter au JSON seulement si un client en a besoin.
Keycloak exige aussi que l'utilisateur porte le rôle de royaume `offline_access`. Les sept utilisateurs seed locaux le
reçoivent dans `realmRoles` afin que `ifs-web` puisse obtenir son jeton de rafraîchissement ; cette attribution reste
limitée au royaume de développement. Un utilisateur ajouté manuellement doit recevoir ce rôle s'il utilise l'application
Web.

Le mapper `amr` utilise `jsonType.label: "String"` et `multivalued: "true"` : les valeurs stockées (`pwd`, `mfa`)
sont des chaînes, et Keycloak émet la revendication `amr` comme un tableau de chaînes.

Les utilisateurs de démonstration ont les rôles Keycloak intégrés `account/manage-account` et `account/view-profile` :
ils peuvent ouvrir la console de compte. Modifier le JSON ne met pas à jour un royaume déjà créé. Pour réparer un
royaume existant, modifier uniquement le client ou les rôles concernés dans la console d'administration ; cela conserve
les utilisateurs et leurs mots de passe. Ne pas remplacer tout le royaume pour corriger une URI ou un rôle.

## 4. Se connecter

| Pour… | Aller sur | Identifiants |
|---|---|---|
| Utiliser IFS | `http://localhost:4200` → bouton de connexion → page Keycloak | un utilisateur de démonstration, mot de passe `Ifs-Demo-2026!` |
| Voir son compte | `https://localhost:8080/realms/ifs/account` | idem |
| Administrer Keycloak | `https://localhost:8080/admin` (lien dans le tableau de bord Aspire) | `admin` / valeur locale du paramètre Aspire `keycloak-admin-password`, conservée dans User Secrets |

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
4. Définir `oid` = un GUID nouveau (PowerShell : `[guid]::NewGuid()`), `tid` = le GUID du tenant simulé
   (Contoso `11111111-1111-1111-1111-111111111111`, Fabrikam `22222222-…`, comptes personnels
   `9188040d-6c67-4c5b-b112-36a304b66dad`). Dans Keycloak 26, les attributs personnalisés peuvent être masqués par le
   profil utilisateur par défaut : utilisez l'Admin REST API ou configurez ces champs dans *Realm settings* → *User
   profile*. Vérifiez le résultat par les revendications du jeton, pas seulement dans l'onglet utilisateur.
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

Le fichier de royaume est importé au premier démarrage du volume. Pour mettre à jour un royaume existant, appliquez
les changements ciblés dans l'Admin Console ou via l'Admin REST API : attachez les scopes manquants, corrigez le
mapper concerné et ajoutez les rôles aux seuls comptes visés. Cela conserve les utilisateurs, mots de passe et
autres données locales.

La suppression du volume Keycloak est une remise à zéro complète : elle efface le royaume, ses utilisateurs, ses
sessions et ses personnalisations. Ne le faites que pour un environnement jetable dont les données peuvent être
perdues; un changement de `ifs-realm.json` seul ne justifie pas sa suppression.

Pour **exporter** des modifications faites à la main dans la console vers le fichier : console → *Realm settings* →
menu *Action* (en haut à droite) → *Partial export* (cocher *Include clients*) — les utilisateurs ne sont pas exportés
par ce menu : les recopier à la main dans `users` du fichier.

## 8. Récupérer l'accès administrateur (`invalid_grant`)

Le paramètre `keycloak-admin-password` n'est lu par Keycloak qu'à la **création** du volume (variables
`KC_BOOTSTRAP_ADMIN_*`). Si le secret a changé depuis, ou si le volume vient d'une autre configuration, le volume
persistant garde l'ancien mot de passe et la console répond `invalid_grant`. Le royaume et ses utilisateurs ne sont
pas en cause : ne supprimez pas le volume. La procédure suivante ajoute un administrateur temporaire sans modifier
les données existantes.

1. Arrêter l'AppHost (`aspire stop`) : les conteneurs persistants restent démarrés.
2. Arrêter (sans le supprimer) le conteneur Keycloak, car la base H2 du mode développement ne s'ouvre qu'une fois :
   ```powershell
   docker ps --filter "name=keycloak" --format "{{.Names}}  {{.Image}}"
   docker stop <nom-du-conteneur-keycloak>
   docker inspect --format '{{range .Mounts}}{{.Name}} -> {{.Destination}}{{println}}{{end}}' <nom-du-conteneur-keycloak>
   ```
   Repérer le volume monté sur `/opt/keycloak/data`.
3. Créer l'administrateur dans un conteneur jetable, avec la même image et le même volume (saisie interactive du
   mot de passe, rien n'est écrit sur disque) :
   ```powershell
   docker run --rm -it -v <volume>:/opt/keycloak/data <image> bootstrap-admin user --username recovery-admin
   ```
   Si le nom `recovery-admin` existe déjà, en choisir un autre. Si Keycloak a été lancé avec des options de base de
   données particulières (variables `KC_DB*` du conteneur), les passer aussi avec `-e`.
4. Redémarrer : `docker start <nom-du-conteneur-keycloak>`, puis `aspire start`.
5. Console → royaume **master** → *Users* → `admin` → *Credentials* → *Reset password* avec la valeur du secret
   `keycloak-admin-password`. Se connecter ensuite avec `admin`, puis supprimer `recovery-admin`.

Le même compte sert à corriger un royaume importé avant une modification de `ifs-realm.json` (URI de redirection du
client `ifs-scalar`, scopes par défaut, rôles `ifs-api`) : voir § 3 et § 7, en modifiant les seuls éléments concernés.

## 9. Dépannage

| Symptôme | Cause | Remède |
|---|---|---|
| `invalid_grant` à la connexion de l'administrateur | Le mot de passe du volume diffère du secret Aspire | § 8 |
| Scalar : « Invalid redirect uri » ou jeton sans `roles` | Royaume persistant plus ancien que `ifs-realm.json` | Console → royaume `ifs` → *Clients* → `ifs-scalar` : *Valid redirect URIs* = `http://localhost:5257/scalar/*` et `https://localhost:7246/scalar/*`, onglet *Client scopes* : `roles`, `ifs-claims` ; pour un rôle, *Users* → *Role mapping* (filtre client `ifs-api`) |
| `401` sur toutes les requêtes de l'API | Émetteur différent (port, schéma ou nom d'hôte changé) | Vérifier `Auth__Authority` = `https://localhost:8080/realms/ifs` dans le tableau de bord Aspire, ressource `api` |
| Connexion refusée, journal Keycloak `KC-SERVICES0093 Invalid parameter value for scope` / `LOGIN_ERROR … Invalid scopes: openid profile email offline_access` | Royaume persistant importé avant l'ajout d'`optionalClientScopes` : le scope intégré `offline_access` n'est pas lié à `ifs-web` | Console → royaume `ifs` → *Clients* → `ifs-web` → onglet *Client scopes* → *Add client scope* → sélectionner le scope intégré `offline_access` → *Add* en **Optional**. Ne pas créer de nouveau scope, ne pas toucher aux utilisateurs ni au volume |
| Journal Keycloak `Offline tokens not allowed for the user or client` pendant l'échange du code OIDC | L'utilisateur n'a pas le rôle de royaume `offline_access`, même si le client a lié le scope optionnel | Console → royaume `ifs` → *Users* → utilisateur → *Role mapping* → ajouter `offline_access`. Pour un royaume persistant, cette correction est additive et ne demande ni réimport ni suppression du volume |
| « Invalid redirect uri » sur la page de connexion | L'application n'est pas sur `http://localhost:4200` | Lancer l'application par Aspire (port fixe), ou ajouter l'URL dans *Clients* → `ifs-web` → *Valid redirect URIs* |
| Le nouvel utilisateur du fichier n'apparaît pas | Le royaume existait déjà : pas de réimport | § 7 |
| `/v1/me` sans `tenantId` | Attribut `tid` absent ou mapper manquant | Vérifier les attributs de l'utilisateur et les mappers du client `ifs-api` |
| La connexion boucle | Cookies d'une ancienne session | Vider les cookies de `localhost:4200` et `localhost:8080` |

## 10. Pour aller plus loin

Documentation officielle : `https://www.keycloak.org/documentation` (Server Administration Guide : *Realms*, *Users*,
*Clients*, *Protocol mappers*). Intégration Aspire : `https://aspire.dev/integrations/security/keycloak/`.
