using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Beacon.Sdk.Beacon.Permission;
using Beacon.Sdk.Beacon.Sign;
using Netezos.Encoding;
using Netezos.Forging.Models;
using TezosSDK.Beacon;
using TezosSDK.Helpers;
using TezosSDK.Scripts.FileUploaders;
using TezosSDK.Scripts.IpfsUploader;
using TezosSDK.Tezos;
using TezosSDK.Tezos.API.Models;
using TezosSDK.Tezos.API.Models.Tokens;
using TezosSDK.Tezos.Gas;
using TezosSDK.Tezos.Wallet;
using UnityEngine;
using Logger = TezosSDK.Helpers.Logger;
using Operation = TezosSDK.Tezos.Gas.Operation;
using Random = System.Random;

namespace TezosSDK.Samples.DemoExample
{
    public class ExampleManager : IExampleManager
    {
        private string _networkRPC;

        private string contractAddress;
        private string gasStationUrl;

        private const int softCurrencyID = 0;

        public ITezos Tezos { get; private set; }
        public GasStation GasStation { get; private set; }
        public User CurrentUser { get; private set; }

        public ExampleManager(string contractAddress, string gasStationUrl, string rpcBaseUrl, NetworkType network)       {
            this.contractAddress = contractAddress;
            this.gasStationUrl = gasStationUrl;
            TezosConfig.Instance.RpcBaseUrl = rpcBaseUrl;
            TezosConfig.Instance.Network = network;
            CurrentUser = null;
        }

        public void Init(Action<bool> callback = null)
        {
            Tezos = TezosSingleton.Instance;
            GasStation = new GasStation(gasStationUrl);
            _networkRPC = TezosConfig.Instance.RpcBaseUrl;
        }

        public void Unpair()
        {
            Tezos.Wallet.Disconnect();
            CurrentUser = null;
        }

        public async void FetchInventoryItems(Action<List<IItemModel>> callback)
        {
            var activeWalletAddress = Tezos.Wallet.GetActiveAddress(); // Address to the current active account

            const string entrypoint = "view_items_of";
            var input = new { @string = activeWalletAddress };

            await Tezos.API.ReadView(
                contractAddress: contractAddress,
                entrypoint: entrypoint,
                input: JsonSerializer.Serialize(input, JsonOptions.DefaultOptions),
                callback: result =>
                {
                    Logger.LogDebug("READING INVENTORY DATA");

                    // deserialize the json data to inventory items
                    CoroutineRunner.Instance.StartWrappedCoroutine(
                        NetezosExtensions.HumanizeValue(
                            val: result,
                            rpcUri: _networkRPC,
                            destination: contractAddress,
                            humanizeEntrypoint: "humanizeInventory",
                            onComplete: (ContractInventoryViewResult[] inventory) =>
                                OnInventoryFetched(inventory, callback))
                    );
                });
        }

        private void OnInventoryFetched(ContractInventoryViewResult[] inventory, Action<List<IItemModel>> callback)
        {
            var dummyItemList = new List<IItemModel>();
            var owner = Tezos.Wallet.GetActiveAddress();

            if (inventory != null)
            {
                foreach (var item in inventory)
                {
                    var id = Convert.ToInt32(item.id);
                    var itemType = id == 0 ? 0 : Convert.ToInt32(item.item.itemType) % 4 + 1;
                    var amount = id == 0 ? Convert.ToInt32(item.amount) : 1;

                    var stats = new StatParams(
                        Convert.ToInt32(item.item.damage),
                        Convert.ToInt32(item.item.armor),
                        Convert.ToInt32(item.item.attackSpeed),
                        Convert.ToInt32(item.item.healthPoints),
                        Convert.ToInt32(item.item.manaPoints)
                    );

                    dummyItemList.Add(new Item(
                        (ItemType)itemType,
                        "Items/TestItem " + itemType,
                        "Item " + id,
                        stats,
                        amount,
                        id,
                        owner));
                }
            }

            callback?.Invoke(dummyItemList);
        }

        public class ContractItem
        {
            public string damage { get; set; }
            public string armor { get; set; }
            public string attackSpeed { get; set; }
            public string healthPoints { get; set; }
            public string manaPoints { get; set; }
            public string itemType { get; set; }
        }

        public class ContractInventoryViewResult
        {
            public string id { get; set; }
            public string amount { get; set; }
            public ContractItem item { get; set; }
        }

        public class ContractMarketplaceViewResult
        {
            public string id { get; set; }
            public string owner { get; set; }
            public string currency { get; set; }
            public string price { get; set; }
            public ContractItem item { get; set; }
        }

        public async void FetchMarketItems(Action<List<IItemModel>> callback)
        {
            const string entrypoint = "view_items_on_market";

            // Prepping parameters for Netezos and for Native is different.
            // Double serialization converts double quotes (") symbol into 'u0002' string
            // var input = "{\"prim\": \"Unit\"}";
            var input = new MichelinePrim
            {
                Prim = PrimType.Unit
            }.ToJson();

            await Tezos.API.ReadView(
                contractAddress: contractAddress,
                entrypoint: entrypoint,
                input: input,
                callback: result =>
                {
                    // deserialize the json data to market items
                    CoroutineRunner.Instance.StartWrappedCoroutine(
                        NetezosExtensions.HumanizeValue(
                            val: result,
                            rpcUri: _networkRPC,
                            destination: contractAddress,
                            humanizeEntrypoint: "humanizeMarketplace",
                            onComplete: (ContractMarketplaceViewResult[] market) =>
                                OnMarketplaceFetched(market, callback))
                    );
                });
        }

        private void OnMarketplaceFetched(ContractMarketplaceViewResult[] market, Action<List<IItemModel>> callback)
        {
            var dummyItemList = new List<IItemModel>();

            if (market != null)
            {
                foreach (var item in market)
                {
                    var id = Convert.ToInt32(item.id);
                    var itemType = id == 0 ? 0 : Convert.ToInt32(item.item.itemType) % 4 + 1;
                    var currency = Convert.ToInt32(item.currency);
                    var price = Convert.ToInt32(item.price);
                    var owner = item.owner;

                    var stats = new StatParams(
                        Convert.ToInt32(item.item.damage),
                        Convert.ToInt32(item.item.armor),
                        Convert.ToInt32(item.item.attackSpeed),
                        Convert.ToInt32(item.item.healthPoints),
                        Convert.ToInt32(item.item.manaPoints)
                    );

                    dummyItemList.Add(new Item(
                        (ItemType)itemType,
                        "Items/TestItem " + itemType,
                        "Item " + id,
                        stats,
                        price,
                        id,
                        owner));
                }
            }

            callback?.Invoke(dummyItemList);
        }

        public async void BuyItem(string owner, int itemID)
        {
            const string entryPoint = "buy";

            var sender = Tezos.Wallet.GetActiveAddress();
            
            var paramValue = Tezos.TokenContract.GetContractScript().BuildParameter(
                entrypoint: entryPoint,
                value: new
                {
                    owner,
                    buyer = sender,
                    token_id = itemID
                });

            var param = new Parameters()
            {
                Entrypoint = entryPoint,
                Value = paramValue
            };

            var destination = contractAddress;
            var result = await GasStation.PostOperations<object>(sender, new List<Operation>() {
                new() {
                 destination = destination,
                 parameters = param 
                }
            });

            Logger.LogDebug(result.ToString());
        }

        public async void MintItem()
        {
            var sender = Tezos.Wallet.GetActiveAddress();

            const string entryPoint = "mint";

            var destination = contractAddress;

            var paramValue = Tezos.TokenContract.GetContractScript().BuildParameter(
                entrypoint: entryPoint,
                value: sender);
            
            var param = new Parameters()
            {
                Entrypoint = entryPoint,
                Value = paramValue
            };

            var result = await GasStation.PostOperations<object>(sender, new List<Operation>()
            {
                new()
                {
                 destination = destination,
                 parameters = param 
                }
            });

            Logger.LogDebug(result.ToString());
        }

        public void MintFA2(Action<TokenBalance> callback)
        {
            var rnd = new Random();
            var randomInt = rnd.Next(1, int.MaxValue);
            var randomAmount = rnd.Next(1, 1024);

            var activeAccount = Tezos.Wallet.GetActiveAddress();
            const string imageAddress = "ipfs://QmX4t8ikQgjvLdqTtL51v6iVun9tNE7y7Txiw4piGQVNgK";

            var metadata = new TokenMetadata
            {
                Name = $"testName_{randomInt}",
                Description = $"testDescription_{randomInt}",
                Symbol = $"TST_{randomInt}",
                Decimals = "0",
                DisplayUri = imageAddress,
                ArtifactUri = imageAddress,
                ThumbnailUri = imageAddress
            };

            Tezos
                .TokenContract
                .Mint(callback, metadata, activeAccount, randomAmount);

#if UNITY_IOS || UNITY_ANDROID
            Application.OpenURL("tezos://");
#endif
        }

        public User GetCurrentUser()
        {
            return CurrentUser;
        }

        public void GetBalance(Action<ulong> callback)
        {
            var routine = Tezos.GetCurrentWalletBalance(callback);
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }

        public void GetSoftBalance(Action<int> callback)
        {
            GetSoftBalanceRoutine(callback);
        }

        private async void GetSoftBalanceRoutine(Action<int> callback)
        {
            var caller = Tezos.Wallet.GetActiveAddress();

            var input = new MichelinePrim
            {
                Prim = PrimType.Pair,
                Args = new List<IMicheline>
                {
                    new MichelineString(caller),
                    new MichelineInt(softCurrencyID)
                }
            }.ToJson();

            await Tezos.API.ReadView(
                contractAddress: contractAddress,
                entrypoint: "get_balance",
                input: input,
                callback: result =>
                {
                    var intProp = result.GetProperty("int");
                    var intValue = Convert.ToInt32(intProp.ToString());
                    callback(intValue);
                });
        }

        public void TransferItem(int itemID, int amount, string address)
        {
            Logger.LogDebug(
                $"Transfering item {itemID} from {Tezos.Wallet.GetActiveAddress()} to Address: {address}");

            var sender = Tezos.Wallet.GetActiveAddress();
            const string entrypoint = "transfer";
            var input = "[ { \"prim\": \"Pair\", \"args\": [ { \"string\": \"" + sender +
                        "\" }, [ { \"prim\": \"Pair\", \"args\": [ { \"string\": \"" + address +
                        "\" }, { \"prim\": \"Pair\", \"args\": [ { \"int\": \"" + itemID + "\" }, { \"int\": \"" +
                        amount +
                        "\" } ] } ] } ] ] } ]";

            Tezos.Wallet.CallContract(contractAddress, entrypoint, input, 0);

#if UNITY_IOS || UNITY_ANDROID
            Application.OpenURL("tezos://");
#endif
        }

        public async void AddItemToMarket(int itemID, int price)
        {
            Debug.Log("Adding Item " + itemID + " to Market with the price of " + price);

            const string entryPoint = "addToMarket";

            var sender = Tezos.Wallet.GetActiveAddress();

            var destination = contractAddress;

            var paramValue = Tezos.TokenContract.GetContractScript().BuildParameter(
                entrypoint: entryPoint,
                value: new
                {
                    currency = 0,
                    price,
                    sender,
                    token_id = itemID
                });

            Debug.Log(paramValue.ToJson());

            var param = new Netezos.Forging.Models.Parameters()
            {
                Entrypoint = entryPoint,
                Value = paramValue
            };

            Debug.Log("param " + JsonSerializer.Serialize(param, JsonOptions.DefaultOptions));

            var result = await GasStation.PostOperations<object>(sender, new List<Operation>() {
                new Operation() {
                 destination = destination,
                 parameters = param 
                }
            });

            Logger.LogDebug(result.ToString());
        }

        public async void RemoveItemFromMarket(int itemID)
        {
            Debug.Log("Removing Item " + itemID + " from market.");

            const string entryPoint = "removeFromMarket";

            var sender = Tezos.Wallet.GetActiveAddress();

            var destination = contractAddress;

            var value = new {
                address = sender,
                token_id = itemID
            };

            var paramValue = new MichelinePrim
            {
                Prim = PrimType.Pair,
                Args = new List<IMicheline>
                {
                    new MichelineString(sender),
                    new MichelineInt(itemID)
                }
            };

            var param = new Parameters()
            {
                Entrypoint = entryPoint,
                Value = paramValue
            };

            var result = await GasStation.PostOperations<object>(sender, new List<Operation>() {
                new() {
                 destination = destination,
                 parameters = param 
                }
            });

            Logger.LogDebug(result.ToString());
        }

        public void DeployContract(Action<string> deployedContractAddress)
        {
            Tezos
                .TokenContract
                .Deploy(deployedContractAddress);
        }

        public void ChangeContract(string activeContractAddress)
        {
            PlayerPrefs.SetString("CurrentContract:" + Tezos.Wallet.GetActiveAddress(), activeContractAddress);
            Tezos.TokenContract.Address = activeContractAddress;
        }

        public void GetCoins()
        {
            FetchInventoryItems(async list =>
            {

                var sender = Tezos.Wallet.GetActiveAddress();

                // If had free coin, you should pay for it now
                if (list.Any(x => x.ID == 0))
                {
                    const string entrypoint = "buy_coins";
                    const string parameter = "{\"prim\": \"Unit\"}";
                    Tezos.Wallet.CallContract(contractAddress, entrypoint,  parameter, 10000000);
                    return;
                }
                
                const string entryPoint = "login";

                var destination = contractAddress;

                var paramValue = Tezos.TokenContract.GetContractScript().BuildParameter(
                    entrypoint: entryPoint,
                    value: sender);

                var param = new Parameters
                {
                    Entrypoint = entryPoint,
                    Value = paramValue
                };

                var result = await GasStation.PostOperations<object>(sender, new List<Operation>()
                {
                    new()
                    {
                        destination = destination,
                        parameters = param
                    }
                });
               
                Logger.LogDebug(result.ToString());
            });
        }

        public async void IsItemOnMarket(int itemID, string owner, Action<bool> callback)
        {
            const string entrypoint = "is_item_on_market";

            var input = new MichelinePrim
            {
                Prim = PrimType.Pair,
                Args = new List<IMicheline>
                {
                    new MichelineString(owner),
                    new MichelineInt(itemID)
                }
            }.ToJson();

            await Tezos.API.ReadView(
                contractAddress: contractAddress,
                entrypoint: entrypoint,
                input: input,
                callback: result =>
                {
                    var boolString = result.GetProperty("prim");
                    var boolVal = boolString.GetString() == "True";
                    callback?.Invoke(boolVal);
                }
            );
        }

        public void RequestSignPayload(SignPayloadType signingType, string payload)
        {
            Tezos.Wallet.RequestSignPayload(signingType, payload);

#if UNITY_IOS || UNITY_ANDROID
            Application.OpenURL("tezos://");
#endif
        }

        public bool VerifyPayload(SignPayloadType signingType, string payload)
        {
            return Tezos.Wallet.VerifySignedPayload(signingType, payload);
        }

        public string GetActiveAccountAddress()
        {
            return Tezos.Wallet.GetActiveAddress();
        }

        public void Login(WalletProviderType walletProvider)
        {
            Tezos.Wallet.Connect(walletProvider);
        }

        public WalletMessageReceiver GetWalletMessageReceiver()
        {
            return Tezos.Wallet.MessageReceiver;
        }

        public void GetOriginatedContracts(Action<IEnumerable<TokenContract>> callback)
        {
            var routine = Tezos.GetOriginatedContracts(callback);
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }
    }
}
