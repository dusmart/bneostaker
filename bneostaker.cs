using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace bneostaker
{
    [DisplayName("bneostaker")]
    [ManifestExtra("Author", "NEOBURGER")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "bNEO stake contract")]
    
    public class BurgerNEOStaker : Nep17Token
    {
        [InitialValue("0x48c40d4666f93408be1bef038b6722404d9a4c2a", ContractParameterType.Hash160)]
        private static readonly UInt160 bNEOHash = default;
        [InitialValue("0x54806765d451e2b0425072730d527d05fbfa9817", ContractParameterType.Hash160)]
        private static readonly UInt160 noBugHash = default;
        [InitialValue("0x54806765d451e2b0425072730d527d05fbfa9817", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULTOWNER = default;

        private const byte PREFIXOWNER = 0x03;
        private const byte PREFIXREWARDPERTOKENSTORED = 0x04;
        private const byte PREFIXREWARD = 0x05;
        private const byte PREFIXPAID = 0x06;
        private const byte PREFIXREWARDPERTOKENRATE = 0x07;
        private const byte PREFIXREWARDPERTOKENUPDATEDTIME = 0x08;

        public override byte Decimals() => 8;
        public override string Symbol() => "sbNEO";
        public static UInt160 Owner() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNER });
        public static BigInteger Reward(UInt160 account) => SyncAccount(account) ? (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account) : 0;
        public static BigInteger RPS() => SyncRPS() ? (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }) : 0;

        public static void _deploy(object data, bool update)
        {
            if (update) { return; }
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, DEFAULTOWNER);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            SyncRPS();
            SyncAccount(from);
            if (Runtime.CallingScriptHash == bNEOHash)
            {
                Mint(from, amount);
            }
            else if (Runtime.CallingScriptHash == Runtime.ExecutingScriptHash)
            {
                ExecutionEngine.Assert(Nep17Token.Transfer(bNEOHash, from, amount, null));
                Burn(Runtime.ExecutingScriptHash, amount);
            }
        }

        public static bool SyncAccount(UInt160 account)
        {
            BigInteger now = Runtime.Time;
            BigInteger lasttime = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENUPDATEDTIME });
            BigInteger rate = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENRATE });
            BigInteger rps = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });
            rps += (now - lasttime) * rate;

            BigInteger balance = BalanceOf(account);
            if (balance > 0)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
                BigInteger paid = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXPAID).Get(account);
                BigInteger earned = balance * (rps - paid) + reward;
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, earned);
            }
            new StorageMap(Storage.CurrentContext, PREFIXPAID).Put(account, rps);
            return true;
        }

        // TODO delete the origin `Transfer` and make this one as `Transfer`
        public static new bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data)
        {
            if (amount > 0)
            {
                SyncAccount(from);
                SyncAccount(to);
            }
            return Nep17Token.Transfer(from, to, amount, data);
        }

        public static void UpdateRewardRate(BigInteger rate) {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            SyncRPS();
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENRATE }, rate);
        }
        
        public static bool SyncRPS() {
            BigInteger ts = TotalSupply();
            BigInteger now = Runtime.Time;
            BigInteger lasttime = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENUPDATEDTIME });
            BigInteger rate = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENRATE });
            BigInteger rps = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }, (now - lasttime) * rate / ts + rps);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENUPDATEDTIME }, now);
            return true;
        }

        public static void claim(UInt160 account) {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));
            SyncAccount(account);
            BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
            if (reward > 0)
            {
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, 0);
                ExecutionEngine.Assert((bool)Contract.Call(noBugHash, "transfer", CallFlags.All, new object[] { Runtime.ExecutingScriptHash, account, reward, null }));
            }
        }

        public static void SetOwner(UInt160 owner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, owner);
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
