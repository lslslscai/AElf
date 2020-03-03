using System.Collections.Generic;
using System.Linq;
using Acs1;
using Acs3;
using AElf.Contracts.Association;
using AElf.Contracts.Treasury;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.MultiToken
{
    public partial class TokenContract
    {
        /// <summary>
        /// Related transactions will be generated by acs1 pre-plugin service,
        /// and will be executed before the origin transaction.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override TransactionFee ChargeTransactionFees(ChargeTransactionFeesInput input)
        {
            Assert(input.MethodName != null && input.ContractAddress != null, "Invalid charge transaction fees input.");

            var transactionFee = new TransactionFee();

            // Primary token not created yet.
            if (string.IsNullOrEmpty(input.PrimaryTokenSymbol))
            {
                return transactionFee;
            }

            // Record tx fee bill during current charging process.
            var bill = new TransactionFeeBill();

            var fromAddress = Context.Sender;
            var methodFees = Context.Call<MethodFees>(input.ContractAddress, nameof(GetMethodFee),
                new StringValue {Value = input.MethodName});
            var successToChargeBaseFee = true;
            if (methodFees != null && methodFees.Fees.Any())
            {
                successToChargeBaseFee = ChargeBaseFee(GetBaseFeeDictionary(methodFees), ref bill);
            }

            var successToChargeSizeFee = ChargeSizeFee(input, ref bill);

            // Update the bill.
            var oldBill = State.ChargedFees[fromAddress];
            State.ChargedFees[fromAddress] = oldBill == null ? bill : oldBill + bill;

            // Update balances.
            foreach (var tokenToAmount in bill.TokenToAmount)
            {
                ModifyBalance(fromAddress, tokenToAmount.Key, -tokenToAmount.Value);
                transactionFee.Value[tokenToAmount.Key] = tokenToAmount.Value;
            }

            transactionFee.IsFailedToCharge = !successToChargeBaseFee || !successToChargeSizeFee;
            return transactionFee;
        }

        private Dictionary<string, long> GetBaseFeeDictionary(MethodFees methodFees)
        {
            var dict = new Dictionary<string, long>();
            foreach (var methodFee in methodFees.Fees)
            {
                if (dict.ContainsKey(methodFee.Symbol))
                {
                    dict[methodFee.Symbol] = dict[methodFee.Symbol].Add(methodFee.BasicFee);
                }
                else
                {
                    dict[methodFee.Symbol] = methodFee.BasicFee;
                }
            }

            return dict;
        }

        private bool ChargeBaseFee(Dictionary<string, long> methodFeeMap, ref TransactionFeeBill bill)
        {
            if (!ChargeFirstSufficientToken(methodFeeMap, out var symbolToChargeBaseFee,
                out var amountToChargeBaseFee, out var existingBalance))
            {
                Context.LogDebug(() => "Failed to charge first sufficient token.");
                if (symbolToChargeBaseFee != null)
                {
                    bill.TokenToAmount.Add(symbolToChargeBaseFee, existingBalance);
                } // If symbol == null, then charge nothing in base fee part.

                return false;
            }

            bill.TokenToAmount.Add(symbolToChargeBaseFee, amountToChargeBaseFee);

            return true;
        }

        private bool ChargeSizeFee(ChargeTransactionFeesInput input, ref TransactionFeeBill bill)
        {
            string symbolChargedForBaseFee = null;
            var amountChargedForBaseFee = 0L;
            var symbolToPayTxFee = input.PrimaryTokenSymbol;
            if (bill.TokenToAmount.Any())
            {
                symbolChargedForBaseFee = bill.TokenToAmount.First().Key;
                amountChargedForBaseFee = bill.TokenToAmount.First().Value;
            }

            var availableBalance = symbolChargedForBaseFee == symbolToPayTxFee
                // Available balance need to deduct amountChargedForBaseFee
                ? GetBalance(Context.Sender, symbolToPayTxFee).Sub(amountChargedForBaseFee)
                : GetBalance(Context.Sender, symbolToPayTxFee);
            var txSizeFeeAmount = input.TransactionSizeFee;

            if (input.SymbolsToPayTxSizeFee.Any())
            {
                var allSymbolToTxFee = input.SymbolsToPayTxSizeFee;
                var availableSymbol = allSymbolToTxFee.FirstOrDefault(x =>
                                          GetBalanceCalculatedBaseOnPrimaryToken(x, symbolChargedForBaseFee,
                                              amountChargedForBaseFee) >= txSizeFeeAmount) ??
                                      allSymbolToTxFee.FirstOrDefault(x =>
                                          GetBalanceCalculatedBaseOnPrimaryToken(x, symbolChargedForBaseFee,
                                              amountChargedForBaseFee) > 0);
                if (availableSymbol != null && availableSymbol.TokenSymbol != symbolToPayTxFee)
                {
                    symbolToPayTxFee = availableSymbol.TokenSymbol;
                    txSizeFeeAmount = txSizeFeeAmount.Mul(availableSymbol.AddedTokenWeight)
                        .Div(availableSymbol.BaseTokenWeight);
                    availableBalance = symbolChargedForBaseFee == symbolToPayTxFee
                        ? GetBalance(Context.Sender, symbolToPayTxFee).Sub(amountChargedForBaseFee)
                        : GetBalance(Context.Sender, symbolToPayTxFee);
                }
            }

            var chargeAmount = availableBalance > txSizeFeeAmount
                ? txSizeFeeAmount
                : availableBalance;

            if (symbolToPayTxFee == null) return availableBalance >= txSizeFeeAmount;

            if (symbolChargedForBaseFee == symbolToPayTxFee)
            {
                bill.TokenToAmount[symbolToPayTxFee] =
                    bill.TokenToAmount[symbolToPayTxFee].Add(chargeAmount);
            }
            else
            {
                bill.TokenToAmount.Add(symbolToPayTxFee, chargeAmount);
            }

            return availableBalance >= txSizeFeeAmount;
        }

        public override ConsumedResourceTokens ChargeResourceToken(ChargeResourceTokenInput input)
        {
            var consumedResourceTokens = new ConsumedResourceTokens();
            Context.LogDebug(() => $"Start executing ChargeResourceToken.{input}");
            if (input.Equals(new ChargeResourceTokenInput()))
            {
                return consumedResourceTokens;
            }

            var bill = new TransactionFeeBill();
            foreach (var pair in input.CostDic)
            {
                Context.LogDebug(() => $"Charging {pair.Value} {pair.Key} tokens.");
                var existingBalance = GetBalance(Context.Sender, pair.Key);
                if (existingBalance < pair.Value)
                {
                    bill.TokenToAmount.Add(pair.Key, existingBalance);
                    var owningBalance = State.OwningResourceToken[Context.Sender][pair.Key]
                        .Add(pair.Value.Sub(existingBalance));
                    State.OwningResourceToken[Context.Sender][pair.Key] = owningBalance;

                    consumedResourceTokens.IsFailedToCharge = true;
                    consumedResourceTokens.Owning.Add(pair.Key, owningBalance);

                    Context.LogDebug(() => $"Insufficient resource. {pair.Key}: {existingBalance} / {pair.Value}");
                }
                else
                {
                    bill.TokenToAmount.Add(pair.Key, pair.Value);
                }
            }

            foreach (var pair in bill.TokenToAmount)
            {
                State.ChargedResourceTokens[input.Caller][Context.Sender][pair.Key] =
                    State.ChargedResourceTokens[input.Caller][Context.Sender][pair.Key].Add(pair.Value);
                consumedResourceTokens.Value.Add(pair.Key, pair.Value);
            }

            Context.LogDebug(() => $"Finished executing ChargeResourceToken.{consumedResourceTokens}");

            return consumedResourceTokens;
        }


        public override Empty CheckResourceToken(Empty input)
        {
            foreach (var symbol in Context.Variables.SymbolListToPayTxFee)
            {
                var balance = GetBalance(Context.Sender, symbol);
                var owningBalance = State.OwningResourceToken[Context.Sender][symbol];
                Assert(balance > owningBalance,
                    $"Contract balance of {symbol} token is not enough. Owning {owningBalance}.");
            }

            return new Empty();
        }

        public override Empty SetSymbolsToPayTXSizeFee(SymbolListToPayTXSizeFee input)
        {
            AssertControllerForSymbolToPayTxSizeFee();
            Assert(input != null, "invalid input");
            bool isPrimaryTokenExist = false;
            var symbolList = new List<string>();
            var primaryTokenSymbol = GetPrimaryTokenSymbol(new Empty());
            Assert(!string.IsNullOrEmpty(primaryTokenSymbol.Value), "primary token does not exist");
            foreach (var tokenInfo in input.SymbolsToPayTxSizeFee)
            {
                if (tokenInfo.TokenSymbol == primaryTokenSymbol.Value)
                {
                    isPrimaryTokenExist = true;
                    Assert(tokenInfo.AddedTokenWeight == 1 && tokenInfo.BaseTokenWeight == 1,
                        $"symbol:{tokenInfo.TokenSymbol} weight should be 1");
                }

                AssertSymbolToPayTxFeeIsValid(tokenInfo);
                Assert(!symbolList.Contains(tokenInfo.TokenSymbol), $"symbol:{tokenInfo.TokenSymbol} repeat");
                symbolList.Add(tokenInfo.TokenSymbol);
            }

            Assert(isPrimaryTokenExist, $"primary token:{primaryTokenSymbol.Value} not included");
            State.SymbolListToPayTxSizeFee.Value = input;
            Context.Fire(new ExtraTokenListModified
            {
                SymbolListToPayTxSizeFee = input
            });
            return new Empty();
        }

        /// <summary>
        /// Example 1:
        /// symbolToAmountMap: {{"ELF", 10}, {"TSA", 1}, {"TSB", 2}}
        ///
        /// [Charge successful]
        /// Sender's balance:
        /// ELF - 9
        /// TSA - 0
        /// TSB - 3
        /// Then charge 2 TSBs.
        ///
        /// [Charge failed]
        /// Sender's balance:
        /// ELF - 9
        /// TSA - 0
        /// TSB - 1
        /// Then charge 9 ELFs
        ///
        /// Example 2:
        /// symbolToAmountMap: {{"TSA", 1}, {"TSB", 2}}
        /// which means the charging token symbol list doesn't contain the native symbol.
        ///
        /// [Charge successful]
        /// Sender's balance:
        /// ELF - 1
        /// TSA - 2
        /// TSB - 2
        /// Then charge 1 TSA
        ///
        /// [Charge failed]
        /// Sender's balance:
        /// ELF - 1
        /// TSA - 0
        /// TSB - 1
        /// Then charge 1 TSB
        ///
        /// [Charge failed]
        /// Sender's balance:
        /// ELF - 1000000000
        /// TSA - 0
        /// TSB - 0
        /// Then charge nothing.
        /// (Contract developer should be suggested to implement acs5 to check certain balance or allowance of sender.)
        /// </summary>
        /// <param name="symbolToAmountMap"></param>
        /// <param name="symbol"></param>
        /// <param name="amount"></param>
        /// <param name="existingBalance"></param>
        /// <returns></returns>
        private bool ChargeFirstSufficientToken(Dictionary<string, long> symbolToAmountMap, out string symbol,
            out long amount, out long existingBalance)
        {
            symbol = null;
            amount = 0L;
            existingBalance = 0L;
            var fromAddress = Context.Sender;
            string symbolOfValidBalance = null;

            // Traverse available token symbols, check balance one by one
            // until there's balance of one certain token is enough to pay the fee.
            foreach (var symbolToAmount in symbolToAmountMap)
            {
                existingBalance = GetBalance(fromAddress, symbolToAmount.Key);
                symbol = symbolToAmount.Key;
                amount = symbolToAmount.Value;

                if (existingBalance > 0)
                {
                    symbolOfValidBalance = symbol;
                }

                if (existingBalance >= amount) break;
            }

            if (existingBalance >= amount) return true;

            var officialTokenContractAddress =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            var primaryTokenSymbol =
                Context.Call<StringValue>(officialTokenContractAddress, nameof(GetPrimaryTokenSymbol), new Empty())
                    .Value;
            if (symbolToAmountMap.Keys.Contains(primaryTokenSymbol))
            {
                symbol = primaryTokenSymbol;
                existingBalance = GetBalance(fromAddress, primaryTokenSymbol);
            }
            else
            {
                symbol = symbolOfValidBalance;
                if (symbol != null)
                {
                    existingBalance = GetBalance(fromAddress, symbolOfValidBalance);
                }
            }

            return false;
        }

        public override Empty ClaimTransactionFees(Empty input)
        {
            Context.LogDebug(() => "Claim transaction fee.");
            if (State.TreasuryContract.Value == null)
            {
                var treasuryContractAddress =
                    Context.GetContractAddressByName(SmartContractConstants.TreasuryContractSystemName);
                if (treasuryContractAddress == null)
                {
                    // Which means Treasury Contract didn't deployed yet. Ignore this method.
                    return new Empty();
                }

                State.TreasuryContract.Value = treasuryContractAddress;
            }

            var transactions = Context.GetPreviousBlockTransactions();
            Context.LogDebug(() => $"Got {transactions.Count} transaction(s) from previous block.");

            var senders = transactions.Select(t => t.From).ToList();
            var totalBill = new TransactionFeeBill();
            foreach (var sender in senders)
            {
                var oldBill = State.ChargedFees[sender];
                if (oldBill != null)
                {
                    totalBill += oldBill;
                }

                // Clear
                State.ChargedFees[sender] = new TransactionFeeBill();
            }

            foreach (var bill in totalBill.TokenToAmount)
            {
                var symbol = bill.Key;
                var amount = bill.Value;
                ModifyBalance(Context.Self, symbol, amount);
                TransferTransactionFeesToFeeReceiver(symbol, amount);
            }

            Context.LogDebug(() => "Finish claim transaction fee.");

            return new Empty();
        }

        public override Empty DonateResourceToken(Empty input)
        {
            Context.LogDebug(() => "Start donate resource token.");

            var isMainChain = true;
            if (State.TreasuryContract.Value == null)
            {
                var treasuryContractAddress =
                    Context.GetContractAddressByName(SmartContractConstants.TreasuryContractSystemName);
                if (treasuryContractAddress == null)
                {
                    isMainChain = false;
                }
                else
                {
                    State.TreasuryContract.Value = treasuryContractAddress;
                }
            }

            PayTransactionFee(isMainChain);

            if (!isMainChain)
            {
                PayRental();
            }

            return new Empty();
        }

        private void PayTransactionFee(bool isMainChain)
        {
            var transactions = Context.GetPreviousBlockTransactions();

            Context.LogDebug(() => $"Got {transactions.Count} transaction(s) from previous block.");

            foreach (var symbol in Context.Variables.SymbolListToPayTxFee)
            {
                var totalAmount = 0L;
                foreach (var transaction in transactions)
                {
                    var caller = transaction.From;
                    var contractAddress = transaction.To;
                    var amount = State.ChargedResourceTokens[caller][contractAddress][symbol];
                    if (amount > 0)
                    {
                        ModifyBalance(contractAddress, symbol, -amount);
                        Context.LogDebug(() => $"Charged {amount} {symbol} tokens from {contractAddress}");
                        totalAmount = totalAmount.Add(amount);
                        State.ChargedResourceTokens[caller][contractAddress][symbol] = 0;
                    }
                }

                Context.LogDebug(() => $"Charged resource token {symbol}: {totalAmount}");

                if (totalAmount > 0)
                {
                    if (isMainChain)
                    {
                        Context.LogDebug(() => $"Adding {totalAmount} of {symbol}s to dividend pool.");
                        // Main Chain.
                        ModifyBalance(Context.Self, symbol, totalAmount);
                        State.TreasuryContract.Donate.Send(new DonateInput
                        {
                            Symbol = symbol,
                            Amount = totalAmount
                        });
                    }
                    else
                    {
                        Context.LogDebug(() => $"Adding {totalAmount} of {symbol}s to consensus address account.");
                        // Side Chain
                        var consensusContractAddress =
                            Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
                        ModifyBalance(consensusContractAddress, symbol, totalAmount);
                    }
                }
            }
        }

        private void PayRental()
        {
            var creator = State.SideChainCreator.Value;
            if (creator == null) return;
            if (State.LastPayRentTime.Value == null)
            {
                // Initial LastPayRentTime first calling DonateResourceToken.
                State.LastPayRentTime.Value = Context.CurrentBlockTime;
                return;
            }

            // We need minutes.
            var duration = (Context.CurrentBlockTime - State.LastPayRentTime.Value).Seconds.Div(60);
            if (duration == 0)
            {
                return;
            }

            // Update LastPayRentTime if it is ready to charge rental.
            State.LastPayRentTime.Value += new Duration {Seconds = duration.Mul(60)};

            foreach (var symbol in Context.Variables.SymbolListToPayRental)
            {
                var donates = 0L;

                var availableBalance = GetBalance(creator, symbol);

                // Try to update owning rental.
                var owningRental = State.OwningRental[symbol];
                if (owningRental > 0)
                {
                    // If Creator own this symbol and current balance can cover the debt, pay the debt at first.
                    if (availableBalance > owningRental)
                    {
                        donates = owningRental;
                        // Need to update available balance,
                        // cause existing balance not necessary equals to available balance.
                        availableBalance = availableBalance.Sub(owningRental);
                        State.OwningRental[symbol] = 0;
                    }
                }

                var rental = duration.Mul(State.ResourceAmount[symbol]).Mul(State.Rental[symbol]);
                if (availableBalance >= rental) // Success
                {
                    donates = donates.Add(rental);
                    ModifyBalance(creator, symbol, -donates);
                }
                else // Fail
                {
                    // Donate all existing balance. Directly reset the donates.
                    donates = GetBalance(creator, symbol);
                    State.Balances[creator][symbol] = 0;

                    // Update owning rental to record a new debt.
                    var own = rental.Sub(availableBalance);
                    State.OwningRental[symbol] = State.OwningRental[symbol].Add(own);

                    Context.Fire(new RentalAccountBalanceInsufficient
                    {
                        Symbol = symbol,
                        Amount = own
                    });
                }

                // Side Chain donates.
                var consensusContractAddress =
                    Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
                ModifyBalance(consensusContractAddress, symbol, donates);

                Context.Fire(new RentalCharged()
                {
                    Symbol = symbol,
                    Amount = donates
                });
            }
        }

        public override Empty UpdateRental(UpdateRentalInput input)
        {
            AssertControllerForSideChainRental();
            foreach (var pair in input.Rental)
            {
                Assert(Context.Variables.SymbolListToPayRental.Contains(pair.Key), "Invalid symbol.");
                Assert(pair.Value >= 0, "Invalid amount.");
                State.Rental[pair.Key] = pair.Value;
            }

            return new Empty();
        }

        public override Empty UpdateRentedResources(UpdateRentedResourcesInput input)
        {
            AssertControllerForSideChainRental();
            foreach (var pair in input.ResourceAmount)
            {
                Assert(Context.Variables.SymbolListToPayRental.Contains(pair.Key), "Invalid symbol.");
                Assert(pair.Value >= 0, "Invalid amount.");
                State.ResourceAmount[pair.Key] = pair.Value;
            }

            return new Empty();
        }

        public override Empty SetSideChainCreator(Address input)
        {
            Assert(State.SideChainCreator.Value == null, "Creator already set.");
            if (State.ParliamentContract.Value == null)
            {
                State.ParliamentContract.Value =
                    Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);
            }

            Assert(Context.Sender == Context.GetZeroSmartContractAddress() ||
                   Context.Sender == State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty()),
                "No permission.");
            State.SideChainCreator.Value = input;
            return new Empty();
        }

        /// <summary>
        /// Burn 10%
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="totalAmount"></param>
        private void TransferTransactionFeesToFeeReceiver(string symbol, long totalAmount)
        {
            Context.LogDebug(() => "Transfer transaction fee to receiver.");

            if (totalAmount <= 0) return;

            var burnAmount = totalAmount.Div(10);
            if (burnAmount > 0)
                Context.SendInline(Context.Self, nameof(Burn), new BurnInput
                {
                    Symbol = symbol,
                    Amount = burnAmount
                });

            var transferAmount = totalAmount.Sub(burnAmount);
            if (transferAmount == 0)
                return;
            if (State.TreasuryContract.Value != null)
            {
                // Main chain would donate tx fees to dividend pool.
                State.TreasuryContract.Donate.Send(new DonateInput
                {
                    Symbol = symbol,
                    Amount = transferAmount
                });
            }
            else
            {
                if (State.FeeReceiver.Value != null)
                {
                    Transfer(new TransferInput
                    {
                        To = State.FeeReceiver.Value,
                        Symbol = symbol,
                        Amount = transferAmount,
                    });
                }
                else
                {
                    // Burn all!
                    Burn(new BurnInput
                    {
                        Symbol = symbol,
                        Amount = transferAmount
                    });
                }
            }
        }

        public override Empty SetFeeReceiver(Address input)
        {
            Assert(State.SideChainCreator.Value == Context.Sender, "No permission.");
            Assert(State.FeeReceiver.Value == null, "Fee receiver already set.");
            State.FeeReceiver.Value = input;
            return new Empty();
        }

        private decimal GetBalanceCalculatedBaseOnPrimaryToken(SymbolToPayTXSizeFee tokenInfo, string baseSymbol,
            long cost)
        {
            var availableBalance = GetBalance(Context.Sender, tokenInfo.TokenSymbol);
            if (tokenInfo.TokenSymbol == baseSymbol)
                availableBalance -= cost;
            return availableBalance.Mul(tokenInfo.BaseTokenWeight)
                .Div(tokenInfo.AddedTokenWeight);
        }

        private void AssertSymbolToPayTxFeeIsValid(SymbolToPayTXSizeFee tokenInfo)
        {
            Assert(!string.IsNullOrEmpty(tokenInfo.TokenSymbol) & tokenInfo.TokenSymbol.All(IsValidSymbolChar),
                "Invalid symbol.");
            Assert(tokenInfo.AddedTokenWeight > 0 && tokenInfo.BaseTokenWeight > 0,
                $"symbol:{tokenInfo.TokenSymbol} weight should be greater than 0");
        }

        private void AssertControllerForSideChainRental()
        {
            Assert(State.SideChainCreator.Value != null, "side chain creator dose not exist");
            var createOrganizationInput = GetControllerCreateInputForSideChainRental();
            var controllerForRental =
                CalculateSideChainRentalController(createOrganizationInput.OrganizationCreationInput);
            Assert(controllerForRental == Context.Sender, "no permission");
        }

        private Address CalculateSideChainRentalController(
            CreateOrganizationInput input)
        {
            if (State.AssociationContract.Value == null)
            {
                State.AssociationContract.Value =
                    Context.GetContractAddressByName(SmartContractConstants.AssociationContractSystemName);
            }

            var address = State.AssociationContract.CalculateOrganizationAddress.Call(input);
            return address;
        }
    }
}