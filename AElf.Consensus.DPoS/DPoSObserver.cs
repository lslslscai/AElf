using System;
using System.Reactive.Linq;
using AElf.Kernel;
using AElf.Management.Interfaces;
using AElf.Miner.Miner;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;

namespace AElf.Consensus.DPoS
{
    // ReSharper disable once InconsistentNaming
    public class DPoSObserver : IConsensusObserver
    {
        private readonly IMiner _miner;
        private readonly INetworkService _networkService;

        public IEventBus EventBus { get; set; }

        public DPoSObserver(IMiner miner, INetworkService networkService)
        {
            _miner = miner;
            _networkService = networkService;
            
            EventBus = NullLocalEventBus.Instance;
        }
        
        public IDisposable Subscribe(byte[] consensusCommand)
        {
            var command = DPoSCommand.Parser.ParseFrom(consensusCommand);
            
            if (command.Behaviour == DPoSBehaviour.PublishInValue)
            {
                return Observable.Timer(TimeSpan.FromMilliseconds(command.CountingMilliseconds))
                    .Select(_ => ConsensusPerformanceType.BroadcastTransaction).Subscribe(this);
            }

            return Observable.Timer(TimeSpan.FromMilliseconds(command.CountingMilliseconds))
                .Select(_ => ConsensusPerformanceType.MineBlock).Subscribe(this);
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(ConsensusPerformanceType value)
        {
            switch (value)
            {
                case ConsensusPerformanceType.MineBlock:
                    break;
                
                case ConsensusPerformanceType.BroadcastTransaction:
                    // Schedule a job to call BroadcastTransaction
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }
    }
}