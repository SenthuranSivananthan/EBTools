#Demonstrates how to enable Azure Netwatcher for all the VMs in an Azure VM Scale Set (VMSS)
#Jointly developed by Jennifer Phuong and Evan Basalik

Azure services parameters
$rgName = "<resource group name>"
$location = "<region hosting the VMSS>"
$VMSSName = "<Underlying VMSS name>" 
$storageAccountName = $VMSSName + "<storage account to put the packet captures>"
$containerName = "networktraces"
$networkWatcherName = "<Network Watcher name>"

$rgName = "rgVMSSNW"
$location ="eastus"
$VMSSName = "vmssevanb"
$storageAccountName = $VMSSName + "vmsstest"
$containerName = "networktraces"
$networkWatcherName = "nw"

#Log in if necessary
if (!Get-AzContext)
{
    Connect-AzAccount
}

#Validate that the Storage account exists and create if not
Write-Host "Creating/validating Storage Account $storageAccountName"
$storageAccount = Get-AzStorageAccount -ResourceGroupName $rgName | Where-Object {$_.StorageAccountName -eq $storageAccountName}
if (($storageAccount).count -eq 0)
{
    #Account didn't exist, create 
    Write-Host "Creating Storage Account $VMSSName$storageAccountName"
    $storageAccount = New-AzStorageAccount -StorageAccountName $storageAccountName -Location $Location -ResourceGroupName $rgName -SkuName Standard_GRS
}
else
{
    Write-Host "Storage Account exists"
}

#Validate that the container exists
Write-Host "Creating/validating container $containerName"
if ((Get-AzStorageContainer -Context $storageAccount.Context | Where-Object {$_.Name -eq $containerName}).count -eq 0)
{
    #Container didn't exist, create
    Write-Host "Creating container $containerName"
    New-AzStorageContainer -Name $containerName -Context $storageAccount.Context
}
else
{
    Write-Host "Container exists"
}

#Grab a pointer to the VMSS
Write-Host "Getting a pointer to the VMSS"
$VMSS = Get-AzVmss -VMScaleSetName $VMSSName -ResourceGroupName $rgName

#Add Network Watcher extension to the VMSS if necessary
if (($VMSS.VirtualMachineProfile.ExtensionProfile.Extensions | Where-Object {$_.Publisher -eq "Microsoft.Azure.NetworkWatcher"}).Count -eq 0)
{
    Write-Host "Adding Network Watcher to VMSS instances"
    $nwExt = (Get-AzVMExtensionImage -Location $location -PublisherName Microsoft.Azure.NetworkWatcher -Type NetworkWatcherAgentWindows | Sort-Object -Descending Version)[0]
    Add-AzVmssExtension -VirtualMachineScaleSet $VMSS -Name "netwatcher" -Publisher $nwExt.PublisherName -AutoUpgradeMinorVersion $True -Type $nwExt.Type -TypeHandlerVersion $nwExt.Version.Substring(0,3)
    Update-AzVmss -VMScaleSetName $VMSSName -ResourceGroupName $rgName -VirtualMachineScaleSet $VMSS

    #Need to loop through and push new model to existing VMs
    Write-Host "Updating existing instances with the newly added extension"
    for ($i = 0; $i -lt $VMSS.Sku.Capacity; $i++) 
    {
        #Get VM from underlying VMSS
        Update-AzVmssInstance -ResourceGroupName $rgName -VMScaleSetName $VMSS.Name -InstanceId $i
    }
}
else 
{
    Write-Host "Network Watcher already installed"
}

#Get Network Watcher Object
Write-Host "Getting a pointer to Network Watcher"
$nw = Get-AzResource | Where-Object {$_.ResourceType -eq "Microsoft.Network/networkWatchers" -and $_.Location -eq $location }
$networkWatcher = Get-AzNetworkWatcher -Name $nw.Name -ResourceGroupName $nw.ResourceGroupName 
if (($networkWatcher).count -eq 0)
{
    $networkWatcher = New-AzNetworkWatcher -Name $networkWatcherName -ResourceGroupName $rgName -Location $location
}
else 
{
    $nw = Get-AzResource | Where-Object {$_.ResourceType -eq "Microsoft.Network/networkWatchers" -and $_.Location -eq $location }
    $networkWatcher = Get-AzNetworkWatcher -Name $nw.Name -ResourceGroupName $nw.ResourceGroupName  
}

#Filters that we can tune to the solution
Write-Host "Creating some packet filters"
$filter1 = New-AzPacketCaptureFilterConfig -Protocol TCP -RemoteIPAddress "1.1.1.1-255.255.255" -LocalIPAddress "10.0.0.3" -LocalPort "1-65535" -RemotePort "20;80;443"
$filter2 = New-AzPacketCaptureFilterConfig -Protocol UDP 

#Loop through and set up Network Watcher on each VM in the VMSS
Write-Host "Kicking off a packet capture for the entire VMSS - VM by VM"
for ($i = 0; $i -lt $VMSS.Sku.Capacity; $i++) 
{
    #Get VM from underlying VMSS
    $VM = Get-AzVmssVM -ResourceGroupName $rgName -VMScaleSetName $VMSSName -InstanceId $i

    #Run the packet capture with a unique packet capture name
    $packetCaptureName = "capture_vm_" + $i  
    New-AzNetworkWatcherPacketCapture -NetworkWatcherName $networkWatcher.Name -ResourceGroupName $networkWatcher.ResourceGroupName -TargetVirtualMachineId $VM.Id -PacketCaptureName $packetCaptureName -StorageAccountId $storageAccount.id -TimeLimitInSeconds 15 -Filter $filter1, $filter2
}

