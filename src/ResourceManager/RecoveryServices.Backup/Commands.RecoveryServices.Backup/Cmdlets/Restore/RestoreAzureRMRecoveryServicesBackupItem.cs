﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Commands.RecoveryServices.Backup.Cmdlets.Models;
using Microsoft.Azure.Commands.RecoveryServices.Backup.Cmdlets.ProviderModel;
using Microsoft.Azure.Commands.Common.Authentication;
using Microsoft.Azure.Commands.Common.Authentication.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ResourcesNS = Microsoft.Azure.Management.Resources;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Commands.RecoveryServices.Backup.Properties;
using Microsoft.WindowsAzure.Commands.Utilities.Common;

namespace Microsoft.Azure.Commands.RecoveryServices.Backup.Cmdlets
{
    [Cmdlet(VerbsData.Restore, "AzureRmRecoveryServicesBackupItem"), OutputType(typeof(JobBase))]
    public class RestoreAzureRmRecoveryServicesBackupItem : RecoveryServicesBackupCmdletBase
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0, HelpMessage = ParamHelpMsgs.RestoreDisk.RecoveryPoint)]
        [ValidateNotNullOrEmpty]
        public RecoveryPointBase RecoveryPoint { get; set; }

        [Parameter(Mandatory = true, Position = 1, HelpMessage = ParamHelpMsgs.RestoreDisk.StorageAccountName)]
        [ValidateNotNullOrEmpty]
        public string StorageAccountName { get; set; }

        [Parameter(Mandatory = true, Position = 2, HelpMessage = ParamHelpMsgs.RestoreDisk.StorageAccountResourceGroupName)]
        [ValidateNotNullOrEmpty]
        public string StorageAccountResourceGroupName { get; set; }

        public override void ExecuteCmdlet()
        {
            ExecutionBlock(() =>
            {
                base.ExecuteCmdlet();
                StorageAccountName = StorageAccountName.ToLower();
                ResourceIdentity identity = new ResourceIdentity();
                identity.ResourceName = StorageAccountName;
                identity.ResourceProviderNamespace = "Microsoft.ClassicStorage/storageAccounts";
                identity.ResourceProviderApiVersion = "2015-12-01";
                identity.ResourceType = string.Empty;

                ResourcesNS.Models.ResourceGetResult resource = null;
                try
                {
                    WriteDebug(String.Format("Query Microsoft.ClassicStorage with name = {0}", StorageAccountName));
                    resource = RmClient.Resources.GetAsync(StorageAccountResourceGroupName, identity, CancellationToken.None).Result;
                }
                catch (Exception)
                {
                    identity.ResourceProviderNamespace = "Microsoft.Storage/storageAccounts";
                    identity.ResourceProviderApiVersion = "2016-01-01";
                    resource = RmClient.Resources.GetAsync(StorageAccountResourceGroupName, identity, CancellationToken.None).Result;
                }
                
                string storageAccountId = resource.Resource.Id;
                string storageAccountlocation = resource.Resource.Location;
                string storageAccountType = resource.Resource.Type;

                WriteDebug(String.Format("StorageId = {0}", storageAccountId));

                PsBackupProviderManager providerManager = new PsBackupProviderManager(new Dictionary<System.Enum, object>()
                {
                    {RestoreBackupItemParams.RecoveryPoint, RecoveryPoint},
                    {RestoreBackupItemParams.StorageAccountId, storageAccountId},
                    {RestoreBackupItemParams.StorageAccountLocation, storageAccountlocation},
                    {RestoreBackupItemParams.StorageAccountType, storageAccountType}
                }, ServiceClientAdapter);

                IPsBackupProvider psBackupProvider = providerManager.GetProviderInstance(RecoveryPoint.WorkloadType, RecoveryPoint.BackupManagementType);
                var jobResponse = psBackupProvider.TriggerRestore();

                WriteDebug(String.Format("Restore submitted"));
                HandleCreatedJob(jobResponse, Resources.RestoreOperation);
            });
        }
    }
}
