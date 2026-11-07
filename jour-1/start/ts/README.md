# Bank : starter TypeScript

Une petite application de gestion de compte bancaire. Elle expose une API web qui permet de consulter le relevé d'un compte, avec le solde après chaque opération, de déposer de l'argent et d'en retirer. Les opérations sont enregistrées dans une base PostgreSQL locale.

## Prérequis

- [Node.js 24 ou supérieur](https://nodejs.org/)
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

Créez ensuite le fichier `jour-1/start/ts/.env` (il n'est pas versionné) et remplacez les valeurs par celles de `API_URL` et `SECRET_KEY` :

```text
SUPABASE_URL=<API_URL>
SUPABASE_KEY=<SECRET_KEY>
```

Utilisez bien `SECRET_KEY` : avec une autre clé, le relevé est vide.

Puis, depuis `jour-1/start/ts` :

```bash
npm install
npm start           # démarre l'API sur http://localhost:5122
npm test            # lance les tests, la base doit être démarrée
npm run typecheck   # vérifie les types
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

## Exemple de relevé

Avec les données de test, le relevé du compte `1` est le suivant :

| Date            | Montant | Solde |
|-----------------|---------|-------|
| 20 janvier 2021 | -500    | 2500  |
| 15 janvier 2021 | 2000    | 3000  |
| 10 janvier 2021 | 1000    | 1000  |

## Tester avec la base

Les tests partagent une seule base. Pour qu'un test ne dépende pas des autres, réservez-lui un compte, et videz ce compte avant et après chaque test. Partez de ce squelette, dans `tests/accountService.test.ts` :

```typescript
import { createClient } from "@supabase/supabase-js";
import { afterEach, beforeEach, expect, test } from "vitest";
import { AccountService } from "../src/business/accountService.ts";

const accountId = 42;

async function clearOperations(): Promise<void> {
  const client = createClient(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);

  const { error } = await client.from("operations").delete().eq("account_id", accountId);

  if (error) {
    throw error;
  }
}

beforeEach(clearOperations);
afterEach(clearOperations);

// Vos tests ici, avec un service créé par new AccountService(process.env)
```

Vitest appelle `beforeEach` avant chaque test, et `afterEach` après, même s'il échoue. Les tests d'un même fichier s'exécutent l'un après l'autre, mais les fichiers s'exécutent en parallèle : un second fichier de tests doit réserver un autre compte.
