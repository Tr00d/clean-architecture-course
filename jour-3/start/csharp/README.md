# Bank : point de départ du troisième jour en C#

Une petite application de gestion de compte bancaire. Elle expose une API web qui permet de consulter le relevé d'un compte, avec le solde après chaque opération, de déposer de l'argent et d'en retirer. Les opérations sont enregistrées dans une base PostgreSQL locale.

Ce projet reprend le code de `jour-1/start/csharp`, avec les tests de caractérisation et la règle du solde des étapes 2 et 3 du premier jour.

Il contient aussi le domaine construit le deuxième jour, dans `src/Bank.Domain`, et ses tests, dans `tests/Bank.Domain.Tests`. Rien ne l'utilise encore : l'API passe toujours par `Bank.Business`.

## Prérequis

- [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/)
- [Supabase CLI](https://supabase.com/docs/guides/local-development/cli/getting-started)

## Démarrer

Depuis la racine du dépôt, démarrez la base de données :

```bash
supabase start
supabase db reset
```

Toujours depuis la racine du dépôt, affichez les informations de connexion :

```bash
supabase status -o env
```

Créez ensuite le fichier `jour-3/start/csharp/src/Bank.Api/appsettings.local.json` (il n'est pas versionné) et remplacez les valeurs par celles de `API_URL` et `SECRET_KEY` :

```json
{
  "Supabase": {
    "Url": "<API_URL>",
    "Key": "<SECRET_KEY>"
  }
}
```

Utilisez bien `SECRET_KEY` : avec une autre clé, le relevé est vide.

Puis, depuis `jour-3/start/csharp` :

```bash
dotnet run --project src/Bank.Api   # démarre l'API sur http://localhost:5122
dotnet test                         # lance les tests, la base doit être démarrée
```

`supabase db reset` remet la base dans son état initial : les opérations ajoutées entre-temps sont supprimées.

## Endpoints

### Consulter le relevé

```bash
curl http://localhost:5122/accounts/1/operations
```

### Déposer

```bash
curl -X POST http://localhost:5122/accounts/1/deposits \
  -H "Content-Type: application/json" \
  -d '{ "amount": 100 }'
```

### Retirer

```bash
curl -X POST http://localhost:5122/accounts/1/withdrawals \
  -H "Content-Type: application/json" \
  -d '{ "amount": 100 }'
```

Un retrait supérieur au solde est refusé avec un code `422`.

## Exemple de relevé

Avec les données de test, le relevé du compte `1` est le suivant :

| Date            | Montant | Solde |
|-----------------|---------|-------|
| 20 janvier 2021 | -500    | 2500  |
| 15 janvier 2021 | 2000    | 3000  |
| 10 janvier 2021 | 1000    | 1000  |
