using AElf.Kernel.SmartContractExecution.Application;
using AElf.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Kernel.SmartContract.Parallel
{
    [DependsOn(typeof(SmartContractAElfModule))]
    public class ParallelExecutionModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            //
            context.Services.AddStoreKeyPrefixProvide<ContractRemarks>("cr");

            context.Services.AddTransient<IBlockExecutingService, BlockExecutingWithParallelService>();
        }
    }
}