# Azure Key Vault Integration

This application has been configured to store sensitive information in Azure Key Vault for enhanced security. This README provides instructions on how to set up and use the Key Vault with this application.

## Prerequisites

1. An Azure subscription
2. Access to Azure Portal
3. Azure CLI installed locally (required for local development with Key Vault)

## Setting Up Azure Key Vault

### 1. Create a Key Vault

You can create an Azure Key Vault using the Azure Portal or Azure CLI.

Using Azure CLI:
# Login to Azure
az login

# Create a resource group (if you don't have one)
az group create --name MyResourceGroup --location eastus

# Create the Key Vault
az keyvault create --name formdemovault --resource-group MyResourceGroup --location eastus
### 2. Add Application Secrets to Key Vault

Add the following secrets to your Key Vault. The secret names should match exactly as shown:
az keyvault secret set --vault-name formdemovault --name "AzureAd:TenantId" --value "afeef710-259a-4e35-9745-387b66f0b0f2"
az keyvault secret set --vault-name formdemovault --name "AzureAd:ClientId" --value "5bc04d75-63cd-4874-a20b-d6c2da97cee5"
az keyvault secret set --vault-name formdemovault --name "AzureAd:Domain" --value "cirrotech.co.uk"
### 3. Assign Permissions to Access the Key Vault

The application uses DefaultAzureCredential to access Key Vault. For local development, you need to be logged in with Azure CLI:
# Login to Azure CLI
az login

# Verify you have access to the Key Vault
az keyvault secret list --vault-name formdemovault
## Running Locally with Azure Key Vault

To run the application locally using Azure Key Vault:

1. Make sure you've set up the Key Vault as described above
2. Make sure you've authenticated with Azure CLI using `az login`
3. Set `"KeyVault:UseLocalSecrets": false` in `appsettings.Development.json` (this has already been configured)
4. The VaultUri is already set in launchSettings.json environment variables
5. Run the application

### Verifying Key Vault Integration

Once the application is running locally, you can verify that Key Vault integration is working by:

1. Checking the console output for "Azure Key Vault configuration added successfully"
2. Navigating to `/api/KeyVault/diagnose` to see the diagnostic output
3. Navigating to `/api/KeyVault/config` to see the configuration values loaded from Key Vault

### Troubleshooting Local Development

If you encounter issues with Key Vault integration when running locally:

1. Make sure you're logged in with Azure CLI:az login
2. Verify you have access to the Key Vault:az keyvault secret list --vault-name formdemovault
3. Check the console output and application logs for error messages

4. Use the `/api/KeyVault/diagnose` endpoint to get diagnostic information

5. Common issues:
   - Azure CLI not logged in
   - Key Vault URI incorrect
   - No permission to access Key Vault secrets
   - Secrets not set up with the correct names

## Production Deployment

For production deployment:

1. Configure the `VaultUri` environment variable on your hosting platform
2. Use Managed Identity for authentication to Key Vault
3. Grant the Managed Identity permissions to read Key Vault secrets

### For App Service:
# Enable system-assigned managed identity
az webapp identity assign --name YourWebAppName --resource-group YourResourceGroup

# Get the principal ID
principalId=$(az webapp identity show --name YourWebAppName --resource-group YourResourceGroup --query principalId --output tsv)

# Grant permission to the App Service to access Key Vault secrets
az keyvault set-policy --name formdemovault --object-id $principalId --secret-permissions get list