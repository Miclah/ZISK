// Minimal free-tier infrastructure for a public ZISK demo deployment.
// Scope: App Service F1 (Linux) + Azure SQL free serverless database.
// Deliberately simple for a portfolio demo - no Key Vault, no VNet, no
// custom domain. Secrets are passed as secure deployment parameters and
// land directly in App Service application settings.

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name used to derive the App Service, plan, and SQL server names. Must be globally unique on azurewebsites.net.')
param appName string = 'miclah-zisk'

@description('SQL Server admin login (not used for app login - ASP.NET Core Identity handles that).')
param sqlAdminLogin string = 'ziskadmin'

@secure()
@description('SQL Server admin password. Must satisfy Azure SQL complexity policy (8+ chars, 3 of 4 categories).')
param sqlAdminPassword string

@secure()
@description('Seed:Passwords:Admin - see README.md "Demo accounts". Required because ZISK_SEED_MODE=demo.')
param seedAdminPassword string

@secure()
@description('Seed:Passwords:Coach')
param seedCoachPassword string

@secure()
@description('Seed:Passwords:Parent')
param seedParentPassword string

@secure()
@description('Seed:Passwords:Child')
param seedChildPassword string

@secure()
@description('Demo:OwnerKey - shared secret for the one-time /__owner?key=... link that lets you (not recruiters) reach /login and the rest of the Identity scaffold. Visit that URL once per browser after deploying.')
param demoOwnerKey string

var sqlServerName = '${appName}-sql'
var sqlDatabaseName = 'ZISK'
var appServicePlanName = '${appName}-plan'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Special convention (0.0.0.0-0.0.0.0): allows Azure services (this App Service
// included) to reach the server without listing individual outbound IPs.
resource sqlFirewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// General Purpose Serverless, opted into Azure SQL's free-database offer
// (one per subscription): auto-pauses on idle instead of billing past the
// free monthly vCore-second allowance.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    minCapacity: json('0.5') // serverless min vCores; must stay a decimal literal, not a string
    autoPauseDelay: 60
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: false // not supported on F1 - app cold-starts after idle
      appSettings: [
        { name: 'ZISK_SEED_MODE', value: 'demo' }
        { name: 'Seed__Passwords__Admin', value: seedAdminPassword }
        { name: 'Seed__Passwords__Coach', value: seedCoachPassword }
        { name: 'Seed__Passwords__Parent', value: seedParentPassword }
        { name: 'Seed__Passwords__Child', value: seedChildPassword }
        { name: 'Demo__OwnerKey', value: demoOwnerKey }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
          type: 'SQLAzure'
        }
      ]
    }
  }
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
