# Deploying ZISK to Azure

Free-tier deployment: App Service Plan F1 (Linux) + Azure SQL free serverless
database. Infrastructure is defined in [main.bicep](main.bicep).

**⚠️ This template has not been run yet** — there is no Azure account behind
this project at the time of writing. Nothing here has been executed against
a real subscription; treat `main.bicep` as a draft and run
`az bicep build --file main.bicep` (or `az deployment group validate`) before
trusting it. Report back anything that fails to compile or deploy.

## Prerequisites

- An Azure account ([free sign-up](https://azure.microsoft.com/free/) — a
  card is required for verification, but nothing is charged while you stay on
  F1 / the SQL free offer)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) with the
  Bicep extension (`az bicep install`)

## 1. One-time manual setup

```bash
az login

# Pick a region close to you; resource group name is your choice.
az group create --name zisk-rg --location westeurope
```

## 2. Deploy the infrastructure

```bash
az deployment group validate \
  --resource-group zisk-rg \
  --template-file azure/main.bicep \
  --parameters sqlAdminPassword='<strong-password>' \
               seedAdminPassword='<demo-admin-password>' \
               seedCoachPassword='<demo-coach-password>' \
               seedParentPassword='<demo-parent-password>' \
               seedChildPassword='<demo-child-password>'

# If validation passes, drop --parameters values in a real .bicepparam file
# or a secured pipeline instead of typing secrets on the command line, then:
az deployment group create \
  --resource-group zisk-rg \
  --template-file azure/main.bicep \
  --parameters @azure/main.bicepparam
```

The demo seed passwords are **not** the same as the local dev passwords in the
root README — pick different ones for the public deployment.

Note the `webAppUrl` output — that's the live demo link for the root README
and GitHub repo "About" section.

## 3. Wire up GitHub Actions OIDC (no client secret stored anywhere)

The `deploy` job in `.github/workflows/ci.yml` logs into Azure via
[`azure/login@v2`](https://github.com/Azure/login) using OIDC federated
credentials instead of a long-lived `AZURE_CREDENTIALS` secret.

```bash
# 1. App registration that GitHub Actions will impersonate
az ad app create --display-name "zisk-github-deploy"
# note the appId from the output - it's $CLIENT_ID below

# 2. Service principal for that app registration
az ad sp create --id <CLIENT_ID>

# 3. Let it deploy into the resource group (scope it down further if you want)
az role assignment create \
  --assignee <CLIENT_ID> \
  --role Contributor \
  --scope /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/zisk-rg

# 4. Federated credential - restricts the token to pushes on main in this repo
az ad app federated-credential create \
  --id <CLIENT_ID> \
  --parameters '{
    "name": "zisk-main-push",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:Miclah/ZISK:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

Then in the GitHub repo (**Settings → Secrets and variables → Actions**):

| Type | Name | Value |
|---|---|---|
| Secret | `AZURE_CLIENT_ID` | `<CLIENT_ID>` from step 1 |
| Secret | `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| Secret | `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
| Variable | `AZURE_WEBAPP_NAME` | the `appName` parameter value, e.g. `miclah-zisk` |

Also create a GitHub **environment** named `production` (Settings →
Environments) — the `deploy` job targets it, which is what lets the
federated-credential subject (`ref:refs/heads/main`) line up with the token
GitHub issues for that job.

## Known limitations (accepted for a free-tier demo)

- **Cold start**: F1 apps unload after idling; the first request after a
  while can take 10-20 seconds.
- **60 CPU minutes/day** on F1 - the demo may throttle under sustained load.
- **One free SQL database per subscription** - if this subscription already
  uses its free SQL allowance elsewhere, `useFreeLimit` in the Bicep template
  will be rejected or billed normally.
- No Key Vault - secrets are passed straight to App Service application
  settings via deployment parameters. Fine for a demo; would not be the
  pattern for a real production deployment.
